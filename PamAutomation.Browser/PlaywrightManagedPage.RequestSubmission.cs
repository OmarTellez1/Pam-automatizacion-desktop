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

        await PrepareRequestFormAsync(account, cancellationToken).ConfigureAwait(false);
        await FillAndVerifyRequestFormAsync(form, cancellationToken).ConfigureAwait(false);
        await CaptureAsync(captureDirectory, BeforeSubmitCaptureName, cancellationToken).ConfigureAwait(false);

        ILocator submit = page.GetByRole(
            AriaRole.Button,
            new() { Name = PamRequestTestSelectors.SubmitRequest, Exact = true });
        await RequireUniqueVisibleAsync(submit, "botón Submit Request", cancellationToken)
            .ConfigureAwait(false);
        await VerifyRequestFormAsync(
            form,
            PamRequestFormVerificationStage.BeforeSubmit,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref realSubmissionAttempts);
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
        }
        catch (TimeoutException exception)
        {
            ILocator invalidTime = page.GetByText(PamRequestTestSelectors.InvalidTimeExpression());
            if (await IsAnyVisibleAsync(invalidTime).ConfigureAwait(false))
            {
                throw new PamRequestSubmissionException(
                    PamRequestSubmissionErrorCode.RequestRejected,
                    exception);
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
            await CaptureAsync(captureDirectory, AfterSubmitCaptureName, cancellationToken).ConfigureAwait(false);
            await DiscardRequestFormAsync(cancellationToken).ConfigureAwait(false);
            return outcome;
        }

        await CaptureAfterSubmissionIgnoringFailureAsync(captureDirectory).ConfigureAwait(false);
        return outcome;
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
