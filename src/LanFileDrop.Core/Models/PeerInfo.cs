namespace LanFileDrop.Core.Models;

public sealed record PeerInfo
{
    public DeviceIdentity Identity { get; }
    public string? Endpoint { get; }

    private PeerInfo(DeviceIdentity identity, string? endpoint)
    {
        Identity = identity;
        Endpoint = endpoint;
    }

    public static PeerInfo Create(DeviceIdentity identity, string? endpoint = null)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (endpoint is not null && string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint must not be empty or whitespace when provided.", nameof(endpoint));
        }

        return new PeerInfo(identity, endpoint);
    }
}
