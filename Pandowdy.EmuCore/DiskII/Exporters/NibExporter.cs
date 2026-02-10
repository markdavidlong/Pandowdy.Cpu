// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using CommonUtil;

namespace Pandowdy.EmuCore.DiskII.Exporters;

/// <summary>
/// Exports internal disk image format to raw nibble format (.nib).
/// </summary>
/// <remarks>
/// <para>
/// <strong>NIB Format:</strong><br/>
/// The .nib format stores raw GCR-encoded track data as 6,656 bytes per track
/// for 35 tracks, totaling 232,960 bytes. This is a simple binary dump
/// of the track data with no header or metadata.
/// </para>
/// <para>
/// <strong>Lossless Export:</strong><br/>
/// This export is LOSSLESS. All track data is preserved exactly as stored in the
/// internal format's CircularBitBuffer. Copy-protected disks and timing-sensitive
/// data are preserved (though timing information itself is not stored in NIB format).
/// </para>
/// </remarks>
public class NibExporter : IDiskImageExporter
{
    private const int NumTracks = 35;
    private const int TrackTotalLength = 6656; // NIB format: 6656 bytes per track
    private const int ExpectedFileSize = NumTracks * TrackTotalLength; // 232,960 bytes

    /// <summary>
    /// Format this exporter produces.
    /// </summary>
    public DiskFormat OutputFormat => DiskFormat.Nib;

    /// <summary>
    /// Export internal format to file.
    /// </summary>
    /// <param name="disk">Internal disk image to export.</param>
    /// <param name="filePath">Path to write the disk image file.</param>
    /// <exception cref="ArgumentNullException">Thrown if disk or filePath is null.</exception>
    /// <exception cref="IOException">Thrown when file write fails.</exception>
    public void Export(InternalDiskImage disk, string filePath)
    {
        if (disk == null)
        {
            throw new ArgumentNullException(nameof(disk));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        // Convert tracks to NIB format
        byte[] nibData = ConvertToNibFormat(disk);

        // Write to file
        try
        {
            File.WriteAllBytes(filePath, nibData);
            Debug.WriteLine($"NibExporter: Exported to {filePath} ({nibData.Length} bytes)");
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write NIB file to {filePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Export to stream (for embedding in project files).
    /// </summary>
    /// <param name="disk">Internal disk image to export.</param>
    /// <param name="stream">Stream to write the disk image data.</param>
    /// <exception cref="ArgumentNullException">Thrown if disk or stream is null.</exception>
    public void Export(InternalDiskImage disk, Stream stream)
    {
        if (disk == null)
        {
            throw new ArgumentNullException(nameof(disk));
        }

        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        // Convert tracks to NIB format
        byte[] nibData = ConvertToNibFormat(disk);

        // Write to stream
        try
        {
            stream.Write(nibData, 0, nibData.Length);
            Debug.WriteLine($"NibExporter: Exported to stream ({nibData.Length} bytes)");
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write NIB data to stream: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts internal disk image to NIB format.
    /// </summary>
    private byte[] ConvertToNibFormat(InternalDiskImage disk)
    {
        byte[] output = new byte[ExpectedFileSize];
        int trackCount = Math.Min(disk.TrackCount, NumTracks);

        for (int track = 0; track < trackCount; track++)
        {
            CircularBitBuffer trackBuffer = disk.Tracks[track];
            int trackBitCount = disk.TrackBitCounts[track];
            int trackByteCount = trackBitCount / 8; // Only full bytes

            // Calculate output offset for this track
            int outputOffset = track * TrackTotalLength;

            // Reset track position to beginning
            trackBuffer.BitPosition = 0;

            // NIB format expects exactly TrackTotalLength bytes per track
            int bytesToWrite = Math.Min(trackByteCount, TrackTotalLength);

            if (trackByteCount > TrackTotalLength)
            {
                Debug.WriteLine($"NibExporter: WARNING - Track {track} is {trackByteCount} bytes, truncating to {TrackTotalLength}");
            }

            // Read bytes from track buffer - must be byte-aligned
            for (int i = 0; i < bytesToWrite; i++)
            {
                // Use LatchNextByte which reads the next byte and advances by 8 bits
                output[outputOffset + i] = trackBuffer.LatchNextByte();
            }

            // Remaining bytes are already zero (padding)
        }

        return output;
    }
}
