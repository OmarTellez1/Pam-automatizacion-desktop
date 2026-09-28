using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Tests.Core.Execution;

[TestClass]
public sealed class RequestIdAccumulatorTests
{
    internal static PamExecutionResult Batch(params string[] ids) => new(
        PamExecutionMode.Real, DateTimeOffset.UtcNow, FinalAction.DisplayRequestIds,
        ids.Length, ids.Length, 0, ids);

    [TestMethod]
    public void CompleteBatches_EightPlusSix_DeduplicatesAndKeepsOrder()
    {
        var accumulator = new RequestIdAccumulator();
        Guid collection = Guid.NewGuid();
        accumulator.SetEnabled(true);
        string[] eight = Enumerable.Range(100, 8).Select(i => i.ToString()).ToArray();
        string[] six = Enumerable.Range(108, 6).Select(i => i.ToString()).ToArray();
        accumulator.Add(collection, Batch(eight));
        accumulator.Add(collection, Batch(six));
        accumulator.Add(collection, Batch("100", "113"));
        CollectionAssert.AreEqual(eight.Concat(six).ToArray(), accumulator.RequestIds.ToArray());
        Assert.AreEqual(14, accumulator.Count);
        Assert.ThrowsExactly<InvalidOperationException>(() => accumulator.SetEnabled(false));
        Assert.ThrowsExactly<InvalidOperationException>(() => accumulator.Add(Guid.NewGuid(), Batch("114")));
        Assert.AreEqual(14, accumulator.Count);
    }

    [TestMethod]
    [DataRow(PamExecutionMode.Real, 3, 2, "102")]
    [DataRow(PamExecutionMode.Real, 3, 3, "102")]
    [DataRow(PamExecutionMode.Test, 1, 1, "102")]
    [DataRow(PamExecutionMode.ExistingPending, 1, 1, "102")]
    [DataRow(PamExecutionMode.Real, 1, 1, "invalid")]
    public void InvalidBatch_RejectsEntireBatchAndRetainsPrevious(PamExecutionMode mode, int total, int processed, string id)
    {
        var accumulator = new RequestIdAccumulator();
        accumulator.SetEnabled(true);
        Guid collection = Guid.NewGuid();
        accumulator.Add(collection, Batch("100", "101"));
        var invalid = new PamExecutionResult(mode, DateTimeOffset.UtcNow, FinalAction.DisplayRequestIds, total, processed, 0, [id]);
        Assert.ThrowsExactly<InvalidOperationException>(() => accumulator.Add(collection, invalid));
        CollectionAssert.AreEqual(new[] { "100", "101" }, accumulator.RequestIds.ToArray());
    }

    [TestMethod]
    public void EmptyAndFreshSession_DoNotLockCollection()
    {
        var accumulator = new RequestIdAccumulator();
        accumulator.SetEnabled(true);
        accumulator.Add(Guid.NewGuid(), Batch());
        Assert.IsNull(accumulator.CollectionId);
        accumulator.EnsureSelectionAllowed(Guid.NewGuid());
        accumulator.SetEnabled(false);
        Assert.ThrowsExactly<InvalidOperationException>(() => accumulator.BeginMailOperation(false));
        Assert.AreEqual(0, new RequestIdAccumulator().Count);
    }

    [TestMethod]
    public void MailState_SafeFailureRetries_UncertainOnlyObserves_ConfirmedUnlocks()
    {
        var accumulator = new RequestIdAccumulator();
        accumulator.SetEnabled(true);
        Guid collection = Guid.NewGuid();
        accumulator.Add(collection, Batch("100"));
        accumulator.BeginMailOperation(false);
        Assert.ThrowsExactly<InvalidOperationException>(() => accumulator.Discard());
        Assert.ThrowsExactly<InvalidOperationException>(() => accumulator.Add(collection, Batch("101")));
        Assert.ThrowsExactly<InvalidOperationException>(() => accumulator.BeginMailOperation(false));
        accumulator.CompleteMailOperation(false, false);
        accumulator.BeginMailOperation(false);
        accumulator.CompleteMailOperation(false, true);
        Assert.AreEqual(1, accumulator.Count);
        Assert.ThrowsExactly<InvalidOperationException>(() => accumulator.BeginMailOperation(false));
        accumulator.BeginMailOperation(true);
        accumulator.CompleteMailOperation(true, false);
        Assert.AreEqual(0, accumulator.Count);
        Assert.IsNull(accumulator.CollectionId);
        Assert.IsTrue(accumulator.IsEnabled);
        accumulator.EnsureSelectionAllowed(Guid.NewGuid());
    }

    [TestMethod]
    public void ExplicitDiscard_UncertainSend_ClearsAndUnlocks()
    {
        var accumulator = new RequestIdAccumulator();
        accumulator.SetEnabled(true);
        accumulator.Add(Guid.NewGuid(), Batch("100"));
        accumulator.BeginMailOperation(false);
        accumulator.CompleteMailOperation(false, true);
        accumulator.Discard();
        Assert.AreEqual(0, accumulator.Count);
        Assert.AreEqual(AccumulatedMailState.Ready, accumulator.MailState);
        accumulator.SetEnabled(false);
    }
}
