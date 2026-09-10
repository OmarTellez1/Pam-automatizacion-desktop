using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Models.Configuration;

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
}
