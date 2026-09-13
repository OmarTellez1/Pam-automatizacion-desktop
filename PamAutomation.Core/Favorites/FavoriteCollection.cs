using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Favorites;

/// <summary>Defines a named subset of Favorites and its own request configuration.</summary>
public sealed record FavoriteCollection
{
    /// <summary>Initializes a favorite collection.</summary>
    public FavoriteCollection(
        Guid id,
        string name,
        RequestSettings request,
        IReadOnlyList<FavoriteAccountKey> accounts)
    {
        if (id == Guid.Empty) throw new ArgumentException("La colección requiere un identificador.", nameof(id));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || name.Any(char.IsControl))
        {
            throw new ArgumentException("El nombre de la colección debe contener entre 1 y 100 caracteres.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(accounts);
        if (request.StartDate == default || request.SessionHours is < 1 or > 8 ||
            string.IsNullOrWhiteSpace(request.RequestReason))
        {
            throw new ArgumentException("La configuración propia de la colección está incompleta.", nameof(request));
        }

        FavoriteAccountKey[] copiedAccounts = accounts.ToArray();
        if (copiedAccounts.Any(static account => account is null) ||
            copiedAccounts.Distinct().Count() != copiedAccounts.Length)
        {
            throw new ArgumentException("La colección contiene identidades favoritas inválidas o repetidas.", nameof(accounts));
        }

        Id = id;
        Name = name.Trim();
        Request = request with { };
        Accounts = Array.AsReadOnly(copiedAccounts);
    }

    /// <summary>Gets the stable local identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the user-visible collection name.</summary>
    public string Name { get; }

    /// <summary>Gets the request configuration used only by this collection.</summary>
    public RequestSettings Request { get; }

    /// <summary>Gets the exact Favorites identities selected by the user.</summary>
    public IReadOnlyList<FavoriteAccountKey> Accounts { get; }

    /// <inheritdoc />
    public override string ToString() => $"Favorite collection ({Accounts.Count} rows)";
}
