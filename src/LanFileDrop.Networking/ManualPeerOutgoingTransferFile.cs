using LanFileDrop.Core.Checksums;
using LanFileDrop.Core.Validation;

namespace LanFileDrop.Networking;

public sealed record ManualPeerOutgoingTransferFile
{
    private readonly Func<CancellationToken, Task<Stream>> _openReadAsync;

    public string FileName { get; }
    public long SizeBytes { get; }
    public FileChecksum Checksum { get; }

    private ManualPeerOutgoingTransferFile(
        string fileName,
        long sizeBytes,
        FileChecksum checksum,
        Func<CancellationToken, Task<Stream>> openReadAsync)
    {
        FileName = fileName;
        SizeBytes = sizeBytes;
        Checksum = checksum;
        _openReadAsync = openReadAsync;
    }

    public static ManualPeerOutgoingTransferFile Create(
        string fileName,
        long sizeBytes,
        FileChecksum checksum,
        Func<CancellationToken, Task<Stream>> openReadAsync)
    {
        var safeFileName = fileName?.Trim() ?? string.Empty;
        PathValidation.EnsureSafeFileName(safeFileName, nameof(fileName));

        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, "Size must not be negative.");
        }

        ArgumentNullException.ThrowIfNull(checksum);
        ArgumentNullException.ThrowIfNull(openReadAsync);

        return new ManualPeerOutgoingTransferFile(safeFileName, sizeBytes, checksum, openReadAsync);
    }

    internal async Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
    {
        var stream = await _openReadAsync(cancellationToken).ConfigureAwait(false);
        return stream ?? throw new InvalidOperationException("Outgoing transfer file stream factory returned null.");
    }
}
