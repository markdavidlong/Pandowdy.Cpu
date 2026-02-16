// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII.Importers;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII.Providers;

/// <summary>
/// Factory for creating disk image providers based on file format detection.
/// </summary>
/// <remarks>
/// This factory examines the file extension to determine which importer to use,
/// then wraps the imported disk image in a <see cref="UnifiedDiskImageProvider"/>.
/// Supported formats:
/// <list type="bullet">
/// <item><strong>.nib</strong> - Raw GCR nibble format (232,960 bytes)</item>
/// <item><strong>.woz</strong> - WOZ format with timing information (most accurate)</item>
/// <item><strong>.dsk, .do, .po</strong> - Sector-based formats (143,360 bytes)</item>
/// <item><strong>.2mg, .2img</strong> - 2IMG wrapper format (with header)</item>
/// </list>
/// All formats are imported to a unified <see cref="InternalDiskImage"/> representation
/// for consistent emulation behavior.
/// </remarks>
public class DiskImageFactory : IDiskImageFactory
{
    // TEMPORARY: Set to true to use legacy SectorDiskImageProvider for .do/.dsk files
    // This helps isolate whether the bug is in SectorImporter or UnifiedDiskImageProvider
    private const bool UseLegacySectorProvider = false;

    /// <summary>
    /// Creates an appropriate disk image provider for the given file.
    /// </summary>
    /// <param name="filePath">Path to disk image file.</param>
    /// <returns>A provider implementation for the detected format.</returns>
    /// <exception cref="ArgumentNullException">Thrown if filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
    /// <exception cref="NotSupportedException">Thrown if format is not supported.</exception>
    public IDiskImageProvider CreateProvider(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Disk image file not found: {filePath}", filePath);
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        System.Diagnostics.Debug.WriteLine($"DiskImageFactory: Creating provider for '{filePath}' (extension: {extension})");

        //// TEMPORARY: Use legacy provider for sector formats to isolate the bug
        //if (UseLegacySectorProvider && extension is ".dsk" or ".do" or ".po")
        //{
        //    System.Diagnostics.Debug.WriteLine($"DiskImageFactory: Using LEGACY SectorDiskImageProvider for {extension}");
        //    return new SectorDiskImageProvider(filePath);
        //}

        // Select appropriate importer based on file extension
        IDiskImageImporter importer = extension switch
        {
            ".nib" => new NibImporter(),
            ".woz" => new WozImporter(),
            ".dsk" or ".do" or ".po" or ".2mg" or ".2img" => new SectorImporter(),
            _ => throw new NotSupportedException(
                $"Unsupported disk image format: {extension}\n" +
                "Supported formats: .nib, .woz, .dsk, .do, .po, .2mg, .2img")
        };

        System.Diagnostics.Debug.WriteLine($"DiskImageFactory: Using {importer.GetType().Name} for {extension}");

        // Import disk image to internal format
        System.Diagnostics.Debug.Write($"Calling importer with {filePath}");
        InternalDiskImage diskImage = importer.Import(filePath);

        System.Diagnostics.Debug.WriteLine($"DiskImageFactory: Imported {diskImage.PhysicalTrackCount} physical tracks ({diskImage.QuarterTrackCount} quarter-tracks), format={diskImage.OriginalFormat}");

        // Wrap with unified provider
        var provider = new UnifiedDiskImageProvider(diskImage);
        System.Diagnostics.Debug.WriteLine($"DiskImageFactory: Created UnifiedDiskImageProvider for '{filePath}'");
        return provider;
    }

    /// <summary>
    /// Checks if a file format is supported based on file extension.
    /// </summary>
    /// <param name="filePath">Path to check.</param>
    /// <returns>True if the format is supported.</returns>
    public bool IsFormatSupported(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".nib" or ".woz" or ".dsk" or ".do" or ".po" or ".2mg" or ".2img";
    }
}
