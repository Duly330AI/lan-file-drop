using LanFileDrop.Core.Validation;

namespace LanFileDrop.Core.Models;

public sealed record FileManifest
{
    public Guid TransferId { get; }
    public string FileName { get; }
    public string? RelativePath { get; }
    public long SizeBytes { get; }
    public string? DeclaredChecksum { get; }

    private FileManifest(
        Guid transferId,
        string fileName,
        string? relativePath,
        long sizeBytes,
        string? declaredChecksum)
    {
        TransferId = transferId;
        FileName = fileName;
        RelativePath = relativePath;
        SizeBytes = sizeBytes;
        DeclaredChecksum = declaredChecksum;
    }

    public static FileManifest Create(
        Guid transferId,
        string fileName,
        long sizeBytes,
        string? relativePath = null,
        string? declaredChecksum = null)
    {
        PathValidation.EnsureSafeFileName(fileName, nameof(fileName));

        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, "Size must not be negative.");
        }

        if (relativePath is not null)
        {
            PathValidation.EnsureSafeRelativePath(relativePath, nameof(relativePath));
        }

        if (declaredChecksum is not null)
        {
            if (string.IsNullOrWhiteSpace(declaredChecksum))
            {
                throw new ArgumentException(
                    "Declared checksum must not be empty or whitespace when provided.",
                    nameof(declaredChecksum));
            }

            declaredChecksum = declaredChecksum.Trim();
        }

        return new FileManifest(transferId, fileName, relativePath, sizeBytes, declaredChecksum);
    }
}
