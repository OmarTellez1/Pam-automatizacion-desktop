namespace PamAutomation.Core.Interfaces.Credentials;

/// <summary>
/// Identifies controlled failures from protected PAM credential storage.
/// </summary>
public enum CredentialStoreErrorCode
{
    /// <summary>A new credential does not contain every required field.</summary>
    IncompleteUpdate,

    /// <summary>The protected credential exists but its payload cannot be interpreted safely.</summary>
    CorruptedData,

    /// <summary>The protected credential exceeds the Windows Credential Manager limit.</summary>
    PayloadTooLarge,

    /// <summary>Windows Credential Manager could not read the credential.</summary>
    ReadFailed,

    /// <summary>Windows Credential Manager could not write the credential.</summary>
    WriteFailed,

    /// <summary>Windows Credential Manager could not delete the credential.</summary>
    DeleteFailed
}
