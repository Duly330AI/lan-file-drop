using LanFileDrop.Core.Checksums;

namespace LanFileDrop.Core.Tests.Checksums;

public class FileChecksumTests
{
    private const string ValidSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void Create_WithValidSha256_SetsProperties()
    {
        var checksum = FileChecksum.Create(ChecksumAlgorithm.Sha256, ValidSha256);

        Assert.Equal(ChecksumAlgorithm.Sha256, checksum.Algorithm);
        Assert.Equal(ValidSha256, checksum.Value);
    }

    [Fact]
    public void Create_NormalizesValueToLowercase()
    {
        var checksum = FileChecksum.Create(ChecksumAlgorithm.Sha256, ValidSha256.ToUpperInvariant());

        Assert.Equal(ValidSha256, checksum.Value);
    }

    [Fact]
    public void Create_TrimsSurroundingWhitespace()
    {
        var checksum = FileChecksum.Create(ChecksumAlgorithm.Sha256, $"  {ValidSha256}  ");

        Assert.Equal(ValidSha256, checksum.Value);
    }

    [Fact]
    public void Create_WithUppercaseValueSurroundedByWhitespace_IsAcceptedTrimmedAndNormalized()
    {
        // Proves length/hex validation runs against the trimmed+lowercased value, not the raw
        // input: the raw string here is 70 chars long and would fail a naive length check.
        var checksum = FileChecksum.Create(ChecksumAlgorithm.Sha256, $"   {ValidSha256.ToUpperInvariant()}   ");

        Assert.Equal(ValidSha256, checksum.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyValue_Throws(string? value)
    {
        Assert.Throws<ArgumentException>(() => FileChecksum.Create(ChecksumAlgorithm.Sha256, value!));
    }

    [Fact]
    public void Create_WithWrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => FileChecksum.Create(ChecksumAlgorithm.Sha256, "abc123"));
    }

    [Fact]
    public void Create_WithNonHexCharacters_Throws()
    {
        var invalid = new string('g', 64);

        Assert.Throws<ArgumentException>(() => FileChecksum.Create(ChecksumAlgorithm.Sha256, invalid));
    }

    [Fact]
    public void Create_WithUnsupportedAlgorithm_Throws()
    {
        Assert.Throws<ArgumentException>(() => FileChecksum.Create((ChecksumAlgorithm)999, ValidSha256));
    }
}
