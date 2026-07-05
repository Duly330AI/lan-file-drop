using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class TransferManifestTests
{
    [Fact]
    public void Create_WithOneFile_SetsProperties()
    {
        var file = FileManifest.Create(Guid.NewGuid(), "a.txt", 100);

        var manifest = TransferManifest.Create(new[] { file });

        Assert.Equal(1, manifest.FileCount);
        Assert.Equal(100, manifest.TotalBytes);
        Assert.NotEqual(Guid.Empty, manifest.TransferId);
    }

    [Fact]
    public void Create_WithMultipleFiles_ComputesTotals()
    {
        var files = new[]
        {
            FileManifest.Create(Guid.NewGuid(), "a.txt", 100),
            FileManifest.Create(Guid.NewGuid(), "b.txt", 50),
        };

        var manifest = TransferManifest.Create(files);

        Assert.Equal(2, manifest.FileCount);
        Assert.Equal(150, manifest.TotalBytes);
    }

    [Fact]
    public void Create_WithNoFiles_Throws()
    {
        Assert.Throws<ArgumentException>(() => TransferManifest.Create(Array.Empty<FileManifest>()));
    }

    [Fact]
    public void Create_WithNullFiles_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TransferManifest.Create(null!));
    }

    [Fact]
    public void Create_DefensivelyCopiesFiles_MutatingSourceListDoesNotAffectManifest()
    {
        var sourceList = new List<FileManifest>
        {
            FileManifest.Create(Guid.NewGuid(), "a.txt", 100),
        };

        var manifest = TransferManifest.Create(sourceList);

        sourceList.Add(FileManifest.Create(Guid.NewGuid(), "b.txt", 250));

        Assert.Equal(1, manifest.FileCount);
        Assert.Equal(100, manifest.TotalBytes);
    }

    [Fact]
    public void Create_WithExplicitTransferId_UsesProvidedValue()
    {
        var transferId = Guid.NewGuid();
        var file = FileManifest.Create(Guid.NewGuid(), "a.txt", 100);

        var manifest = TransferManifest.Create(new[] { file }, transferId);

        Assert.Equal(transferId, manifest.TransferId);
    }
}
