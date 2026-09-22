using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Diagnostics;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Resilience;

namespace PamAutomation.Core.Requests;

/// <summary>Coordinates read-only discovery and comparison of PAM <c>Pending</c> requests.</summary>
public sealed class PamPendingService
{
    private readonly IPamAuthenticatedSessionFactory authenticatedSessionFactory;
    private readonly IPamPendingPortalFactory portalFactory;
    private readonly IAsyncDelay asyncDelay;
    private readonly IPendingReconciliationDiagnosticSink? diagnosticSink;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<PamPendingService> logger;

    /// <summary>Initializes the read-only workflow.</summary>
    public PamPendingService(
        IPamAuthenticatedSessionFactory authenticatedSessionFactory,
        IPamPendingPortalFactory portalFactory,
        IAsyncDelay asyncDelay,
        IPendingReconciliationDiagnosticSink? diagnosticSink = null,
        TimeProvider? timeProvider = null,
        ILogger<PamPendingService>? logger = null)
    {
        this.authenticatedSessionFactory = authenticatedSessionFactory
            ?? throw new ArgumentNullException(nameof(authenticatedSessionFactory));
        this.portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
        this.asyncDelay = asyncDelay ?? throw new ArgumentNullException(nameof(asyncDelay));
        this.diagnosticSink = diagnosticSink;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.logger = logger ?? NullLogger<PamPendingService>.Instance;
    }

