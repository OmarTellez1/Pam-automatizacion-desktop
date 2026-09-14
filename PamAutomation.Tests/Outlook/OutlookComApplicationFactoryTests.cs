using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Outlook.Interop;
using System.Runtime.InteropServices;

namespace PamAutomation.Tests.Outlook;

[TestClass]
[TestCategory("Unit")]
public sealed class OutlookComApplicationFactoryTests
{
    [TestMethod]
    public void Create_LateBoundApplication_TraversesAccountAndTemporaryMessageWithoutPia()
    {
        // Arrange
        var nativeApplication = new FakeApplication("sender@example.test");
        var activator = new FakeActivator(nativeApplication);
        var factory = new OutlookComApplicationFactory(activator, new FakeLauncher(), new FakeWaiter());

        // Act
        using IOutlookComApplication application = factory.Create();
        IReadOnlyList<IOutlookComAccount> accounts = application.GetAccounts();
        using IOutlookComAccount account = accounts[0];
        using IOutlookComMailItem message = application.CreateMailItem();
        message.SetSendUsingAccount(account);
        message.Discard();

        // Assert
        Assert.AreEqual(1, activator.CreateCount);
        Assert.AreEqual("sender@example.test", account.SmtpAddress);
        Assert.AreEqual(0, nativeApplication.LastItemType);
        Assert.AreSame(nativeApplication.Session.Accounts.Item(1), nativeApplication.MailItem.SendUsingAccount);
        Assert.AreEqual(1, nativeApplication.MailItem.CloseMode);
    }

    [TestMethod]
    public void Create_RunningApplication_UsesExistingInstanceWithoutLaunching()
    {
        var nativeApplication = new FakeApplication("sender@example.test");
        var activator = new FakeActivator(nativeApplication) { RunningApplication = nativeApplication };
        var launcher = new FakeLauncher();
        var factory = new OutlookComApplicationFactory(activator, launcher, new FakeWaiter());

        using IOutlookComApplication application = factory.Create();

        Assert.AreEqual(1, activator.RunningLookupCount);
        Assert.AreEqual(0, activator.CreateCount);
        Assert.AreEqual(0, launcher.StartCount);
    }

    [TestMethod]
    public void Create_ServerExecutionFailure_LaunchesRegisteredOutlookAndUsesRecoveredInstance()
    {
        var nativeApplication = new FakeApplication("sender@example.test");
        var activator = new FakeActivator(nativeApplication)
        {
            CreateFailure = new COMException("Synthetic server failure.", unchecked((int)0x80080005))
        };
        var launcher = new FakeLauncher(() => activator.RunningApplication = nativeApplication);
        var waiter = new FakeWaiter();
        var factory = new OutlookComApplicationFactory(activator, launcher, waiter);

        using IOutlookComApplication application = factory.Create();

        Assert.AreEqual(1, activator.CreateCount);
        Assert.AreEqual(2, activator.RunningLookupCount);
        Assert.AreEqual(1, launcher.StartCount);
        Assert.AreEqual(1, waiter.WaitCount);
    }

    [TestMethod]
    public void Create_UnrelatedComFailure_DoesNotLaunchOrRetry()
    {
        var activator = new FakeActivator(new FakeApplication("sender@example.test"))
        {
            CreateFailure = new COMException("Synthetic registration failure.", unchecked((int)0x80040154))
        };
        var launcher = new FakeLauncher();
        var waiter = new FakeWaiter();
        var factory = new OutlookComApplicationFactory(activator, launcher, waiter);

        COMException exception = Assert.ThrowsExactly<COMException>(() => factory.Create());

        Assert.AreEqual(unchecked((int)0x80040154), exception.HResult);
        Assert.AreEqual(0, launcher.StartCount);
        Assert.AreEqual(0, waiter.WaitCount);
    }

    [TestMethod]
    public void Create_RecoveryFails_ThrowsControlledUnavailableFailure()
    {
        var activator = new FakeActivator(new FakeApplication("sender@example.test"))
        {
            CreateFailure = new COMException("Synthetic server failure.", unchecked((int)0x80080005))
        };
        var factory = new OutlookComApplicationFactory(
            activator,
            new FakeLauncher(),
            new FakeWaiter());

        OutlookComUnavailableException exception =
            Assert.ThrowsExactly<OutlookComUnavailableException>(() => factory.Create());

        Assert.AreEqual(unchecked((int)0x80080005), exception.HResult);
        Assert.AreEqual(2, activator.CreateCount);
    }

    private sealed class FakeActivator(FakeApplication application) : IOutlookComActivator
    {
        public object? RunningApplication { get; set; }
        public Exception? CreateFailure { get; init; }
        public int CreateCount { get; private set; }
        public int RunningLookupCount { get; private set; }

        public object? TryGetRunningApplication()
        {
            RunningLookupCount++;
            return RunningApplication;
        }

        public object CreateApplication()
        {
            CreateCount++;
            if (CreateFailure is not null) throw CreateFailure;
            return application;
        }
    }

    private sealed class FakeLauncher(Action? onStart = null) : IOutlookApplicationLauncher
    {
        public int StartCount { get; private set; }

        public void Start()
        {
            StartCount++;
            onStart?.Invoke();
        }
    }

    private sealed class FakeWaiter : IOutlookStartupWaiter
    {
        public int WaitCount { get; private set; }
        public void Wait() => WaitCount++;
    }

    public sealed class FakeApplication(string smtpAddress)
    {
        public FakeSession Session { get; } = new(smtpAddress);
        public FakeMailItem MailItem { get; } = new();
        public int LastItemType { get; private set; } = -1;

        public FakeMailItem CreateItem(int itemType)
        {
            LastItemType = itemType;
            return MailItem;
        }
    }

    public sealed class FakeSession(string smtpAddress)
    {
        public FakeAccounts Accounts { get; } = new(smtpAddress);
    }

    public sealed class FakeAccounts(string smtpAddress)
    {
        private readonly FakeAccount account = new(smtpAddress);
        public int Count => 1;
        public FakeAccount Item(int index) => index == 1 ? account : throw new IndexOutOfRangeException();
    }

    public sealed class FakeAccount(string smtpAddress)
    {
        public string SmtpAddress { get; } = smtpAddress;
    }

    public sealed class FakeMailItem
    {
        public object? SendUsingAccount { get; set; }
        public int? CloseMode { get; private set; }
        public void Close(int mode) => CloseMode = mode;
    }
}
