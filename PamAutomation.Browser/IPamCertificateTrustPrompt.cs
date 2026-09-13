namespace PamAutomation.Browser;

/// <summary>Notifies the user that Edge requires manual approval of the PAM certificate.</summary>
public interface IPamCertificateTrustPrompt
{
    /// <summary>Explains how to continue past the certificate warning before login automation resumes.</summary>
    Task ShowAsync(CancellationToken cancellationToken = default);
}
