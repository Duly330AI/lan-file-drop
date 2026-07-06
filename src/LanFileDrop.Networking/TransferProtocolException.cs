namespace LanFileDrop.Networking;

public sealed class TransferProtocolException : Exception
{
    public TransferProtocolException(string message)
        : base(message)
    {
    }

    public TransferProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
