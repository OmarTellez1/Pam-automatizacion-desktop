using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Requests;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("Unit")]
public sealed class PamPendingPortalTests
{
    [TestMethod]
    public void Create_ForeignSession_RejectsSession()
    {
        var factory = new PamPendingPortalFactory();

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => factory.Create(new ForeignSession()));

        Assert.Contains("PamBrowserSessionFactory", exception.Message);
    }

    [TestMethod]
    public async Task ReadAllAsync_BoundPage_ForwardsSnapshotAndCaptureDirectory()
    {
        IReadOnlyList<PamPendingRequest> requests = [CreatePending()];
        var page = new FakePendingPage(requests);
        string captureDirectory = Path.GetFullPath("fixture-captures");
        IPamPendingPortal portal = new PamPendingPortal(page, captureDirectory);

        IReadOnlyList<PamPendingRequest> result = await portal.ReadAllAsync(CancellationToken.None);

        Assert.AreSame(requests, result);
        Assert.AreEqual(captureDirectory, page.CaptureDirectory);
    }

    [TestMethod]
    [DataRow("Page 1/1", 1, 1)]
    [DataRow("page 2/15", 2, 15)]
    [DataRow(" Page 10/10 ", 10, 10)]
    public void Pagination_ValidLabel_ReturnsPageInformation(
        string label,
        int expectedCurrent,
        int expectedTotal)
    {
        (int current, int total) = PamPendingPagination.Parse(label);

        Assert.AreEqual(expectedCurrent, current);
        Assert.AreEqual(expectedTotal, total);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Page 1 of 2")]
    [DataRow("Page 3/2")]
    [DataRow("Page 0/0")]
    public void Pagination_InvalidLabel_ThrowsSafeError(string? label)
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => PamPendingPagination.Parse(label));

        Assert.AreEqual("El paginador de Pending no tiene un formato válido.", exception.Message);
    }

    [TestMethod]
    [DataRow("Sep 10, 2026, 8:20 AM", 8, 20)]
    [DataRow("September 10, 2026, 2:05 PM", 14, 5)]
    [DataRow("  Sep  10,  2026,  12:00 AM  ", 0, 0)]
    public void StartParser_ValidEnglishValue_ReturnsTypedStart(
        string value,
        int expectedHour,
        int expectedMinute)
    {
        (DateOnly date, TimeOnly time) = PamPendingStartParser.Parse(value);

        Assert.AreEqual(new DateOnly(2026, 9, 10), date);
        Assert.AreEqual(new TimeOnly(expectedHour, expectedMinute), time);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("2026-09-10 08:20")]
    [DataRow("Sep 31, 2026, 8:20 AM")]
    public void StartParser_InvalidValue_ThrowsSafeError(string? value)
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => PamPendingStartParser.Parse(value));

        Assert.AreEqual("Pending contiene un Request Start Date inválido.", exception.Message);
    }

    private static PamPendingRequest CreatePending() => new(
        "170001",
        "system",
        "application",
        "account",
        new DateOnly(2026, 9, 10),
        new TimeOnly(8, 20),
        new DateOnly(2026, 9, 10),
        new TimeOnly(8, 25),
        "Pending",
        "description");

    private sealed class FakePendingPage(IReadOnlyList<PamPendingRequest> requests) : IPamPendingPage
    {
        public string? CaptureDirectory { get; private set; }

        public Task<IReadOnlyList<PamPendingRequest>> ReadAllPendingAsync(
            string? captureDirectory,
            CancellationToken cancellationToken)
        {
            CaptureDirectory = captureDirectory;
            return Task.FromResult(requests);
        }
    }

    private sealed class ForeignSession : IPamBrowserSession
    {
        public Task<PamNavigationResponse> NavigateAsync(
            Uri uri,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
