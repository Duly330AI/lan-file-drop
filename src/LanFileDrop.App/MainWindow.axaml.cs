using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LanFileDrop.Core.Checksums;
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
    private OutgoingTransferDraft? _outgoingTransferDraft;
    private PreparedOutgoingTransferManifest? _preparedManifest;
    private bool _isPreparingManifest;

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
        var retainedPickedFiles = false;

        try
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider is null || !storageProvider.CanOpen)
            {
                ClearSelectedFiles();
                ClearOutgoingDraft();
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
                ClearSelectedFiles();
                ClearOutgoingDraft();
                UpdateSelectedFilesPreview("File selection cancelled. No files selected and no files sent.");
                return;
            }

            var selectedFiles = new List<SelectedFilePreview>(files.Count);
            foreach (var file in files)
            {
                var properties = await file.GetBasicPropertiesAsync();
                selectedFiles.Add(new SelectedFilePreview(GetDisplayFileName(file.Name), properties.Size, file));
            }

            DisposeSelectedFileHandles();
            _selectedFiles.Clear();
            _selectedFiles.AddRange(selectedFiles);
            retainedPickedFiles = true;
            ClearOutgoingDraft();
            UpdateSelectedFilesPreview($"{GetFileCountText(_selectedFiles.Count)} selected for preview only. Nothing sent.");
        }
        catch (Exception)
        {
            ClearSelectedFiles();
            ClearOutgoingDraft();
            UpdateSelectedFilesPreview("File selection failed. No files selected and no files sent.");
        }
        finally
        {
            if (!retainedPickedFiles)
            {
                DisposePickedFiles(files);
            }

            SelectFilesButton.IsEnabled = true;
        }
    }

    private void OnClearSelectionClick(object? sender, RoutedEventArgs e)
    {
        ClearSelectedFiles();
        ClearOutgoingDraft();
        UpdateSelectedFilesPreview("Selection cleared. No files selected and no files sent.");
    }

    private void OnPrepareTransferDraftClick(object? sender, RoutedEventArgs e)
    {
        var endpoint = _validatedManualPeerEndpoint;
        if (endpoint is null || _selectedFiles.Count == 0)
        {
            ClearOutgoingDraft();
            SelectedFilesStatusText.Text = "Validate a peer and select files before preparing a draft. Nothing sent.";
            UpdateSendReadiness();
            return;
        }

        try
        {
            _outgoingTransferDraft = CreateOutgoingDraft(endpoint);
            _preparedManifest = null;
            UpdateOutgoingDraftDisplay();
            SelectedFilesStatusText.Text = "Transfer draft prepared for review only. Nothing sent.";
        }
        catch (Exception)
        {
            ClearOutgoingDraft();
            SelectedFilesStatusText.Text = "Could not prepare transfer draft from current preview. Nothing sent.";
        }

        UpdateSendReadiness();
    }

    private async void OnPrepareManifestClick(object? sender, RoutedEventArgs e)
    {
        var endpoint = _validatedManualPeerEndpoint;
        if (endpoint is null || _selectedFiles.Count == 0)
        {
            ClearOutgoingDraft();
            SelectedFilesStatusText.Text = "Validate a peer and select files before preparing a manifest. Nothing sent.";
            UpdateSendReadiness();
            return;
        }

        var selectedSnapshot = _selectedFiles.ToArray();
        _outgoingTransferDraft ??= CreateOutgoingDraft(endpoint);
        _preparedManifest = null;
        _isPreparingManifest = true;
        SelectFilesButton.IsEnabled = false;
        ClearSelectionButton.IsEnabled = false;
        SelectedFilesStatusText.Text = "Calculating checksums and preparing manifest. Nothing is sent.";
        UpdateOutgoingDraftDisplay();
        UpdateSendReadiness();

        try
        {
            var preparedFiles = new List<PreparedOutgoingTransferManifestFile>(selectedSnapshot.Length);
            foreach (var selectedFile in selectedSnapshot)
            {
                using var stream = await selectedFile.StorageFile.OpenReadAsync();
                var checksum = ChecksumCalculator.ComputeSha256(stream);
                preparedFiles.Add(PreparedOutgoingTransferManifestFile.Create(
                    selectedFile.Name,
                    selectedFile.SizeBytes,
                    checksum));
            }

            if (_validatedManualPeerEndpoint == endpoint && _selectedFiles.SequenceEqual(selectedSnapshot))
            {
                _preparedManifest = PreparedOutgoingTransferManifest.Create(endpoint.Display, preparedFiles);
                SelectedFilesStatusText.Text = "Checksums calculated and manifest prepared. Nothing sent.";
            }
            else
            {
                _preparedManifest = null;
                SelectedFilesStatusText.Text = "Manifest preparation finished for a previous selection. Nothing sent.";
            }
        }
        catch (Exception)
        {
            _preparedManifest = null;
            SelectedFilesStatusText.Text = "Checksum or manifest preparation failed. No files sent.";
        }
        finally
        {
            _isPreparingManifest = false;
            SelectFilesButton.IsEnabled = true;
            ClearSelectionButton.IsEnabled = _selectedFiles.Count > 0;
            UpdateOutgoingDraftDisplay();
            UpdateSendReadiness();
        }
    }

    private void OnClearOutgoingDraftClick(object? sender, RoutedEventArgs e)
    {
        if (_isPreparingManifest)
        {
            SelectedFilesStatusText.Text = "Manifest preparation is running. Nothing sent.";
            return;
        }

        ClearOutgoingDraft();
        SelectedFilesStatusText.Text = "Transfer draft cleared. File preview unchanged. Nothing sent.";
    }

    private void OnValidatePeerClick(object? sender, RoutedEventArgs e)
    {
        if (ManualPeerEndpoint.TryParse(ManualPeerInput.Text, out var endpoint, out var error))
        {
            var display = endpoint!.Display;

            _validatedManualPeerEndpoint = endpoint;
            _lastManualPeerProbeStatus = null;
            ClearOutgoingDraft();
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
        ClearOutgoingDraft();
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
        var canPrepare = !_isPreparingManifest && _validatedManualPeerEndpoint is not null && _selectedFiles.Count > 0;
        PrepareTransferDraftButton.IsEnabled = canPrepare;
        PrepareManifestButton.IsEnabled = canPrepare;

        ReadinessPeerText.Text = GetPeerReadinessText();
        ReadinessFilesText.Text = _selectedFiles.Count == 0
            ? "Files: none selected."
            : $"Files: {GetFileCountText(_selectedFiles.Count)} selected, total size {GetTotalSizeText(_selectedFiles)}.";

        var draftStatus = _outgoingTransferDraft is null
            ? "No transfer draft prepared."
            : "Transfer draft prepared for review only.";
        var manifestStatus = _isPreparingManifest
            ? "Manifest preparation running."
            : _preparedManifest is null
                ? "Manifest not prepared."
                : "Manifest prepared with SHA-256 checksums.";
        ReadinessTransferText.Text =
            $"Transfer: {draftStatus} {manifestStatus} Transfer not implemented yet; Send remains disabled. Nothing sent.";
    }

    private void ClearOutgoingDraft()
    {
        _outgoingTransferDraft = null;
        _preparedManifest = null;
        UpdateOutgoingDraftDisplay();
        UpdateSendReadiness();
    }

    private void UpdateOutgoingDraftDisplay()
    {
        if (_outgoingTransferDraft is null)
        {
            OutgoingDraftPanel.IsVisible = false;
            ClearOutgoingDraftButton.IsEnabled = false;
            OutgoingDraftPeerText.Text = string.Empty;
            OutgoingDraftFilesText.Text = string.Empty;
            OutgoingDraftManifestText.Text = string.Empty;
            OutgoingDraftSafetyText.Text = string.Empty;
            return;
        }

        OutgoingDraftPanel.IsVisible = true;
        ClearOutgoingDraftButton.IsEnabled = !_isPreparingManifest;
        OutgoingDraftPeerText.Text = $"Target peer: {_outgoingTransferDraft.TargetPeerDisplay}";
        OutgoingDraftFilesText.Text =
            $"Files: {GetFileCountText(_outgoingTransferDraft.FileCount)}, total known size {GetDraftTotalSizeText(_outgoingTransferDraft)}.";
        OutgoingDraftManifestText.Text = GetManifestStatusText();
        OutgoingDraftSafetyText.Text =
            "Receiver confirmation required later. Not sent; transfer not implemented.";
    }

    private OutgoingTransferDraft CreateOutgoingDraft(ManualPeerEndpoint endpoint)
    {
        var draftFiles = _selectedFiles.Select(file =>
            OutgoingTransferDraftFile.Create(file.Name, file.SizeBytes));

        return OutgoingTransferDraft.Create(endpoint.Display, draftFiles);
    }

    private string GetManifestStatusText()
    {
        if (_isPreparingManifest)
        {
            return "Checksum status: calculating SHA-256. Manifest status: preparing. Nothing sent.";
        }

        if (_preparedManifest is null)
        {
            return "Checksum status: not calculated. Manifest status: not prepared.";
        }

        return $"Checksum status: calculated for {GetFileCountText(_preparedManifest.FileCount)} using SHA-256. Manifest status: prepared.";
    }

    private static string GetDraftTotalSizeText(OutgoingTransferDraft draft)
    {
        var totalSizeText = FormatByteCount(draft.TotalKnownSizeBytes);
        return draft.HasUnknownSizes ? $"{totalSizeText} known, plus unavailable sizes" : totalSizeText;
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
                // Cleanup failure must not start a transfer or crash the UI.
            }
        }
    }

    private void ClearSelectedFiles()
    {
        DisposeSelectedFileHandles();
        _selectedFiles.Clear();
    }

    private void DisposeSelectedFileHandles()
    {
        DisposePickedFiles(_selectedFiles.Select(file => file.StorageFile));
    }

    protected override void OnClosed(EventArgs e)
    {
        DisposeSelectedFileHandles();
        _selectedFiles.Clear();
        base.OnClosed(e);
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

    private sealed record SelectedFilePreview(string Name, ulong? SizeBytes, IStorageFile StorageFile);
}
