namespace LanFileDrop.Networking;

public enum ManualPeerTransferStatus
{
    Completed,
    Rejected,
    InvalidRequest,
    DestinationAlreadyExists,
    SizeMismatch,
    ChecksumMismatch,
    WriteFailed,
    ProtocolError,
}
