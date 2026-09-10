using System.Net.Mail;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Outlook;

namespace PamAutomation.App.Services;

/// <summary>Coordinates protected credential updates and user-initiated connectivity checks.</summary>
public sealed class CredentialConfigurationWorkflow : ICredentialConfigurationWorkflow
{
    private readonly IPamCredentialStore credentialStore;
    private readonly IPamAuthenticatedSessionFactory authentication;
    private readonly IOutlookAccountSettingsStore outlookAccountStore;
    private readonly IOutlookClassicClient outlookClient;

    /// <summary>Initializes the workflow with protected stores and external adapters.</summary>
    public CredentialConfigurationWorkflow(
        IPamCredentialStore credentialStore,
        IPamAuthenticatedSessionFactory authentication,
        IOutlookAccountSettingsStore outlookAccountStore,
        IOutlookClassicClient outlookClient)
    {
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        this.outlookAccountStore = outlookAccountStore ?? throw new ArgumentNullException(nameof(outlookAccountStore));
        this.outlookClient = outlookClient ?? throw new ArgumentNullException(nameof(outlookClient));
    }

    /// <inheritdoc />
    public async Task<ConfigurationOperationResult> SaveAndTestPamAsync(
        PamCredentialUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        try
        {
            credentialStore.Save(update);
        }
        catch
        {
            return ConfigurationOperationResult.Failure(
                "No fue posible guardar las credenciales en Windows Credential Manager.");
        }

        try
        {
            PamCredentials? credentials = credentialStore.LoadForAuthentication();
            if (credentials is null)
            {
                return ConfigurationOperationResult.Failure(
                    "Las credenciales se guardaron, pero no fue posible recuperarlas para probar el acceso.");
            }

            await using var session = await authentication
                .CreateAuthenticatedAsync(credentials, cancellationToken)
                .ConfigureAwait(false);
            return ConfigurationOperationResult.Success(
                "Credenciales guardadas y acceso al PAM comprobado correctamente.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ConfigurationOperationResult.Failure(
                "Las credenciales se guardaron, pero la prueba de acceso al PAM no se pudo completar.");
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationOperationResult> SaveAndVerifyOutlookAsync(
        string smtpAddress,
        CancellationToken cancellationToken = default)
    {
        string? normalized = NormalizeSmtp(smtpAddress);
        if (normalized is null)
        {
            return ConfigurationOperationResult.Failure("Ingresa una dirección de correo válida.");
        }

        try
        {
            await outlookClient.ValidateAccountAsync(normalized, cancellationToken).ConfigureAwait(false);
            await outlookAccountStore.SaveAsync(
                new OutlookAccountSettings { SmtpAddress = normalized }, cancellationToken).ConfigureAwait(false);
            return ConfigurationOperationResult.Success(
                "Cuenta guardada y verificada en Outlook clásico.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ConfigurationOperationResult.Failure(
                "No fue posible verificar esa cuenta en Outlook clásico. Revisa la sesión iniciada.");
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationOperationResult> SendOutlookTestAsync(
        string smtpAddress,
        CancellationToken cancellationToken = default)
    {
        string? normalized = NormalizeSmtp(smtpAddress);
        if (normalized is null)
        {
            return ConfigurationOperationResult.Failure("Primero configura una dirección de Outlook válida.");
        }

        try
        {
            OutlookDeliveryResult result = await outlookClient.SendAsync(
                new OutlookSendRequest(
                    normalized,
                    "[PRUEBA OUTLOOK] Pam Automation",
                    "<p>Prueba de configuración de Pam Automation.</p>",
                    [normalized],
                    [],
                    []),
                cancellationToken).ConfigureAwait(false);
            return ConfigurationOperationResult.Success(result.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ConfigurationOperationResult.Failure(
                "Outlook no pudo completar el correo de prueba. No se modificó la configuración guardada.");
        }
    }

    private static string? NormalizeSmtp(string value)
    {
        string normalized = value.Trim();
        return MailAddress.TryCreate(normalized, out MailAddress? address) &&
               string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase)
            ? address.Address
            : null;
    }
}
