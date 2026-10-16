using System;
using System.IO;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Security;

/// <summary>
/// Security authorization checks for Smart Filters (#5684).
/// Enforces read boundaries on stored smart-filter parameters (e.g. displacement mapPath, color lookup lutPath)
/// to prevent unauthorized file access or directory traversal attacks.
/// </summary>
public static class SmartFilterSecurity
{
    /// <summary>
    /// Validates that a file path referenced in stored filter parameters is authorized for read access.
    /// </summary>
    /// <param name="filePath">Target file path referenced by the filter.</param>
    /// <param name="allowedDirectory">Optional root directory boundary (e.g. active workspace root).</param>
    /// <returns>True if access is authorized; otherwise throws an UnauthorizedAccessException or returns false.</returns>
    public static bool ValidateFilterPath(string? filePath, string? allowedDirectory = null, bool throwOnDenial = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            if (throwOnDenial) throw new ArgumentException("Filter file path cannot be empty.");
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(filePath);

            // Reject paths containing illegal characters
            if (filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                if (throwOnDenial) throw new UnauthorizedAccessException($"Path '{filePath}' contains invalid characters.");
                return false;
            }

            if (!string.IsNullOrEmpty(allowedDirectory))
            {
                string allowedRoot = Path.GetFullPath(allowedDirectory);
                if (!allowedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    allowedRoot += Path.DirectorySeparatorChar;
                }

                if (!fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && !string.Equals(fullPath, Path.TrimEndingDirectorySeparator(allowedRoot), StringComparison.OrdinalIgnoreCase))
                {
                    if (throwOnDenial)
                    {
                        throw new UnauthorizedAccessException($"Path '{filePath}' resolves outside the authorized boundary '{allowedDirectory}'.");
                    }
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            if (throwOnDenial)
            {
                throw new UnauthorizedAccessException($"Failed to validate filter path '{filePath}': {ex.Message}", ex);
            }
            return false;
        }
    }
}
