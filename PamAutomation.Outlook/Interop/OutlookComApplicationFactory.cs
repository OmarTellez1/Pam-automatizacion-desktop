using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Outlook.Interop;

internal interface IOutlookComActivator
{
    object? TryGetRunningApplication();

    object CreateApplication();
}

internal interface IOutlookApplicationLauncher
{
    void Start();
}

internal interface IOutlookStartupWaiter
{
    void Wait();
}

internal sealed class OutlookComActivator : IOutlookComActivator
{
    private const string OutlookApplicationProgId = "Outlook.Application";
    private const int MonikerUnavailableHResult = unchecked((int)0x800401E3);

    public object? TryGetRunningApplication()
    {
        Type applicationType = GetApplicationType();
        Guid classId = applicationType.GUID;
        try
        {
            return GetActiveObject(ref classId, IntPtr.Zero);
        }
        catch (COMException exception) when (exception.HResult == MonikerUnavailableHResult)
        {
            return null;
        }
    }

    public object CreateApplication()
    {
        Type applicationType = GetApplicationType();
        return Activator.CreateInstance(applicationType)
            ?? throw new COMException("Outlook classic COM activation returned no application instance.");
    }

    private static Type GetApplicationType() =>
        Type.GetTypeFromProgID(OutlookApplicationProgId, throwOnError: true)
        ?? throw new COMException("Outlook classic COM is not registered.");

    [DllImport("oleaut32.dll", PreserveSig = false)]
    [return: MarshalAs(UnmanagedType.Interface)]
    private static extern object GetActiveObject(ref Guid classId, IntPtr reserved);
}

internal sealed class OutlookApplicationLauncher : IOutlookApplicationLauncher
{
    public void Start()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "outlook.exe",
                UseShellExecute = true
            });
            if (process is null)
            {
                throw new InvalidOperationException("Windows did not return an Outlook process.");
            }
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("Windows could not start registered Outlook classic.", exception);
        }
    }
}

internal sealed class OutlookStartupWaiter : IOutlookStartupWaiter
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(3);

    public void Wait() => Thread.Sleep(StartupDelay);
}

internal sealed class OutlookComUnavailableException : Exception
{
    private const int ServerExecutionFailureHResult = unchecked((int)0x80080005);

    public OutlookComUnavailableException(Exception innerException)
        : base("Outlook classic did not respond to controlled COM recovery.", innerException)
    {
        HResult = ServerExecutionFailureHResult;
    }
}

internal sealed class OutlookComApplicationFactory : IOutlookComApplicationFactory
{
    private const int ServerExecutionFailureHResult = unchecked((int)0x80080005);
    private readonly IOutlookComActivator activator;
    private readonly IOutlookApplicationLauncher launcher;
    private readonly IOutlookStartupWaiter startupWaiter;
    private readonly ILogger logger;

    public OutlookComApplicationFactory(ILogger? logger = null)
        : this(
            new OutlookComActivator(),
            new OutlookApplicationLauncher(),
            new OutlookStartupWaiter(),
            logger)
    {
    }

    internal OutlookComApplicationFactory(
        IOutlookComActivator activator,
        IOutlookApplicationLauncher launcher,
        IOutlookStartupWaiter startupWaiter,
        ILogger? logger = null)
    {
        this.activator = activator ?? throw new ArgumentNullException(nameof(activator));
        this.launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        this.startupWaiter = startupWaiter ?? throw new ArgumentNullException(nameof(startupWaiter));
        this.logger = logger ?? NullLogger.Instance;
    }

    public IOutlookComApplication Create()
    {
        object? runningApplication = activator.TryGetRunningApplication();
        if (runningApplication is not null)
        {
            logger.LogInformation("Outlook COM connection completed. Status={Status}.", "ExistingInstance");
            return new OutlookComApplication(runningApplication);
        }

        try
        {
            return new OutlookComApplication(activator.CreateApplication());
        }
        catch (COMException exception) when (exception.HResult == ServerExecutionFailureHResult)
        {
            logger.LogWarning(exception, "Outlook COM recovery started at {Stage}.", "RegisteredApplicationLaunch");
            return RecoverAfterServerExecutionFailure(exception);
        }
    }

