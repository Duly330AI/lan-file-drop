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
}
