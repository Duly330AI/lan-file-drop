using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using LanFileDrop.Core.Models;
using LanFileDrop.Networking.Protocol;

namespace LanFileDrop.Networking;

public static class LoopbackTransferClient
{
    public static async Task SendAsync(
        IPEndPoint endpoint,
        FileManifest manifest,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(payload);

        // This prototype is loopback-only by design. Reject any non-loopback target before
        // opening a socket so a real LAN/remote address can never be contacted here.
        if (!IPAddress.IsLoopback(endpoint.Address))
        {
            throw new ArgumentException("Only loopback endpoints are allowed in the loopback transfer prototype.", nameof(endpoint));
        }

        using var client = new TcpClient();
        await client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();

        var header = new TransferHeaderDto
        {
            FileName = manifest.FileName,
            DeclaredSizeBytes = manifest.SizeBytes,
            DeclaredChecksumSha256 = manifest.DeclaredChecksum,
        };

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header);

        await FrameIO.WriteFrameAsync(stream, headerBytes, cancellationToken).ConfigureAwait(false);
        await FrameIO.WriteFrameAsync(stream, payload, cancellationToken).ConfigureAwait(false);
    }
}
