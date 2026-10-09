namespace SRT.Complaint.Validation;

/// <summary>
/// Resolves a stored attachment's relative path against the configured upload root.
/// StoredPath is always server-generated (GUID filename, optionally prefixed with a
/// reference-number folder) — never derived from user input — but this still guards
/// against a future bug ever producing a path that escapes the upload root.
/// </summary>
public static class FileStorage
{
    /// <summary>Returns the resolved absolute path if it stays within <paramref name="root"/>, otherwise null.</summary>
    public static string? ResolveWithinRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
            fullRoot += Path.DirectorySeparatorChar;

        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        return fullPath.StartsWith(fullRoot, StringComparison.Ordinal) ? fullPath : null;
    }
}
