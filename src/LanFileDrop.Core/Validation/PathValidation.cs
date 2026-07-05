namespace LanFileDrop.Core.Validation;

public static class PathValidation
{
    public static void EnsureSafeFileName(string fileName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must not be empty.", paramName);
        }

        if (fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new ArgumentException("File name must not contain path separators.", paramName);
        }

        if (fileName is "." or "..")
        {
            throw new ArgumentException("File name must not be a path traversal segment.", paramName);
        }
    }

    public static void EnsureSafeRelativePath(string relativePath, string paramName)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path must not be empty when provided.", paramName);
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Relative path must not be rooted/absolute.", paramName);
        }

        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => segment == ".."))
        {
            throw new ArgumentException("Relative path must not contain '..' segments.", paramName);
        }
    }
}
