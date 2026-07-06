using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using LanFileDrop.Networking.Protocol;

namespace LanFileDrop.Networking;

public sealed class LoopbackTransferServer : IDisposable
{
    private readonly TcpListener _listener;

    private LoopbackTransferServer(TcpListener listener)
    {
        _listener = listener;
    }

    public IPEndPoint EndPoint => (IPEndPoint)_listener.LocalEndpoint;

    public static LoopbackTransferServer Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new LoopbackTransferServer(listener);
    }

    public async Task<TransferResult> ReceiveOneAsync(CancellationToken cancellationToken)
    {
        using var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();

        var headerBytes = await FrameIO.ReadFrameAsync(stream, FrameIO.MaxHeaderBytes, cancellationToken).ConfigureAwait(false);

        TransferHeaderDto header;
        try
        {
            header = JsonSerializer.Deserialize<TransferHeaderDto>(headerBytes)
                ?? throw new TransferProtocolException("Received an empty transfer header.");
        }
        catch (JsonException ex)
        {
            throw new TransferProtocolException("Received a malformed transfer header.", ex);
        }

        var payload = await FrameIO.ReadFrameAsync(stream, FrameIO.MaxPayloadBytes, cancellationToken).ConfigureAwait(false);

        return TransferResult.Create(header.FileName, header.DeclaredSizeBytes, header.DeclaredChecksumSha256, payload);
    }

    public void Dispose() => _listener.Stop();
}
