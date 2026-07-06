namespace LanFileDrop.Networking;

public sealed record ManualPeerIncomingTransferRequest
{
    public Guid TransferId { get; }
    public IReadOnlyList<ManualPeerTransferFileSummary> Files { get; }

    public int FileCount => Files.Count;
    public long TotalBytes => Files.Sum(file => file.SizeBytes);

    private ManualPeerIncomingTransferRequest(Guid transferId, IReadOnlyList<ManualPeerTransferFileSummary> files)
    {
        TransferId = transferId;
        Files = files;
    }

    public static ManualPeerIncomingTransferRequest Create(
        Guid transferId,
        IEnumerable<ManualPeerTransferFileSummary> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var fileList = files.Select(file => file ?? throw new ArgumentException(
            "Incoming transfer request files must not contain null entries.",
            nameof(files))).ToArray();

        if (fileList.Length == 0)
        {
            throw new ArgumentException("An incoming transfer request must contain at least one file.", nameof(files));
        }

        return new ManualPeerIncomingTransferRequest(transferId, Array.AsReadOnly(fileList));
    }
}
