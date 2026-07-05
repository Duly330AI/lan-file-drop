namespace LanFileDrop.Core.Checksums;

public sealed record FileChecksum
{
    public ChecksumAlgorithm Algorithm { get; }
    public string Value { get; }

    private FileChecksum(ChecksumAlgorithm algorithm, string value)
    {
        Algorithm = algorithm;
        Value = value;
    }

    public static FileChecksum Create(ChecksumAlgorithm algorithm, string value)
    {
        if (!Enum.IsDefined(algorithm))
        {
            throw new ArgumentException("Unsupported checksum algorithm.", nameof(algorithm));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Checksum value must not be empty.", nameof(value));
        }

        var normalized = value.Trim().ToLowerInvariant();

        switch (algorithm)
        {
            case ChecksumAlgorithm.Sha256:
                EnsureValidSha256(normalized);
                break;
        }

        return new FileChecksum(algorithm, normalized);
    }

    private static void EnsureValidSha256(string value)
    {
        if (value.Length != 64)
        {
            throw new ArgumentException("SHA-256 checksum must be 64 hex characters.", nameof(value));
        }

        if (!value.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("SHA-256 checksum must contain only hex characters.", nameof(value));
        }
    }
}
