namespace PamAutomation.Outlook.Interop;

internal interface IOutlookComApplicationFactory
{
    IOutlookComApplication Create();
}

internal interface IOutlookComApplication : IDisposable
{
    IReadOnlyList<IOutlookComAccount> GetAccounts();

    IOutlookComMailItem CreateMailItem();
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

    void Send();

    void Discard();
}

internal interface IStaExecutor
{
    Task<T> ExecuteAsync<T>(Func<T> operation, CancellationToken cancellationToken);
}
