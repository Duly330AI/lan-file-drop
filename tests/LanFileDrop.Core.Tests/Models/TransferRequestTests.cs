using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class TransferRequestTests
{
    private static DeviceIdentity Sender => DeviceIdentity.Create("Sender PC");

    [Fact]
    public void Create_WithOneFile_SetsProperties()
    {
        var file = FileManifest.Create(Guid.NewGuid(), "a.txt", 100);

        var request = TransferRequest.Create(Sender, new[] { file });

        Assert.Single(request.Files);
        Assert.Equal(100, request.TotalBytes);
        Assert.NotEqual(Guid.Empty, request.RequestId);
    }

    [Fact]
    public void Create_WithMultipleFiles_ComputesTotalBytes()
    {
        var files = new[]
        {
            FileManifest.Create(Guid.NewGuid(), "a.txt", 100),
            FileManifest.Create(Guid.NewGuid(), "b.txt", 250),
            FileManifest.Create(Guid.NewGuid(), "c.txt", 0),
        };

        var request = TransferRequest.Create(Sender, files);

        Assert.Equal(350, request.TotalBytes);
        Assert.Equal(3, request.Files.Count);
    }

    [Fact]
    public void Create_WithNoFiles_Throws()
    {
        Assert.Throws<ArgumentException>(() => TransferRequest.Create(Sender, Array.Empty<FileManifest>()));
    }

    [Fact]
    public void Create_WithNullSender_Throws()
    {
        var file = FileManifest.Create(Guid.NewGuid(), "a.txt", 100);

        Assert.Throws<ArgumentNullException>(() => TransferRequest.Create(null!, new[] { file }));
    }

    [Fact]
    public void Create_WithNullFiles_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TransferRequest.Create(Sender, null!));
    }

    [Fact]
    public void Create_DefensivelyCopiesFiles_MutatingSourceListDoesNotAffectRequest()
    {
        var sourceList = new List<FileManifest>
        {
            FileManifest.Create(Guid.NewGuid(), "a.txt", 100),
        };

        var request = TransferRequest.Create(Sender, sourceList);

        sourceList.Add(FileManifest.Create(Guid.NewGuid(), "b.txt", 250));

        Assert.Single(request.Files);
        Assert.Equal(100, request.TotalBytes);
    }
}
