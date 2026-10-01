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
    private int formCorrectionCount;
    private PamRequestFormMismatchField correctedFormFields;

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
        await VerifyRequestFormAsync(PamRequestFormData.FromSettings(settings),
            PamRequestFormVerificationStage.BeforeSubmit, cancellationToken).ConfigureAwait(false);
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


    internal async Task FillAndVerifyRequestFormAsync(
        PamRequestFormData form,
        CancellationToken cancellationToken)
    {
        formCorrectionCount = 0;
        correctedFormFields = PamRequestFormMismatchField.None;
        RequestFormLocators fields = GetRequestFormLocators();
        await WaitForWritableInputsAsync(fields, form, cancellationToken).ConfigureAwait(false);
        await SelectStartDateAsync(fields.StartDate, form.StartDate, cancellationToken)
            .ConfigureAwait(false);
        string expectedTime = form.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        await fields.StartTime.FillAsync(expectedTime)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
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

    internal async Task VerifyRequestFormAsync(
        PamRequestFormData form,
        PamRequestFormVerificationStage stage,
        CancellationToken cancellationToken)
    {
        PamRequestFormMismatchField lastMismatches = PamRequestFormMismatchField.None;
        PamAutomation.Core.Diagnostics.PamExecutionAttempt.Current?.RecordStage(stage.ToString());
        int observationCount = 0;
        int readFailureCount = 0;
        PamRequestFormReadFailure lastReadFailure = PamRequestFormReadFailure.None;
        PamRequestFormDiagnosticValues? lastObserved = null;
        PamRequestFormDiagnosticValues expected = ExpectedFormValues(form);
        long startedAt = Environment.TickCount64;
        long stableSince = 0;
        int matchingSamples = 0;
        int replacementCount = 0;
        PamRequestFormMismatchField unavailableFields = PamRequestFormMismatchField.None;
        PamRequestFormMismatchField previousMismatches = PamRequestFormMismatchField.None;
        var stableControls = new List<IElementHandle>();
        CancellationToken callerToken = cancellationToken;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        deadline.CancelAfter(PamOperationTimingPolicy.FormStabilityTimeout);
        cancellationToken = deadline.Token;
        try
        {
            while (Environment.TickCount64 - startedAt <
                   PamOperationTimingPolicy.FormStabilityTimeout.TotalMilliseconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                observationCount++;
                try
                {
                    RequestFormReadResult result = await ReadRequestFormMismatchesAsync(form).WaitAsync(cancellationToken).ConfigureAwait(false);
                    lastMismatches = result.Mismatches;
                    lastObserved = result.Observed;
                    if (lastMismatches == PamRequestFormMismatchField.None)
                    {
                        // A replaced control invalidates a streak even if its value looks correct.
                        bool retained = true;
                        foreach (IElementHandle control in stableControls)
                            retained &= await control.IsVisibleAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                        if (!retained) { replacementCount++; stableSince = 0; matchingSamples = 0; }
                        if (matchingSamples == 0)
                        {
                            foreach (IElementHandle control in stableControls) await control.DisposeAsync().ConfigureAwait(false);
                            stableControls.Clear();
                            foreach (ILocator control in GetRequestFormLocators().All)
                            {
                                IElementHandle? handle = await control.ElementHandleAsync(new() { Timeout = (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds }).WaitAsync(cancellationToken).ConfigureAwait(false);
                                if (handle is null) throw new InvalidOperationException("El control del formulario no está disponible.");
                                stableControls.Add(handle);
                            }
                            stableSince = Environment.TickCount64;
                        }
                        matchingSamples++;
                        // Three intervals also catch the formerly missed reset after 250 ms.
                        if (matchingSamples >= 2 && Environment.TickCount64 - stableSince >= PamOperationTimingPolicy.FormMinimumStabilityWindow.TotalMilliseconds) return;
                    }
                    else
                    {
                        stableSince = 0;
                        matchingSamples = 0;
                        if (lastMismatches == previousMismatches && formCorrectionCount == 0 &&
                            !lastMismatches.HasFlag(PamRequestFormMismatchField.ApplicationSession))
                        {
                            formCorrectionCount++;
                            correctedFormFields = lastMismatches;
                            await CorrectRequestFormAsync(form, lastMismatches, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    previousMismatches = lastMismatches;
                }
                catch (Exception exception) when (IsTransientPortalReadException(exception))
                {
                    readFailureCount++;
                    stableSince = 0;
                    matchingSamples = 0;
                    previousMismatches = PamRequestFormMismatchField.None;
                    lastReadFailure = exception switch
                    {
                        RequestFormControlUnavailableException => PamRequestFormReadFailure.ControlUnavailable,
                        TimeoutException => PamRequestFormReadFailure.Timeout,
                        ArgumentException => PamRequestFormReadFailure.Argument,
                        _ => PamRequestFormReadFailure.InvalidOperation,
                    };
                    if (exception is RequestFormControlUnavailableException unavailable) unavailableFields = unavailable.Field;
                }

                await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
            }

            throw Failure();
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw Failure();
        }
        finally
        {
            foreach (IElementHandle control in stableControls) await control.DisposeAsync().ConfigureAwait(false);
        }

        PamRequestFormVerificationException Failure() => new(
            stage,
            lastMismatches,
            observationCount,
            readFailureCount,
            lastReadFailure,
            expected,
            lastObserved)
        {
            ConsecutiveMatchCount = matchingSamples,
            CorrectionCount = formCorrectionCount,
            CorrectedFields = correctedFormFields,
            UnavailableFields = unavailableFields,
            ControlReplacementCount = replacementCount
        };
    }

    private static async Task WaitForWritableInputsAsync(RequestFormLocators fields, PamRequestFormData form, CancellationToken token)
    {
        int observations = 0;
        PamRequestFormMismatchField unavailable = PamRequestFormMismatchField.None;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(PamOperationTimingPolicy.FormStabilityTimeout);
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                observations++;
                try { await RequireWritableInputsAsync(fields, deadline.Token).ConfigureAwait(false); return; }
                catch (RequestFormControlUnavailableException exception) { unavailable = exception.Field; }
                await Task.Delay(ObservationInterval, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new PamRequestFormVerificationException(PamRequestFormVerificationStage.AfterFill,
                unavailable, Math.Max(1, observations), Math.Max(1, observations),
                PamRequestFormReadFailure.ControlUnavailable, ExpectedFormValues(form), null)
            { UnavailableFields = unavailable };
        }
    }

    private static PamRequestFormDiagnosticValues ExpectedFormValues(PamRequestFormData form) => new(
        form.StartDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture),
        form.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
        form.DurationDays.ToString(CultureInfo.InvariantCulture),
        form.DurationHours.ToString(CultureInfo.InvariantCulture),
        form.DurationMinutes.ToString(CultureInfo.InvariantCulture), form.Reason.Length, "true");


    private static async Task RequireWritableInputsAsync(RequestFormLocators fields, CancellationToken token)
    {
        foreach (var (field, control) in fields.Named.Where(item => item.Field is not (PamRequestFormMismatchField.StartDate or PamRequestFormMismatchField.ApplicationSession)))
        {
            if (await control.CountAsync().WaitAsync(token).ConfigureAwait(false) != 1 ||
                !await control.IsVisibleAsync().WaitAsync(token).ConfigureAwait(false) ||
                !await control.IsEditableAsync(new() { Timeout = (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds }).WaitAsync(token).ConfigureAwait(false))
                throw new RequestFormControlUnavailableException(field);
        }
    }

    private async Task CorrectRequestFormAsync(PamRequestFormData form, PamRequestFormMismatchField mismatches, CancellationToken token)
    {
        RequestFormLocators fields = GetRequestFormLocators();
        if (mismatches.HasFlag(PamRequestFormMismatchField.StartDate))
        {
            await SelectStartDateAsync(fields.StartDate, form.StartDate, token).ConfigureAwait(false);
            mismatches |= PamRequestFormMismatchField.StartTime;
            correctedFormFields |= PamRequestFormMismatchField.StartTime;
        }
        var repairs = new (PamRequestFormMismatchField Field, ILocator Control, string Value)[]
        {
            (PamRequestFormMismatchField.StartTime, fields.StartTime, form.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)),
            (PamRequestFormMismatchField.Days, fields.Days, form.DurationDays.ToString(CultureInfo.InvariantCulture)),
            (PamRequestFormMismatchField.Hours, fields.Hours, form.DurationHours.ToString(CultureInfo.InvariantCulture)),
            (PamRequestFormMismatchField.Minutes, fields.Minutes, form.DurationMinutes.ToString(CultureInfo.InvariantCulture)),
            (PamRequestFormMismatchField.Reason, fields.Reason, form.Reason)
        };
        foreach (var repair in repairs.Where(repair => mismatches.HasFlag(repair.Field)))
        {
            if (await repair.Control.CountAsync().WaitAsync(token).ConfigureAwait(false) != 1 ||
                !await repair.Control.IsVisibleAsync().WaitAsync(token).ConfigureAwait(false) ||
                !await repair.Control.IsEditableAsync().WaitAsync(token).ConfigureAwait(false))
                throw new InvalidOperationException("El control del formulario no permite una corrección segura.");
            await repair.Control.FillAsync(repair.Value, new() { Timeout = (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds }).WaitAsync(token).ConfigureAwait(false);
        }
    }

    private async Task<RequestFormReadResult> ReadRequestFormMismatchesAsync(PamRequestFormData form)
    {
        RequestFormLocators fields = GetRequestFormLocators();
        string expectedDate = form.StartDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
        foreach (var (field, control) in fields.Named)
        {
            if (await control.CountAsync().ConfigureAwait(false) != 1 ||
                !await control.IsVisibleAsync().ConfigureAwait(false) ||
                !await control.IsEnabledAsync().ConfigureAwait(false))
                throw new RequestFormControlUnavailableException(field);
        }
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
        await RequireWritableInputsAsync(fields, CancellationToken.None).ConfigureAwait(false);
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
        ILocator ApplicationSession)
    {
        public ILocator[] All => [StartDate, StartTime, Days, Hours, Minutes, Reason, ApplicationSession];
        public (PamRequestFormMismatchField Field, ILocator Control)[] Named =>
            [(PamRequestFormMismatchField.StartDate, StartDate), (PamRequestFormMismatchField.StartTime, StartTime),
             (PamRequestFormMismatchField.Days, Days), (PamRequestFormMismatchField.Hours, Hours),
             (PamRequestFormMismatchField.Minutes, Minutes), (PamRequestFormMismatchField.Reason, Reason),
             (PamRequestFormMismatchField.ApplicationSession, ApplicationSession)];
    }
    private sealed class RequestFormControlUnavailableException(PamRequestFormMismatchField field)
        : InvalidOperationException("El formulario no tiene controles únicos disponibles.")
    {
        public PamRequestFormMismatchField Field { get; } = field;
    }
}
