using Microsoft.Playwright;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Requests;

namespace PamAutomation.Browser;

internal sealed partial class PlaywrightManagedPage
{
    private const string BeforeSubmitCaptureName = "fase12-request-form-before-submit.png";
    private const string AfterSubmitCaptureName = "fase12-request-result-after-submit.png";
    private int realSubmissionAttempts;

    public async Task<PamRequestPortalOutcome> SubmitRequestFormAsync(
        PamFavoriteAccount account,
        PamRequestFormData form,
        string? captureDirectory,
        int? maximumSubmissionAttempts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(form);
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionBlockInstalled)
        {
            throw new InvalidOperationException(
                "Una sesión configurada para modo de prueba no puede enviar solicitudes.");
        }

        if (maximumSubmissionAttempts is { } maximum &&
            Volatile.Read(ref realSubmissionAttempts) >= maximum)
        {
            throw new InvalidOperationException("Se alcanzó el límite autorizado de Submit Request.");
        }

        try
        {
            await PrepareRequestFormAsync(account, cancellationToken).ConfigureAwait(false);
            await FillAndVerifyRequestFormAsync(form, cancellationToken).ConfigureAwait(false);
            await CaptureAsync(captureDirectory, BeforeSubmitCaptureName, cancellationToken).ConfigureAwait(false);

            ILocator submitForValidation = page.GetByRole(
                AriaRole.Button,
                new() { Name = PamRequestTestSelectors.SubmitRequest, Exact = true });
            await RequireUniqueVisibleAsync(submitForValidation, "botón Submit Request", cancellationToken)
                .ConfigureAwait(false);
            await VerifyRequestFormAsync(
                form,
                PamRequestFormVerificationStage.BeforeSubmit,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

        }
        catch (Exception exception)
        {
            throw new PamRequestSubmissionException(exception is OperationCanceledException
                ? PamRequestSubmissionErrorCode.Canceled : PamRequestSubmissionErrorCode.PortalUnavailable,
                exception, submitMayHaveOccurred: false);
        }
        ILocator submit = page.GetByRole(AriaRole.Button, new() { Name = PamRequestTestSelectors.SubmitRequest, Exact = true });

        Interlocked.Increment(ref realSubmissionAttempts);
        PamAutomation.Core.Diagnostics.PamExecutionAttempt.Current?.RecordStage("Submit");
        if (PamAutomation.Core.Diagnostics.PamExecutionAttempt.Current is { } attempt)
        {
            var counts = attempt.Capture();
            attempt.RecordCounts(counts.AcceptedCount, counts.ConflictCount, counts.RejectedCount,
                counts.UncertainCount + 1, Math.Max(0, counts.ExpectedCount - counts.FormOrdinal),
                failedBeforeSubmission: counts.FailedBeforeSubmissionCount);
        }
        int postObserved = 0;
        bool responseObserved = false;
        EventHandler<IRequest> observePost = (_, request) =>
        {
            if (request.Method == "POST" && Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? uri) &&
                uri.AbsolutePath.EndsWith("/api/ps-portal/requests", StringComparison.OrdinalIgnoreCase))
                Interlocked.Exchange(ref postObserved, 1);
        };
        page.Request += observePost;
        try
        {
            IResponse response;
            try
            {
                response = await page.RunAndWaitForResponseAsync(
                    async () => await submit.ClickAsync().ConfigureAwait(false),
                    IsSubmissionResponse,
                    new PageRunAndWaitForResponseOptions
                    {
                        Timeout = (float)PortalTimeout.TotalMilliseconds,
                    }).ConfigureAwait(false);
                responseObserved = true;
            }
            catch (TimeoutException exception)
            {
                ILocator invalidTime = page.GetByText(PamRequestTestSelectors.InvalidTimeExpression());
                if (Volatile.Read(ref postObserved) == 0 && await IsAnyVisibleAsync(invalidTime).ConfigureAwait(false))
                {
                    throw new PamRequestSubmissionException(
                        PamRequestSubmissionErrorCode.RequestRejected,
                        exception,
                        submitMayHaveOccurred: false, isKnownRejection: true);
                }

                throw;
            }

            PamRequestPortalOutcome outcome;
            if (response.Status is >= 200 and < 300)
            {
                outcome = PamRequestPortalOutcome.Submitted();
            }
            else
            {
                string payload = await response.TextAsync().ConfigureAwait(false);
                outcome = PamSubmissionResponseClassifier.Classify(response.Status, payload);
            }

            if (outcome.Status == PamRequestSubmissionStatus.ApprovedAccessOverlap)
            {
                try
                {
                    await CaptureAsync(captureDirectory, AfterSubmitCaptureName, cancellationToken).ConfigureAwait(false);
                    await DiscardRequestFormAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    throw new PamRequestSubmissionException(PamRequestSubmissionErrorCode.PortalUnavailable,
                        exception, submitMayHaveOccurred: true, confirmedOutcome: outcome);
                }
                return outcome;
            }

            await CaptureAfterSubmissionIgnoringFailureAsync(captureDirectory).ConfigureAwait(false);
            return outcome;
        }
        catch (Exception exception)
        {
            PamRequestSubmissionException? failure = exception as PamRequestSubmissionException;
            throw new PamRequestSubmissionException(failure?.ErrorCode ?? PamRequestSubmissionErrorCode.PortalUnavailable,
                exception, submitMayHaveOccurred: failure?.SubmitMayHaveOccurred ?? true,
                confirmedOutcome: failure?.ConfirmedOutcome, isKnownRejection: failure?.IsKnownRejection ?? false)
            { Evidence = new PamSubmissionEvidence(true, Volatile.Read(ref postObserved) != 0, responseObserved) };
        }
        finally { page.Request -= observePost; }
    }

    private static bool IsSubmissionResponse(IResponse response)
    {
        if (!string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(response.Url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return uri.AbsolutePath.EndsWith(
            "/api/ps-portal/requests",
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task CaptureAsync(
        string? captureDirectory,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (captureDirectory is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        string path = Path.Combine(captureDirectory, fileName);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = true,
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CaptureAfterSubmissionIgnoringFailureAsync(string? captureDirectory)
    {
        if (captureDirectory is null)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(750)).ConfigureAwait(false);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(captureDirectory, AfterSubmitCaptureName),
                FullPage = true,
            }).ConfigureAwait(false);
        }
        catch
        {
            // A development screenshot cannot invalidate a confirmed server-side submission.
        }
    }
}
