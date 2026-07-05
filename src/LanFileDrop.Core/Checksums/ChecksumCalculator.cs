using System.Security.Cryptography;

namespace LanFileDrop.Core.Checksums;

public static class ChecksumCalculator
{
    public static FileChecksum ComputeSha256(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var hash = SHA256.HashData(data);
        return FileChecksum.Create(ChecksumAlgorithm.Sha256, Convert.ToHexString(hash));
    }

    /// <remarks>
    /// Reads from the stream's current position to its end and leaves the stream open.
    /// If the stream is seekable, its position is restored to where it started; a
    /// non-seekable stream is left positioned at its end, since rewinding is not possible.
    /// </remarks>
    public static FileChecksum ComputeSha256(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var originalPosition = stream.CanSeek ? stream.Position : -1;

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);

        if (stream.CanSeek)
        {
            stream.Position = originalPosition;
        }

        return FileChecksum.Create(ChecksumAlgorithm.Sha256, Convert.ToHexString(hash));
    }
}
