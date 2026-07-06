using System.Buffers.Binary;

namespace LanFileDrop.Networking.Protocol;

// The length prefix is written as a 4-byte big-endian (network byte order) unsigned integer
// so the wire format is deterministic and host-architecture independent.
internal static class FrameIO
{
    public const int MaxHeaderBytes = 64 * 1024;
    public const int MaxPayloadBytes = 64 * 1024 * 1024;

    public static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var lengthPrefix = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, (uint)payload.Length);
        await stream.WriteAsync(lengthPrefix, cancellationToken).ConfigureAwait(false);

        if (payload.Length > 0)
        {
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<byte[]> ReadFrameAsync(Stream stream, int maxLength, CancellationToken cancellationToken)
    {
        var lengthBytes = await ReadExactAsync(stream, sizeof(uint), cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);

        if (length > (uint)maxLength)
        {
            throw new InvalidOperationException($"Frame length {length} is outside the allowed range 0..{maxLength}.");
        }

        return length == 0
            ? Array.Empty<byte>()
            : await ReadExactAsync(stream, (int)length, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;

        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                throw new EndOfStreamException("Connection closed before the expected data was fully received.");
            }

            offset += read;
        }

        return buffer;
    }
}
