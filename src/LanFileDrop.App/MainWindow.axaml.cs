using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
    private string? _receiveDirectoryPath;
    private string _receiveFolderDisplayText = "No receive folder selected";
    private ManualPeerTransferReceiver? _receiver;
    private CancellationTokenSource? _receiverCts;
    private Task? _receiverTask;
    private ManualPeerIncomingTransferRequest? _pendingIncomingRequest;
    private TaskCompletionSource<TransferDecision>? _pendingIncomingDecision;
    private CancellationTokenSource? _sendCts;
    private bool _isSelectingFiles;
    private bool _isPreparingManifest;
    private bool _isSending;
    private bool _isReceiverRunning;

    public MainWindow()
    {
        InitializeComponent();
        UpdateSelectedFilesPreview("No files selected. Nothing sent. Select files to preview names and sizes.");
        UpdateReceiverControls();
    }

    private async void OnSelectFilesClick(object? sender, RoutedEventArgs e)
    {
        if (_isSending)
        {
            SelectedFilesStatusText.Text = "A send is running. Selection cannot change right now.";
            return;
        }

        _isSelectingFiles = true;
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

            _isSelectingFiles = false;
            UpdateSendReadiness();
        }
    }

    private void OnClearSelectionClick(object? sender, RoutedEventArgs e)
    {
        if (_isSending)
        {
            SelectedFilesStatusText.Text = "A send is running. Selection cannot change right now.";
            return;
        }

        ClearSelectedFiles();
        ClearOutgoingDraft();
        UpdateSelectedFilesPreview("Selection cleared. No files selected and no files sent.");
    }

    private void OnPrepareTransferDraftClick(object? sender, RoutedEventArgs e)
    {
        if (_isSending)
        {
            SelectedFilesStatusText.Text = "A send is running. Draft cannot change right now.";
            return;
        }

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
        if (_isSending)
        {
            SelectedFilesStatusText.Text = "A send is running. Manifest cannot change right now.";
            return;
        }

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
            UpdateOutgoingDraftDisplay();
            UpdateSendReadiness();
        }
    }

    private void OnClearOutgoingDraftClick(object? sender, RoutedEventArgs e)
    {
        if (_isSending)
        {
            SelectedFilesStatusText.Text = "A send is running. Draft cannot be cleared right now.";
            return;
        }

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
        if (_isSending)
        {
            ManualPeerStatusText.Text = "A send is running. Peer cannot change right now.";
            return;
        }

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
        if (_isSending)
        {
            return;
        }

        if (_validatedManualPeerEndpoint is null)
        {
            return;
        }

        ClearValidatedManualPeer();
        ManualPeerStatusText.Text = "Manual peer changed. Validate again before probing.";
    }

    private async void OnProbeConnectionClick(object? sender, RoutedEventArgs e)
    {
        if (_isSending)
        {
            ManualPeerStatusText.Text = "A send is running. Probe cannot start right now.";
            return;
        }

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

        if (_validatedManualPeerEndpoint == endpoint && !_isSending)
        {
            ProbeConnectionButton.IsEnabled = true;
        }

        UpdateSendReadiness();
    }

    private async void OnSendPreparedTransferClick(object? sender, RoutedEventArgs e)
    {
        if (!TryGetPreparedSendSnapshot(
            out var endpoint,
            out var manifest,
            out var selectedSnapshot,
            out var notReadyReason))
        {
            SelectedFilesStatusText.Text = notReadyReason;
            UpdateSendReadiness();
            return;
        }

        _isSending = true;
        _sendCts = new CancellationTokenSource();
        ProbeConnectionButton.IsEnabled = false;
        SelectedFilesStatusText.Text = "Sending prepared transfer. Waiting for receiver decision if needed.";
        UpdateOutgoingDraftDisplay();
        UpdateSendReadiness();

        try
        {
            var outgoingFiles = CreateManualPeerOutgoingFiles(manifest, selectedSnapshot);
            var result = await ManualPeerTransferSender.SendAsync(endpoint, manifest, outgoingFiles, _sendCts.Token);

            SelectedFilesStatusText.Text = GetSendResultText(result.Status);
        }
        catch (OperationCanceledException)
        {
            SelectedFilesStatusText.Text = "Send cancelled. No full local paths were shown.";
        }
        catch (Exception)
        {
            SelectedFilesStatusText.Text = "Send failed. No full local paths were shown.";
        }
        finally
        {
            _sendCts?.Dispose();
            _sendCts = null;
            _isSending = false;
            UpdateOutgoingDraftDisplay();
            UpdateSendReadiness();
            ProbeConnectionButton.IsEnabled = _validatedManualPeerEndpoint is not null;
        }
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

    private async void OnSelectReceiveFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_isReceiverRunning)
        {
            ReceiverStatusText.Text = "Stop the receiver before changing the receive folder.";
            return;
        }

        SelectReceiveFolderButton.IsEnabled = false;
        ReceiverStatusText.Text = "Opening receive folder picker. No folder scan is performed.";

        IReadOnlyList<IStorageFolder> folders = [];

        try
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider is null || !storageProvider.CanOpen)
            {
                ReceiverStatusText.Text = "Receive folder picker is not available.";
                return;
            }

            folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select receive folder",
                AllowMultiple = false,
            });

            if (folders.Count == 0)
            {
                ReceiverStatusText.Text = "Receive folder selection cancelled.";
                return;
            }

            var folder = folders[0];
            var localPath = folder.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                _receiveDirectoryPath = null;
                _receiveFolderDisplayText = "No receive folder selected";
                ReceiverStatusText.Text = "Selected receive folder is not available as a local folder.";
                return;
            }

            _receiveDirectoryPath = localPath;
            _receiveFolderDisplayText = GetDisplayFolderName(folder.Name);
            ReceiverStatusText.Text = "Receive folder selected. Start receiver when ready.";
        }
        catch (Exception)
        {
            _receiveDirectoryPath = null;
            _receiveFolderDisplayText = "No receive folder selected";
            ReceiverStatusText.Text = "Receive folder selection failed.";
        }
        finally
        {
            DisposePickedFolders(folders);
            UpdateReceiverControls();
        }
    }

    private void OnStartReceiverClick(object? sender, RoutedEventArgs e)
    {
        if (_isReceiverRunning)
        {
            ReceiverStatusText.Text = "Receiver is already running.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_receiveDirectoryPath))
        {
            ReceiverStatusText.Text = "Select a receive folder before starting the receiver.";
            UpdateReceiverControls();
            return;
        }

        if (!TryParseReceivePort(out var port))
        {
            ReceiverStatusText.Text = "Enter a valid receive port between 1 and 65535.";
            UpdateReceiverControls();
            return;
        }

        try
        {
            var receiver = ManualPeerTransferReceiver.Start(IPAddress.Any, port);
            var cts = new CancellationTokenSource();

            _receiver = receiver;
            _receiverCts = cts;
            _isReceiverRunning = true;
            _receiverTask = RunReceiverAsync(receiver, _receiveDirectoryPath, cts.Token);

            ReceiverStatusText.Text = $"Receiver listening on port {port} for one transfer. No discovery.";
            IncomingRequestSummaryText.Text = "Waiting for one incoming transfer.";
            IncomingRequestFilesText.Text = "No request metadata yet.";
            IncomingRequestIntegrityText.Text = "No files are written before Accept.";
        }
        catch (Exception)
        {
            ReceiverStatusText.Text = "Receiver could not start. Check the port and receive folder.";
            _receiver = null;
            _receiverCts?.Dispose();
            _receiverCts = null;
            _receiverTask = null;
            _isReceiverRunning = false;
        }
        finally
        {
            UpdateReceiverControls();
        }
    }

    private void OnStopReceiverClick(object? sender, RoutedEventArgs e)
    {
        if (!_isReceiverRunning)
        {
            ReceiverStatusText.Text = "Receiver is already stopped.";
            return;
        }

        _pendingIncomingDecision?.TrySetResult(TransferDecision.Reject("Receiver stopped."));
        _receiverCts?.Cancel();
        ReceiverStatusText.Text = "Receiver stop requested.";
        AcceptIncomingButton.IsEnabled = false;
        RejectIncomingButton.IsEnabled = false;
        UpdateReceiverControls();
    }

    private void OnAcceptIncomingClick(object? sender, RoutedEventArgs e)
    {
        var pendingDecision = _pendingIncomingDecision;
        if (pendingDecision is null || _pendingIncomingRequest is null)
        {
            return;
        }

        AcceptIncomingButton.IsEnabled = false;
        RejectIncomingButton.IsEnabled = false;
        ReceiverStatusText.Text = "Incoming transfer accepted. Receiving and verifying before final write.";
        pendingDecision.TrySetResult(TransferDecision.Accept());
    }

    private void OnRejectIncomingClick(object? sender, RoutedEventArgs e)
    {
        var pendingDecision = _pendingIncomingDecision;
        if (pendingDecision is null || _pendingIncomingRequest is null)
        {
            return;
        }

        AcceptIncomingButton.IsEnabled = false;
        RejectIncomingButton.IsEnabled = false;
        ReceiverStatusText.Text = "Incoming transfer rejected. No files written.";
        pendingDecision.TrySetResult(TransferDecision.Reject("Rejected by receiver."));
    }

    private async Task RunReceiverAsync(
        ManualPeerTransferReceiver receiver,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await receiver.ReceiveOneAsync(
                destinationDirectory,
                ConfirmIncomingTransferAsync,
                cancellationToken);

            await Dispatcher.UIThread.InvokeAsync(() => ApplyReceiveResult(receiver, result));
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_receiver == receiver)
                {
                    ReceiverStatusText.Text = "Receiver cancelled.";
                    IncomingRequestSummaryText.Text = "No incoming transfer is active.";
                    IncomingRequestFilesText.Text = "No request metadata yet.";
                    IncomingRequestIntegrityText.Text = "No files were written by the cancelled receive operation.";
                }
            });
        }
        catch (Exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_receiver == receiver)
                {
                    ReceiverStatusText.Text = "Receive failed.";
                    IncomingRequestSummaryText.Text = "No incoming transfer is active.";
                    IncomingRequestFilesText.Text = "No request metadata yet.";
                    IncomingRequestIntegrityText.Text = "No full paths were shown.";
                }
            });
        }
        finally
        {
            receiver.Dispose();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_receiver == receiver)
                {
                    _receiver = null;
                    _receiverCts?.Dispose();
                    _receiverCts = null;
                    _receiverTask = null;
                    _isReceiverRunning = false;
                    _pendingIncomingRequest = null;
                    _pendingIncomingDecision = null;
                    UpdateReceiverControls();
                }
            });
        }
    }

    private async Task<TransferDecision> ConfirmIncomingTransferAsync(
        ManualPeerIncomingTransferRequest request,
        CancellationToken cancellationToken)
    {
        var decisionSource = new TaskCompletionSource<TransferDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        TransferDecision? decision = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _pendingIncomingRequest = request;
            _pendingIncomingDecision = decisionSource;
            ReceiverStatusText.Text = "Incoming transfer is waiting for Accept or Reject.";
            IncomingRequestSummaryText.Text =
                $"Request {GetShortRequestId(request.TransferId)}: {GetFileCountText(request.FileCount)}, total size {FormatByteCount(request.TotalBytes)}.";
            IncomingRequestFilesText.Text = $"Files: {GetIncomingFileListText(request)}";
            IncomingRequestIntegrityText.Text = "SHA-256 manifest received. No files are written before Accept.";
            UpdateReceiverControls();
        });

        try
        {
            decision = await decisionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return decision;
        }
        catch (OperationCanceledException)
        {
            return TransferDecision.Reject("Receiver cancelled.");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_pendingIncomingDecision == decisionSource)
                {
                    _pendingIncomingDecision = null;
                    _pendingIncomingRequest = null;
                    AcceptIncomingButton.IsEnabled = false;
                    RejectIncomingButton.IsEnabled = false;

                    ReceiverStatusText.Text = decision?.Accepted == true
                        ? "Incoming transfer accepted. Receiving and verifying before final write."
                        : "Incoming transfer rejected or cancelled. No files written.";
                }
            });
        }
    }

    private void ApplyReceiveResult(
        ManualPeerTransferReceiver receiver,
        ManualPeerTransferReceiveResult result)
    {
        if (_receiver != receiver)
        {
            return;
        }

        ReceiverStatusText.Text = GetReceiveResultText(result.Status);
        IncomingRequestSummaryText.Text = result.Request is null
            ? "No incoming transfer is active."
            : $"Completed request {GetShortRequestId(result.Request.TransferId)}.";
        IncomingRequestFilesText.Text = result.Success
            ? $"Received {GetFileCountText(result.ReceivedFiles.Count)}."
            : "No completed receive to show.";
        IncomingRequestIntegrityText.Text = result.Success
            ? "Files were written only after Accept and checksum verification."
            : "No silent overwrite or path disclosure.";
    }

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
        var canEditSelection = !_isSelectingFiles && !_isPreparingManifest && !_isSending;
        SelectFilesButton.IsEnabled = canEditSelection;
        ClearSelectionButton.IsEnabled = canEditSelection && _selectedFiles.Count > 0;
        ManualPeerInput.IsEnabled = !_isSending;
        ValidatePeerButton.IsEnabled = !_isSending;

        var canPrepare = !_isPreparingManifest && !_isSending && _validatedManualPeerEndpoint is not null && _selectedFiles.Count > 0;
        PrepareTransferDraftButton.IsEnabled = canPrepare;
        PrepareManifestButton.IsEnabled = canPrepare;
        SendButton.IsEnabled = CanSendPreparedTransfer();

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
        var sendStatus = _isSending
            ? "Send is running."
            : CanSendPreparedTransfer()
                ? "Send prepared transfer is ready."
                : "Send prepared transfer is disabled until peer, selected files, and prepared manifest match.";
        ReadinessTransferText.Text =
            $"Transfer: {draftStatus} {manifestStatus} {sendStatus}";
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
            "Send uses the validated peer and prepared manifest only after explicit click. Receiver confirmation is required before writing.";
    }

    private OutgoingTransferDraft CreateOutgoingDraft(ManualPeerEndpoint endpoint)
    {
        var draftFiles = _selectedFiles.Select(file =>
            OutgoingTransferDraftFile.Create(file.Name, file.SizeBytes));

        return OutgoingTransferDraft.Create(endpoint.Display, draftFiles);
    }

    private bool CanSendPreparedTransfer() =>
        TryGetPreparedSendSnapshot(out _, out _, out _, out _);

    private bool TryGetPreparedSendSnapshot(
        out ManualPeerEndpoint endpoint,
        out PreparedOutgoingTransferManifest manifest,
        out SelectedFilePreview[] selectedSnapshot,
        out string notReadyReason)
    {
        endpoint = null!;
        manifest = null!;
        selectedSnapshot = [];
        notReadyReason = "Prepare a manifest before sending.";

        if (_isSelectingFiles)
        {
            notReadyReason = "File selection is running. Send is disabled.";
            return false;
        }

        if (_isPreparingManifest)
        {
            notReadyReason = "Manifest preparation is running. Send is disabled.";
            return false;
        }

        if (_isSending)
        {
            notReadyReason = "A send is already running.";
            return false;
        }

        if (_validatedManualPeerEndpoint is not { } currentEndpoint)
        {
            notReadyReason = "Validate a peer before sending.";
            return false;
        }

        if (_preparedManifest is not { } currentManifest)
        {
            notReadyReason = "Prepare a manifest before sending.";
            return false;
        }

        selectedSnapshot = _selectedFiles.ToArray();
        if (selectedSnapshot.Length == 0)
        {
            notReadyReason = "Select files before sending.";
            return false;
        }

        if (!StringComparer.Ordinal.Equals(currentManifest.TargetPeerDisplay, currentEndpoint.Display) ||
            currentManifest.Files.Count != selectedSnapshot.Length)
        {
            notReadyReason = "Prepared manifest is stale. Prepare it again before sending.";
            return false;
        }

        for (var index = 0; index < selectedSnapshot.Length; index++)
        {
            var selectedFile = selectedSnapshot[index];
            var manifestFile = currentManifest.Files[index];

            if (manifestFile.SizeBytes is null || selectedFile.SizeBytes is null)
            {
                notReadyReason = "Selected file sizes must be available before sending.";
                return false;
            }

            if (!StringComparer.Ordinal.Equals(manifestFile.FileName, selectedFile.Name) ||
                manifestFile.SizeBytes != selectedFile.SizeBytes)
            {
                notReadyReason = "Selected files no longer match the prepared manifest.";
                return false;
            }
        }

        endpoint = currentEndpoint;
        manifest = currentManifest;
        notReadyReason = string.Empty;
        return true;
    }

    private static IReadOnlyList<ManualPeerOutgoingTransferFile> CreateManualPeerOutgoingFiles(
        PreparedOutgoingTransferManifest manifest,
        IReadOnlyList<SelectedFilePreview> selectedFiles)
    {
        var outgoingFiles = new List<ManualPeerOutgoingTransferFile>(manifest.FileCount);

        for (var index = 0; index < manifest.Files.Count; index++)
        {
            var manifestFile = manifest.Files[index];
            var selectedFile = selectedFiles[index];
            var sizeBytes = checked((long)manifestFile.SizeBytes!.Value);
            var storageFile = selectedFile.StorageFile;

            outgoingFiles.Add(ManualPeerOutgoingTransferFile.Create(
                manifestFile.FileName,
                sizeBytes,
                manifestFile.Checksum,
                _ => OpenSelectedFileForSendAsync(storageFile)));
        }

        return outgoingFiles;
    }

    private static async Task<Stream> OpenSelectedFileForSendAsync(IStorageFile storageFile) =>
        await storageFile.OpenReadAsync();

    private static string GetSendResultText(ManualPeerTransferStatus status) =>
        status switch
        {
            ManualPeerTransferStatus.Completed => "Send completed.",
            ManualPeerTransferStatus.Rejected => "Send rejected by receiver. No files written.",
            ManualPeerTransferStatus.InvalidRequest => "Send rejected as invalid. No files written.",
            ManualPeerTransferStatus.DestinationAlreadyExists => "Send stopped: receiver destination already exists. No overwrite.",
            ManualPeerTransferStatus.SizeMismatch => "Send failed: receiver size verification failed.",
            ManualPeerTransferStatus.ChecksumMismatch => "Send failed: receiver checksum verification failed.",
            ManualPeerTransferStatus.WriteFailed => "Send failed while receiver wrote files.",
            ManualPeerTransferStatus.ProtocolError => "Send failed because the transfer connection ended unexpectedly.",
            _ => "Send finished with an unknown status.",
        };

    private static string GetReceiveResultText(ManualPeerTransferStatus status) =>
        status switch
        {
            ManualPeerTransferStatus.Completed => "Receive completed. Files were written after Accept and verification.",
            ManualPeerTransferStatus.Rejected => "Receive rejected. No files written.",
            ManualPeerTransferStatus.InvalidRequest => "Incoming request was invalid. No files written.",
            ManualPeerTransferStatus.DestinationAlreadyExists => "Receive stopped: destination already exists. No overwrite.",
            ManualPeerTransferStatus.SizeMismatch => "Receive failed: size verification failed. No final files written.",
            ManualPeerTransferStatus.ChecksumMismatch => "Receive failed: checksum verification failed. No final files written.",
            ManualPeerTransferStatus.WriteFailed => "Receive failed while writing files.",
            ManualPeerTransferStatus.ProtocolError => "Receive failed because the transfer connection ended unexpectedly.",
            _ => "Receive finished with an unknown status.",
        };

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

    private void UpdateReceiverControls()
    {
        var receiverTaskRunning = _receiverTask is not null && !_receiverTask.IsCompleted;
        ReceiveFolderStatusText.Text = _receiveDirectoryPath is null
            ? "No receive folder selected"
            : $"{_receiveFolderDisplayText} selected";

        SelectReceiveFolderButton.IsEnabled = !_isReceiverRunning;
        ReceivePortInput.IsEnabled = !_isReceiverRunning;
        StartReceiverButton.IsEnabled = !_isReceiverRunning && _receiveDirectoryPath is not null;
        StopReceiverButton.IsEnabled = _isReceiverRunning || receiverTaskRunning;

        var hasPendingDecision = _pendingIncomingDecision is not null;
        AcceptIncomingButton.IsEnabled = hasPendingDecision;
        RejectIncomingButton.IsEnabled = hasPendingDecision;
    }

    private bool TryParseReceivePort(out int port)
    {
        port = 0;
        return int.TryParse(ReceivePortInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out port) &&
            port is >= 1 and <= 65535;
    }

    private static string GetShortRequestId(Guid requestId) => requestId.ToString("N")[..8];

    private static string GetIncomingFileListText(ManualPeerIncomingTransferRequest request)
    {
        var shownFiles = request.Files
            .Take(5)
            .Select(file => $"{file.FileName} ({FormatByteCount(file.SizeBytes)})")
            .ToArray();
        var fileList = string.Join(", ", shownFiles);
        var remaining = request.FileCount - shownFiles.Length;

        return remaining > 0
            ? $"{fileList}, and {remaining} more"
            : fileList;
    }

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

    private static string GetDisplayFolderName(string folderName)
    {
        var displayName = string.IsNullOrWhiteSpace(folderName)
            ? "Receive folder"
            : folderName.Trim().TrimEnd('/', '\\');
        var slashIndex = displayName.LastIndexOf('/');
        var backslashIndex = displayName.LastIndexOf('\\');
        var separatorIndex = Math.Max(slashIndex, backslashIndex);

        if (separatorIndex >= 0)
        {
            displayName = displayName[(separatorIndex + 1)..];
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Receive folder";
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

    private static void DisposePickedFolders(IEnumerable<IStorageFolder> folders)
    {
        foreach (var folder in folders)
        {
            try
            {
                folder.Dispose();
            }
            catch (Exception)
            {
                // Cleanup failure must not start a receiver or expose local paths.
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
        _sendCts?.Cancel();
        _receiverCts?.Cancel();
        _pendingIncomingDecision?.TrySetResult(TransferDecision.Reject("Window closed."));
        _receiver?.Dispose();
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
