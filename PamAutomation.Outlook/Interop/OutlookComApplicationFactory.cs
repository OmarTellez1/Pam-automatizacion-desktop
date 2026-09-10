using System.Runtime.InteropServices;
using PamAutomation.Core.Outlook;
using OutlookInterop = global::Microsoft.Office.Interop.Outlook;

namespace PamAutomation.Outlook.Interop;

internal sealed class OutlookComApplicationFactory : IOutlookComApplicationFactory
{
    public IOutlookComApplication Create() => new OutlookComApplication(new OutlookInterop.Application());
}

internal sealed class OutlookComApplication : IOutlookComApplication
{
    private const string RequestFingerprintProperty = "PamAutomationRequestIds";
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

    public OutlookExistingMessageStatus FindExistingMessage(string smtpAddress, string subject, string requestFingerprint)
    {
        OutlookInterop.NameSpace session = GetApplication().Session;
        OutlookInterop.Store? store = null;
        try
        {
            store = ResolveDeliveryStore(session, smtpAddress);
            if (FolderContains(store, OutlookInterop.OlDefaultFolders.olFolderSentMail, subject, requestFingerprint, moveToDrafts: false))
                return OutlookExistingMessageStatus.Sent;
            if (FolderContains(store, OutlookInterop.OlDefaultFolders.olFolderOutbox, subject, requestFingerprint, moveToDrafts: true))
                return OutlookExistingMessageStatus.OutboxMovedToDraft;
            if (FolderContains(store, OutlookInterop.OlDefaultFolders.olFolderDrafts, subject, requestFingerprint, moveToDrafts: false))
                return OutlookExistingMessageStatus.Draft;
            return OutlookExistingMessageStatus.None;
        }
        finally
        {
            ComReference.Release(store);
            ComReference.Release(session);
        }
    }

    private static OutlookInterop.Store ResolveDeliveryStore(OutlookInterop.NameSpace session, string smtpAddress)
    {
        OutlookInterop.Accounts? accounts = null;
        OutlookInterop.Store? selectedStore = null;
        int matches = 0;
        try
        {
            accounts = session.Accounts;
            for (int index = 1; index <= accounts.Count; index++)
            {
                OutlookInterop.Account? account = null;
                try
                {
                    account = accounts[index];
                    if (!string.Equals(account.SmtpAddress, smtpAddress, StringComparison.OrdinalIgnoreCase)) continue;
                    matches++;
                    if (matches == 1) selectedStore = account.DeliveryStore;
                }
                finally
                {
                    ComReference.Release(account);
                }
            }

            if (matches == 0) throw new OutlookAccountResolutionException(OutlookAccountResolutionErrorCode.AccountNotFound);
            if (matches > 1)
            {
                ComReference.Release(selectedStore);
                throw new OutlookAccountResolutionException(OutlookAccountResolutionErrorCode.MultipleAccountsFound);
            }
            return selectedStore!;
        }
        finally
        {
            ComReference.Release(accounts);
        }
    }

    private static bool FolderContains(
        OutlookInterop.Store store,
        OutlookInterop.OlDefaultFolders folderKind,
        string subject,
        string requestFingerprint,
        bool moveToDrafts)
    {
        OutlookInterop.MAPIFolder? folder = null;
        OutlookInterop.MAPIFolder? drafts = null;
        OutlookInterop.Items? items = null;
        try
        {
            folder = store.GetDefaultFolder(folderKind);
            items = folder.Items;
            for (int index = 1; index <= items.Count; index++)
            {
                object? candidate = null;
                OutlookInterop.UserProperties? properties = null;
                OutlookInterop.UserProperty? fingerprint = null;
                try
                {
                    candidate = items[index];
                    if (candidate is not OutlookInterop.MailItem message ||
                        !string.Equals(message.Subject, subject, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    properties = message.UserProperties;
                    fingerprint = properties.Find(RequestFingerprintProperty, true);
                    if (!string.Equals(fingerprint?.Value?.ToString(), requestFingerprint, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (moveToDrafts)
                    {
                        drafts = store.GetDefaultFolder(OutlookInterop.OlDefaultFolders.olFolderDrafts);
                        object moved = message.Move(drafts);
                        ComReference.Release(moved);
                    }

                    return true;
                }
                finally
                {
                    ComReference.Release(fingerprint);
                    ComReference.Release(properties);
                    ComReference.Release(candidate);
                }
            }

            return false;
        }
        finally
        {
            ComReference.Release(items);
            ComReference.Release(drafts);
            ComReference.Release(folder);
        }
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
    private const string RequestFingerprintProperty = "PamAutomationRequestIds";
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

    public void SetRequestFingerprint(string requestFingerprint)
    {
        OutlookInterop.UserProperties properties = GetMailItem().UserProperties;
        OutlookInterop.UserProperty? property = null;
        try
        {
            property = properties.Find(RequestFingerprintProperty, true)
                ?? properties.Add(RequestFingerprintProperty, OutlookInterop.OlUserPropertyType.olText, true);
            property.Value = requestFingerprint;
        }
        finally
        {
            ComReference.Release(property);
            ComReference.Release(properties);
        }
    }

    public void AddInlineImage(OutlookInlineImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        string directory = Path.Combine(Path.GetTempPath(), "PamAutomation", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".png");
        OutlookInterop.Attachments? attachments = null;
        OutlookInterop.Attachment? attachment = null;
        OutlookInterop.PropertyAccessor? properties = null;
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(filePath, image.Data);
            attachments = GetMailItem().Attachments;
            attachment = attachments.Add(filePath, OutlookInterop.OlAttachmentType.olByValue);
            properties = attachment.PropertyAccessor;
            properties.SetProperty("http://schemas.microsoft.com/mapi/proptag/0x3712001F", image.ContentId);
            properties.SetProperty("http://schemas.microsoft.com/mapi/proptag/0x7FFE000B", true);
        }
        finally
        {
            ComReference.Release(properties);
            ComReference.Release(attachment);
            ComReference.Release(attachments);
            try { File.Delete(filePath); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            try { Directory.Delete(directory); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

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
