// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII.Exporters;

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Helper class for mapping file extensions to disk formats and selecting appropriate exporters.
/// </summary>
/// <remarks>
/// <para>
/// This class provides utilities for determining disk image formats from file paths,
/// creating the appropriate exporter for a given format, and checking export support.
/// </para>
/// <para>
/// <strong>Supported Formats:</strong><br/>
/// - .woz → <see cref="DiskFormat.Woz"/> (WozExporter)<br/>
/// - .nib → <see cref="DiskFormat.Nib"/> (NibExporter)<br/>
/// - .dsk → <see cref="DiskFormat.Dsk"/> (SectorExporter with DOS ordering)<br/>
/// - .do → <see cref="DiskFormat.Do"/> (SectorExporter with DOS ordering)<br/>
/// - .po → <see cref="DiskFormat.Po"/> (SectorExporter with ProDOS ordering)<br/>
/// </para>
/// </remarks>
public static class DiskFormatHelper
{
    /// <summary>
    /// Gets the disk format from a file extension.
    /// </summary>
    /// <param name="extension">File extension (with or without leading dot, case-insensitive).</param>
    /// <returns>The corresponding <see cref="DiskFormat"/>, or <see cref="DiskFormat.Unknown"/> if not recognized.</returns>
    public static DiskFormat GetFormatFromExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return DiskFormat.Unknown;
        }

        // Remove leading dot if present
        if (extension.StartsWith('.'))
        {
            extension = extension[1..];
        }

        return extension.ToLowerInvariant() switch
        {
            "woz" => DiskFormat.Woz,
            "nib" => DiskFormat.Nib,
            "dsk" => DiskFormat.Dsk,
            "do" => DiskFormat.Do,
            "po" => DiskFormat.Po,
            _ => DiskFormat.Unknown
        };
    }

    /// <summary>
    /// Gets the disk format from a file path.
    /// </summary>
    /// <param name="filePath">Full file path.</param>
    /// <returns>The corresponding <see cref="DiskFormat"/>, or <see cref="DiskFormat.Unknown"/> if not recognized.</returns>
    public static DiskFormat GetFormatFromPath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return DiskFormat.Unknown;
        }

        string extension = Path.GetExtension(filePath);
        return GetFormatFromExtension(extension);
    }

    /// <summary>
    /// Gets an exporter instance for the specified format.
    /// </summary>
    /// <param name="format">The disk format to export to.</param>
    /// <returns>An <see cref="IDiskImageExporter"/> instance for the format.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the format is <see cref="DiskFormat.Unknown"/> or <see cref="DiskFormat.Internal"/>.
    /// </exception>
    public static IDiskImageExporter GetExporterForFormat(DiskFormat format)
    {
        return format switch
        {
            DiskFormat.Woz => new WozExporter(),
            DiskFormat.Nib => new NibExporter(),
            DiskFormat.Dsk => new SectorExporter(DiskFormat.Dsk),
            DiskFormat.Do => new SectorExporter(DiskFormat.Do),
            DiskFormat.Po => new SectorExporter(DiskFormat.Po),
            DiskFormat.Unknown => throw new ArgumentException("Cannot create exporter for Unknown format.", nameof(format)),
            DiskFormat.Internal => throw new ArgumentException("Internal format cannot be directly exported. Use a specific external format.", nameof(format)),
            _ => throw new ArgumentException($"Unsupported disk format: {format}", nameof(format))
        };
    }

    /// <summary>
    /// Gets an exporter instance for the file path's format.
    /// </summary>
    /// <param name="filePath">Full file path.</param>
    /// <returns>An <see cref="IDiskImageExporter"/> instance for the format.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the file extension is not recognized or unsupported.
    /// </exception>
    public static IDiskImageExporter GetExporterForPath(string filePath)
    {
        DiskFormat format = GetFormatFromPath(filePath);
        return GetExporterForFormat(format);
    }

    /// <summary>
    /// Checks whether a format supports export operations.
    /// </summary>
    /// <param name="format">The disk format to check.</param>
    /// <returns>True if the format can be exported to, false otherwise.</returns>
    public static bool IsExportSupported(DiskFormat format)
    {
        return format switch
        {
            DiskFormat.Woz => true,
            DiskFormat.Nib => true,
            DiskFormat.Dsk => true,
            DiskFormat.Do => true,
            DiskFormat.Po => true,
            _ => false
        };
    }
}
