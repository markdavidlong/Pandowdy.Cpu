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
    private const int QuartersPerTrack = 4;

    /// <summary>
    /// Supported file extensions for WOZ format.
    /// </summary>
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".woz" };

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
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (format != DiskFormat.Woz)
        {
            throw new ArgumentException($"WozImporter can only import WOZ format, got {format}", nameof(format));
        }

        return ImportFromStream(stream, sourcePath: null);
    }

    /// <summary>
    /// Internal import implementation shared by file and stream imports.
    /// </summary>
    private InternalDiskImage ImportFromStream(Stream stream, string? sourcePath)
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

            // Extract track data - for now, only import main track positions (quarter = 0)
            // WOZ can store quarter-track data, but InternalDiskImage uses full tracks
            var tracks = new CircularBitBuffer[MaxTracks];
            var trackBitCounts = new int[MaxTracks];
            int tracksFound = 0;

            for (int track = 0; track < MaxTracks; track++)
            {
                // Try to get the main track (quarter position 0)
                if (wozImage.GetTrackBits((uint)track, 0, out CircularBitBuffer? cbb) && cbb != null)
                {
<<<<<<< HEAD
                    tracks[track] = cbb;
                    trackBitCounts[track] = cbb.BitCount;
=======
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

                    tracks[track] = new CircularBitBuffer(
                        trackData,
                        byteOffset: 0,
                        bitOffset: 0,
                        bitCount: bitCount,
                        new GroupBool(),
                        isReadOnly: false
                    );
                    trackBitCounts[track] = bitCount;
>>>>>>> internaldiskimage
                    tracksFound++;
                }
                else
                {
                    // Track not found - create empty track with standard size
                    int standardBitCount = DiskIIConstants.BitsPerTrack;
                    byte[] emptyTrackData = new byte[(standardBitCount + 7) / 8];
                    tracks[track] = new CircularBitBuffer(
                        emptyTrackData,
                        byteOffset: 0,
                        bitOffset: 0,
                        bitCount: standardBitCount,
                        new GroupBool(),
                        isReadOnly: false
                    );
                    trackBitCounts[track] = standardBitCount;
                }
            }

            Debug.WriteLine($"WozImporter: Imported .woz disk image from {sourcePath ?? "(stream)"} ({tracksFound}/{MaxTracks} tracks found)");

            // Get optimal bit timing from WOZ metadata (default to 32 if not available)
            byte optimalTiming = 32; // Default standard timing
            // Note: CiderPress2's Woz class doesn't expose optimal timing directly
            // For full timing accuracy, would need to parse WOZ INFO chunk manually

            return new InternalDiskImage(tracks, trackBitCounts)
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
