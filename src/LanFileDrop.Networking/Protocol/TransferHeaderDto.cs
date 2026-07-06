namespace LanFileDrop.Networking.Protocol;

internal sealed class TransferHeaderDto
{
    public string FileName { get; set; } = string.Empty;
    public long DeclaredSizeBytes { get; set; }
    public string? DeclaredChecksumSha256 { get; set; }
}
