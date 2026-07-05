using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class TransferDecisionTests
{
    [Fact]
    public void Accept_SetsAcceptedTrueAndNoReason()
    {
        var decision = TransferDecision.Accept();

        Assert.True(decision.Accepted);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public void Reject_WithoutReason_SetsAcceptedFalse()
    {
        var decision = TransferDecision.Reject();

        Assert.False(decision.Accepted);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public void Reject_WithReason_SetsReason()
    {
        var decision = TransferDecision.Reject("Not expecting a transfer right now.");

        Assert.False(decision.Accepted);
        Assert.Equal("Not expecting a transfer right now.", decision.Reason);
    }
}
