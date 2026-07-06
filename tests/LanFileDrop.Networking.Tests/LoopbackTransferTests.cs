using System.Net;
using System.Text;
using LanFileDrop.Core.Checksums;
using LanFileDrop.Core.Models;

namespace LanFileDrop.Networking.Tests;

public class LoopbackTransferTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Start_BindsToLoopbackAddressWithEphemeralPort()
    {
        using var server = LoopbackTransferServer.Start();

        Assert.Equal(IPAddress.Loopback, server.EndPoint.Address);
        Assert.NotEqual(0, server.EndPoint.Port);
    }

    [Fact]
    public async Task SendAsync_TransfersPayloadAndServerReceivesIdenticalBytesWithMatchingChecksum()
    {
        using var server = LoopbackTransferServer.Start();
        using var cts = new CancellationTokenSource(TestTimeout);

        var payload = Encoding.UTF8.GetBytes("hello loopback transfer");
        var checksum = ChecksumCalculator.ComputeSha256(payload);
        var manifest = FileManifest.Create(Guid.NewGuid(), "greeting.txt", payload.Length, declaredChecksum: checksum.Value);

        var receiveTask = server.ReceiveOneAsync(cts.Token);
        await LoopbackTransferClient.SendAsync(server.EndPoint, manifest, payload, cts.Token);
        var result = await receiveTask;

        Assert.True(result.Success);
        Assert.Equal(payload, result.Payload);
        Assert.Equal("greeting.txt", result.FileName);
        Assert.Equal(payload.Length, result.ActualSizeBytes);
        Assert.True(result.ChecksumMatches);
    }

    [Fact]
    public async Task SendAsync_WithEmptyPayloadAndZeroDeclaredSize_TransfersSuccessfully()
    {
        using var server = LoopbackTransferServer.Start();
        using var cts = new CancellationTokenSource(TestTimeout);

        var manifest = FileManifest.Create(Guid.NewGuid(), "empty.txt", 0);

        var receiveTask = server.ReceiveOneAsync(cts.Token);
        await LoopbackTransferClient.SendAsync(server.EndPoint, manifest, Array.Empty<byte>(), cts.Token);
        var result = await receiveTask;

        Assert.True(result.Success);
        Assert.Empty(result.Payload);
        Assert.Equal(0, result.ActualSizeBytes);
    }

    [Fact]
    public async Task SendAsync_WithPayloadSizeNotMatchingDeclaredSize_ReturnsUnsuccessfulResult()
    {
        using var server = LoopbackTransferServer.Start();
        using var cts = new CancellationTokenSource(TestTimeout);

        var payload = Encoding.UTF8.GetBytes("actual payload longer than declared");
        var manifest = FileManifest.Create(Guid.NewGuid(), "mismatch.txt", sizeBytes: 5);

        var receiveTask = server.ReceiveOneAsync(cts.Token);
        await LoopbackTransferClient.SendAsync(server.EndPoint, manifest, payload, cts.Token);
        var result = await receiveTask;

        Assert.False(result.SizeMatches);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task SendAsync_WithWrongDeclaredChecksum_ReturnsUnsuccessfulResult()
    {
        using var server = LoopbackTransferServer.Start();
        using var cts = new CancellationTokenSource(TestTimeout);

        var payload = Encoding.UTF8.GetBytes("payload content");
        var wrongChecksum = ChecksumCalculator.ComputeSha256(Encoding.UTF8.GetBytes("different content"));
        var manifest = FileManifest.Create(Guid.NewGuid(), "file.txt", payload.Length, declaredChecksum: wrongChecksum.Value);

        var receiveTask = server.ReceiveOneAsync(cts.Token);
        await LoopbackTransferClient.SendAsync(server.EndPoint, manifest, payload, cts.Token);
        var result = await receiveTask;

        Assert.True(result.SizeMatches);
        Assert.False(result.ChecksumMatches);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task SendAsync_WithNonLoopbackEndpoint_ThrowsBeforeConnecting()
    {
        // RFC 5737 TEST-NET-3 documentation address — never routed, never a real peer,
        // and not a private LAN range. SendAsync must reject it before any socket is opened.
        var nonLoopbackEndpoint = new IPEndPoint(IPAddress.Parse("203.0.113.1"), 9);
        var payload = Encoding.UTF8.GetBytes("data");
        var manifest = FileManifest.Create(Guid.NewGuid(), "file.txt", payload.Length);
        using var cts = new CancellationTokenSource(TestTimeout);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            LoopbackTransferClient.SendAsync(nonLoopbackEndpoint, manifest, payload, cts.Token));
    }

    [Fact]
    public async Task SendAsync_WithNullEndpoint_ThrowsArgumentNullException()
    {
        var payload = Encoding.UTF8.GetBytes("data");
        var manifest = FileManifest.Create(Guid.NewGuid(), "file.txt", payload.Length);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            LoopbackTransferClient.SendAsync(null!, manifest, payload, CancellationToken.None));
    }

    [Fact]
    public async Task ReceiveOneAsync_WithMalformedDeclaredChecksumHeader_ThrowsTransferProtocolException()
    {
        using var server = LoopbackTransferServer.Start();
        using var cts = new CancellationTokenSource(TestTimeout);

        var payload = Encoding.UTF8.GetBytes("payload content");
        // Well-formed at the manifest level (non-empty) but not a valid SHA-256 hex string,
        // so it is a protocol error rather than a business-level checksum mismatch.
        var manifest = FileManifest.Create(Guid.NewGuid(), "file.txt", payload.Length, declaredChecksum: "not-a-valid-sha256");

        var receiveTask = server.ReceiveOneAsync(cts.Token);
        await LoopbackTransferClient.SendAsync(server.EndPoint, manifest, payload, cts.Token);

        await Assert.ThrowsAsync<TransferProtocolException>(() => receiveTask);
    }

    [Fact]
    public async Task ReceiveOneAsync_WithNoIncomingConnection_RespectsCancellation()
    {
        using var server = LoopbackTransferServer.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<OperationCanceledException>(() => server.ReceiveOneAsync(cts.Token));
    }
}
