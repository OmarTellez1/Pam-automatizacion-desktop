using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Requests;

namespace PamAutomation.Tests.Core.Execution;

[TestClass]
public sealed class PamExecutionModelsTests
{
    [TestMethod]
    public void Result_CopiesRequestIdsAndRedactsTextRepresentation()
    {
        var ids = new List<string> { "123", "456" };
        var result = new PamExecutionResult(
            PamExecutionMode.Real,
            new DateTimeOffset(2026, 9, 10, 17, 0, 0, TimeSpan.FromHours(-5)),
            FinalAction.DisplayRequestIds,
            favoriteCount: 3,
            processedCount: 3,
            conflictCount: 1,
            ids);

        ids[0] = "secret";

        Assert.AreEqual("123", result.RequestIds[0]);
        Assert.DoesNotContain("123", result.ToString());
    }

    [TestMethod]
    public void Result_RejectsDuplicateRequestIds()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new PamExecutionResult(
            PamExecutionMode.Real,
            DateTimeOffset.UtcNow,
            FinalAction.DisplayRequestIds,
            2,
            2,
            0,
            ["123", "123"]));
    }

    [TestMethod]
    public void Preparation_CopiesRequestSettings()
    {
        var request = new RequestSettings
        {
            StartDate = new DateOnly(2026, 9, 10),
            StartTime = new TimeOnly(17, 0),
            SessionHours = 1,
            RequestReason = "Validación"
        };

        var preparation = new PamExecutionPreparation(14, request, FinalAction.DisplayRequestIds);

        Assert.AreNotSame(request, preparation.Request);
        Assert.AreEqual(request, preparation.Request);
    }

    [TestMethod]
    public void ValidateReconciledRequestIds_NewIdsWithExpectedCount_AcceptsResult()
    {
        var preparation = new PamExecutionPreparation(
            2,
            CreateRequestSettings(),
            FinalAction.DisplayRequestIds,
            pendingRequestIdsBeforeSubmission: ["100", "101"]);

        preparation.ValidateReconciledRequestIds(["102", "103"], submittedCount: 2);
    }

    [TestMethod]
    public void ValidateReconciledRequestIds_PreexistingId_RejectsResultBeforePresentationOrEmail()
    {
        var preparation = new PamExecutionPreparation(
            2,
            CreateRequestSettings(),
            FinalAction.SendEmail,
            pendingRequestIdsBeforeSubmission: ["100", "101"]);

        PamPendingException exception = Assert.ThrowsExactly<PamPendingException>(
            () => preparation.ValidateReconciledRequestIds(["101", "102"], submittedCount: 2));

        Assert.AreEqual(PamPendingErrorCode.FieldMismatch, exception.ErrorCode);
    }

    [TestMethod]
    public void ValidateReconciledRequestIds_MissingNewId_RejectsIncompleteResult()
    {
        var preparation = new PamExecutionPreparation(
            2,
            CreateRequestSettings(),
            FinalAction.DisplayRequestIds,
            pendingRequestIdsBeforeSubmission: ["100"]);

        Assert.ThrowsExactly<PamPendingException>(
            () => preparation.ValidateReconciledRequestIds(["101"], submittedCount: 2));
    }

    [TestMethod]
    public void Preparation_CompleteExistingIdsAndEmailAction_AllowsReuse()
    {
        var preparation = new PamExecutionPreparation(
            2,
            CreateRequestSettings(),
            FinalAction.SendEmail,
            pendingRequestIdsBeforeSubmission: ["100", "101", "999"],
            exactPendingDuplicateCount: 2,
            reusablePendingRequestIds: ["100", "101"]);

        Assert.IsTrue(preparation.CanReusePendingRequestIdsForEmail);
        CollectionAssert.AreEqual(new[] { "100", "101" }, preparation.ReusablePendingRequestIds.ToArray());
    }

    [TestMethod]
    public void Preparation_ExistingIdsWithDisplayAction_DoesNotAllowEmailReuse()
    {
        var preparation = new PamExecutionPreparation(
            1,
            CreateRequestSettings(),
            FinalAction.DisplayRequestIds,
            pendingRequestIdsBeforeSubmission: ["100"],
            exactPendingDuplicateCount: 1,
            reusablePendingRequestIds: ["100"]);

        Assert.IsFalse(preparation.CanReusePendingRequestIdsForEmail);
    }

    [TestMethod]
    public void Preparation_ReusableIdOutsideBaseline_RejectsUnsafeSet()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new PamExecutionPreparation(
            1,
            CreateRequestSettings(),
            FinalAction.SendEmail,
            pendingRequestIdsBeforeSubmission: ["100"],
            exactPendingDuplicateCount: 1,
            reusablePendingRequestIds: ["101"]));
    }

    private static RequestSettings CreateRequestSettings() => new()
    {
        StartDate = new DateOnly(2026, 9, 10),
        StartTime = new TimeOnly(17, 0),
        SessionHours = 1,
        RequestReason = "Validación",
    };
}
