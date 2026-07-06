using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using LanFileDrop.Core.Checksums;
using LanFileDrop.Core.Models;
using LanFileDrop.Networking.Protocol;

namespace LanFileDrop.Networking;

public sealed class ManualPeerTransferReceiver : IDisposable
{
    // Generic, path-free reasons. Result and wire messages never expose local destination paths.
    private const string InvalidRequestReason = "The transfer request was rejected as invalid.";
    private const string DestinationExistsReason = "Destination file already exists.";
    private const string SizeMismatchReason = "Received payload size did not match the prepared manifest.";
    private const string ChecksumMismatchReason = "Received payload checksum did not match the prepared manifest.";
    private const string WriteFailedReason = "Writing the received transfer to disk failed.";
    private const string ConnectionClosedReason = "The connection closed before the transfer completed.";

    // App-generated temp file name; never derived from the sender-supplied file name.
    private const string TempFilePrefix = ".lanfiledrop-";
    private const string TempFileSuffix = ".part";

    private readonly TcpListener _listener;

    private ManualPeerTransferReceiver(TcpListener listener)
    {
        _listener = listener;
    }

    public IPEndPoint EndPoint => (IPEndPoint)_listener.LocalEndpoint;

    public static ManualPeerTransferReceiver Start(IPAddress listenAddress, int port = 0)
    {
        ArgumentNullException.ThrowIfNull(listenAddress);
        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 0 and 65535.");
        }

