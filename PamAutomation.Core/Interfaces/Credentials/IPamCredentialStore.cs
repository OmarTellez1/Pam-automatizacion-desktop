using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Core.Interfaces.Credentials;

/// <summary>
/// Provides protected, per-user storage for PAM credentials.
/// </summary>
public interface IPamCredentialStore
{
    /// <summary>
    /// Determines whether a complete and readable credential is available.
    /// </summary>
    /// <returns><see langword="true"/> when a complete credential is available.</returns>
    bool HasCredentials();

    /// <summary>
    /// Loads credentials for the authentication workflow.
    /// </summary>
    /// <returns>The protected credential, or <see langword="null"/> when none exists.</returns>
    /// <remarks>This method must not be used to populate plaintext UI fields.</remarks>
    PamCredentials? LoadForAuthentication();

    /// <summary>
    /// Creates or updates the protected credential. Empty fields preserve existing values.
    /// </summary>
    /// <param name="update">Values entered during the create or edit operation.</param>
    void Save(PamCredentialUpdate update);

    /// <summary>
    /// Deletes the protected credential when it exists.
    /// </summary>
    void Delete();
}
