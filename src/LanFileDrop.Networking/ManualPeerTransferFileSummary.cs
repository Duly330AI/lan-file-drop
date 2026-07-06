using LanFileDrop.Core.Checksums;
using LanFileDrop.Core.Validation;

namespace LanFileDrop.Networking;

public sealed record ManualPeerTransferFileSummary
{
    public string FileName { get; }
    public long SizeBytes { get; }
    public FileChecksum Checksum { get; }

    private ManualPeerTransferFileSummary(string fileName, long sizeBytes, FileChecksum checksum)
    {
        FileName = fileName;
        SizeBytes = sizeBytes;
        Checksum = checksum;
    }

    public static ManualPeerTransferFileSummary Create(string fileName, long sizeBytes, FileChecksum checksum)
    {
        var safeFileName = fileName?.Trim() ?? string.Empty;
        PathValidation.EnsureSafeFileName(safeFileName, nameof(fileName));

        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, "Size must not be negative.");
        }

        ArgumentNullException.ThrowIfNull(checksum);

        return new ManualPeerTransferFileSummary(safeFileName, sizeBytes, checksum);
    }
}
