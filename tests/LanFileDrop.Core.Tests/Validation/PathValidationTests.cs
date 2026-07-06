using LanFileDrop.Core.Validation;

namespace LanFileDrop.Core.Tests.Validation;

public class PathValidationTests
{
    [Theory]
    [InlineData(@"C:\Windows\file.txt")]
    [InlineData(@"\\server\share\file.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("../file.txt")]
    [InlineData("sub/../../file.txt")]
    public void EnsureSafeRelativePath_WithUnsafePath_Throws(string relativePath)
    {
        Assert.Throws<ArgumentException>(() => PathValidation.EnsureSafeRelativePath(relativePath, "relativePath"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureSafeRelativePath_WithEmptyPath_Throws(string relativePath)
    {
        Assert.Throws<ArgumentException>(() => PathValidation.EnsureSafeRelativePath(relativePath, "relativePath"));
    }

    [Theory]
    [InlineData("sub/folder")]
    [InlineData("sub/folder/file-name_1.txt")]
    public void EnsureSafeRelativePath_WithSafePath_DoesNotThrow(string relativePath)
    {
        var exception = Record.Exception(() => PathValidation.EnsureSafeRelativePath(relativePath, "relativePath"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("COM9")]
    [InlineData("LPT1")]
    [InlineData("LPT9")]
    [InlineData("CON.txt")]
    [InlineData("nul.bin")]
    [InlineData("COM1.dat")]
    [InlineData("LPT9.log")]
    [InlineData("NUL ")]
    public void EnsureSafeFileName_WithReservedDeviceName_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => PathValidation.EnsureSafeFileName(fileName, "fileName"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("sub/file.txt")]
    [InlineData(@"sub\file.txt")]
    public void EnsureSafeFileName_WithUnsafeName_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => PathValidation.EnsureSafeFileName(fileName, "fileName"));
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("console.log")]  // starts with CON but the base name is not reserved
    [InlineData("COM10.dat")]    // outside COM1-COM9
    [InlineData("COM0.dat")]
    [InlineData("LPT0")]
    [InlineData("report-CON.txt")]
    [InlineData("nulls.bin")]
    public void EnsureSafeFileName_WithSafeName_DoesNotThrow(string fileName)
    {
        var exception = Record.Exception(() => PathValidation.EnsureSafeFileName(fileName, "fileName"));

        Assert.Null(exception);
    }
}
