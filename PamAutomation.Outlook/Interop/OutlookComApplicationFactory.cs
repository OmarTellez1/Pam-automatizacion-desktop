using System.Runtime.InteropServices;
using OutlookInterop = global::Microsoft.Office.Interop.Outlook;

namespace PamAutomation.Outlook.Interop;

internal sealed class OutlookComApplicationFactory : IOutlookComApplicationFactory
{
    public IOutlookComApplication Create() => new OutlookComApplication(new OutlookInterop.Application());
}

internal sealed class OutlookComApplication : IOutlookComApplication
{
    private OutlookInterop.Application? application;

    public OutlookComApplication(OutlookInterop.Application application)
    {
        this.application = application ?? throw new ArgumentNullException(nameof(application));
    }

    public IReadOnlyList<IOutlookComAccount> GetAccounts()
    {
        OutlookInterop.Application currentApplication = GetApplication();
        OutlookInterop.NameSpace session = currentApplication.Session;
        OutlookInterop.Accounts? accounts = null;
        List<IOutlookComAccount> result = [];

        try
        {
            accounts = session.Accounts;
            for (int index = 1; index <= accounts.Count; index++)
            {
                OutlookInterop.Account account = accounts[index];
                result.Add(new OutlookComAccount(account));
            }

            return result;
        }
        catch
        {
            foreach (IOutlookComAccount account in result)
            {
                account.Dispose();
            }

            throw;
        }
        finally
        {
            ComReference.Release(accounts);
            ComReference.Release(session);
        }
    }

    public IOutlookComMailItem CreateMailItem()
    {
        OutlookInterop.Application currentApplication = GetApplication();
        OutlookInterop.MailItem item = (OutlookInterop.MailItem)currentApplication.CreateItem(OutlookInterop.OlItemType.olMailItem);
        return new OutlookComMailItem(item);
    }

    public void Dispose()
    {
        OutlookInterop.Application? currentApplication = Interlocked.Exchange(ref application, null);
        ComReference.Release(currentApplication);
    }

    private OutlookInterop.Application GetApplication() => application ?? throw new ObjectDisposedException(nameof(OutlookComApplication));
}

internal sealed class OutlookComAccount : IOutlookComAccount
{
    private OutlookInterop.Account? account;

    public OutlookComAccount(OutlookInterop.Account account)
    {
        this.account = account ?? throw new ArgumentNullException(nameof(account));
    }

    public string SmtpAddress => GetAccount().SmtpAddress;

    public OutlookInterop.Account NativeAccount => GetAccount();

    public void Dispose()
    {
        OutlookInterop.Account? currentAccount = Interlocked.Exchange(ref account, null);
        ComReference.Release(currentAccount);
    }

    private OutlookInterop.Account GetAccount() => account ?? throw new ObjectDisposedException(nameof(OutlookComAccount));
}

internal sealed class OutlookComMailItem : IOutlookComMailItem
{
    private OutlookInterop.MailItem? mailItem;

    public OutlookComMailItem(OutlookInterop.MailItem mailItem)
    {
        this.mailItem = mailItem ?? throw new ArgumentNullException(nameof(mailItem));
    }

    public void SetSendUsingAccount(IOutlookComAccount account)
    {
        if (account is not OutlookComAccount nativeAccount)
        {
            throw new ArgumentException("La cuenta de Outlook no pertenece al adaptador COM.", nameof(account));
        }

        GetMailItem().SendUsingAccount = nativeAccount.NativeAccount;
    }

    public void SetSubject(string subject) => GetMailItem().Subject = subject;

    public void SetHtmlBody(string htmlBody) => GetMailItem().HTMLBody = htmlBody;

    public void SetToRecipients(IReadOnlyList<string> recipients) => GetMailItem().To = string.Join(";", recipients);

    public void SetCcRecipients(IReadOnlyList<string> recipients) => GetMailItem().CC = string.Join(";", recipients);

    public void Send() => GetMailItem().Send();

    public void Discard() => GetMailItem().Close(OutlookInterop.OlInspectorClose.olDiscard);

    public void Dispose()
    {
        OutlookInterop.MailItem? currentMailItem = Interlocked.Exchange(ref mailItem, null);
        ComReference.Release(currentMailItem);
    }

    private OutlookInterop.MailItem GetMailItem() => mailItem ?? throw new ObjectDisposedException(nameof(OutlookComMailItem));
}

internal static class ComReference
{
    public static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
