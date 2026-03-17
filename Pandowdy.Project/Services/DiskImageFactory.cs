// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Exporters;
using Pandowdy.EmuCore.DiskII.Importers;

namespace Pandowdy.Project.Services;

/// <summary>
/// Factory for creating disk image importers and exporters based on format.
/// </summary>
/// <remarks>
/// <para>
/// This service centralizes the logic for selecting the appropriate importer or
/// exporter for a given disk format. It provides a single point of configuration
/// for format-to-implementation mappings.
/// </para>
/// <para>
/// <strong>Importers:</strong> Convert external disk formats to <see cref="InternalDiskImage"/>.<br/>
/// <strong>Exporters:</strong> Convert <see cref="InternalDiskImage"/> to external disk formats.
/// </para>
/// </remarks>
internal static class DiskImageFactory
{
    /// <summary>
    /// Creates an importer for the specified disk format.
    /// </summary>
    /// <param name="format">The disk format to import.</param>
    /// <returns>An <see cref="IDiskImageImporter"/> instance for the format.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the format is <see cref="DiskFormat.Unknown"/> or <see cref="DiskFormat.Internal"/>,
    /// or when no importer is available for the format.
    /// </exception>
    public static IDiskImageImporter GetImporter(DiskFormat format)
    {
        return format switch
        {
            DiskFormat.Woz => new WozImporter(),
            DiskFormat.Nib => new NibImporter(),
            // Note: DSK, DO, and PO all use sector-based imports
            // They will be added when SectorImporter is implemented
            DiskFormat.Dsk or DiskFormat.Do or DiskFormat.Po =>
                throw new NotImplementedException($"Sector-based import for {format} is not yet implemented"),
            DiskFormat.Unknown =>
                throw new ArgumentException("Cannot create importer for Unknown disk format", nameof(format)),
            DiskFormat.Internal =>
                throw new ArgumentException("Cannot import Internal format (already in internal format)", nameof(format)),
            _ =>
                throw new ArgumentException($"Unsupported disk format for import: {format}", nameof(format))
        };
    }

    /// <summary>
    /// Creates an exporter for the specified disk format.
    /// </summary>
    /// <param name="format">The disk format to export to.</param>
    /// <returns>An <see cref="IDiskImageExporter"/> instance for the format.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the format is <see cref="DiskFormat.Unknown"/> or <see cref="DiskFormat.Internal"/>.
    /// </exception>
    public static IDiskImageExporter GetExporter(DiskFormat format)
    {
        return format switch
        {
            DiskFormat.Woz => new WozExporter(),
            DiskFormat.Nib => new NibExporter(),
            DiskFormat.Dsk or DiskFormat.Do or DiskFormat.Po => new SectorExporter(format),
            DiskFormat.Unknown =>
                throw new ArgumentException("Cannot create exporter for Unknown disk format", nameof(format)),
            DiskFormat.Internal =>
                throw new ArgumentException("Cannot export to Internal format (use PIDI blob serialization)", nameof(format)),
            _ =>
                throw new ArgumentException($"Unsupported disk format for export: {format}", nameof(format))
        };
    }
}
