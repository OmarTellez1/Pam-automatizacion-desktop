using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Outlook;
using PamAutomation.Outlook.Interop;
using System.Runtime.InteropServices;

namespace PamAutomation.Tests.Outlook;

[TestClass]
[TestCategory("Unit")]
public sealed class OutlookMessageLookupTests
{
    [TestMethod]
    public void Lookup_FiltersSubjectBeforeOpeningMessages_AndEscapesLiteral()
    {
        var store = new FakeStore();
        const string subject = "PAM d'Operación \"test\"";
        store.Sent.Items.Candidates.Messages.Add(new FakeMessage(subject, "10,20"));
        using var application = new OutlookComApplication(new FakeApplication(store));

        var status = application.FindExistingMessage("sender@example.test", subject, "10,20", sentOnly: true);

        Assert.AreEqual(OutlookExistingMessageStatus.Sent, status);
        Assert.AreEqual("@SQL=\"http://schemas.microsoft.com/mapi/proptag/0x0037001f\" = 'PAM d''Operación \"test\"'", store.Sent.Items.LastFilter);
        CollectionAssert.AreEqual(new[] { 5 }, store.VisitedFolders);
    }

    [TestMethod]
    public void Lookup_SentOnly_DoesNotOpenLockedOutboxOrDrafts()
    {
        var store = new FakeStore { RejectUnsentFolders = true };
        using var application = new OutlookComApplication(new FakeApplication(store));

        var status = application.FindExistingMessage("sender@example.test", "PAM", "10", sentOnly: true);

        Assert.AreEqual(OutlookExistingMessageStatus.None, status);
        CollectionAssert.AreEqual(new[] { 5 }, store.VisitedFolders);
    }

    [TestMethod]
    [DataRow(4, OutlookExistingMessageStatus.Outbox)]
    [DataRow(16, OutlookExistingMessageStatus.Draft)]
    public void Lookup_AllFolders_FindsUnsentMessage(int folderKind, OutlookExistingMessageStatus expected)
    {
        var store = new FakeStore();
        (folderKind == 4 ? store.Outbox : store.Drafts).Items.Candidates.Messages.Add(new FakeMessage("PAM", "10"));
        using var application = new OutlookComApplication(new FakeApplication(store));

        Assert.AreEqual(expected, application.FindExistingMessage("sender@example.test", "PAM", "10"));
        Assert.Contains(folderKind, store.VisitedFolders);
    }

    [TestMethod]
    public void Lookup_ValidatesClassBeforeSubject_AndRequiresExactSubjectAndFingerprint()
    {
        var store = new FakeStore();
        store.Sent.Items.Candidates.Messages.AddRange([
            new FakeMessage("PAM", "10") { MessageClass = "REPORT.IPM.Note.NDR", RejectSubject = true },
            new FakeMessage("pam", "10"),
            new FakeMessage("PAM", "100"),
            new FakeMessage("PAM", null)]);
        using var application = new OutlookComApplication(new FakeApplication(store));

        Assert.AreEqual(OutlookExistingMessageStatus.None,
            application.FindExistingMessage("sender@example.test", "PAM", "10", sentOnly: true));

        store.Sent.Items.Candidates.Messages.Add(new FakeMessage("PAM", "10"));
        Assert.AreEqual(OutlookExistingMessageStatus.Sent,
            application.FindExistingMessage("sender@example.test", "PAM", "10", sentOnly: true));
    }

    [TestMethod]
    public void Lookup_UnreadableCandidate_ThrowsWithSafeLocationInsteadOfReturningAbsent()
    {
        var store = new FakeStore();
        store.Sent.Items.Candidates.Messages.Add(new FakeMessage("private subject", "12345") { RejectFingerprint = true });
        using var application = new OutlookComApplication(new FakeApplication(store));

        var error = Assert.ThrowsExactly<OutlookMessageLookupException>(() =>
            application.FindExistingMessage("sender@example.test", "private subject", "12345"));

        Assert.AreEqual("Sent", error.Folder);
        Assert.AreEqual("ReadUserProperties", error.Operation);
        Assert.AreEqual(1, error.CandidateCount);
        Assert.AreEqual(1, error.CandidateIndex);
        Assert.AreEqual(unchecked((int)0x80040114), error.HResult);
        Assert.DoesNotContain("private subject", error.Message);
        Assert.DoesNotContain("12345", error.Message);
        Assert.DoesNotContain("sender@example.test", error.Message);
        CollectionAssert.AreEqual(new[] { 5 }, store.VisitedFolders);
    }

