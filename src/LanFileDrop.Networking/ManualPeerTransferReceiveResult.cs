namespace LanFileDrop.Networking;

public sealed record ManualPeerTransferReceiveResult
{
    public ManualPeerTransferStatus Status { get; }
    public ManualPeerIncomingTransferRequest? Request { get; }
    public IReadOnlyList<ManualPeerTransferFileSummary> ReceivedFiles { get; }
    public string? Reason { get; }

    public bool Success => Status == ManualPeerTransferStatus.Completed;

    private ManualPeerTransferReceiveResult(
        ManualPeerTransferStatus status,
        ManualPeerIncomingTransferRequest? request,
        IReadOnlyList<ManualPeerTransferFileSummary> receivedFiles,
        string? reason)
    {
        Status = status;
        Request = request;
        ReceivedFiles = receivedFiles;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
    }

    internal static ManualPeerTransferReceiveResult Create(
        ManualPeerTransferStatus status,
        ManualPeerIncomingTransferRequest? request,
        IEnumerable<ManualPeerTransferFileSummary>? receivedFiles = null,
        string? reason = null)
    {
        var fileList = receivedFiles?.ToArray() ?? [];
        return new ManualPeerTransferReceiveResult(status, request, Array.AsReadOnly(fileList), reason);
    }
}
