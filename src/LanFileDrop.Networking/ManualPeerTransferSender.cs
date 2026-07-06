using System.Net.Sockets;
using System.Text.Json;
using LanFileDrop.Core.Models;
using LanFileDrop.Networking.Protocol;

namespace LanFileDrop.Networking;

public static class ManualPeerTransferSender
{
    public static async Task<ManualPeerTransferSendResult> SendAsync(
        ManualPeerEndpoint endpoint,
        PreparedOutgoingTransferManifest manifest,
        IReadOnlyList<ManualPeerOutgoingTransferFile> files,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(files);

        ValidatePreparedManifest(endpoint, manifest, files);

        using var client = new TcpClient(endpoint.Address.AddressFamily);
        await client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();

        await WriteJsonFrameAsync(stream, CreateRequestDto(files), cancellationToken).ConfigureAwait(false);

        var decision = await ReadJsonFrameAsync<ManualPeerTransferDecisionDto>(
            stream,
            "Received a malformed manual transfer decision.",
            cancellationToken).ConfigureAwait(false);

        if (!decision.Accepted)
        {
            return ManualPeerTransferSendResult.Create(
                ParseStatus(decision.FailureStatus, ManualPeerTransferStatus.Rejected),
                decision.Reason);
        }

        foreach (var file in files)
        {
            await using var fileStream = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            await FrameIO.WriteFrameFromStreamAsync(
                stream,
                fileStream,
                file.SizeBytes,
                FrameIO.MaxPayloadBytes,
                cancellationToken).ConfigureAwait(false);
        }

        var completion = await ReadJsonFrameAsync<ManualPeerTransferCompletionDto>(
            stream,
            "Received a malformed manual transfer completion.",
            cancellationToken).ConfigureAwait(false);

        return ManualPeerTransferSendResult.Create(
            ParseStatus(completion.Status, ManualPeerTransferStatus.ProtocolError),
            completion.Reason);
    }

    private static void ValidatePreparedManifest(
        ManualPeerEndpoint endpoint,
        PreparedOutgoingTransferManifest manifest,
        IReadOnlyList<ManualPeerOutgoingTransferFile> files)
    {
        if (!StringComparer.Ordinal.Equals(manifest.TargetPeerDisplay, endpoint.Display))
        {
            throw new ArgumentException("Prepared manifest target must match the validated manual peer endpoint.", nameof(manifest));
        }

        if (manifest.Files.Count != files.Count)
        {
            throw new ArgumentException("Prepared manifest file count must match outgoing file stream count.", nameof(files));
        }

        var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < manifest.Files.Count; index++)
        {
            var preparedFile = manifest.Files[index];
            var outgoingFile = files[index] ?? throw new ArgumentException(
                "Outgoing file stream list must not contain null entries.",
                nameof(files));

            if (!seenFileNames.Add(preparedFile.FileName))
            {
                throw new ArgumentException("Prepared manifest must not contain duplicate file names.", nameof(manifest));
            }

            var preparedSizeBytes = preparedFile.SizeBytes;
            if (preparedSizeBytes is null)
            {
                throw new InvalidOperationException("Prepared manifest files must have known sizes before sending.");
            }

            if (preparedSizeBytes.Value > (ulong)FrameIO.MaxPayloadBytes)
            {
                throw new InvalidOperationException(
                    $"Prepared manifest file exceeds the current transfer payload limit of {FrameIO.MaxPayloadBytes} bytes.");
            }

            var preparedSize = checked((long)preparedSizeBytes.Value);
            var matchesPreparedManifest =
                StringComparer.Ordinal.Equals(preparedFile.FileName, outgoingFile.FileName) &&
                preparedSize == outgoingFile.SizeBytes &&
                StringComparer.Ordinal.Equals(preparedFile.Checksum.Value, outgoingFile.Checksum.Value);

            if (!matchesPreparedManifest)
            {
                throw new ArgumentException("Outgoing files must match the prepared manifest exactly.", nameof(files));
            }
        }
    }

    private static ManualPeerTransferRequestDto CreateRequestDto(IReadOnlyList<ManualPeerOutgoingTransferFile> files) =>
        new()
        {
            TransferId = Guid.NewGuid().ToString("D"),
            Files = files.Select(file => new ManualPeerTransferFileDto
            {
                FileName = file.FileName,
                SizeBytes = file.SizeBytes,
                ChecksumSha256 = file.Checksum.Value,
            }).ToList(),
        };

    private static async Task WriteJsonFrameAsync<T>(
        Stream stream,
        T value,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await FrameIO.WriteFrameAsync(stream, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadJsonFrameAsync<T>(
        Stream stream,
        string malformedMessage,
        CancellationToken cancellationToken)
    {
        var bytes = await FrameIO.ReadFrameAsync(stream, FrameIO.MaxHeaderBytes, cancellationToken).ConfigureAwait(false);

        try
        {
            return JsonSerializer.Deserialize<T>(bytes)
                ?? throw new TransferProtocolException("Received an empty manual transfer frame.");
        }
        catch (JsonException ex)
        {
            throw new TransferProtocolException(malformedMessage, ex);
        }
    }

    private static ManualPeerTransferStatus ParseStatus(string? status, ManualPeerTransferStatus fallback) =>
        Enum.TryParse<ManualPeerTransferStatus>(status, ignoreCase: false, out var parsed)
            ? parsed
            : fallback;
}
