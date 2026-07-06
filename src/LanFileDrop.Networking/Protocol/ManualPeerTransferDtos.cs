namespace LanFileDrop.Networking.Protocol;

internal sealed class ManualPeerTransferRequestDto
{
    public string TransferId { get; set; } = string.Empty;
    public List<ManualPeerTransferFileDto> Files { get; set; } = [];
}

internal sealed class ManualPeerTransferFileDto
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
}

internal sealed class ManualPeerTransferDecisionDto
{
    public bool Accepted { get; set; }
    public string? FailureStatus { get; set; }
    public string? Reason { get; set; }
}

internal sealed class ManualPeerTransferCompletionDto
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
