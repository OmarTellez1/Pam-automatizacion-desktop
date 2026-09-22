using System.Net;
using System.Text;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Requests;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("Unit")]
public sealed class PamPendingAtomicGridLoadTests
{
    private const string LocalBrowserOptInVariable = "PAM_AUTOMATION_ALLOW_LOCAL_BROWSER_GRID";
    private const string LocalBrowserOptInValue = "LOCAL_ONLY";

    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(100)]
    [DataRow(200)]
    public void ParsePendingGridSnapshot_LargeAtomicSnapshot_PreservesEveryRow(int rowCount)
    {
        PendingGridRowSnapshot[] snapshot = CreateSnapshotRows(startingRequestId: 200_000, rowCount);

        IReadOnlyList<PamPendingRequest> requests =
            PlaywrightManagedPage.ParsePendingGridSnapshot(snapshot);

        Assert.HasCount(rowCount, requests);
        Assert.HasCount(rowCount, requests.Select(static request => request.RequestId).Distinct());
        Assert.AreEqual("200000", requests[0].RequestId);
        Assert.AreEqual((200_000 + rowCount - 1).ToString(), requests[^1].RequestId);
    }

    [TestMethod]
    public void ParsePendingGridSnapshot_TwoPagesOfTwoHundredAndFiftyRows_PreservesUniqueIds()
    {
        PendingGridRowSnapshot[] firstPage = CreateSnapshotRows(startingRequestId: 300_000, rowCount: 200);
        PendingGridRowSnapshot[] secondPage = CreateSnapshotRows(startingRequestId: 300_200, rowCount: 50);

        PamPendingRequest[] requests =
        [
            .. PlaywrightManagedPage.ParsePendingGridSnapshot(firstPage),
            .. PlaywrightManagedPage.ParsePendingGridSnapshot(secondPage),
        ];

        Assert.HasCount(250, requests);
        Assert.HasCount(250, requests.Select(static request => request.RequestId).Distinct());
        Assert.AreEqual("300000", requests[0].RequestId);
        Assert.AreEqual("300249", requests[^1].RequestId);
    }

    [TestMethod]
    [TestCategory("ExplicitLocalBrowser")]
    [Timeout(120_000)]
    public async Task CapturePendingGridSnapshotAsync_LocalEdgeGrid_UsesOneBulkSnapshotAtEverySupportedSize()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(LocalBrowserOptInVariable),
            LocalBrowserOptInValue,
            StringComparison.Ordinal))
        {
            return;
        }

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "msedge",
            Headless = false,
        });
        IPage page = await browser.NewPageAsync();

        foreach (int rowCount in new[] { 30, 60, 100, 200 })
        {
            await page.SetContentAsync(CreateGridHtml(startingRequestId: 400_000, rowCount));
            ILocator rows = page.Locator(PamPendingSelectors.Rows);

            PendingGridRowSnapshot[] snapshot = await PlaywrightManagedPage
                .CapturePendingGridSnapshotAsync(rows, TestContext.CancellationToken);
            IReadOnlyList<PamPendingRequest> requests =
                PlaywrightManagedPage.ParsePendingGridSnapshot(snapshot);

            Assert.HasCount(rowCount, snapshot, $"La captura masiva perdió filas con {rowCount} registros.");
            Assert.HasCount(rowCount, requests, $"El parser perdió filas con {rowCount} registros.");
        }

        PamPendingRequest[] paginated = [];
        foreach ((int start, int count) in new[] { (500_000, 200), (500_200, 50) })
        {
            await page.SetContentAsync(CreateGridHtml(start, count));
            PendingGridRowSnapshot[] snapshot = await PlaywrightManagedPage
                .CapturePendingGridSnapshotAsync(
                    page.Locator(PamPendingSelectors.Rows),
                    TestContext.CancellationToken);
            paginated =
            [
                .. paginated,
                .. PlaywrightManagedPage.ParsePendingGridSnapshot(snapshot),
            ];
        }

        Assert.HasCount(250, paginated);
        Assert.HasCount(250, paginated.Select(static request => request.RequestId).Distinct());
    }

    public TestContext TestContext { get; set; }

    private static PendingGridRowSnapshot[] CreateSnapshotRows(int startingRequestId, int rowCount) =>
        Enumerable.Range(0, rowCount)
            .Select(index => CreateSnapshotRow((startingRequestId + index).ToString()))
            .ToArray();

    private static PendingGridRowSnapshot CreateSnapshotRow(string requestId) => new()
    {
        Cells =
        [
            CreateSnapshotCell(PamPendingSelectors.RequestIdColumn, requestId),
            CreateSnapshotCell(PamPendingSelectors.SystemColumn, "system"),
            CreateSnapshotCell(PamPendingSelectors.ApplicationColumn, "application"),
            CreateSnapshotCell(PamPendingSelectors.AccountColumn, "account"),
            CreateSnapshotCell(PamPendingSelectors.ReleaseColumn, "Sep 22, 2026, 8:30 AM"),
            CreateSnapshotCell(PamPendingSelectors.ExpiresColumn, "Sep 22, 2026, 9:30 AM"),
            CreateSnapshotCell(PamPendingSelectors.ResponseStatusColumn, "Pending"),
            CreateSnapshotCell(PamPendingSelectors.DescriptionColumn, "Solicitud de acceso PAM"),
        ],
    };

    private static PendingGridCellSnapshot CreateSnapshotCell(string columnId, string value) => new()
    {
        ColumnId = columnId,
        Value = value,
    };

    private static string CreateGridHtml(int startingRequestId, int rowCount)
    {
        var html = new StringBuilder("<table><tbody>");
        for (int index = 0; index < rowCount; index++)
        {
            string requestId = (startingRequestId + index).ToString();
            html.Append("<tr aria-label=\"Row ")
                .Append(index + 1)
                .Append("\">")
                .Append(CreateCell(PamPendingSelectors.RequestIdColumn, requestId))
                .Append(CreateCell(PamPendingSelectors.SystemColumn, $"system-{index}"))
                .Append(CreateCell(PamPendingSelectors.ApplicationColumn, $"application-{index}"))
                .Append(CreateCell(PamPendingSelectors.AccountColumn, $"account-{index}"))
                .Append(CreateCell(PamPendingSelectors.ReleaseColumn, "Sep 22, 2026, 8:30 AM"))
                .Append(CreateCell(PamPendingSelectors.ExpiresColumn, "Sep 22, 2026, 9:30 AM"))
                .Append(CreateCell(PamPendingSelectors.ResponseStatusColumn, "Pending"))
                .Append(CreateCell(PamPendingSelectors.DescriptionColumn, "Solicitud de acceso PAM"))
                .Append("</tr>");
        }

        return html.Append("</tbody></table>").ToString();
    }

    private static string CreateCell(string columnId, string value) =>
        $"<td data-column-id=\"{WebUtility.HtmlEncode(columnId)}\">{WebUtility.HtmlEncode(value)}</td>";
}
