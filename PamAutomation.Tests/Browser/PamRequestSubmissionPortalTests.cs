using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Requests;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("Unit")]
public sealed class PamRequestSubmissionPortalTests
{
    [TestMethod]
    public void Create_ForeignSession_RejectsSession()
    {
        var factory = new PamRequestSubmissionPortalFactory();

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => factory.Create(new ForeignSession()));

        Assert.Contains("PamBrowserSessionFactory", exception.Message);
    }

    [TestMethod]
    public async Task SubmitAsync_BoundPage_ForwardsRequestAndSafetyOptions()
    {
        var page = new FakeSubmissionPage();
        string captureDirectory = Path.GetFullPath("fixture-captures");
        IPamRequestSubmissionPortal portal = new PamRequestSubmissionPortal(
            page,
            captureDirectory,
            maximumSubmissionAttempts: 1);
        var account = new PamFavoriteAccount("system", "application", "account");
        var form = new PamRequestFormData(
            new DateOnly(2026, 9, 10),
            new TimeOnly(9, 0),
            0,
            0,
            5,
            "Reason");

        PamRequestPortalOutcome result = await portal.SubmitAsync(
            account,
            form,
            CancellationToken.None);

        Assert.AreEqual(PamRequestSubmissionStatus.Submitted, result.Status);
        Assert.AreSame(account, page.Account);
        Assert.AreSame(form, page.Form);
        Assert.AreEqual(captureDirectory, page.CaptureDirectory);
        Assert.AreEqual(1, page.MaximumSubmissionAttempts);
    }

    [TestMethod]
    public void Classify_Success_ReturnsSubmitted()
    {
        PamRequestPortalOutcome outcome = PamSubmissionResponseClassifier.Classify(201, null);

        Assert.AreEqual(PamRequestSubmissionStatus.Submitted, outcome.Status);
        Assert.IsNull(outcome.ConflictCode);
    }

    [TestMethod]
    public void Classify_ApprovedAccessOverlap_ReturnsRecognizedConflict()
    {
        string payload = $$"""
            {
              "ErrorDetails": {
                "ErrorCode": {
                  "Key": "{{PamRequestConflictCodes.ApprovedAccessOverlap}}"
                }
              }
            }
            """;

        PamRequestPortalOutcome outcome = PamSubmissionResponseClassifier.Classify(400, payload);

        Assert.AreEqual(PamRequestSubmissionStatus.ApprovedAccessOverlap, outcome.Status);
        Assert.AreEqual(PamRequestConflictCodes.ApprovedAccessOverlap, outcome.ConflictCode);
    }

    [TestMethod]
    [DataRow(400, "not-json")]
    [DataRow(400, "[]")]
    [DataRow(400, "null")]
    [DataRow(400, "\"error\"")]
    [DataRow(401, "{}")]
    [DataRow(500, null)]
    public void Classify_UnknownResponse_ThrowsSafeFailure(int status, string? payload)
    {
        PamRequestSubmissionException exception =
            Assert.ThrowsExactly<PamRequestSubmissionException>(() =>
                PamSubmissionResponseClassifier.Classify(status, payload));

        Assert.AreEqual(PamRequestSubmissionErrorCode.RequestRejected, exception.ErrorCode);
        Assert.DoesNotContain("not-json", exception.Message);
    }

    private sealed class FakeSubmissionPage : IPamRequestSubmissionPage
    {
        public PamFavoriteAccount? Account { get; private set; }
        public PamRequestFormData? Form { get; private set; }
        public string? CaptureDirectory { get; private set; }
        public int? MaximumSubmissionAttempts { get; private set; }

        public Task<PamRequestPortalOutcome> SubmitRequestFormAsync(
            PamFavoriteAccount account,
            PamRequestFormData form,
            string? captureDirectory,
            int? maximumSubmissionAttempts,
            CancellationToken cancellationToken)
        {
            Account = account;
            Form = form;
            CaptureDirectory = captureDirectory;
            MaximumSubmissionAttempts = maximumSubmissionAttempts;
            return Task.FromResult(PamRequestPortalOutcome.Submitted());
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
