namespace LanFileDrop.Core.Models;

public sealed record OutgoingTransferDraft
{
    public string TargetPeerDisplay { get; }
    public IReadOnlyList<OutgoingTransferDraftFile> Files { get; }
    public DateTimeOffset CreatedUtc { get; }

    public int FileCount => Files.Count;

    public ulong TotalKnownSizeBytes => Files
        .Where(file => file.SizeBytes is not null)
        .Aggregate(0UL, (total, file) => total + file.SizeBytes!.Value);

    public bool HasUnknownSizes => Files.Any(file => file.SizeBytes is null);

    private OutgoingTransferDraft(
        string targetPeerDisplay,
        IReadOnlyList<OutgoingTransferDraftFile> files,
        DateTimeOffset createdUtc)
    {
        TargetPeerDisplay = targetPeerDisplay;
        Files = files;
        CreatedUtc = createdUtc;
    }

    public static OutgoingTransferDraft Create(
        string targetPeerDisplay,
        IEnumerable<OutgoingTransferDraftFile> files,
        DateTimeOffset? createdUtc = null)
    {
        var safeTargetPeerDisplay = targetPeerDisplay?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(safeTargetPeerDisplay))
        {
            throw new ArgumentException("Target peer display must not be empty.", nameof(targetPeerDisplay));
        }

        if (safeTargetPeerDisplay.Any(character => character is '\r' or '\n'))
        {
            throw new ArgumentException("Target peer display must be a single line.", nameof(targetPeerDisplay));
        }

        ArgumentNullException.ThrowIfNull(files);

        var fileList = files.Select(file => file ?? throw new ArgumentException(
            "Draft files must not contain null entries.",
            nameof(files))).ToArray();

        if (fileList.Length == 0)
        {
            throw new ArgumentException("An outgoing transfer draft must contain at least one file.", nameof(files));
        }

        return new OutgoingTransferDraft(
            safeTargetPeerDisplay,
            Array.AsReadOnly(fileList),
            createdUtc ?? DateTimeOffset.UtcNow);
    }
}
