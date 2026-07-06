using LanFileDrop.Core.Checksums;
using LanFileDrop.Core.Validation;

namespace LanFileDrop.Core.Models;

public sealed record PreparedOutgoingTransferManifestFile
{
    public string FileName { get; }
    public ulong? SizeBytes { get; }
    public FileChecksum Checksum { get; }

    private PreparedOutgoingTransferManifestFile(string fileName, ulong? sizeBytes, FileChecksum checksum)
    {
        FileName = fileName;
        SizeBytes = sizeBytes;
        Checksum = checksum;
    }

    public static PreparedOutgoingTransferManifestFile Create(
        string fileName,
        ulong? sizeBytes,
        FileChecksum checksum)
    {
        var safeFileName = fileName?.Trim() ?? string.Empty;

        PathValidation.EnsureSafeFileName(safeFileName, nameof(fileName));
        ArgumentNullException.ThrowIfNull(checksum);

        return new PreparedOutgoingTransferManifestFile(safeFileName, sizeBytes, checksum);
    }
}
