using System.Text;

namespace PamAutomation.Core.Accounts;

/// <summary>Describes one principal or secondary PAM identity without exposing credentials.</summary>
public sealed record PamAccountSummary
{
    public const int MaximumNameLength = 80;

    public PamAccountSummary(PamAccountId id, string name, bool isPrimary)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("La cuenta requiere un identificador válido.", nameof(id));
        Id = id;
        Name = NormalizeName(name);
        IsPrimary = isPrimary;
    }

    public PamAccountId Id { get; }
    public string Name { get; }
    public bool IsPrimary { get; }

    public static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string normalized = name.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length is 0 or > MaximumNameLength || normalized.Any(char.IsControl))
            throw new ArgumentException($"El nombre debe contener entre 1 y {MaximumNameLength} caracteres válidos.", nameof(name));
        return normalized;
    }

    public override string ToString() => "PAM account";
}
