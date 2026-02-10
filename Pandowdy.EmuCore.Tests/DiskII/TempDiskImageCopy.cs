// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Creates a temporary copy of a disk image file for use in tests.
/// Handles file locking issues with retry logic and automatically cleans up on disposal.
/// </summary>
public sealed class TempDiskImageCopy : IDisposable
{
    private static readonly Random _random = new();
    private readonly string _tempFilePath;
    private bool _disposed;

    /// <summary>
    /// Gets the path to the temporary disk image file.
    /// </summary>
    public string FilePath => _tempFilePath;

    /// <summary>
    /// Creates a temporary copy of the specified disk image file.
    /// </summary>
    /// <param name="sourceFilePath">Path to the source disk image file.</param>
    /// <param name="maxRetries">Maximum number of retry attempts if file is locked (default: 10).</param>
    /// <param name="minDelayMs">Minimum delay in milliseconds between retries (default: 1000).</param>
    /// <param name="maxDelayMs">Maximum delay in milliseconds between retries (default: 3000).</param>
    /// <exception cref="IOException">Thrown if the file cannot be copied after max retries.</exception>
    public TempDiskImageCopy(string sourceFilePath, int maxRetries = 10, int minDelayMs = 1000, int maxDelayMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentNullException(nameof(sourceFilePath));
        }

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Source disk image not found: {sourceFilePath}", sourceFilePath);
        }

        // Create temp file with same extension as source
        string extension = Path.GetExtension(sourceFilePath);
        _tempFilePath = Path.GetTempFileName();
        
        // Replace the .tmp extension with the actual disk image extension
        if (!string.IsNullOrEmpty(extension))
        {
            string tempWithExtension = Path.ChangeExtension(_tempFilePath, extension);
            File.Move(_tempFilePath, tempWithExtension);
            _tempFilePath = tempWithExtension;
        }

        // Copy with retry logic
        CopyFileWithRetry(sourceFilePath, _tempFilePath, maxRetries, minDelayMs, maxDelayMs);
    }

    /// <summary>
    /// Copies a file with retry logic to handle file locking issues.
    /// </summary>
    private static void CopyFileWithRetry(string source, string destination, int maxRetries, int minDelayMs, int maxDelayMs)
    {
        int attempt = 0;
        
        while (attempt < maxRetries)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return; // Success!
            }
            catch (IOException ex) when (attempt < maxRetries - 1)
            {
                // File is likely locked by another process/test
                attempt++;
                
                // Random delay to avoid thundering herd problem
                int delayMs = _random.Next(minDelayMs, maxDelayMs + 1);
                Thread.Sleep(delayMs);
                
                // If this is the last retry, rethrow
                if (attempt >= maxRetries - 1)
                {
                    throw new IOException(
                        $"Failed to copy disk image after {maxRetries} attempts. Source: {source}", ex);
                }
            }
        }
    }

    /// <summary>
    /// Creates a temporary copy of a disk image file. Returns null if the source file doesn't exist.
    /// This is useful for tests that should skip if test images are not available.
    /// </summary>
    /// <param name="sourceFilePath">Path to the source disk image file.</param>
    /// <param name="maxRetries">Maximum number of retry attempts if file is locked (default: 10).</param>
    /// <param name="minDelayMs">Minimum delay in milliseconds between retries (default: 1000).</param>
    /// <param name="maxDelayMs">Maximum delay in milliseconds between retries (default: 3000).</param>
    /// <returns>A TempDiskImageCopy instance, or null if the source file doesn't exist.</returns>
    public static TempDiskImageCopy? TryCreate(string sourceFilePath, int maxRetries = 10, int minDelayMs = 1000, int maxDelayMs = 3000)
    {
        if (!File.Exists(sourceFilePath))
        {
            return null;
        }

        return new TempDiskImageCopy(sourceFilePath, maxRetries, minDelayMs, maxDelayMs);
    }

    /// <summary>
    /// Deletes the temporary disk image file.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }
        catch (IOException)
        {
            // Ignore errors during cleanup
        }

        _disposed = true;
    }
}
