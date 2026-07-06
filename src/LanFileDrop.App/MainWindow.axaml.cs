using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LanFileDrop.Core.Models;
using LanFileDrop.Networking;

namespace LanFileDrop.App;

public partial class MainWindow : Window
{
    private const int MaxSelectedFilePreviewRows = 50;
    private const int MaxDisplayFileNameLength = 80;

    private readonly List<SelectedFilePreview> _selectedFiles = [];
    private ManualPeerEndpoint? _validatedManualPeerEndpoint;
    private ManualPeerConnectionProbeStatus? _lastManualPeerProbeStatus;

    public MainWindow()
    {
        InitializeComponent();
        UpdateSelectedFilesPreview("No files selected. Nothing sent. Select files to preview names and sizes.");
    }

    private async void OnSelectFilesClick(object? sender, RoutedEventArgs e)
    {
        SelectFilesButton.IsEnabled = false;
        SelectedFilesStatusText.Text = "Opening file picker. Preview only; no files are sent.";

        IReadOnlyList<IStorageFile> files = [];

        try
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider is null || !storageProvider.CanOpen)
            {
                _selectedFiles.Clear();
                UpdateSelectedFilesPreview("File picker is not available. No files selected and no files sent.");
                return;
            }

            files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select files to preview",
                AllowMultiple = true,
            });

            if (files.Count == 0)
            {
                _selectedFiles.Clear();
                UpdateSelectedFilesPreview("File selection cancelled. No files selected and no files sent.");
                return;
            }

            var selectedFiles = new List<SelectedFilePreview>(files.Count);
            foreach (var file in files)
            {
                var properties = await file.GetBasicPropertiesAsync();
                selectedFiles.Add(new SelectedFilePreview(GetDisplayFileName(file.Name), properties.Size));
            }

            _selectedFiles.Clear();
            _selectedFiles.AddRange(selectedFiles);
            UpdateSelectedFilesPreview($"{GetFileCountText(_selectedFiles.Count)} selected for preview only. Nothing sent.");
        }
        catch (Exception)
        {
            _selectedFiles.Clear();
            UpdateSelectedFilesPreview("File selection failed. No files selected and no files sent.");
        }
        finally
        {
            DisposePickedFiles(files);
            SelectFilesButton.IsEnabled = true;
        }
    }

    private void OnClearSelectionClick(object? sender, RoutedEventArgs e)
    {
        _selectedFiles.Clear();
        UpdateSelectedFilesPreview("Selection cleared. No files selected and no files sent.");
    }

    private void OnValidatePeerClick(object? sender, RoutedEventArgs e)
    {
        if (ManualPeerEndpoint.TryParse(ManualPeerInput.Text, out var endpoint, out var error))
        {
            var display = endpoint!.Display;

            _validatedManualPeerEndpoint = endpoint;
            _lastManualPeerProbeStatus = null;
            ManualPeerStatusText.Text = $"Validated peer: {display}";
            PeerPlaceholderText.IsVisible = false;
            ValidatedManualPeerText.IsVisible = true;
            ValidatedManualPeerText.Text = $"{display} — Validated only — not connected";
            ProbeConnectionButton.IsEnabled = true;
            UpdateSendReadiness();
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
            _lastManualPeerProbeStatus = null;
            UpdateSendReadiness();
            return;
        }

        ProbeConnectionButton.IsEnabled = false;
        _lastManualPeerProbeStatus = null;
        UpdateSendReadiness();
        ManualPeerStatusText.Text = $"Probing {endpoint.Display} — no files are sent and no transfer is started.";

        var result = await ManualPeerConnectionProbe.ProbeAsync(endpoint);

        if (_validatedManualPeerEndpoint == endpoint)
        {
            _lastManualPeerProbeStatus = result.Status;
        }

        ManualPeerStatusText.Text = _validatedManualPeerEndpoint == endpoint
            ? GetProbeStatusText(result.Status)
            : "Probe finished for a previous peer — no files sent.";

        if (_validatedManualPeerEndpoint == endpoint)
        {
            ProbeConnectionButton.IsEnabled = true;
        }

        UpdateSendReadiness();
    }

    private void ClearValidatedManualPeer()
    {
        _validatedManualPeerEndpoint = null;
        _lastManualPeerProbeStatus = null;
        ProbeConnectionButton.IsEnabled = false;
        PeerPlaceholderText.IsVisible = true;
        ValidatedManualPeerText.IsVisible = false;
        ValidatedManualPeerText.Text = string.Empty;
        UpdateSendReadiness();
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

    private void UpdateSelectedFilesPreview(string statusText)
    {
        SelectedFilesPreviewPanel.Children.Clear();

        if (_selectedFiles.Count == 0)
        {
            SelectedFilesSummaryText.Text = "No files selected";
            SelectedFilesStatusText.Text = statusText;
            ClearSelectionButton.IsEnabled = false;
            UpdateSendReadiness();
            return;
        }

        ClearSelectionButton.IsEnabled = true;
        SelectedFilesSummaryText.Text =
            $"{GetFileCountText(_selectedFiles.Count)} selected. Total size: {GetTotalSizeText(_selectedFiles)}";
        SelectedFilesStatusText.Text = statusText;

        var shownRowCount = Math.Min(_selectedFiles.Count, MaxSelectedFilePreviewRows);
        for (var index = 0; index < shownRowCount; index++)
        {
            var previewItem = _selectedFiles[index];
            var itemText = new TextBlock
            {
                Text = $"{previewItem.Name} ({FormatFileSize(previewItem.SizeBytes)})",
                TextWrapping = TextWrapping.Wrap,
            };
            itemText.Classes.Add("muted");
            SelectedFilesPreviewPanel.Children.Add(itemText);
        }

        var hiddenRowCount = _selectedFiles.Count - shownRowCount;
        if (hiddenRowCount > 0)
        {
            var summaryText = new TextBlock
            {
                Text = $"... and {hiddenRowCount} more files not shown",
                TextWrapping = TextWrapping.Wrap,
            };
            summaryText.Classes.Add("muted");
            SelectedFilesPreviewPanel.Children.Add(summaryText);
        }

        UpdateSendReadiness();
    }

    private void UpdateSendReadiness()
    {
        ReadinessPeerText.Text = GetPeerReadinessText();
        ReadinessFilesText.Text = _selectedFiles.Count == 0
            ? "Files: none selected."
            : $"Files: {GetFileCountText(_selectedFiles.Count)} selected, total size {GetTotalSizeText(_selectedFiles)}.";
        ReadinessTransferText.Text = "Transfer: not implemented yet; Send remains disabled. Ready checks only. Nothing sent.";
    }

    private string GetPeerReadinessText()
    {
        if (_validatedManualPeerEndpoint is null)
        {
            return "Peer: not validated.";
        }

        if (_lastManualPeerProbeStatus is { } probeStatus)
        {
            return $"Peer: validated; last probe {GetProbeReadinessStatusText(probeStatus)}.";
        }

        return "Peer: validated only; no probe result yet.";
    }

    private static string GetProbeReadinessStatusText(ManualPeerConnectionProbeStatus status) =>
        status switch
        {
            ManualPeerConnectionProbeStatus.Connected => "connected",
            ManualPeerConnectionProbeStatus.Timeout => "timed out",
            ManualPeerConnectionProbeStatus.Failed => "failed",
            ManualPeerConnectionProbeStatus.Cancelled => "cancelled",
            _ => "finished",
        };

    private static string GetDisplayFileName(string fileName)
    {
        var displayName = string.IsNullOrWhiteSpace(fileName)
            ? "Unnamed file"
            : fileName.Trim().TrimEnd('/', '\\');
        var slashIndex = displayName.LastIndexOf('/');
        var backslashIndex = displayName.LastIndexOf('\\');
        var separatorIndex = Math.Max(slashIndex, backslashIndex);

        if (separatorIndex >= 0)
        {
            displayName = displayName[(separatorIndex + 1)..];
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Unnamed file";
        }

        displayName = displayName.Replace('\r', ' ').Replace('\n', ' ');
        return ShortenDisplayFileName(displayName);
    }

    private static string ShortenDisplayFileName(string displayName)
    {
        if (displayName.Length <= MaxDisplayFileNameLength)
        {
            return displayName;
        }

        var dotIndex = displayName.LastIndexOf('.');
        if (dotIndex > 0 && dotIndex < displayName.Length - 1)
        {
            var extension = displayName[dotIndex..];
            if (extension.Length <= 12 && extension.Length + 8 < MaxDisplayFileNameLength)
            {
                var prefixLength = MaxDisplayFileNameLength - extension.Length - 3;
                return $"{displayName[..prefixLength]}...{extension}";
            }
        }

        return $"{displayName[..(MaxDisplayFileNameLength - 3)]}...";
    }

    private static void DisposePickedFiles(IEnumerable<IStorageFile> files)
    {
        foreach (var file in files)
        {
            try
            {
                file.Dispose();
            }
            catch (Exception)
            {
                // Picker handles are not retained; cleanup failure must not start a transfer or crash the UI.
            }
        }
    }

    private static string GetFileCountText(int count) => count == 1 ? "1 file" : $"{count} files";

    private static string GetTotalSizeText(IEnumerable<SelectedFilePreview> selectedFiles)
    {
        decimal totalSizeBytes = 0;
        var hasUnknownSize = false;

        foreach (var previewItem in selectedFiles)
        {
            if (previewItem.SizeBytes is { } sizeBytes)
            {
                totalSizeBytes += sizeBytes;
                continue;
            }

            hasUnknownSize = true;
        }

        var totalSizeText = FormatByteCount(totalSizeBytes);
        return hasUnknownSize ? $"{totalSizeText} known, plus unavailable sizes" : totalSizeText;
    }

    private static string FormatFileSize(ulong? sizeBytes) =>
        sizeBytes is { } knownSize ? FormatByteCount(knownSize) : "size unavailable";

    private static string FormatByteCount(decimal sizeBytes)
    {
        var unitIndex = 0;
        string[] units = ["B", "KB", "MB", "GB", "TB"];

        while (sizeBytes >= 1024 && unitIndex < units.Length - 1)
        {
            sizeBytes /= 1024;
            unitIndex++;
        }

        var format = unitIndex == 0 ? "0" : "0.#";
        return $"{sizeBytes.ToString(format, CultureInfo.InvariantCulture)} {units[unitIndex]}";
    }

    private sealed record SelectedFilePreview(string Name, ulong? SizeBytes);
}
