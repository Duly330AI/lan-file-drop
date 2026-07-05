namespace LanFileDrop.Core.Models;

public sealed record TransferDecision
{
    public bool Accepted { get; }
    public string? Reason { get; }

    private TransferDecision(bool accepted, string? reason)
    {
        Accepted = accepted;
        Reason = reason;
    }

    public static TransferDecision Accept() => new(accepted: true, reason: null);

    public static TransferDecision Reject(string? reason = null) => new(accepted: false, reason);
}
