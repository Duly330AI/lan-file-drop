using System.Text;
using LanFileDrop.Core.Checksums;
using LanFileDrop.Core.Tests.TestSupport;

namespace LanFileDrop.Core.Tests.Checksums;

public class ChecksumCalculatorTests
{
    // FIPS 180-4 Appendix B.1 known-answer test vector for SHA-256("abc").
    private const string KnownSha256OfAbc = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void ComputeSha256_FromBytes_MatchesKnownVector()
    {
        var bytes = Encoding.UTF8.GetBytes("abc");

        var checksum = ChecksumCalculator.ComputeSha256(bytes);

        Assert.Equal(ChecksumAlgorithm.Sha256, checksum.Algorithm);
        Assert.Equal(KnownSha256OfAbc, checksum.Value);
    }

    [Fact]
    public void ComputeSha256_FromStream_MatchesKnownVector()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));

        var checksum = ChecksumCalculator.ComputeSha256(stream);

        Assert.Equal(KnownSha256OfAbc, checksum.Value);
    }

    [Fact]
    public void ComputeSha256_FromSeekableStream_RestoresOriginalPosition()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        stream.Position = 0;

        ChecksumCalculator.ComputeSha256(stream);

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void ComputeSha256_FromStream_LeavesStreamOpenAndReadable()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));

        ChecksumCalculator.ComputeSha256(stream);

        Assert.True(stream.CanRead);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        Assert.Equal("abc", reader.ReadToEnd());
    }

    [Fact]
    public void ComputeSha256_FromNonSeekableStream_ComputesHashAndDoesNotAttemptToRestorePosition()
    {
        using var inner = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        using var nonSeekableStream = new NonSeekableStream(inner);

        // If ComputeSha256 tried to read/set Position on a non-seekable stream, this would
        // throw NotSupportedException instead of returning the checksum.
        var checksum = ChecksumCalculator.ComputeSha256(nonSeekableStream);

        Assert.Equal(KnownSha256OfAbc, checksum.Value);
        Assert.False(nonSeekableStream.CanSeek);
    }

    [Fact]
    public void ComputeSha256_FromNullBytes_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ChecksumCalculator.ComputeSha256((byte[])null!));
    }

    [Fact]
    public void ComputeSha256_FromNullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ChecksumCalculator.ComputeSha256((Stream)null!));
    }
}
