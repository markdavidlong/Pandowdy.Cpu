// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Creates a temporary file that is automatically deleted when disposed.
/// Useful for test output files to ensure proper cleanup.
/// </summary>
public sealed class TempOutputFile : IDisposable
{
    private readonly string _filePath;
    private bool _disposed;

    /// <summary>
    /// Gets the path to the temporary file.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Creates a temporary output file with optional extension.
    /// </summary>
    /// <param name="extension">Optional file extension (e.g., ".dsk", ".nib"). If null, uses .tmp</param>
    public TempOutputFile(string? extension = null)
    {
        _filePath = Path.GetTempFileName();
        
        // Replace extension if specified
        if (!string.IsNullOrEmpty(extension))
        {
            string tempWithExtension = Path.ChangeExtension(_filePath, extension);
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath); // Delete the .tmp file
            }
            _filePath = tempWithExtension;
        }
    }

    /// <summary>
    /// Implicit conversion to string for convenience.
    /// </summary>
    public static implicit operator string(TempOutputFile tempFile) => tempFile.FilePath;

    /// <summary>
    /// Deletes the temporary file.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (IOException)
        {
            // Ignore errors during cleanup - file might be locked or already deleted
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore permission errors during cleanup
        }

        _disposed = true;
    }
}
