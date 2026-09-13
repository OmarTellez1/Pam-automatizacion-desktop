using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Core.Requests;

/// <summary>Coordinates read-only discovery and comparison of PAM <c>Pending</c> requests.</summary>
public sealed class PamPendingService
{
    private static readonly string[] KnownApplicationFamilies =
    [
        "nosqlbooster",
        "plsql",
        "putty",
        "winscp",
    ];

    private readonly IPamAuthenticatedSessionFactory authenticatedSessionFactory;
    private readonly IPamPendingPortalFactory portalFactory;
    private readonly IAsyncDelay asyncDelay;

    /// <summary>Initializes the read-only workflow.</summary>
    public PamPendingService(
        IPamAuthenticatedSessionFactory authenticatedSessionFactory,
        IPamPendingPortalFactory portalFactory,
        IAsyncDelay asyncDelay)
    {
        this.authenticatedSessionFactory = authenticatedSessionFactory
            ?? throw new ArgumentNullException(nameof(authenticatedSessionFactory));
        this.portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
        this.asyncDelay = asyncDelay ?? throw new ArgumentNullException(nameof(asyncDelay));
    }

    /// <summary>Authenticates, reads every <c>Pending</c> page and closes the temporary session.</summary>
    public async Task<IReadOnlyList<PamPendingRequest>> ReadAllAsync(
        PamCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        cancellationToken.ThrowIfCancellationRequested();

        await using IPamBrowserSession session = await authenticatedSessionFactory
            .CreateAuthenticatedAsync(credentials, cancellationToken)
            .ConfigureAwait(false);
        return await ReadAllAsync(session, cancellationToken).ConfigureAwait(false);
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
        catch (Exception exception)
        {
            throw new PamPendingException(PamPendingErrorCode.PortalUnavailable, exception);
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

        ILookup<string, PamPendingRequest> candidates = validatedRequests
            .Where(request => request.StartDate == form.StartDate && request.StartTime == form.StartTime)
            .ToLookup(AccountKey, StringComparer.Ordinal);
        var matches = new List<PamPendingRequestMatch>(expectedAccounts.Count);
        var missing = new List<PamFavoriteAccount>();
        for (int index = 0; index < expectedAccounts.Count; index++)
        {
            PamFavoriteAccount account = expectedAccounts[index];
            PamPendingRequest[] accountCandidates = candidates[AccountKey(account)].ToArray();
            PamPendingRequest[] accountMatches = accountCandidates
                .Where(request => ApplicationMatches(account.ApplicationName, request.ApplicationName))
                .ToArray();
            if (accountMatches.Length > 1)
            {
                throw new PamPendingException(PamPendingErrorCode.AmbiguousIdentity);
            }

            if (accountMatches.Length == 0)
            {
                if (accountCandidates.Length > 0)
                {
                    throw new PamPendingException(PamPendingErrorCode.FieldMismatch);
                }

                missing.Add(account);
                continue;
            }

            PamPendingRequest match = accountMatches[0];
            DateTime expectedExpiration = form.StartDate
                .ToDateTime(form.StartTime)
                .AddMinutes(form.TotalMinutes);
            if (match.ExpiresDate != DateOnly.FromDateTime(expectedExpiration) ||
                match.ExpiresTime != TimeOnly.FromDateTime(expectedExpiration) ||
                !string.Equals(
                    NormalizeDescription(match.Description),
                    NormalizeDescription(form.Reason),
                    StringComparison.Ordinal))
            {
                throw new PamPendingException(PamPendingErrorCode.FieldMismatch);
            }

            matches.Add(new PamPendingRequestMatch(account, match));
        }

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

        return validatedRequests
            .Where(request => request.StartDate == form.StartDate &&
                request.StartTime == form.StartTime &&
                request.ExpiresDate == DateOnly.FromDateTime(expectedExpiration) &&
                request.ExpiresTime == TimeOnly.FromDateTime(expectedExpiration) &&
                string.Equals(
                    NormalizeDescription(request.Description),
                    NormalizeDescription(form.Reason),
                    StringComparison.Ordinal) &&
                expectedAccounts.Any(account =>
                    string.Equals(AccountKey(account), AccountKey(request), StringComparison.Ordinal) &&
                    ApplicationMatches(account.ApplicationName, request.ApplicationName)))
            .ToArray();
    }

    /// <summary>Polls <c>Pending</c> for an eventually consistent complete comparison.</summary>
    public async Task<PamPendingComparisonResult> WaitForCompleteComparisonAsync(
        IPamBrowserSession session,
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form,
        int maximumAttempts = 6,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? excludedRequestIds = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(expectedAccounts);
        ArgumentNullException.ThrowIfNull(form);
        if (maximumAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        }

        TimeSpan delay = retryDelay ?? TimeSpan.FromSeconds(2);
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }

        PamPendingComparisonResult? latest = null;
        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            IReadOnlyList<PamPendingRequest> requests = await ReadAllAsync(session, cancellationToken)
                .ConfigureAwait(false);
            latest = Compare(requests, expectedAccounts, form, excludedRequestIds);
            if (latest.IsComplete || attempt == maximumAttempts)
            {
                return latest;
            }

            await asyncDelay.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("No se completó la comparación de Pending.");
    }

    /// <summary>Authenticates once and polls <c>Pending</c> until all expected rows are present.</summary>
    public async Task<PamPendingComparisonResult> WaitForCompleteComparisonAsync(
        PamCredentials credentials,
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form,
        int maximumAttempts = 6,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? excludedRequestIds = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        await using IPamBrowserSession session = await authenticatedSessionFactory
            .CreateAuthenticatedAsync(credentials, cancellationToken)
            .ConfigureAwait(false);
        return await WaitForCompleteComparisonAsync(
                session,
                expectedAccounts,
                form,
                maximumAttempts,
                retryDelay,
                cancellationToken,
                excludedRequestIds)
            .ConfigureAwait(false);
    }

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

    private static bool ApplicationMatches(string requested, string observed)
    {
        string normalizedRequested = Normalize(requested);
        string normalizedObserved = Normalize(observed);
        if (string.Equals(normalizedObserved, normalizedRequested, StringComparison.Ordinal) ||
            normalizedObserved.StartsWith(normalizedRequested + " - ", StringComparison.Ordinal))
        {
            return true;
        }

        string? requestedFamily = GetKnownApplicationFamily(normalizedRequested);
        return requestedFamily is not null &&
            string.Equals(requestedFamily, GetKnownApplicationFamily(normalizedObserved), StringComparison.Ordinal);
    }

    private static string? GetKnownApplicationFamily(string normalizedApplication) =>
        KnownApplicationFamilies.FirstOrDefault(family =>
            string.Equals(normalizedApplication, family, StringComparison.Ordinal) ||
            normalizedApplication.StartsWith(family + " ", StringComparison.Ordinal) ||
            normalizedApplication.StartsWith(family + "-", StringComparison.Ordinal));

    private static string NormalizeDescription(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
