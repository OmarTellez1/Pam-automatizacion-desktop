namespace PamAutomation.Infrastructure.Credentials;

internal interface IWindowsCredentialApi
{
    CredentialManagerEntry? Read(string targetName);

    void Write(CredentialManagerEntry entry);

    void Delete(string targetName);
}
