using System.Net;
using System.Net.Sockets;
using System.Reflection;
using LanFileDrop.Core.Models;

namespace LanFileDrop.Networking.Tests;

public class ManualPeerConnectionProbeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RefusedProbeTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ProbeAsync_WithListeningLoopbackEndpoint_ReturnsConnected()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var endpoint = CreateLoopbackEndpoint(((IPEndPoint)listener.LocalEndpoint).Port);
            using var cts = new CancellationTokenSource(TestTimeout);

            var acceptTask = listener.AcceptTcpClientAsync(cts.Token);
            var result = await ManualPeerConnectionProbe.ProbeAsync(endpoint, ProbeTimeout, cts.Token);
            using var acceptedClient = await acceptTask;

            Assert.Equal(ManualPeerConnectionProbeStatus.Connected, result.Status);
            Assert.True(result.Connected);
            Assert.Null(result.Error);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ProbeAsync_WithUnusedLoopbackPort_ReturnsFailed()
    {
        var endpoint = CreateLoopbackEndpoint(GetUnusedLoopbackPort());
        using var cts = new CancellationTokenSource(TestTimeout);

        var result = await ManualPeerConnectionProbe.ProbeAsync(endpoint, RefusedProbeTimeout, cts.Token);

        Assert.Equal(ManualPeerConnectionProbeStatus.Failed, result.Status);
        Assert.False(result.Connected);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task ProbeAsync_WithCancelledToken_ReturnsCancelled()
    {
        var endpoint = CreateLoopbackEndpoint(GetUnusedLoopbackPort());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await ManualPeerConnectionProbe.ProbeAsync(endpoint, ProbeTimeout, cts.Token);

        Assert.Equal(ManualPeerConnectionProbeStatus.Cancelled, result.Status);
        Assert.False(result.Connected);
    }

    [Fact]
    public async Task ProbeAsync_WithNullEndpoint_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ManualPeerConnectionProbe.ProbeAsync(null!, ProbeTimeout, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task ProbeAsync_WithInvalidTimeout_ThrowsArgumentOutOfRangeException(int timeoutSeconds)
    {
        var endpoint = CreateLoopbackEndpoint(GetUnusedLoopbackPort());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ManualPeerConnectionProbe.ProbeAsync(endpoint, TimeSpan.FromSeconds(timeoutSeconds), CancellationToken.None));
    }

    [Fact]
    public void ProbeAsync_PublicApiAcceptsManualPeerEndpointNotRawString()
    {
        var methods = typeof(ManualPeerConnectionProbe)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(ManualPeerConnectionProbe.ProbeAsync))
            .ToArray();

        Assert.NotEmpty(methods);
        Assert.Contains(methods, method =>
            method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ManualPeerEndpoint)));
        Assert.All(methods, method =>
            Assert.DoesNotContain(method.GetParameters(), parameter => parameter.ParameterType == typeof(string)));
    }

    private static ManualPeerEndpoint CreateLoopbackEndpoint(int port)
    {
        var ok = ManualPeerEndpoint.TryParse($"127.0.0.1:{port}", out var endpoint, out var error);

        Assert.True(ok, error);
        return endpoint!;
    }

    private static int GetUnusedLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
