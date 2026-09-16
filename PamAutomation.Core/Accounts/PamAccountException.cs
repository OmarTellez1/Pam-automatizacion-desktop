namespace PamAutomation.Core.Accounts;

public enum PamAccountErrorCode
{
    InvalidName,
    DuplicateName,
    LimitReached,
    PrincipalAlreadyExists,
    AccountNotFound,
    PrincipalCannotBeDeleted,
    PrincipalCredentialsManagedSeparately,
    InvalidCredentials,
    InvalidContent,
    UnsupportedSchema,
    ReadFailed,
    WriteFailed
}

/// <summary>Represents an expected and safe PAM-account administration failure.</summary>
public sealed class PamAccountException : Exception
{
    public PamAccountException(PamAccountErrorCode errorCode, string message, Exception? innerException = null)
        : base(message, innerException) => ErrorCode = errorCode;

    public PamAccountErrorCode ErrorCode { get; }
}
