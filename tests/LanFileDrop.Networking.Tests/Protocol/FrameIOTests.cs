using LanFileDrop.Networking.Protocol;

namespace LanFileDrop.Networking.Tests.Protocol;

public class FrameIOTests
{
    [Fact]
    public async Task WriteFrameAsync_ThenReadFrameAsync_RoundTripsPayload()
    {
        using var stream = new MemoryStream();
        var payload = new byte[] { 1, 2, 3, 4, 5 };

        await FrameIO.WriteFrameAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;
        var result = await FrameIO.ReadFrameAsync(stream, maxLength: 1024, CancellationToken.None);

        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task WriteFrameAsync_ThenReadFrameAsync_RoundTripsEmptyPayload()
    {
        using var stream = new MemoryStream();

        await FrameIO.WriteFrameAsync(stream, Array.Empty<byte>(), CancellationToken.None);
        stream.Position = 0;
        var result = await FrameIO.ReadFrameAsync(stream, maxLength: 1024, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task WriteFrameAsync_WritesLengthPrefixAsBigEndian()
    {
        using var stream = new MemoryStream();
        var payload = new byte[] { 0xAA }; // length 1

        await FrameIO.WriteFrameAsync(stream, payload, CancellationToken.None);

        var written = stream.ToArray();
        // 4-byte big-endian length prefix for length 1, then the payload byte.
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x01, 0xAA }, written);
    }

    [Fact]
    public async Task ReadFrameAsync_WithLengthExceedingMax_Throws()
    {
        using var stream = new MemoryStream();
        var payload = new byte[100];

        await FrameIO.WriteFrameAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FrameIO.ReadFrameAsync(stream, maxLength: 10, CancellationToken.None));
    }

    [Fact]
    public async Task ReadFrameAsync_WhenStreamEndsBeforeExpectedLength_Throws()
    {
        using var stream = new MemoryStream();
        await FrameIO.WriteFrameAsync(stream, new byte[] { 1, 2, 3 }, CancellationToken.None);
        stream.SetLength(stream.Length - 1);
        stream.Position = 0;

        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            FrameIO.ReadFrameAsync(stream, maxLength: 1024, CancellationToken.None));
    }
}