    /// <summary>Authenticates, reads every <c>Pending</c> page and closes the temporary session.</summary>
    public async Task<IReadOnlyList<PamPendingRequest>> ReadAllAsync(
        PamCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        return await WithAuthenticatedReadRecoveryAsync(
            credentials,
            "Preflight",
            (session, sessionAttempt) => ReadPendingWithRecoveryAsync(
                session, "Preflight", sessionAttempt, 0, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads every <c>Pending</c> page using a caller-owned authenticated session.</summary>
    public async Task<IReadOnlyList<PamPendingRequest>> ReadAllAsync(
        IPamBrowserSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            IReadOnlyList<PamPendingRequest> requests = await portalFactory
                .Create(session)
                .ReadAllAsync(cancellationToken)
                .ConfigureAwait(false);
            return Validate(requests);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PamPendingException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new PamPendingException(PamPendingErrorCode.PortalUnavailable, exception);
        }
        catch (Exception exception)
        {
            // Unknown failures must remain terminal, not become network retries.
            throw new PamPendingException(PamPendingErrorCode.InvalidRequest, exception);
        }
    }

    /// <summary>Matches expected accounts at one exact start date and time.</summary>
    public PamPendingComparisonResult Compare(
        IReadOnlyList<PamPendingRequest> requests,
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form,
        IReadOnlyCollection<string>? excludedRequestIds = null)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(expectedAccounts);
        ArgumentNullException.ThrowIfNull(form);

        HashSet<string> excluded = excludedRequestIds?.ToHashSet(StringComparer.Ordinal) ?? [];
        IReadOnlyList<PamPendingRequest> validatedRequests = Validate(requests)
            .Where(request => !excluded.Contains(request.RequestId))
            .ToArray();
        ValidateExpectedAccounts(expectedAccounts);

        DateTime expectedExpiration = form.StartDate
            .ToDateTime(form.StartTime)
            .AddMinutes(form.TotalMinutes);
        ILookup<string, PamPendingRequest> candidates = validatedRequests
            .Where(request => IsExactBatchRequest(request, form, expectedExpiration))
            .ToLookup(AccountKey, StringComparer.Ordinal);
        PamPendingRequestMatch?[] matchesByExpectedIndex = new PamPendingRequestMatch?[expectedAccounts.Count];
        bool[] missingByExpectedIndex = new bool[expectedAccounts.Count];

        foreach (IGrouping<string, (PamFavoriteAccount Account, int Index)> expectedGroup in expectedAccounts
            .Select(static (account, index) => (Account: account, Index: index))
            .GroupBy(static item => AccountKey(item.Account), StringComparer.Ordinal))
        {
            (PamFavoriteAccount Account, int Index)[] expectedItems = expectedGroup.ToArray();
            PamPendingRequest[] accountMatches = candidates[expectedGroup.Key]
                .OrderBy(static request => request.RequestId.Length)
                .ThenBy(static request => request.RequestId, StringComparer.Ordinal)
                .ToArray();
            if (accountMatches.Length > expectedItems.Length)
            {
                throw new PamPendingException(PamPendingErrorCode.AmbiguousIdentity);
            }

            for (int index = 0; index < expectedItems.Length; index++)
            {
                (PamFavoriteAccount account, int expectedIndex) = expectedItems[index];
                if (index < accountMatches.Length)
                {
                    matchesByExpectedIndex[expectedIndex] = new PamPendingRequestMatch(
                        account,
                        accountMatches[index]);
                }
                else
                {
                    missingByExpectedIndex[expectedIndex] = true;
                }
            }
        }

        PamPendingRequestMatch[] matches = matchesByExpectedIndex
            .Where(static match => match is not null)
            .Select(static match => match!)
            .ToArray();
        PamFavoriteAccount[] missing = expectedAccounts
            .Where((_, index) => missingByExpectedIndex[index])
            .ToArray();
        return new PamPendingComparisonResult(matches, missing);
    }

    /// <summary>Finds every existing Pending row that exactly repeats one expected request.</summary>
    public IReadOnlyList<PamPendingRequest> FindExactMatches(
        IReadOnlyList<PamPendingRequest> requests,
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(expectedAccounts);
        ArgumentNullException.ThrowIfNull(form);

        IReadOnlyList<PamPendingRequest> validatedRequests = Validate(requests);
        ValidateExpectedAccounts(expectedAccounts);
        DateTime expectedExpiration = form.StartDate
            .ToDateTime(form.StartTime)
            .AddMinutes(form.TotalMinutes);

        HashSet<string> expectedAccountKeys = expectedAccounts
            .Select(AccountKey)
            .ToHashSet(StringComparer.Ordinal);

        return validatedRequests
            .Where(request => IsExactBatchRequest(request, form, expectedExpiration) &&
                expectedAccountKeys.Contains(AccountKey(request)))
            .ToArray();
    }

    /// <summary>Polls <c>Pending</c> for an eventually consistent complete comparison.</summary>
    public async Task<PamPendingComparisonResult> WaitForCompleteComparisonAsync(
        IPamBrowserSession session,
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form,
        int maximumAttempts = PamOperationTimingPolicy.PendingComparisonMaximumAttempts,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? excludedRequestIds = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        TimeSpan delay = ValidatePollingArguments(
            expectedAccounts,
            form,
            maximumAttempts,
            retryDelay);
        diagnosticSink?.RecordStage("Reconciliation");
        try
        {
            return await WaitForCompleteComparisonInSessionAsync(
                    session,
                    expectedAccounts,
                    form,
                    maximumAttempts,
                    delay,
                    sessionAttempt: 1,
                    cancellationToken,
                    excludedRequestIds)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            diagnosticSink?.RecordFailure("Canceled");
            throw;
        }
        catch (PamPendingException exception)
        {
            diagnosticSink?.RecordFailure(exception.ErrorCode.ToString());
            throw;
        }
    }

    private async Task<PamPendingComparisonResult> WaitForCompleteComparisonInSessionAsync(
        IPamBrowserSession session,
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form,
        int maximumAttempts,
        TimeSpan retryDelay,
        int sessionAttempt,
        CancellationToken cancellationToken,
        IReadOnlyCollection<string>? excludedRequestIds)
    {
        PamPendingComparisonResult? latest = null;
        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            IReadOnlyList<PamPendingRequest> requests = await ReadPendingWithRecoveryAsync(
                    session,
                    "Reconciliation",
                    sessionAttempt,
                    attempt,
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                latest = Compare(requests, expectedAccounts, form, excludedRequestIds);
                diagnosticSink?.RecordAttempt(CreateDiagnosticAttempt(
                    attempt,
                    requests,
                    excludedRequestIds,
                    expectedAccounts,
                    form,
                    latest,
                    errorCode: null));
            }
            catch (PamPendingException exception)
            {
                diagnosticSink?.RecordAttempt(CreateDiagnosticAttempt(
                    attempt,
                    requests,
                    excludedRequestIds,
                    expectedAccounts,
                    form,
                    comparison: null,
                    exception.ErrorCode));
                throw;
            }

            if (latest.IsComplete || attempt == maximumAttempts)
            {
                return latest;
            }

            await asyncDelay.DelayAsync(retryDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("No se completó la comparación de Pending.");
    }

    private async Task<IReadOnlyList<PamPendingRequest>> ReadPendingWithRecoveryAsync(
        IPamBrowserSession session,
        string stage,
        int sessionAttempt,
        int comparisonAttempt,
        CancellationToken cancellationToken)
    {
        for (int readAttempt = 1;
             readAttempt <= PamOperationTimingPolicy.PendingReadMaximumAttemptsPerSession;
             readAttempt++)
        {
            try
            {
                IReadOnlyList<PamPendingRequest> requests = await ReadAllAsync(session, cancellationToken)
                    .ConfigureAwait(false);
                diagnosticSink?.RecordRead(new PendingReadAttemptDiagnostic(
                    timeProvider.GetUtcNow(), stage, sessionAttempt, comparisonAttempt, readAttempt, null));
                if (readAttempt > 1)
                {
                    logger.LogInformation(
                        "Pending read recovered in the current session. SessionAttempt={SessionAttempt}, ComparisonAttempt={ComparisonAttempt}, ReadAttempt={ReadAttempt}.",
                        sessionAttempt,
                        comparisonAttempt,
                        readAttempt);
                }

                return requests;
            }
            catch (OperationCanceledException)
            {
                diagnosticSink?.RecordRead(new PendingReadAttemptDiagnostic(
                    timeProvider.GetUtcNow(), stage, sessionAttempt, comparisonAttempt, readAttempt, "Canceled"));
                throw;
            }
            catch (PamPendingException exception)
            {
                diagnosticSink?.RecordRead(new PendingReadAttemptDiagnostic(
                    timeProvider.GetUtcNow(), stage, sessionAttempt, comparisonAttempt, readAttempt,
                    exception.ErrorCode.ToString()));
                if (!IsRecoverableReadFailure(exception)) throw;

                string action = readAttempt < PamOperationTimingPolicy.PendingReadMaximumAttemptsPerSession
                    ? "ReloadPendingView"
                    : "ReadSessionFailed";
                diagnosticSink?.RecordRecovery(new PendingReadRecoveryDiagnostic(
                    timeProvider.GetUtcNow(),
                    sessionAttempt,
                    comparisonAttempt,
                    readAttempt,
                    action,
                    exception.ErrorCode.ToString()) { Stage = stage });
                logger.LogWarning(
                    exception,
                    "Pending read failed transiently. SessionAttempt={SessionAttempt}, ComparisonAttempt={ComparisonAttempt}, ReadAttempt={ReadAttempt}, RecoveryState={RecoveryState}.",
                    sessionAttempt,
                    comparisonAttempt,
                    readAttempt,
                    action);
                if (readAttempt == PamOperationTimingPolicy.PendingReadMaximumAttemptsPerSession)
                {
                    throw;
                }

                await asyncDelay.DelayAsync(
                    PamOperationTimingPolicy.PendingReadRecoveryDelay,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Pending agotó los intentos de lectura configurados.");
    }

    private PendingComparisonAttemptDiagnostic CreateDiagnosticAttempt(
        int attempt,
        IReadOnlyList<PamPendingRequest> requests,
        IReadOnlyCollection<string>? excludedRequestIds,
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form,
        PamPendingComparisonResult? comparison,
        PamPendingErrorCode? errorCode)
    {
        HashSet<string> baseline = excludedRequestIds?.ToHashSet(StringComparer.Ordinal) ?? [];
        HashSet<string> currentIds = requests.Select(static request => request.RequestId)
            .ToHashSet(StringComparer.Ordinal);
        PamPendingRequest[] newRequests = requests
            .Where(request => !baseline.Contains(request.RequestId))
            .ToArray();
        DateTime expectedExpiration = form.StartDate
            .ToDateTime(form.StartTime)
            .AddMinutes(form.TotalMinutes);
        HashSet<string> expectedKeys = expectedAccounts
            .Select(AccountKey)
            .ToHashSet(StringComparer.Ordinal);
        PamPendingRequest[] exactCandidates = newRequests
            .Where(request =>
                IsExactBatchRequest(request, form, expectedExpiration) &&
                expectedKeys.Contains(AccountKey(request)))
            .ToArray();
        PendingGroupMultiplicityDiagnostic[] groups = expectedAccounts
            .GroupBy(AccountKey, StringComparer.Ordinal)
            .Select(group => new PendingGroupMultiplicityDiagnostic(
                group.First().SystemName,
                group.First().AccountName,
                group.Count(),
                exactCandidates.Count(request =>
                    string.Equals(AccountKey(request), group.Key, StringComparison.Ordinal))))
            .ToArray();
        return new(
            attempt,
            timeProvider.GetUtcNow(),
            requests.Count,
            currentIds.Count,
            baseline.Count(currentIds.Contains),
            baseline.Count(id => !currentIds.Contains(id)),
            newRequests.Length,
            exactCandidates.Length,
            newRequests.Length - exactCandidates.Length,
            comparison?.MatchedCount ?? 0,
            comparison?.MissingCount ?? 0,
            errorCode?.ToString(),
            groups,
            requests.Select(PendingRequestDiagnostic.Create).ToArray(),
            newRequests.Select(PendingRequestDiagnostic.Create).ToArray());
    }

    /// <summary>Polls <c>Pending</c> and replaces only its temporary read session after recoverable failures.</summary>
    public async Task<PamPendingComparisonResult> WaitForCompleteComparisonAsync(
        PamCredentials credentials,
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form,
        int maximumAttempts = PamOperationTimingPolicy.PendingComparisonMaximumAttempts,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? excludedRequestIds = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        TimeSpan delay = ValidatePollingArguments(
            expectedAccounts,
            form,
            maximumAttempts,
            retryDelay);
        // Capture inputs once so replacing the read session cannot replace the baseline.
        PamFavoriteAccount[] expected = expectedAccounts.ToArray();
        string[]? baseline = excludedRequestIds?.ToArray();
        return await WithAuthenticatedReadRecoveryAsync(
            credentials,
            "Reconciliation",
            (session, sessionAttempt) => WaitForCompleteComparisonInSessionAsync(
                session, expected, form, maximumAttempts, delay, sessionAttempt, cancellationToken, baseline),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> WithAuthenticatedReadRecoveryAsync<T>(
        PamCredentials credentials,
        string stage,
        Func<IPamBrowserSession, int, Task<T>> read,
        CancellationToken cancellationToken)
    {
        diagnosticSink?.RecordStage(stage);
        try
        {
            return await WithAuthenticatedReadSessionsAsync(credentials, stage, read, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            diagnosticSink?.RecordFailure("Canceled");
            throw;
        }
        catch (PamPendingException exception)
        {
            diagnosticSink?.RecordFailure(exception.ErrorCode.ToString());
            throw;
        }
        catch (Exception)
        {
            // Authentication/session failures are not retried and expose no exception message.
            diagnosticSink?.RecordFailure("ReadSessionUnavailable");
            throw;
        }
    }

    private async Task<T> WithAuthenticatedReadSessionsAsync<T>(
        PamCredentials credentials,
        string stage,
        Func<IPamBrowserSession, int, Task<T>> read,
        CancellationToken cancellationToken)
    {
        for (int sessionAttempt = 1;
             sessionAttempt <= PamOperationTimingPolicy.PendingReadSessionMaximumAttempts;
             sessionAttempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Authentication is outside the read retry catch. A rejected login is terminal.
            IPamBrowserSession session = await authenticatedSessionFactory
                .CreateAuthenticatedAsync(credentials, cancellationToken).ConfigureAwait(false);
            try
            {
                T result;
                await using (session.ConfigureAwait(false))
                {
                    result = await read(session, sessionAttempt).ConfigureAwait(false);
                }
                if (sessionAttempt > 1)
                {
                    logger.LogInformation(
                        "Pending read recovered with a replacement session. SessionAttempt={SessionAttempt}.",
                        sessionAttempt);
                }

                return result;
            }
            catch (PamPendingException exception) when (
                IsRecoverableReadFailure(exception) &&
                sessionAttempt < PamOperationTimingPolicy.PendingReadSessionMaximumAttempts)
            {
                diagnosticSink?.RecordRecovery(new PendingReadRecoveryDiagnostic(
                    timeProvider.GetUtcNow(),
                    sessionAttempt,
                    ComparisonAttempt: 0,
                    ReadAttempt: 0,
                    Action: "RecreateAuthenticatedReadSession",
                    exception.ErrorCode.ToString()) { Stage = stage });
                logger.LogWarning(
                    exception,
                    "Pending read session will be recreated without resubmitting requests. SessionAttempt={SessionAttempt}, MaximumSessionAttempts={MaximumSessionAttempts}.",
                    sessionAttempt,
                    PamOperationTimingPolicy.PendingReadSessionMaximumAttempts);
                await asyncDelay.DelayAsync(
                    PamOperationTimingPolicy.PendingReadRecoveryDelay,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Pending agotó las sesiones temporales de recuperación.");
    }

    private static TimeSpan ValidatePollingArguments(
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form,
        int maximumAttempts,
        TimeSpan? retryDelay)
    {
        ArgumentNullException.ThrowIfNull(expectedAccounts);
        ArgumentNullException.ThrowIfNull(form);
        if (maximumAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        }

        TimeSpan delay = retryDelay ?? PamOperationTimingPolicy.PendingComparisonRetryDelay;
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }

        return delay;
    }

    private static bool IsRecoverableReadFailure(PamPendingException exception) =>
        exception.ErrorCode is PamPendingErrorCode.PortalUnavailable or PamPendingErrorCode.SnapshotUnstable;

    private static IReadOnlyList<PamPendingRequest> Validate(IReadOnlyList<PamPendingRequest>? requests)
    {
        if (requests is null || requests.Any(static request => request is null))
        {
            throw new PamPendingException(PamPendingErrorCode.InvalidRequest);
        }

        if (requests.Select(static request => request.RequestId)
            .Distinct(StringComparer.Ordinal).Count() != requests.Count)
        {
            throw new PamPendingException(PamPendingErrorCode.DuplicateRequestId);
        }

        return requests.ToArray();
    }

    private static void ValidateExpectedAccounts(IReadOnlyList<PamFavoriteAccount> expectedAccounts)
    {
        if (expectedAccounts.Any(static account => account is null))
        {
            throw new PamPendingException(PamPendingErrorCode.AmbiguousIdentity);
        }

        string[] expectedKeys = expectedAccounts.Select(IdentityKey).ToArray();
        if (expectedKeys.Distinct(StringComparer.Ordinal).Count() != expectedKeys.Length)
        {
            throw new PamPendingException(PamPendingErrorCode.AmbiguousIdentity);
        }
    }

    private static string IdentityKey(PamFavoriteAccount account) =>
        CreateIdentityKey(account.SystemName, account.AccountName, account.ApplicationName);

    private static string AccountKey(PamFavoriteAccount account) =>
        string.Join('|', Normalize(account.SystemName), Normalize(account.AccountName));

    private static string AccountKey(PamPendingRequest request) =>
        string.Join('|', Normalize(request.SystemName), Normalize(request.AccountName));

    private static string CreateIdentityKey(string system, string account, string application) =>
        string.Join('|', Normalize(system), Normalize(account), Normalize(application));

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static bool IsExactBatchRequest(
        PamPendingRequest request,
        PamRequestFormData form,
        DateTime expectedExpiration) =>
        request.StartDate == form.StartDate &&
        request.StartTime == form.StartTime &&
        request.ExpiresDate == DateOnly.FromDateTime(expectedExpiration) &&
        request.ExpiresTime == TimeOnly.FromDateTime(expectedExpiration) &&
        string.Equals(
            NormalizeDescription(request.Description),
            NormalizeDescription(form.Reason),
            StringComparison.Ordinal);

    private static string NormalizeDescription(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
