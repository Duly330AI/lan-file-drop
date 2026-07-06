using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LanFileDrop.Core.Checksums;
using LanFileDrop.Core.Models;
using LanFileDrop.Networking.Protocol;

namespace LanFileDrop.Networking.Tests;

public class ManualPeerTransferTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task SendAsync_WithAcceptedLoopbackTransfer_WritesFileAfterChecksumVerification()
    {
        using var receiver = ManualPeerTransferReceiver.Start(IPAddress.Loopback);
        using var cts = new CancellationTokenSource(TestTimeout);
        using var destination = TestDirectory.Create();
        var endpoint = CreateLoopbackEndpoint(receiver.EndPoint.Port);
        var payload = Encoding.UTF8.GetBytes("accepted transfer payload");
        var (manifest, files) = CreateOutgoingTransfer(endpoint, ("accepted.txt", payload, null));

        var receiveTask = receiver.ReceiveOneAsync(
            destination.Path,
            (request, _) =>
            {
                Assert.Equal(1, request.FileCount);
                Assert.Equal("accepted.txt", request.Files[0].FileName);
                Assert.Equal(payload.Length, request.Files[0].SizeBytes);
                return Task.FromResult(TransferDecision.Accept());
            },
            cts.Token);

        var sendResult = await ManualPeerTransferSender.SendAsync(endpoint, manifest, files, cts.Token);
        var receiveResult = await receiveTask;

        Assert.Equal(ManualPeerTransferStatus.Completed, sendResult.Status);
        Assert.True(sendResult.Success);
        Assert.Equal(ManualPeerTransferStatus.Completed, receiveResult.Status);
        Assert.True(receiveResult.Success);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destination.FilePath("accepted.txt"), cts.Token));
    }

    [Fact]
    public async Task SendAsync_WhenReceiverRejects_WritesNothingAndSenderDoesNotOpenPayload()
    {
        using var receiver = ManualPeerTransferReceiver.Start(IPAddress.Loopback);
        using var cts = new CancellationTokenSource(TestTimeout);
        using var destination = TestDirectory.Create();
        var endpoint = CreateLoopbackEndpoint(receiver.EndPoint.Port);
        var payload = Encoding.UTF8.GetBytes("rejected transfer payload");
        var payloadOpened = false;
        var checksum = ChecksumCalculator.ComputeSha256(payload);
        var manifest = PreparedOutgoingTransferManifest.Create(
            endpoint.Display,
            [PreparedOutgoingTransferManifestFile.Create("rejected.txt", (ulong)payload.Length, checksum)]);
        var files = new[]
        {
            ManualPeerOutgoingTransferFile.Create(
                "rejected.txt",
                payload.Length,
                checksum,
                _ =>
                {
                    payloadOpened = true;
                    return Task.FromResult<Stream>(new MemoryStream(payload, writable: false));
                }),
        };

        var receiveTask = receiver.ReceiveOneAsync(
            destination.Path,
            (_, _) => Task.FromResult(TransferDecision.Reject("User rejected.")),
            cts.Token);

        var sendResult = await ManualPeerTransferSender.SendAsync(endpoint, manifest, files, cts.Token);
        var receiveResult = await receiveTask;

        Assert.Equal(ManualPeerTransferStatus.Rejected, sendResult.Status);
        Assert.False(sendResult.Success);
        Assert.Equal(ManualPeerTransferStatus.Rejected, receiveResult.Status);
        Assert.False(receiveResult.Success);
        Assert.False(payloadOpened);
        Assert.Empty(Directory.EnumerateFiles(destination.Path));
    }

    [Fact]
    public async Task SendAsync_WithChecksumMismatch_FailsAndWritesNoFile()
    {
        using var receiver = ManualPeerTransferReceiver.Start(IPAddress.Loopback);
        using var cts = new CancellationTokenSource(TestTimeout);
        using var destination = TestDirectory.Create();
        var endpoint = CreateLoopbackEndpoint(receiver.EndPoint.Port);
        var expectedPayload = Encoding.UTF8.GetBytes("expected_data");
        var actualPayload = Encoding.UTF8.GetBytes("received_data");
        Assert.Equal(expectedPayload.Length, actualPayload.Length);

        var expectedChecksum = ChecksumCalculator.ComputeSha256(expectedPayload);
        var (manifest, files) = CreateOutgoingTransfer(
            endpoint,
            ("mismatch.txt", actualPayload, expectedChecksum));

        var receiveTask = receiver.ReceiveOneAsync(
            destination.Path,
            (_, _) => Task.FromResult(TransferDecision.Accept()),
            cts.Token);

        var sendResult = await ManualPeerTransferSender.SendAsync(endpoint, manifest, files, cts.Token);
        var receiveResult = await receiveTask;

        Assert.Equal(ManualPeerTransferStatus.ChecksumMismatch, sendResult.Status);
        Assert.False(sendResult.Success);
        Assert.Equal(ManualPeerTransferStatus.ChecksumMismatch, receiveResult.Status);
        Assert.False(receiveResult.Success);
        Assert.False(File.Exists(destination.FilePath("mismatch.txt")));
        // No final file and no leftover temp file: checksum failure cleans up after itself.
        Assert.Empty(Directory.EnumerateFiles(destination.Path));
    }

    [Fact]
    public async Task SendAsync_WithMultipleFilesAndLaterChecksumMismatch_LeavesNoFinalFiles()
    {
        using var receiver = ManualPeerTransferReceiver.Start(IPAddress.Loopback);
        using var cts = new CancellationTokenSource(TestTimeout);
        using var destination = TestDirectory.Create();
        var endpoint = CreateLoopbackEndpoint(receiver.EndPoint.Port);
        var firstPayload = Encoding.UTF8.GetBytes("first file is valid");
        var secondPayload = Encoding.UTF8.GetBytes("second file is corrupt");
        var wrongSecondChecksum = ChecksumCalculator.ComputeSha256(Encoding.UTF8.GetBytes("not the second payload"));
        var (manifest, files) = CreateOutgoingTransfer(
            endpoint,
            ("first.txt", firstPayload, null),
            ("second.txt", secondPayload, wrongSecondChecksum));

        var receiveTask = receiver.ReceiveOneAsync(
            destination.Path,
            (_, _) => Task.FromResult(TransferDecision.Accept()),
            cts.Token);

        var sendResult = await ManualPeerTransferSender.SendAsync(endpoint, manifest, files, cts.Token);
        var receiveResult = await receiveTask;

        Assert.Equal(ManualPeerTransferStatus.ChecksumMismatch, sendResult.Status);
        Assert.Equal(ManualPeerTransferStatus.ChecksumMismatch, receiveResult.Status);
        // The first file verified fine, but promotion is all-or-nothing: neither final file
        // appears and both temp files are cleaned up.
        Assert.False(File.Exists(destination.FilePath("first.txt")));
        Assert.False(File.Exists(destination.FilePath("second.txt")));
        Assert.Empty(Directory.EnumerateFiles(destination.Path));
    }

    [Fact]
    public async Task SendAsync_WhenDestinationExists_FailsWithoutOverwritingAndDoesNotOpenPayload()
    {
        using var receiver = ManualPeerTransferReceiver.Start(IPAddress.Loopback);
        using var cts = new CancellationTokenSource(TestTimeout);
        using var destination = TestDirectory.Create();
        var endpoint = CreateLoopbackEndpoint(receiver.EndPoint.Port);
        var existingPath = destination.FilePath("existing.txt");
        await File.WriteAllTextAsync(existingPath, "keep existing", cts.Token);
        var payload = Encoding.UTF8.GetBytes("new payload");
        var checksum = ChecksumCalculator.ComputeSha256(payload);
        var payloadOpened = false;
        var manifest = PreparedOutgoingTransferManifest.Create(
            endpoint.Display,
            [PreparedOutgoingTransferManifestFile.Create("existing.txt", (ulong)payload.Length, checksum)]);
        var files = new[]
        {
            ManualPeerOutgoingTransferFile.Create(
                "existing.txt",
                payload.Length,
                checksum,
                _ =>
                {
                    payloadOpened = true;
                    return Task.FromResult<Stream>(new MemoryStream(payload, writable: false));
                }),
        };

        var receiveTask = receiver.ReceiveOneAsync(
            destination.Path,
            (_, _) => Task.FromResult(TransferDecision.Accept()),
            cts.Token);

        var sendResult = await ManualPeerTransferSender.SendAsync(endpoint, manifest, files, cts.Token);
        var receiveResult = await receiveTask;

        Assert.Equal(ManualPeerTransferStatus.DestinationAlreadyExists, sendResult.Status);
        Assert.False(sendResult.Success);
        Assert.Equal(ManualPeerTransferStatus.DestinationAlreadyExists, receiveResult.Status);
        Assert.False(receiveResult.Success);
        Assert.False(payloadOpened);
        Assert.Equal("keep existing", await File.ReadAllTextAsync(existingPath, cts.Token));
    }

    [Theory]
    [InlineData("folder/file.txt")]
    [InlineData("CON.txt")]
    [InlineData("..")]
    public async Task ReceiveOneAsync_WithUnsafeFileName_ReturnsInvalidRequestBeforeConfirmationAndWritesNothing(string fileName)
    {
        using var receiver = ManualPeerTransferReceiver.Start(IPAddress.Loopback);
        using var cts = new CancellationTokenSource(TestTimeout);
        using var destination = TestDirectory.Create();
        var confirmationCalled = false;

        var receiveTask = receiver.ReceiveOneAsync(
            destination.Path,
            (_, _) =>
            {
                confirmationCalled = true;
                return Task.FromResult(TransferDecision.Accept());
            },
            cts.Token);

        var decision = await SendRawRequestAndReadDecisionAsync(
            receiver.EndPoint.Port,
            [new ManualPeerTransferFileDto
            {
                FileName = fileName,
                SizeBytes = 1,
                ChecksumSha256 = ChecksumCalculator.ComputeSha256([1]).Value,
            }],
            cts.Token);
        var receiveResult = await receiveTask;

        // The sender receives a clean rejection decision instead of a dropped connection.
        Assert.False(decision.Accepted);
        Assert.Equal(nameof(ManualPeerTransferStatus.InvalidRequest), decision.FailureStatus);
        Assert.Equal(ManualPeerTransferStatus.InvalidRequest, receiveResult.Status);
        Assert.False(confirmationCalled);
        Assert.Empty(Directory.EnumerateFiles(destination.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ReceiveOneAsync_WithDuplicateFileNames_ReturnsInvalidRequestAndWritesNothing()
    {
        using var receiver = ManualPeerTransferReceiver.Start(IPAddress.Loopback);
        using var cts = new CancellationTokenSource(TestTimeout);
        using var destination = TestDirectory.Create();
        var confirmationCalled = false;
        var checksum = ChecksumCalculator.ComputeSha256([1]).Value;

        var receiveTask = receiver.ReceiveOneAsync(
            destination.Path,
            (_, _) =>
            {
                confirmationCalled = true;
                return Task.FromResult(TransferDecision.Accept());
            },
            cts.Token);

        var decision = await SendRawRequestAndReadDecisionAsync(
            receiver.EndPoint.Port,
            [
                new ManualPeerTransferFileDto { FileName = "dup.txt", SizeBytes = 1, ChecksumSha256 = checksum },
                new ManualPeerTransferFileDto { FileName = "DUP.TXT", SizeBytes = 1, ChecksumSha256 = checksum },
            ],
            cts.Token);
        var receiveResult = await receiveTask;

        Assert.False(decision.Accepted);
        Assert.Equal(nameof(ManualPeerTransferStatus.InvalidRequest), decision.FailureStatus);
        Assert.Equal(ManualPeerTransferStatus.InvalidRequest, receiveResult.Status);
        Assert.False(confirmationCalled);
        Assert.Empty(Directory.EnumerateFiles(destination.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ReceiveOneAsync_DoesNotWriteBeforeReceiverConfirmationCompletes()
    {
        using var receiver = ManualPeerTransferReceiver.Start(IPAddress.Loopback);
        using var cts = new CancellationTokenSource(TestTimeout);
        using var destination = TestDirectory.Create();
        var endpoint = CreateLoopbackEndpoint(receiver.EndPoint.Port);
        var payload = Encoding.UTF8.GetBytes("wait for accept");
        var (manifest, files) = CreateOutgoingTransfer(endpoint, ("wait.txt", payload, null));
        var confirmationReached = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConfirmation = new TaskCompletionSource<TransferDecision>(TaskCreationOptions.RunContinuationsAsynchronously);

        var receiveTask = receiver.ReceiveOneAsync(
            destination.Path,
            (_, _) =>
            {
                confirmationReached.SetResult(null);
                return releaseConfirmation.Task;
            },
            cts.Token);
        var sendTask = ManualPeerTransferSender.SendAsync(endpoint, manifest, files, cts.Token);

        await confirmationReached.Task.WaitAsync(TestTimeout);
        await Task.Delay(100, cts.Token);

        Assert.False(File.Exists(destination.FilePath("wait.txt")));
        Assert.False(sendTask.IsCompleted);

        releaseConfirmation.SetResult(TransferDecision.Accept());
        var sendResult = await sendTask;
        var receiveResult = await receiveTask;

        Assert.Equal(ManualPeerTransferStatus.Completed, sendResult.Status);
        Assert.Equal(ManualPeerTransferStatus.Completed, receiveResult.Status);
        Assert.Equal(payload, await File.ReadAllBytesAsync(destination.FilePath("wait.txt"), cts.Token));
    }

    [Fact]
    public void SendAsync_PublicApiRequiresPreparedManifest()
    {
        var methods = typeof(ManualPeerTransferSender)
            .GetMethods()
            .Where(method => method.Name == nameof(ManualPeerTransferSender.SendAsync))
            .ToArray();

        Assert.NotEmpty(methods);
        Assert.All(methods, method =>
            Assert.Contains(method.GetParameters(), parameter =>
                parameter.ParameterType == typeof(PreparedOutgoingTransferManifest)));
    }

    private static (
        PreparedOutgoingTransferManifest Manifest,
        IReadOnlyList<ManualPeerOutgoingTransferFile> Files) CreateOutgoingTransfer(
            ManualPeerEndpoint endpoint,
            params (string FileName, byte[] Payload, FileChecksum? ManifestChecksum)[] files)
    {
        var preparedFiles = files
            .Select(file => PreparedOutgoingTransferManifestFile.Create(
                file.FileName,
                (ulong)file.Payload.Length,
                file.ManifestChecksum ?? ChecksumCalculator.ComputeSha256(file.Payload)))
            .ToArray();
        var outgoingFiles = files
            .Zip(preparedFiles, (file, preparedFile) => ManualPeerOutgoingTransferFile.Create(
                preparedFile.FileName,
                checked((long)preparedFile.SizeBytes!.Value),
                preparedFile.Checksum,
                _ => Task.FromResult<Stream>(new MemoryStream(file.Payload, writable: false))))
            .ToArray();

        return (
            PreparedOutgoingTransferManifest.Create(endpoint.Display, preparedFiles),
            outgoingFiles);
    }

    private static ManualPeerEndpoint CreateLoopbackEndpoint(int port)
    {
        var ok = ManualPeerEndpoint.TryParse($"127.0.0.1:{port}", out var endpoint, out var error);

        Assert.True(ok, error);
        return endpoint!;
    }

    // Sends a raw request frame from a non-conforming peer and reads the receiver's decision,
    // proving a bad request yields a clean decision rather than a dropped connection.
    private static async Task<ManualPeerTransferDecisionDto> SendRawRequestAndReadDecisionAsync(
        int port,
        IReadOnlyList<ManualPeerTransferFileDto> files,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
        await using var stream = client.GetStream();

        var request = new ManualPeerTransferRequestDto
        {
            TransferId = Guid.NewGuid().ToString("D"),
            Files = files.ToList(),
        };
        await FrameIO.WriteFrameAsync(stream, JsonSerializer.SerializeToUtf8Bytes(request), cancellationToken);

        var decisionBytes = await FrameIO.ReadFrameAsync(stream, FrameIO.MaxHeaderBytes, cancellationToken);
        return JsonSerializer.Deserialize<ManualPeerTransferDecisionDto>(decisionBytes)!;
    }

    private sealed class TestDirectory : IDisposable
    {
        private TestDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lan-file-drop-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TestDirectory(path);
        }

        public string FilePath(string fileName) => System.IO.Path.Combine(Path, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
