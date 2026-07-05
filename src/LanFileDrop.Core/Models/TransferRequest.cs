namespace LanFileDrop.Core.Models;

public sealed record TransferRequest
{
    public Guid RequestId { get; }
    public DeviceIdentity Sender { get; }
    public IReadOnlyList<FileManifest> Files { get; }
    public DateTimeOffset CreatedUtc { get; }

    public long TotalBytes => Files.Sum(file => file.SizeBytes);

    private TransferRequest(
        Guid requestId,
        DeviceIdentity sender,
        IReadOnlyList<FileManifest> files,
        DateTimeOffset createdUtc)
    {
        RequestId = requestId;
        Sender = sender;
        Files = files;
        CreatedUtc = createdUtc;
    }

    public static TransferRequest Create(
        DeviceIdentity sender,
        IEnumerable<FileManifest> files,
        Guid? requestId = null,
        DateTimeOffset? createdUtc = null)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(files);

        var fileList = files.ToArray();

        if (fileList.Length == 0)
        {
            throw new ArgumentException("A transfer request must contain at least one file.", nameof(files));
        }

        return new TransferRequest(requestId ?? Guid.NewGuid(), sender, fileList, createdUtc ?? DateTimeOffset.UtcNow);
    }
}
