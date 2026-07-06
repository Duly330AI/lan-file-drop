using LanFileDrop.Core.Checksums;

namespace LanFileDrop.Networking;

public sealed record TransferResult
{
    public string FileName { get; }
    public long DeclaredSizeBytes { get; }
    public long ActualSizeBytes { get; }
    public FileChecksum? DeclaredChecksum { get; }
    public FileChecksum ActualChecksum { get; }
    public byte[] Payload { get; }

    public bool SizeMatches => ActualSizeBytes == DeclaredSizeBytes;
    public bool ChecksumMatches => DeclaredChecksum is null || DeclaredChecksum.Value == ActualChecksum.Value;
    public bool Success => SizeMatches && ChecksumMatches;

    private TransferResult(
        string fileName,
        long declaredSizeBytes,
        long actualSizeBytes,
        FileChecksum? declaredChecksum,
        FileChecksum actualChecksum,
        byte[] payload)
    {
        FileName = fileName;
        DeclaredSizeBytes = declaredSizeBytes;
        ActualSizeBytes = actualSizeBytes;
        DeclaredChecksum = declaredChecksum;
        ActualChecksum = actualChecksum;
        Payload = payload;
    }

    internal static TransferResult Create(
        string fileName,
        long declaredSizeBytes,
        string? declaredChecksumSha256,
        byte[] payload)
    {
        FileChecksum? declaredChecksum;
        try
        {
            declaredChecksum = declaredChecksumSha256 is null
                ? null
                : FileChecksum.Create(ChecksumAlgorithm.Sha256, declaredChecksumSha256);
        }
        catch (ArgumentException ex)
        {
            throw new TransferProtocolException("The received header contained a malformed SHA-256 checksum.", ex);
        }

        var actualChecksum = ChecksumCalculator.ComputeSha256(payload);

        return new TransferResult(fileName, declaredSizeBytes, payload.LongLength, declaredChecksum, actualChecksum, payload);
    }
}