        var listener = new TcpListener(listenAddress, port);
        listener.Start();
        return new ManualPeerTransferReceiver(listener);
    }

    public async Task<ManualPeerTransferReceiveResult> ReceiveOneAsync(
        string destinationDirectory,
        Func<ManualPeerIncomingTransferRequest, CancellationToken, Task<TransferDecision>> confirmAsync,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException("Destination directory must not be empty.", nameof(destinationDirectory));
        }

        ArgumentNullException.ThrowIfNull(confirmAsync);

        using var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();

        var requestFrame = await FrameIO.ReadFrameAsync(stream, FrameIO.MaxHeaderBytes, cancellationToken).ConfigureAwait(false);

        // A structurally readable frame that fails semantic validation (path-like or reserved
        // name, duplicate, malformed checksum, oversize, ...) is answered with a clean rejection
        // decision rather than by throwing across the connection. No payload is read, nothing is
        // written, and the confirmation callback is never invoked.
        if (!TryBuildRequest(destinationDirectory, requestFrame, out var request, out var destinationPaths))
        {
            await WriteDecisionAsync(stream, accepted: false, ManualPeerTransferStatus.InvalidRequest, InvalidRequestReason, cancellationToken).ConfigureAwait(false);
            return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.InvalidRequest, request: null, reason: InvalidRequestReason);
        }

        var decision = await confirmAsync(request, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Receiver confirmation callback returned null.");

        if (!decision.Accepted)
        {
            await WriteDecisionAsync(stream, accepted: false, ManualPeerTransferStatus.Rejected, decision.Reason, cancellationToken).ConfigureAwait(false);
            return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.Rejected, request, reason: decision.Reason);
        }

        var destinationRoot = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);

        if (destinationPaths.Any(item => File.Exists(item.Path)))
        {
            await WriteDecisionAsync(stream, accepted: false, ManualPeerTransferStatus.DestinationAlreadyExists, DestinationExistsReason, cancellationToken).ConfigureAwait(false);
            return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.DestinationAlreadyExists, request, reason: DestinationExistsReason);
        }

        await WriteDecisionAsync(stream, accepted: true, failureStatus: null, reason: null, cancellationToken).ConfigureAwait(false);

        return await ReceiveVerifyPromoteAsync(stream, request, destinationPaths, destinationRoot, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _listener.Stop();

    private static async Task<ManualPeerTransferReceiveResult> ReceiveVerifyPromoteAsync(
        Stream stream,
        ManualPeerIncomingTransferRequest request,
        IReadOnlyList<DestinationPath> destinationPaths,
        string destinationRoot,
        CancellationToken cancellationToken)
    {
        var temps = new List<TempTransferFile>(destinationPaths.Count);
        try
        {
            // Phase 1: stream each payload straight to a private temp file while hashing it. A
            // payload is never fully held in memory, and until verification succeeds only temp
            // files (cleaned up below) exist on disk.
            foreach (var destination in destinationPaths)
            {
                var temp = new TempTransferFile(destination, CreateTempFilePath(destinationRoot));
                temps.Add(temp);

                long receivedLength;
                FileChecksum actualChecksum;
                try
                {
                    using var hash = SHA256.Create();
                    await using (var output = new FileStream(
                        temp.Path,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        useAsync: true))
                    await using (var hashing = new CryptoStream(output, hash, CryptoStreamMode.Write, leaveOpen: true))
                    {
                        receivedLength = await FrameIO.ReadFrameToStreamAsync(
                            stream,
                            hashing,
                            FrameIO.MaxPayloadBytes,
                            cancellationToken).ConfigureAwait(false);
                        await hashing.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
                    }

                    actualChecksum = FileChecksum.Create(ChecksumAlgorithm.Sha256, Convert.ToHexString(hash.Hash!));
                }
                catch (EndOfStreamException)
                {
                    return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.ProtocolError, request, reason: ConnectionClosedReason);
                }
                catch (InvalidOperationException)
                {
                    return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.ProtocolError, request, reason: ConnectionClosedReason);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    await TryWriteCompletionAsync(stream, ManualPeerTransferStatus.WriteFailed, WriteFailedReason, cancellationToken).ConfigureAwait(false);
                    return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.WriteFailed, request, reason: WriteFailedReason);
                }

                if (receivedLength != destination.File.SizeBytes)
                {
                    await WriteCompletionAsync(stream, ManualPeerTransferStatus.SizeMismatch, SizeMismatchReason, cancellationToken).ConfigureAwait(false);
                    return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.SizeMismatch, request, reason: SizeMismatchReason);
                }

                if (!StringComparer.Ordinal.Equals(actualChecksum.Value, destination.File.Checksum.Value))
                {
                    await WriteCompletionAsync(stream, ManualPeerTransferStatus.ChecksumMismatch, ChecksumMismatchReason, cancellationToken).ConfigureAwait(false);
                    return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.ChecksumMismatch, request, reason: ChecksumMismatchReason);
                }
            }

            // Phase 2: every payload is verified, so promote temp files to their final names.
            // Promotion is all-or-nothing: a mid-way failure rolls back the final files this
            // transfer created, so a partial multi-file transfer never leaves final files behind.
            var promoted = new List<TempTransferFile>(temps.Count);
            foreach (var temp in temps)
            {
                try
                {
                    File.Move(temp.Path, temp.Destination.Path, overwrite: false);
                }
                catch (IOException) when (File.Exists(temp.Destination.Path))
                {
                    RollbackPromoted(promoted);
                    await WriteCompletionAsync(stream, ManualPeerTransferStatus.DestinationAlreadyExists, DestinationExistsReason, cancellationToken).ConfigureAwait(false);
                    return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.DestinationAlreadyExists, request, reason: DestinationExistsReason);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    RollbackPromoted(promoted);
                    await TryWriteCompletionAsync(stream, ManualPeerTransferStatus.WriteFailed, WriteFailedReason, cancellationToken).ConfigureAwait(false);
                    return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.WriteFailed, request, reason: WriteFailedReason);
                }

                temp.Promoted = true;
                promoted.Add(temp);
            }

            await WriteCompletionAsync(stream, ManualPeerTransferStatus.Completed, reason: null, cancellationToken).ConfigureAwait(false);
            return ManualPeerTransferReceiveResult.Create(ManualPeerTransferStatus.Completed, request, request.Files);
        }
        finally
        {
            CleanupTemps(temps);
        }
    }

    private static bool TryBuildRequest(
        string destinationDirectory,
        byte[] requestFrame,
        [NotNullWhen(true)] out ManualPeerIncomingTransferRequest? request,
        out IReadOnlyList<DestinationPath> destinationPaths)
    {
        request = null;
        destinationPaths = Array.Empty<DestinationPath>();

        ManualPeerTransferRequestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ManualPeerTransferRequestDto>(requestFrame);
        }
        catch (JsonException)
        {
            return false;
        }

        if (dto?.Files is null || !Guid.TryParse(dto.TransferId, out var transferId))
        {
            return false;
        }

        var destinationRoot = Path.GetFullPath(destinationDirectory);
        var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var summaries = new List<ManualPeerTransferFileSummary>(dto.Files.Count);
        var paths = new List<DestinationPath>(dto.Files.Count);

        foreach (var file in dto.Files)
        {
            if (file is null || !IsLocalWriteSafeFileName(file.FileName))
            {
                return false;
            }

            ManualPeerTransferFileSummary summary;
            try
            {
                summary = ManualPeerTransferFileSummary.Create(
                    file.FileName,
                    file.SizeBytes,
                    FileChecksum.Create(ChecksumAlgorithm.Sha256, file.ChecksumSha256));
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (summary.SizeBytes > FrameIO.MaxPayloadBytes || !seenFileNames.Add(summary.FileName))
            {
                return false;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, summary.FileName));
            if (!IsInsideDirectory(destinationRoot, destinationPath))
            {
                return false;
            }

            summaries.Add(summary);
            paths.Add(new DestinationPath(summary, destinationPath));
        }

        try
        {
            request = ManualPeerIncomingTransferRequest.Create(transferId, summaries);
        }
        catch (ArgumentException)
        {
            return false;
        }

        destinationPaths = paths;
        return true;
    }

    private static string CreateTempFilePath(string destinationRoot) =>
        Path.Combine(destinationRoot, $"{TempFilePrefix}{Guid.NewGuid():N}{TempFileSuffix}");

    private static void RollbackPromoted(IReadOnlyList<TempTransferFile> promoted)
    {
        // These final files were created by this transfer (moved from our own temp files),
        // so deleting them on rollback never removes a pre-existing user file.
        foreach (var temp in promoted)
        {
            TryDelete(temp.Destination.Path);
        }
    }

    private static void CleanupTemps(IReadOnlyList<TempTransferFile> temps)
    {
        foreach (var temp in temps)
        {
            if (!temp.Promoted)
            {
                TryDelete(temp.Path);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsLocalWriteSafeFileName(string fileName) =>
        !string.IsNullOrEmpty(fileName) && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static bool IsInsideDirectory(string directory, string path)
    {
        var normalizedDirectory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteDecisionAsync(
        Stream stream,
        bool accepted,
        ManualPeerTransferStatus? failureStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var dto = new ManualPeerTransferDecisionDto
        {
            Accepted = accepted,
            FailureStatus = failureStatus?.ToString(),
            Reason = reason,
        };

        await WriteJsonFrameAsync(stream, dto, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteCompletionAsync(
        Stream stream,
        ManualPeerTransferStatus status,
        string? reason,
        CancellationToken cancellationToken)
    {
        var dto = new ManualPeerTransferCompletionDto
        {
            Status = status.ToString(),
            Reason = reason,
        };

        await WriteJsonFrameAsync(stream, dto, cancellationToken).ConfigureAwait(false);
    }

    // Best-effort completion for failure paths where the connection may already be broken.
    private static async Task TryWriteCompletionAsync(
        Stream stream,
        ManualPeerTransferStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteCompletionAsync(stream, status, reason, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    private static async Task WriteJsonFrameAsync<T>(
        Stream stream,
        T value,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await FrameIO.WriteFrameAsync(stream, bytes, cancellationToken).ConfigureAwait(false);
    }

    private sealed record DestinationPath(ManualPeerTransferFileSummary File, string Path);

    private sealed class TempTransferFile(DestinationPath destination, string path)
    {
        public DestinationPath Destination { get; } = destination;
        public string Path { get; } = path;
        public bool Promoted { get; set; }
    }
}
