using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class TransferStateTests
{
    [Theory]
    [InlineData(TransferState.Pending)]
    [InlineData(TransferState.Accepted)]
    [InlineData(TransferState.Rejected)]
    [InlineData(TransferState.InProgress)]
    [InlineData(TransferState.Completed)]
    [InlineData(TransferState.Failed)]
    [InlineData(TransferState.Cancelled)]
    public void AllExpectedStates_AreDefined(TransferState state)
    {
        Assert.True(Enum.IsDefined(state));
    }

    [Fact]
    public void ExactlySevenStates_AreDefined()
    {
        var values = Enum.GetValues<TransferState>();

        Assert.Equal(7, values.Length);
    }
}
