// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using CommonUtil;
using DiskArc.Disk;

namespace Pandowdy.EmuCore.DiskII.Importers;

/// <summary>
/// Imports .woz format disk images to internal format.
/// </summary>
/// <remarks>
/// <para>
/// <strong>.WOZ Format:</strong><br/>
/// The .woz format stores raw flux transitions with timing information from Apple II 5.25"
/// floppy disks. It's the most accurate disk format, supporting variable-length tracks,
/// non-byte-aligned nibble data, quarter tracks, and copy protection schemes.
/// </para>
/// <para>
/// <strong>Import Process:</strong><br/>
/// Uses CiderPress2's Woz class to parse the WOZ file and extract CircularBitBuffer data
/// for each track. WOZ files can store quarter-track data, but for the internal format
/// we only import the main tracks (quarter position 0 of each track).
/// </para>
/// <para>
/// <strong>Timing Information:</strong><br/>
/// WOZ files contain optimal bit timing metadata which is preserved in the internal format.
/// This allows accurate emulation of non-standard disk speeds used by copy protection.
/// </para>
/// </remarks>
public class WozImporter : IDiskImageImporter
{
    private const int MaxTracks = 35;

    /// <summary>
    /// Supported file extensions for WOZ format.
    /// </summary>
    public IReadOnlyList<string> SupportedExtensions { get; } = [".woz"];

    /// <summary>
    /// Import a .woz disk image file to internal format.
    /// </summary>
    /// <param name="filePath">Path to the .woz file.</param>
    /// <returns>Internal disk image representation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
    /// <exception cref="InvalidDataException">Thrown if file format is invalid.</exception>
    public InternalDiskImage Import(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Disk image file not found: {filePath}", filePath);
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ImportFromStream(stream, filePath);
    }

    /// <summary>
    /// Import from a stream (for embedded disk images).
    /// </summary>
    /// <param name="stream">Stream containing .woz disk image data.</param>
    /// <param name="format">Format hint (should be DiskFormat.Woz).</param>
    /// <returns>Internal disk image representation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if stream is null.</exception>
    /// <exception cref="InvalidDataException">Thrown if stream format is invalid.</exception>
    public InternalDiskImage Import(Stream stream, DiskFormat format)
    {
 
        ArgumentNullException.ThrowIfNull(stream);

        if (format != DiskFormat.Woz)
        {
            throw new ArgumentException($"WozImporter can only import WOZ format, got {format}", nameof(format));
        }

        return ImportFromStream(stream, sourcePath: null);
    }

    /// <summary>
    /// Internal import implementation shared by file and stream imports.
    /// </summary>
    private  static InternalDiskImage ImportFromStream(Stream stream, string? sourcePath)
    {
        // Open WOZ file using CiderPress2 and ensure proper disposal
        // Provide a no-op MessageLog to avoid NullReferenceException in Woz.Dispose
        Woz wozImage;
        try
        {
            wozImage = Woz.OpenDisk(stream, new AppHook(new NullMessageLog()));
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Failed to open WOZ file: {ex.Message}", ex);
        }

        try
        {
            // Verify it's a 5.25" disk
            if (wozImage.DiskKind != DiskArc.Defs.MediaKind.GCR_525)
            {
                throw new InvalidDataException(
                    $"Only 5.25\" GCR disks are supported. Found: {wozImage.DiskKind}");
            }

            // Extract all quarter-track data from WOZ file
            // WOZ format supports quarter-track resolution for copy-protected disks
            int quarterTrackCount = InternalDiskImage.CalculateQuarterTrackCount(MaxTracks);
            var quarterTracks = new CircularBitBuffer?[quarterTrackCount];
            var quarterTrackBitCounts = new int[quarterTrackCount];
            int tracksFound = 0;

            // Iterate through all quarter-track positions
            for (int track = 0; track < MaxTracks; track++)
            {
                // For each physical track, check all quarter positions (0, 1, 2, 3)
                // But the last track (34) has no quarter positions beyond it
                int maxQuarter = (track < MaxTracks - 1) ? 4 : 1;

                for (int quarter = 0; quarter < maxQuarter; quarter++)
                {
                    int quarterIndex = InternalDiskImage.TrackAndQuarterToIndex(track, quarter);

                    // Try to get this quarter-track from the WOZ image
                    if (wozImage.GetTrackBits((uint)track, (uint)quarter, out CircularBitBuffer? cbb) && cbb != null)
                    {
                        // DiskArc returns read-only buffers; copy into writable buffers
                        // so the emulator can write to disk
                        int bitCount = cbb.BitCount;
                        int byteCount = (bitCount + 7) / 8;
                        byte[] trackData = new byte[byteCount];

                        cbb.BitPosition = 0;
                        for (int i = 0; i < byteCount; i++)
                        {
                            trackData[i] = cbb.ReadOctet();
                        }

                        quarterTracks[quarterIndex] = new CircularBitBuffer(
                            trackData,
                            byteOffset: 0,
                            bitOffset: 0,
                            bitCount: bitCount,
                            new GroupBool(),
                            isReadOnly: false
                        );
                        quarterTrackBitCounts[quarterIndex] = bitCount;
                        tracksFound++;
                    }
                    // If no data for this quarter-track, leave it null (unwritten)
                    // The emulator will return MC3470 random noise when reading
                }
            }

            Debug.WriteLine($"WozImporter: Imported .woz disk image from {sourcePath ?? "(stream)"} ({tracksFound}/{quarterTrackCount} quarter-tracks found)");

            // Get optimal bit timing from WOZ metadata (default to 32 if not available)
            byte optimalTiming = 32; // Default standard timing
            // Note: CiderPress2's Woz class doesn't expose optimal timing directly
            // For full timing accuracy, would need to parse WOZ INFO chunk manually

            return new InternalDiskImage(MaxTracks, quarterTracks, quarterTrackBitCounts)
            {
                SourceFilePath = sourcePath,
                OriginalFormat = DiskFormat.Woz,
                OptimalBitTiming = optimalTiming,
                IsWriteProtected = false // Can be changed after import
            };
        }
        finally
        {
            // Explicitly dispose to avoid GC finalization assertion in DEBUG builds
            wozImage.Dispose();
        }
    }
}
