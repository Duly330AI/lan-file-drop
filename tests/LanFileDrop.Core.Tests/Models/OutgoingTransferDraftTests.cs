using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class OutgoingTransferDraftTests
{
    [Fact]
    public void Create_WithValidData_SetsPreviewProperties()
    {
        var createdUtc = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        var files = new[]
        {
            OutgoingTransferDraftFile.Create("alpha.txt", 100),
            OutgoingTransferDraftFile.Create("beta.jpg", 250),
        };

        var draft = OutgoingTransferDraft.Create("Validated peer display", files, createdUtc);

        Assert.Equal("Validated peer display", draft.TargetPeerDisplay);
        Assert.Equal(2, draft.FileCount);
        Assert.Equal(350UL, draft.TotalKnownSizeBytes);
        Assert.False(draft.HasUnknownSizes);
        Assert.Equal(createdUtc, draft.CreatedUtc);
    }

    [Fact]
    public void Create_WithEmptyFileList_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            OutgoingTransferDraft.Create("Validated peer display", Array.Empty<OutgoingTransferDraftFile>()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyTargetPeerDisplay_Throws(string targetPeerDisplay)
    {
        var file = OutgoingTransferDraftFile.Create("file.txt", 10);

        Assert.Throws<ArgumentException>(() =>
            OutgoingTransferDraft.Create(targetPeerDisplay, new[] { file }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DraftFileCreate_WithEmptyFileName_Throws(string? fileName)
    {
        Assert.Throws<ArgumentException>(() => OutgoingTransferDraftFile.Create(fileName!, 10));
    }

    [Theory]
    [InlineData("folder/file.txt")]
    [InlineData("folder\\file.txt")]
    [InlineData(".")]
    [InlineData("..")]
    public void DraftFileCreate_WithPathLikeFileName_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => OutgoingTransferDraftFile.Create(fileName, 10));
    }

    [Fact]
    public void Create_ComputesKnownSizeAndUnknownSizeFlag()
    {
        var files = new[]
        {
            OutgoingTransferDraftFile.Create("known-a.txt", 100),
            OutgoingTransferDraftFile.Create("unknown.bin"),
            OutgoingTransferDraftFile.Create("known-b.txt", 40),
        };

        var draft = OutgoingTransferDraft.Create("Validated peer display", files);

        Assert.Equal(3, draft.FileCount);
        Assert.Equal(140UL, draft.TotalKnownSizeBytes);
        Assert.True(draft.HasUnknownSizes);
    }

    [Fact]
    public void Create_DefensivelyCopiesFiles()
    {
        var source = new List<OutgoingTransferDraftFile>
        {
            OutgoingTransferDraftFile.Create("one.txt", 1),
        };

        var draft = OutgoingTransferDraft.Create("Validated peer display", source);

        source.Add(OutgoingTransferDraftFile.Create("two.txt", 2));

        Assert.Single(draft.Files);
        Assert.Equal(1UL, draft.TotalKnownSizeBytes);
    }

    [Fact]
    public void Create_ExposesReadOnlyFileCollection()
    {
        var draft = OutgoingTransferDraft.Create(
            "Validated peer display",
            new[] { OutgoingTransferDraftFile.Create("one.txt", 1) });

        Assert.Throws<NotSupportedException>(() =>
            ((IList<OutgoingTransferDraftFile>)draft.Files).Add(OutgoingTransferDraftFile.Create("two.txt", 2)));
    }
}
