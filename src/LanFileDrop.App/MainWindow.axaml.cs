using Avalonia.Controls;
using Avalonia.Interactivity;
using LanFileDrop.Core.Models;
using LanFileDrop.Networking;

namespace LanFileDrop.App;

public partial class MainWindow : Window
{
    private ManualPeerEndpoint? _validatedManualPeerEndpoint;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnValidatePeerClick(object? sender, RoutedEventArgs e)
    {
        if (ManualPeerEndpoint.TryParse(ManualPeerInput.Text, out var endpoint, out var error))
        {
            var display = endpoint!.Display;

            _validatedManualPeerEndpoint = endpoint;
            ManualPeerStatusText.Text = $"Validated peer: {display}";
            PeerPlaceholderText.IsVisible = false;
            ValidatedManualPeerText.IsVisible = true;
            ValidatedManualPeerText.Text = $"{display} — Validated only — not connected";
            ProbeConnectionButton.IsEnabled = true;
            return;
        }

        ManualPeerStatusText.Text = error ?? "Manual peer endpoint is invalid.";
        ClearValidatedManualPeer();
    }

    private void OnManualPeerInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_validatedManualPeerEndpoint is null)
        {
            return;
        }

        ClearValidatedManualPeer();
        ManualPeerStatusText.Text = "Manual peer changed. Validate again before probing.";
    }

    private async void OnProbeConnectionClick(object? sender, RoutedEventArgs e)
    {
        var endpoint = _validatedManualPeerEndpoint;
        if (endpoint is null)
        {
            ProbeConnectionButton.IsEnabled = false;
            ManualPeerStatusText.Text = "Validate a peer before probing — no files sent.";
            return;
        }

        ProbeConnectionButton.IsEnabled = false;
        ManualPeerStatusText.Text = $"Probing {endpoint.Display} — no files are sent and no transfer is started.";

        var result = await ManualPeerConnectionProbe.ProbeAsync(endpoint);

        ManualPeerStatusText.Text = _validatedManualPeerEndpoint == endpoint
            ? GetProbeStatusText(result.Status)
            : "Probe finished for a previous peer — no files sent.";

        if (_validatedManualPeerEndpoint == endpoint)
        {
            ProbeConnectionButton.IsEnabled = true;
        }
    }

    private void ClearValidatedManualPeer()
    {
        _validatedManualPeerEndpoint = null;
        ProbeConnectionButton.IsEnabled = false;
        PeerPlaceholderText.IsVisible = true;
        ValidatedManualPeerText.IsVisible = false;
        ValidatedManualPeerText.Text = string.Empty;
    }

    private static string GetProbeStatusText(ManualPeerConnectionProbeStatus status) =>
        status switch
        {
            ManualPeerConnectionProbeStatus.Connected => "Probe connected — no files sent",
            ManualPeerConnectionProbeStatus.Timeout => "Probe timed out — no files sent",
            ManualPeerConnectionProbeStatus.Failed => "Probe failed — no files sent",
            ManualPeerConnectionProbeStatus.Cancelled => "Probe cancelled — no files sent",
            _ => "Probe finished — no files sent",
        };
}
