using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class DeviceIdentityTests
{
    [Fact]
    public void Create_WithValidDisplayName_SetsProperties()
    {
        var identity = DeviceIdentity.Create("Living Room PC");

        Assert.Equal("Living Room PC", identity.DisplayName);
        Assert.NotEqual(Guid.Empty, identity.InstanceId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyDisplayName_Throws(string? displayName)
    {
        Assert.Throws<ArgumentException>(() => DeviceIdentity.Create(displayName!));
    }

    [Fact]
    public void Create_TrimsDisplayName()
    {
        var identity = DeviceIdentity.Create("  Office PC  ");

        Assert.Equal("Office PC", identity.DisplayName);
    }

    [Fact]
    public void Create_WithExplicitInstanceId_UsesProvidedValue()
    {
        var instanceId = Guid.NewGuid();

        var identity = DeviceIdentity.Create("PC", instanceId);

        Assert.Equal(instanceId, identity.InstanceId);
    }
}
