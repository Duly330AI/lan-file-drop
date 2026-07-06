namespace LanFileDrop.Core.Validation;

public static class PathValidation
{
    // Windows reserved device names. These are rejected on every OS so a manifest is
    // accepted or refused identically regardless of where the code runs. Matching is
    // case-insensitive and also covers names carrying an extension (e.g. CON.txt).
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

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

        if (IsReservedDeviceName(fileName))
        {
            throw new ArgumentException("File name must not be a reserved device name.", paramName);
        }
    }

    // Windows ignores a trailing extension and trailing spaces when resolving device names,
    // so "CON", "con.txt" and "NUL " all collapse to a reserved base name.
    private static bool IsReservedDeviceName(string fileName)
    {
        var dotIndex = fileName.IndexOf('.');
        var baseName = (dotIndex >= 0 ? fileName[..dotIndex] : fileName).TrimEnd(' ');
        return ReservedDeviceNames.Contains(baseName);
    }

    public static void EnsureSafeRelativePath(string relativePath, string paramName)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path must not be empty when provided.", paramName);
        }

        if (IsRooted(relativePath))
        {
            throw new ArgumentException("Relative path must not be rooted/absolute.", paramName);
        }

        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => segment == ".."))
        {
            throw new ArgumentException("Relative path must not contain '..' segments.", paramName);
        }
    }

    // Deliberately does not use Path.IsPathRooted: its rooted/UNC detection depends on the
    // host OS, and manifests must be rejected the same way regardless of where this runs.
    private static bool IsRooted(string path)
    {
        if (path.StartsWith('/') || path.StartsWith('\\'))
        {
            return true;
        }

        return path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }
}
