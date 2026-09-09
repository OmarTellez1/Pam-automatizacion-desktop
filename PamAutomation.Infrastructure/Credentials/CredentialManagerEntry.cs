namespace PamAutomation.Infrastructure.Credentials;

internal sealed class CredentialManagerEntry(
    string targetName,
    string username,
    byte[] credentialBlob)
{
    public string TargetName { get; } = targetName;

    public string Username { get; } = username;

    public byte[] CredentialBlob { get; } = credentialBlob;
}
