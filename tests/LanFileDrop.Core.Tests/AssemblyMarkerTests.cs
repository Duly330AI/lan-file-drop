namespace LanFileDrop.Core.Tests;

public class AssemblyMarkerTests
{
    [Fact]
    public void CoreAssemblyIsReferencable()
    {
        Assert.NotNull(typeof(AssemblyMarker));
    }
}
