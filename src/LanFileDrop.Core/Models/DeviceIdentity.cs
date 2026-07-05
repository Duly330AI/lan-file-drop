namespace LanFileDrop.Core.Models;

public sealed record DeviceIdentity
{
    public string DisplayName { get; }
    public Guid InstanceId { get; }
    public DateTimeOffset CreatedUtc { get; }

    private DeviceIdentity(string displayName, Guid instanceId, DateTimeOffset createdUtc)
    {
        DisplayName = displayName;
        InstanceId = instanceId;
        CreatedUtc = createdUtc;
    }

    public static DeviceIdentity Create(string displayName, Guid? instanceId = null, DateTimeOffset? createdUtc = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name must not be empty.", nameof(displayName));
        }

        return new DeviceIdentity(displayName.Trim(), instanceId ?? Guid.NewGuid(), createdUtc ?? DateTimeOffset.UtcNow);
    }
}
