namespace LanFileDrop.Networking;

public sealed record ManualPeerTransferSendResult
{
    public ManualPeerTransferStatus Status { get; }
    public string? Reason { get; }

    public bool Success => Status == ManualPeerTransferStatus.Completed;

    private ManualPeerTransferSendResult(ManualPeerTransferStatus status, string? reason)
    {
        Status = status;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
    }

    internal static ManualPeerTransferSendResult Create(ManualPeerTransferStatus status, string? reason = null) =>
        new(status, reason);
}
