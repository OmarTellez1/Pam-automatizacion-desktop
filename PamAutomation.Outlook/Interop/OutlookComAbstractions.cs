namespace PamAutomation.Outlook.Interop;

using PamAutomation.Core.Outlook;

internal interface IOutlookComApplicationFactory
{
    IOutlookComApplication Create();
}

internal interface IOutlookComApplication : IDisposable
{
    IReadOnlyList<IOutlookComAccount> GetAccounts();

    IOutlookComMailItem CreateMailItem();

    OutlookExistingMessageStatus FindExistingMessage(
        string smtpAddress, string subject, string requestFingerprint, bool sentOnly = false);
}

internal interface IOutlookComAccount : IDisposable
{
    string SmtpAddress { get; }
}

internal interface IOutlookComMailItem : IDisposable
{
    void SetSendUsingAccount(IOutlookComAccount account);

    void SetSubject(string subject);

    void SetHtmlBody(string htmlBody);

    void SetToRecipients(IReadOnlyList<string> recipients);

    void SetCcRecipients(IReadOnlyList<string> recipients);

    void SetRequestFingerprint(string requestFingerprint);

    void AddInlineImage(OutlookInlineImage image);

    void Send();

    void Discard();
}

internal interface IStaExecutor
{
    Task<T> ExecuteAsync<T>(Func<T> operation, CancellationToken cancellationToken);
}
