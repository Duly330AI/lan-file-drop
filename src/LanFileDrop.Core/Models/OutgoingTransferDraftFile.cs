using LanFileDrop.Core.Validation;

namespace LanFileDrop.Core.Models;

public sealed record OutgoingTransferDraftFile
{
    public string FileName { get; }
    public ulong? SizeBytes { get; }

    private OutgoingTransferDraftFile(string fileName, ulong? sizeBytes)
    {
        FileName = fileName;
        SizeBytes = sizeBytes;
    }

    public static OutgoingTransferDraftFile Create(string fileName, ulong? sizeBytes = null)
    {
        var safeFileName = fileName?.Trim() ?? string.Empty;

        PathValidation.EnsureSafeFileName(safeFileName, nameof(fileName));

        return new OutgoingTransferDraftFile(safeFileName, sizeBytes);
    }
}
