using System.Net.Sockets;
using LanFileDrop.Core.Models;

namespace LanFileDrop.Networking;

public static class ManualPeerConnectionProbe
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(10);

    public static async Task<ManualPeerConnectionResult> ProbeAsync(
        ManualPeerEndpoint endpoint,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var boundedTimeout = timeout ?? DefaultTimeout;
        if (boundedTimeout <= TimeSpan.Zero || boundedTimeout > MaxTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                $"Timeout must be greater than zero and no longer than {MaxTimeout.TotalSeconds} seconds.");
        }

        using var timeoutCts = new CancellationTokenSource(boundedTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            using var client = new TcpClient(endpoint.Address.AddressFamily);
            await client.ConnectAsync(endpoint.Address, endpoint.Port, linkedCts.Token).ConfigureAwait(false);
            return ManualPeerConnectionResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ManualPeerConnectionResult.Cancelled();
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return ManualPeerConnectionResult.Timeout();
        }
        catch (SocketException ex)
        {
            return ManualPeerConnectionResult.Failed(ex.Message);
        }
    }
}
