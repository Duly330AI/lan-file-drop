namespace LanFileDrop.Networking.Tests;

public class AssemblyMarkerTests
{
    [Fact]
    public void CoreAndNetworkingAssembliesAreReferencable()
    {
        Assert.NotNull(typeof(Core.AssemblyMarker));
        Assert.NotNull(typeof(Networking.AssemblyMarker));
    }
}
