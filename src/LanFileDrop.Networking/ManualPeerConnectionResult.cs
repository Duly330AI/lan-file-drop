namespace LanFileDrop.Networking;

public sealed record ManualPeerConnectionResult
{
    private ManualPeerConnectionResult(ManualPeerConnectionProbeStatus status, string? error)
    {
        Status = status;
        Error = error;
    }

    public ManualPeerConnectionProbeStatus Status { get; }
    public string? Error { get; }
    public bool Connected => Status == ManualPeerConnectionProbeStatus.Connected;

    public static ManualPeerConnectionResult Success() =>
        new(ManualPeerConnectionProbeStatus.Connected, error: null);

    public static ManualPeerConnectionResult Timeout() =>
        new(ManualPeerConnectionProbeStatus.Timeout, error: null);

    public static ManualPeerConnectionResult Failed(string error) =>
        new(ManualPeerConnectionProbeStatus.Failed, error);

    public static ManualPeerConnectionResult Cancelled() =>
        new(ManualPeerConnectionProbeStatus.Cancelled, error: null);
}