    private IOutlookComApplication RecoverAfterServerExecutionFailure(COMException originalException)
    {
        try
        {
            launcher.Start();
            startupWaiter.Wait();

            object application = activator.TryGetRunningApplication() ?? activator.CreateApplication();
            logger.LogInformation("Outlook COM recovery completed. Status={Status}.", "Recovered");
            return new OutlookComApplication(application);
        }
        catch (Exception recoveryException)
        {
            logger.LogError(recoveryException, "Outlook COM recovery failed at {Stage}.", "RecoveryRetry");
            throw new OutlookComUnavailableException(
                new AggregateException(originalException, recoveryException));
        }
    }
}

internal sealed class OutlookComApplication : IOutlookComApplication
{
    private const string RequestFingerprintProperty = "PamAutomationRequestIds";
    private object? application;

    public OutlookComApplication(object application)
    {
        this.application = application ?? throw new ArgumentNullException(nameof(application));
    }

    public IReadOnlyList<IOutlookComAccount> GetAccounts()
    {
        object session = ((dynamic)GetApplication()).Session;
        object? accounts = null;
        List<IOutlookComAccount> result = [];

        try
        {
            accounts = ((dynamic)session).Accounts;
            int count = Convert.ToInt32(((dynamic)accounts).Count, CultureInfo.InvariantCulture);
            for (int index = 1; index <= count; index++)
            {
                object account = ((dynamic)accounts).Item(index);
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
        object item = ((dynamic)GetApplication()).CreateItem(OutlookComConstants.MailItem);
        return new OutlookComMailItem(item);
    }

    public OutlookExistingMessageStatus FindExistingMessage(
        string smtpAddress,
        string subject,
        string requestFingerprint)
    {
        object session = ((dynamic)GetApplication()).Session;
        object? store = null;
        try
        {
            store = ResolveDeliveryStore(session, smtpAddress);
            if (FolderContains(store, OutlookComConstants.FolderSentMail, subject, requestFingerprint, moveToDrafts: false))
                return OutlookExistingMessageStatus.Sent;
            if (FolderContains(store, OutlookComConstants.FolderOutbox, subject, requestFingerprint, moveToDrafts: true))
                return OutlookExistingMessageStatus.OutboxMovedToDraft;
            if (FolderContains(store, OutlookComConstants.FolderDrafts, subject, requestFingerprint, moveToDrafts: false))
                return OutlookExistingMessageStatus.Draft;
            return OutlookExistingMessageStatus.None;
        }
        finally
        {
            ComReference.Release(store);
            ComReference.Release(session);
        }
    }

    public void Dispose()
    {
        object? currentApplication = Interlocked.Exchange(ref application, null);
        ComReference.Release(currentApplication);
    }

    private static object ResolveDeliveryStore(object session, string smtpAddress)
    {
        object? accounts = null;
        object? selectedStore = null;
        int matches = 0;
        try
        {
            accounts = ((dynamic)session).Accounts;
            int count = Convert.ToInt32(((dynamic)accounts).Count, CultureInfo.InvariantCulture);
            for (int index = 1; index <= count; index++)
            {
                object? account = null;
                try
                {
                    account = ((dynamic)accounts).Item(index);
                    string accountAddress = Convert.ToString(
                        ((dynamic)account).SmtpAddress, CultureInfo.InvariantCulture) ?? string.Empty;
                    if (!string.Equals(accountAddress, smtpAddress, StringComparison.OrdinalIgnoreCase)) continue;
                    matches++;
                    if (matches == 1) selectedStore = ((dynamic)account).DeliveryStore;
                }
                finally
                {
                    ComReference.Release(account);
                }
            }

            if (matches == 0)
                throw new OutlookAccountResolutionException(OutlookAccountResolutionErrorCode.AccountNotFound);
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
        object store,
        int folderKind,
        string subject,
        string requestFingerprint,
        bool moveToDrafts)
    {
        object? folder = null;
        object? drafts = null;
        object? items = null;
        try
        {
            folder = ((dynamic)store).GetDefaultFolder(folderKind);
            items = ((dynamic)folder).Items;
            int count = Convert.ToInt32(((dynamic)items).Count, CultureInfo.InvariantCulture);
            for (int index = 1; index <= count; index++)
            {
                object? candidate = null;
                object? properties = null;
                object? fingerprint = null;
                try
                {
                    candidate = ((dynamic)items).Item(index);
                    string messageClass = Convert.ToString(
                        ((dynamic)candidate).MessageClass, CultureInfo.InvariantCulture) ?? string.Empty;
                    string candidateSubject = Convert.ToString(
                        ((dynamic)candidate).Subject, CultureInfo.InvariantCulture) ?? string.Empty;
                    if (!messageClass.StartsWith("IPM.Note", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(candidateSubject, subject, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    properties = ((dynamic)candidate).UserProperties;
                    fingerprint = ((dynamic)properties).Find(RequestFingerprintProperty, true);
                    string? fingerprintValue = fingerprint is null
                        ? null
                        : Convert.ToString(((dynamic)fingerprint).Value, CultureInfo.InvariantCulture);
                    if (!string.Equals(fingerprintValue, requestFingerprint, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (moveToDrafts)
                    {
                        drafts = ((dynamic)store).GetDefaultFolder(OutlookComConstants.FolderDrafts);
                        object moved = ((dynamic)candidate).Move(drafts);
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

    private object GetApplication() => application ?? throw new ObjectDisposedException(nameof(OutlookComApplication));
}

internal sealed class OutlookComAccount : IOutlookComAccount
{
    private object? account;

    public OutlookComAccount(object account)
    {
        this.account = account ?? throw new ArgumentNullException(nameof(account));
    }

    public string SmtpAddress => Convert.ToString(
        ((dynamic)GetAccount()).SmtpAddress, CultureInfo.InvariantCulture) ?? string.Empty;

    public object NativeAccount => GetAccount();

    public void Dispose()
    {
        object? currentAccount = Interlocked.Exchange(ref account, null);
        ComReference.Release(currentAccount);
    }

    private object GetAccount() => account ?? throw new ObjectDisposedException(nameof(OutlookComAccount));
}

internal sealed class OutlookComMailItem : IOutlookComMailItem
{
    private const string RequestFingerprintProperty = "PamAutomationRequestIds";
    private object? mailItem;

    public OutlookComMailItem(object mailItem)
    {
        this.mailItem = mailItem ?? throw new ArgumentNullException(nameof(mailItem));
    }

    public void SetSendUsingAccount(IOutlookComAccount account)
    {
        if (account is not OutlookComAccount nativeAccount)
        {
            throw new ArgumentException("La cuenta de Outlook no pertenece al adaptador COM.", nameof(account));
        }

        ((dynamic)GetMailItem()).SendUsingAccount = nativeAccount.NativeAccount;
    }

    public void SetSubject(string subject) => ((dynamic)GetMailItem()).Subject = subject;

    public void SetHtmlBody(string htmlBody) => ((dynamic)GetMailItem()).HTMLBody = htmlBody;

    public void SetToRecipients(IReadOnlyList<string> recipients) =>
        ((dynamic)GetMailItem()).To = string.Join(";", recipients);

    public void SetCcRecipients(IReadOnlyList<string> recipients) =>
        ((dynamic)GetMailItem()).CC = string.Join(";", recipients);

    public void SetRequestFingerprint(string requestFingerprint)
    {
        object properties = ((dynamic)GetMailItem()).UserProperties;
        object? property = null;
        try
        {
            property = ((dynamic)properties).Find(RequestFingerprintProperty, true);
            property ??= ((dynamic)properties).Add(
                RequestFingerprintProperty,
                OutlookComConstants.UserPropertyText,
                true);
            ((dynamic)property).Value = requestFingerprint;
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
        object? attachments = null;
        object? attachment = null;
        object? properties = null;
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(filePath, image.Data);
            attachments = ((dynamic)GetMailItem()).Attachments;
            attachment = ((dynamic)attachments).Add(filePath, OutlookComConstants.AttachmentByValue);
            properties = ((dynamic)attachment).PropertyAccessor;
            ((dynamic)properties).SetProperty("http://schemas.microsoft.com/mapi/proptag/0x3712001F", image.ContentId);
            ((dynamic)properties).SetProperty("http://schemas.microsoft.com/mapi/proptag/0x7FFE000B", true);
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

    public void Send() => ((dynamic)GetMailItem()).Send();

    public void Discard() => ((dynamic)GetMailItem()).Close(OutlookComConstants.InspectorDiscard);

    public void Dispose()
    {
        object? currentMailItem = Interlocked.Exchange(ref mailItem, null);
        ComReference.Release(currentMailItem);
    }

    private object GetMailItem() => mailItem ?? throw new ObjectDisposedException(nameof(OutlookComMailItem));
}

internal static class OutlookComConstants
{
    public const int MailItem = 0;
    public const int FolderOutbox = 4;
    public const int FolderSentMail = 5;
    public const int FolderDrafts = 16;
    public const int InspectorDiscard = 1;
    public const int AttachmentByValue = 1;
    public const int UserPropertyText = 1;
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
