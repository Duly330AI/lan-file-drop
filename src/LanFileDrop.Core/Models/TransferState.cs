namespace LanFileDrop.Core.Models;

public enum TransferState
{
    Pending,
    Accepted,
    Rejected,
    InProgress,
    Completed,
    Failed,
    Cancelled,
}
