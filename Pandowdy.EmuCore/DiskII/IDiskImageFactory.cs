// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Factory for creating disk image providers based on file format detection.
/// </summary>
/// <remarks>
/// This factory examines file extensions and/or file headers to determine
/// the appropriate <see cref="IDiskImageProvider"/> implementation for a given disk image.
/// </remarks>
public interface IDiskImageFactory
{
    /// <summary>
    /// Creates an appropriate disk image provider for the given file.
    /// </summary>
    /// <param name="filePath">Path to disk image file.</param>
    /// <returns>A provider implementation for the detected format.</returns>
    /// <exception cref="ArgumentNullException">Thrown if filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
    /// <exception cref="NotSupportedException">Thrown if format is not supported.</exception>
    IDiskImageProvider CreateProvider(string filePath);

    /// <summary>
    /// Checks if a file format is supported based on file extension.
    /// </summary>
    /// <param name="filePath">Path to check.</param>
    /// <returns>True if the format is supported.</returns>
    bool IsFormatSupported(string filePath);
}
