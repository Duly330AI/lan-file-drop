using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class FileManifestTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var transferId = Guid.NewGuid();

        var manifest = FileManifest.Create(transferId, "photo.jpg", 1024);

        Assert.Equal(transferId, manifest.TransferId);
        Assert.Equal("photo.jpg", manifest.FileName);
        Assert.Equal(1024, manifest.SizeBytes);
        Assert.Null(manifest.RelativePath);
    }

    [Fact]
    public void Create_WithZeroSize_IsAllowed()
    {
        var manifest = FileManifest.Create(Guid.NewGuid(), "empty.txt", 0);

        Assert.Equal(0, manifest.SizeBytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyFileName_Throws(string? fileName)
    {
        Assert.Throws<ArgumentException>(() => FileManifest.Create(Guid.NewGuid(), fileName!, 10));
    }

    [Theory]
    [InlineData("folder/file.txt")]
    [InlineData("folder\\file.txt")]
    [InlineData("..")]
    public void Create_WithUnsafeFileName_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => FileManifest.Create(Guid.NewGuid(), fileName, 10));
    }

    [Fact]
    public void Create_WithNegativeSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FileManifest.Create(Guid.NewGuid(), "file.txt", -1));
    }

    [Theory]
    [InlineData(@"C:\Windows\file.txt")]
    [InlineData("/etc/file.txt")]
    public void Create_WithAbsoluteRelativePath_Throws(string relativePath)
    {
        Assert.Throws<ArgumentException>(() =>
            FileManifest.Create(Guid.NewGuid(), "file.txt", 10, relativePath));
    }

    [Theory]
    [InlineData("../file.txt")]
    [InlineData("sub/../../file.txt")]
    public void Create_WithPathTraversalInRelativePath_Throws(string relativePath)
    {
        Assert.Throws<ArgumentException>(() =>
            FileManifest.Create(Guid.NewGuid(), "file.txt", 10, relativePath));
    }

    [Fact]
    public void Create_WithSafeRelativePath_IsAllowed()
    {
        var manifest = FileManifest.Create(Guid.NewGuid(), "file.txt", 10, "sub/folder");

        Assert.Equal("sub/folder", manifest.RelativePath);
    }

    [Fact]
    public void Create_WithNullChecksum_IsAllowed()
    {
        var manifest = FileManifest.Create(Guid.NewGuid(), "file.txt", 10, declaredChecksum: null);

        Assert.Null(manifest.DeclaredChecksum);
    }

    [Fact]
    public void Create_WithChecksum_IsPreserved()
    {
        var manifest = FileManifest.Create(Guid.NewGuid(), "file.txt", 10, declaredChecksum: "abc123");

        Assert.Equal("abc123", manifest.DeclaredChecksum);
    }

    [Fact]
    public void Create_WithChecksumHavingSurroundingWhitespace_IsTrimmed()
    {
        var manifest = FileManifest.Create(Guid.NewGuid(), "file.txt", 10, declaredChecksum: "  abc123  ");

        Assert.Equal("abc123", manifest.DeclaredChecksum);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceChecksum_Throws(string declaredChecksum)
    {
        Assert.Throws<ArgumentException>(() =>
            FileManifest.Create(Guid.NewGuid(), "file.txt", 10, declaredChecksum: declaredChecksum));
    }
}
