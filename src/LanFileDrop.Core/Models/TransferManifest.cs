namespace LanFileDrop.Core.Models;

public sealed record TransferManifest
{
    public Guid TransferId { get; }
    public IReadOnlyList<FileManifest> Files { get; }
    public DateTimeOffset CreatedUtc { get; }

    public int FileCount => Files.Count;

    public long TotalBytes => Files.Sum(file => file.SizeBytes);

    private TransferManifest(Guid transferId, IReadOnlyList<FileManifest> files, DateTimeOffset createdUtc)
    {
        TransferId = transferId;
        Files = files;
        CreatedUtc = createdUtc;
    }

    public static TransferManifest Create(
        IEnumerable<FileManifest> files,
        Guid? transferId = null,
        DateTimeOffset? createdUtc = null)
    {
        ArgumentNullException.ThrowIfNull(files);

        var fileList = files.ToArray();

        if (fileList.Length == 0)
        {
            throw new ArgumentException("A transfer manifest must contain at least one file.", nameof(files));
        }

        return new TransferManifest(transferId ?? Guid.NewGuid(), fileList, createdUtc ?? DateTimeOffset.UtcNow);
    }
}
