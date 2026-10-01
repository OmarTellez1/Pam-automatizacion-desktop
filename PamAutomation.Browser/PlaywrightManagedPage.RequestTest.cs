using System.Globalization;
using Microsoft.Playwright;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Resilience;
using PamAutomation.Core.Requests;

namespace PamAutomation.Browser;

internal sealed partial class PlaywrightManagedPage
{
    private bool submissionBlockInstalled;
    private int blockedSubmissionAttempts;

    public async Task ValidateAndDiscardRequestFormAsync(
        PamFavoriteAccount account,
        RequestSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureSubmissionBlockedAsync(cancellationToken).ConfigureAwait(false);
        int attemptsBefore = Volatile.Read(ref blockedSubmissionAttempts);
        await PrepareRequestFormAsync(account, cancellationToken).ConfigureAwait(false);
        await FillAndVerifyRequestFormAsync(
            PamRequestFormData.FromSettings(settings),
            cancellationToken).ConfigureAwait(false);
        await DiscardRequestFormAsync(cancellationToken).ConfigureAwait(false);

        if (Volatile.Read(ref blockedSubmissionAttempts) != attemptsBefore)
        {
            throw new InvalidOperationException("El modo de prueba bloqueó un intento inesperado de envío.");
        }
    }

    private async Task EnsureSubmissionBlockedAsync(CancellationToken cancellationToken)
    {
        if (submissionBlockInstalled)
        {
            return;
        }

        await page.RouteAsync(PamRequestTestSelectors.SubmissionEndpointExpression(), async route =>
        {
            if (!string.Equals(route.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await route.FallbackAsync().ConfigureAwait(false);
                return;
            }

            Interlocked.Increment(ref blockedSubmissionAttempts);
            await route.AbortAsync().ConfigureAwait(false);
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
        submissionBlockInstalled = true;
    }


    private async Task FillAndVerifyRequestFormAsync(
        PamRequestFormData form,
        CancellationToken cancellationToken)
    {
        RequestFormLocators fields = GetRequestFormLocators();
        await SelectStartDateAsync(fields.StartDate, form.StartDate, cancellationToken)
            .ConfigureAwait(false);
        string expectedTime = form.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        await fields.StartTime.FillAsync(expectedTime)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
            await fields.StartTime.InputValueAsync().ConfigureAwait(false),
            expectedTime,
            StringComparison.Ordinal))
        {
            await fields.StartTime.FillAsync(expectedTime)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        await fields.Days.FillAsync(form.DurationDays.ToString(CultureInfo.InvariantCulture))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await fields.Hours.FillAsync(form.DurationHours.ToString(CultureInfo.InvariantCulture))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await fields.Minutes.FillAsync(form.DurationMinutes.ToString(CultureInfo.InvariantCulture))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await fields.Reason.FillAsync(form.Reason).WaitAsync(cancellationToken).ConfigureAwait(false);
        await VerifyRequestFormAsync(
            form,
            PamRequestFormVerificationStage.AfterFill,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task VerifyRequestFormAsync(
        PamRequestFormData form,
        PamRequestFormVerificationStage stage,
        CancellationToken cancellationToken)
    {
        PamRequestFormMismatchField lastMismatches = PamRequestFormMismatchField.None;
        int observationCount = 0;
        int readFailureCount = 0;
        PamRequestFormReadFailure lastReadFailure = PamRequestFormReadFailure.None;
        PamRequestFormDiagnosticValues? lastObserved = null;
        var expected = new PamRequestFormDiagnosticValues(
            form.StartDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture),
            form.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            form.DurationDays.ToString(CultureInfo.InvariantCulture),
            form.DurationHours.ToString(CultureInfo.InvariantCulture),
            form.DurationMinutes.ToString(CultureInfo.InvariantCulture),
            form.Reason.Length,
            "true");
        long startedAt = Environment.TickCount64;
        while (Environment.TickCount64 - startedAt <
               PamOperationTimingPolicy.FormStabilityTimeout.TotalMilliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            observationCount++;
            try
            {
                RequestFormReadResult result = await ReadRequestFormMismatchesAsync(form).ConfigureAwait(false);
                lastMismatches = result.Mismatches;
                lastObserved = result.Observed;
                if (lastMismatches == PamRequestFormMismatchField.None)
                {
                    return;
                }
            }
            catch (Exception exception) when (IsTransientPortalReadException(exception))
            {
                readFailureCount++;
                lastReadFailure = exception switch
                {
                    TimeoutException => PamRequestFormReadFailure.Timeout,
                    ArgumentException => PamRequestFormReadFailure.Argument,
                    _ => PamRequestFormReadFailure.InvalidOperation,
                };
            }

            await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new PamRequestFormVerificationException(
            stage,
            lastMismatches,
            observationCount,
            readFailureCount,
            lastReadFailure,
            expected,
            lastObserved);
    }

    private async Task<RequestFormReadResult> ReadRequestFormMismatchesAsync(PamRequestFormData form)
    {
        RequestFormLocators fields = GetRequestFormLocators();
        string expectedDate = form.StartDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
        string expectedTime = form.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        PamRequestFormMismatchField mismatches = PamRequestFormMismatchField.None;
        var inputOptions = new LocatorInputValueOptions
        {
            Timeout = (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds,
        };
        var attributeOptions = new LocatorGetAttributeOptions
        {
            Timeout = (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds,
        };
        string actualDate = await fields.StartDate.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualTime = await fields.StartTime.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualDays = await fields.Days.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualHours = await fields.Hours.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualMinutes = await fields.Minutes.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualReason = await fields.Reason.InputValueAsync(inputOptions).ConfigureAwait(false);
        string? actualApplicationSession = await fields.ApplicationSession
            .GetAttributeAsync("aria-checked", attributeOptions).ConfigureAwait(false);
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.StartDate, actualDate, expectedDate);
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.StartTime, actualTime, expectedTime);
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.Days, actualDays,
            form.DurationDays.ToString(CultureInfo.InvariantCulture));
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.Hours, actualHours,
            form.DurationHours.ToString(CultureInfo.InvariantCulture));
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.Minutes, actualMinutes,
            form.DurationMinutes.ToString(CultureInfo.InvariantCulture));
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.Reason, actualReason, form.Reason);
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.ApplicationSession,
            actualApplicationSession, "true", StringComparison.OrdinalIgnoreCase);
        var observed = new PamRequestFormDiagnosticValues(actualDate, actualTime, actualDays, actualHours,
            actualMinutes, actualReason.Length, actualApplicationSession);
        return new RequestFormReadResult(mismatches, observed);
    }

    private sealed record RequestFormReadResult(
        PamRequestFormMismatchField Mismatches,
        PamRequestFormDiagnosticValues Observed);

    private static PamRequestFormMismatchField AddMismatch(
        PamRequestFormMismatchField mismatches,
        PamRequestFormMismatchField field,
        string? actual,
        string expected,
        StringComparison comparison = StringComparison.Ordinal)
    {
        return string.Equals(actual, expected, comparison) ? mismatches : mismatches | field;
    }

    private Task SelectStartDateAsync(
        ILocator startDate,
        DateOnly targetDate,
        CancellationToken cancellationToken) =>
        new PamCalendarDateSelector(page).SelectAsync(startDate, targetDate, cancellationToken);

    private async Task DiscardRequestFormAsync(CancellationToken cancellationToken)
    {
        ILocator cancel = page.GetByRole(
            AriaRole.Button,
            new() { Name = PamRequestTestSelectors.Cancel, Exact = true });
        await RequireUniqueVisibleAsync(cancel, PamRequestTestSelectors.Cancel, cancellationToken)
            .ConfigureAwait(false);
        await cancel.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        ILocator discard = page.GetByRole(
            AriaRole.Button,
            new() { Name = PamRequestTestSelectors.DiscardChanges, Exact = true });
        await RequireUniqueVisibleAsync(discard, PamRequestTestSelectors.DiscardChanges, cancellationToken)
            .ConfigureAwait(false);
        await discard.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        await cancel.WaitForAsync(new()
        {
            State = WaitForSelectorState.Hidden,
            Timeout = (float)PortalTimeout.TotalMilliseconds,
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private RequestFormLocators GetRequestFormLocators() => new(
        page.GetByLabel(PamRequestTestSelectors.StartDate, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.StartTime, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.Days, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.Hours, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.Minutes, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.Reason, new() { Exact = true }),
        page.GetByRole(AriaRole.Switch, new() { Name = PamRequestTestSelectors.ApplicationSession, Exact = true }));

    private sealed record RequestFormLocators(
        ILocator StartDate,
        ILocator StartTime,
        ILocator Days,
        ILocator Hours,
        ILocator Minutes,
        ILocator Reason,
        ILocator ApplicationSession);
}
