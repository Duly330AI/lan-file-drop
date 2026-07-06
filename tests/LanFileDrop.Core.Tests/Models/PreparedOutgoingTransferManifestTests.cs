using LanFileDrop.Core.Checksums;
using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class PreparedOutgoingTransferManifestTests
{
    private const string ValidSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Create_WithValidData_SetsPreparedMetadata()
    {
        var createdUtc = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        var checksum = FileChecksum.Create(ChecksumAlgorithm.Sha256, ValidSha256);
        var files = new[]
        {
            PreparedOutgoingTransferManifestFile.Create("alpha.txt", 100, checksum),
            PreparedOutgoingTransferManifestFile.Create("beta.jpg", 250, checksum),
        };

        var manifest = PreparedOutgoingTransferManifest.Create("Validated peer display", files, createdUtc);

        Assert.Equal("Validated peer display", manifest.TargetPeerDisplay);
        Assert.Equal(2, manifest.FileCount);
        Assert.Equal(350UL, manifest.TotalKnownSizeBytes);
        Assert.False(manifest.HasUnknownSizes);
        Assert.Equal(createdUtc, manifest.CreatedUtc);
        Assert.All(manifest.Files, file => Assert.Equal(ChecksumAlgorithm.Sha256, file.Checksum.Algorithm));
    }

    [Fact]
    public void Create_WithEmptyFileList_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PreparedOutgoingTransferManifest.Create(
                "Validated peer display",
                Array.Empty<PreparedOutgoingTransferManifestFile>()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyTargetPeerDisplay_Throws(string targetPeerDisplay)
    {
        var file = PreparedOutgoingTransferManifestFile.Create("file.txt", 10, CreateChecksum());

        Assert.Throws<ArgumentException>(() =>
            PreparedOutgoingTransferManifest.Create(targetPeerDisplay, new[] { file }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ManifestFileCreate_WithEmptyFileName_Throws(string? fileName)
    {
        Assert.Throws<ArgumentException>(() =>
            PreparedOutgoingTransferManifestFile.Create(fileName!, 10, CreateChecksum()));
    }

    [Theory]
    [InlineData("folder/file.txt")]
    [InlineData("folder\\file.txt")]
    [InlineData(".")]
    [InlineData("..")]
    public void ManifestFileCreate_WithPathLikeFileName_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() =>
            PreparedOutgoingTransferManifestFile.Create(fileName, 10, CreateChecksum()));
    }

    [Fact]
    public void ManifestFileCreate_WithNullChecksum_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PreparedOutgoingTransferManifestFile.Create("file.txt", 10, null!));
    }

    [Fact]
    public void Create_ComputesKnownSizeAndUnknownSizeFlag()
    {
        var checksum = CreateChecksum();
        var files = new[]
        {
            PreparedOutgoingTransferManifestFile.Create("known-a.txt", 100, checksum),
            PreparedOutgoingTransferManifestFile.Create("unknown.bin", null, checksum),
            PreparedOutgoingTransferManifestFile.Create("known-b.txt", 40, checksum),
        };

        var manifest = PreparedOutgoingTransferManifest.Create("Validated peer display", files);

        Assert.Equal(3, manifest.FileCount);
        Assert.Equal(140UL, manifest.TotalKnownSizeBytes);
        Assert.True(manifest.HasUnknownSizes);
    }

    [Fact]
    public void Create_DefensivelyCopiesFiles()
    {
        var source = new List<PreparedOutgoingTransferManifestFile>
        {
            PreparedOutgoingTransferManifestFile.Create("one.txt", 1, CreateChecksum()),
        };

        var manifest = PreparedOutgoingTransferManifest.Create("Validated peer display", source);

        source.Add(PreparedOutgoingTransferManifestFile.Create("two.txt", 2, CreateChecksum()));

        Assert.Single(manifest.Files);
        Assert.Equal(1UL, manifest.TotalKnownSizeBytes);
    }

    [Fact]
    public void Create_ExposesReadOnlyFileCollection()
    {
        var manifest = PreparedOutgoingTransferManifest.Create(
            "Validated peer display",
            new[] { PreparedOutgoingTransferManifestFile.Create("one.txt", 1, CreateChecksum()) });

        Assert.Throws<NotSupportedException>(() =>
            ((IList<PreparedOutgoingTransferManifestFile>)manifest.Files).Add(
                PreparedOutgoingTransferManifestFile.Create("two.txt", 2, CreateChecksum())));
    }

    [Fact]
    public void ManifestFileCreate_AcceptsChecksumFromFileChecksum()
    {
        var checksum = FileChecksum.Create(ChecksumAlgorithm.Sha256, ValidSha256.ToUpperInvariant());

        var file = PreparedOutgoingTransferManifestFile.Create("file.txt", 10, checksum);

        Assert.Equal(ChecksumAlgorithm.Sha256, file.Checksum.Algorithm);
        Assert.Equal(ValidSha256, file.Checksum.Value);
    }

    private static FileChecksum CreateChecksum() => FileChecksum.Create(ChecksumAlgorithm.Sha256, ValidSha256);
}
