using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Outlook.Interop;

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
        var factory = new OutlookComApplicationFactory(activator);

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

    private sealed class FakeActivator(FakeApplication application) : IOutlookComActivator
    {
        public int CreateCount { get; private set; }

        public object CreateApplication()
        {
            CreateCount++;
            return application;
        }
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
