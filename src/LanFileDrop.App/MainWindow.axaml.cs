using Avalonia.Controls;
using Avalonia.Interactivity;
using LanFileDrop.Core.Models;

namespace LanFileDrop.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnValidatePeerClick(object? sender, RoutedEventArgs e)
    {
        if (ManualPeerEndpoint.TryParse(ManualPeerInput.Text, out var endpoint, out var error))
        {
            var display = endpoint!.Display;

            ManualPeerStatusText.Text = $"Validated peer: {display}";
            PeerPlaceholderText.IsVisible = false;
            ValidatedManualPeerText.IsVisible = true;
            ValidatedManualPeerText.Text = $"{display} — Validated only — not connected";
            return;
        }

        ManualPeerStatusText.Text = error ?? "Manual peer endpoint is invalid.";
        PeerPlaceholderText.IsVisible = true;
        ValidatedManualPeerText.IsVisible = false;
        ValidatedManualPeerText.Text = string.Empty;
    }
}
