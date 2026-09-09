namespace PamAutomation.Core.Favorites;

/// <summary>Represents a controlled failure while navigating or reading PAM Favorites.</summary>
public sealed class PamFavoritesException : Exception
{
    /// <summary>Initializes a Favorites failure with a stable code.</summary>
    /// <param name="errorCode">Stable category of failure.</param>
    /// <param name="innerException">Underlying technical failure, when available.</param>
    public PamFavoritesException(PamFavoritesErrorCode errorCode, Exception? innerException = null)
        : base(GetMessage(errorCode), innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the stable category of failure.</summary>
    public PamFavoritesErrorCode ErrorCode { get; }

    private static string GetMessage(PamFavoritesErrorCode errorCode) => errorCode switch
    {
        PamFavoritesErrorCode.PortalUnavailable => "No fue posible abrir o leer Favorites en el portal PAM.",
        PamFavoritesErrorCode.NoAccounts => "Favorites no contiene cuentas disponibles.",
        PamFavoritesErrorCode.InvalidAccount => "Favorites contiene una cuenta con identidad incompleta.",
        PamFavoritesErrorCode.DuplicateAccount => "Favorites contiene identidades de cuenta duplicadas.",
        _ => "No fue posible completar la lectura de Favorites.",
    };
}