    [TestMethod]
    public void Lookup_FilterFailure_PreservesStageAndDoesNotScanWholeFolder()
    {
        var store = new FakeStore();
        store.Sent.Items.RejectFilter = true;
        using var application = new OutlookComApplication(new FakeApplication(store));

        var error = Assert.ThrowsExactly<OutlookMessageLookupException>(() =>
            application.FindExistingMessage("sender@example.test", "PAM", "10"));

        Assert.AreEqual("RestrictSubject", error.Operation);
        Assert.IsNull(error.CandidateCount);
        Assert.AreEqual(unchecked((int)0x80070005), error.HResult);
    }

    // Public types let the production late-bound COM adapter exercise real traversal with safe doubles.
    public sealed class FakeApplication(FakeStore store)
    {
        public FakeSession Session { get; } = new(store);
    }

    public sealed class FakeSession(FakeStore store)
    {
        public FakeAccounts Accounts { get; } = new(store);
    }

    public sealed class FakeAccounts(FakeStore store)
    {
        public int Count => 1;
        public FakeAccount Item(int index) => index == 1 ? new(store) : throw new IndexOutOfRangeException();
    }

    public sealed class FakeAccount(FakeStore store)
    {
        public string SmtpAddress => "sender@example.test";
        public FakeStore DeliveryStore => store;
    }

    public sealed class FakeStore
    {
        public FakeFolder Sent { get; } = new();
        public FakeFolder Outbox { get; } = new();
        public FakeFolder Drafts { get; } = new();
        public List<int> VisitedFolders { get; } = [];
        public bool RejectUnsentFolders { get; init; }
        public FakeFolder GetDefaultFolder(int kind)
        {
            VisitedFolders.Add(kind);
            if (RejectUnsentFolders && kind != 5) throw new COMException("Locked.", unchecked((int)0x80040114));
            return kind switch { 5 => Sent, 4 => Outbox, 16 => Drafts, _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
        }
    }

    public sealed class FakeFolder
    {
        public FakeItems Items { get; } = new();
    }

    public sealed class FakeItems
    {
        public FakeCandidates Candidates { get; } = new();
        public string? LastFilter { get; private set; }
        public bool RejectFilter { get; set; }
        public int Count => throw new AssertFailedException("Must not enumerate unfiltered messages.");
        public object Item(int index) => throw new AssertFailedException("Must only open filtered candidates.");
        public FakeCandidates Restrict(string filter)
        {
            LastFilter = filter;
            if (RejectFilter) throw new COMException("Denied.", unchecked((int)0x80070005));
            return Candidates;
        }
    }

    public sealed class FakeCandidates
    {
        public List<FakeMessage> Messages { get; } = [];
        public int Count => Messages.Count;
        public FakeMessage Item(int index) => Messages[index - 1];
    }

    public sealed class FakeMessage(string subject, string? fingerprint)
    {
        public string MessageClass { get; init; } = "IPM.Note";
        public bool RejectSubject { get; init; }
        public bool RejectFingerprint { get; init; }
        public string Subject => RejectSubject ? throw new AssertFailedException("Check message class first.") : subject;
        public FakeProperties UserProperties => RejectFingerprint
            ? throw new COMException("Locked.", unchecked((int)0x80040114)) : new(fingerprint);
    }

    public sealed class FakeProperties(string? fingerprint)
    {
        public FakeProperty? Find(string name, bool custom) =>
            name == "PamAutomationRequestIds" && custom && fingerprint is not null ? new(fingerprint) : null;
    }

    public sealed class FakeProperty(string value)
    {
        public string Value => value;
    }
}
