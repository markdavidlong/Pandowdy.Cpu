// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using CommonUtil;

namespace Pandowdy.EmuCore.DiskII.Importers;

/// <summary>
/// Imports .nib (nibble) format disk images to internal format.
/// </summary>
/// <remarks>
/// <para>
/// <strong>.NIB Format:</strong><br/>
/// The .nib format stores raw 6-and-2 GCR-encoded disk data exactly as it appears on physical
/// disk. Each track contains 6656 bytes (53,248 bits), and a standard disk has 35 tracks.
/// Unlike flux-based formats (WOZ), NIB files contain no timing information.
/// </para>
/// <para>
/// <strong>Import Process:</strong><br/>
/// Reads the raw byte data and wraps each track in a CircularBitBuffer. Since NIB format
/// is already bit-perfect GCR data, no encoding is needed - just wrap the existing bytes.
/// </para>
/// </remarks>
public class NibImporter : IDiskImageImporter
{
    /// <summary>
    /// Supported file extensions for NIB format.
    /// </summary>
    public IReadOnlyList<string> SupportedExtensions { get; } = [".nib"];

    /// <summary>
    /// Import a .nib disk image file to internal format.
    /// </summary>
    /// <param name="filePath">Path to the .nib file.</param>
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
    /// <param name="stream">Stream containing .nib disk image data.</param>
    /// <param name="format">Format hint (should be DiskFormat.Nib).</param>
    /// <returns>Internal disk image representation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if stream is null.</exception>
    /// <exception cref="InvalidDataException">Thrown if stream format is invalid.</exception>
    public InternalDiskImage Import(Stream stream, DiskFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (format != DiskFormat.Nib)
        {
            throw new ArgumentException($"NibImporter can only import NIB format, got {format}", nameof(format));
        }

        return ImportFromStream(stream, sourcePath: null);
    }

    /// <summary>
    /// Internal import implementation shared by file and stream imports.
    /// </summary>
    private static InternalDiskImage ImportFromStream(Stream stream, string? sourcePath)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        // Load the entire disk image into memory
        byte[] diskData = new byte[stream.Length];
        int bytesRead = stream.Read(diskData, 0, diskData.Length);

        if (bytesRead != diskData.Length)
        {
            throw new InvalidDataException($"Failed to read complete disk image. Expected {diskData.Length} bytes, got {bytesRead}.");
        }

        // Validate file size (should be 35 tracks × 6656 bytes = 232,960 bytes)
        int expectedSize = DiskIIConstants.TrackCount * DiskIIConstants.BytesPerNibTrack;
        if (diskData.Length != expectedSize)
        {
            throw new InvalidDataException(
                $"Invalid .nib file size. Expected {expectedSize} bytes, got {diskData.Length} bytes.");
        }


        // Create quarter-track arrays for the internal disk image
        // NIB format only has whole-track data, so we populate indices 0, 4, 8, 12...
        int quarterTrackCount = InternalDiskImage.CalculateQuarterTrackCount(DiskIIConstants.TrackCount);
        var quarterTracks = new CircularBitBuffer?[quarterTrackCount];
        var quarterTrackBitCounts = new int[quarterTrackCount];

        for (int track = 0; track < DiskIIConstants.TrackCount; track++)
        {
            int byteOffset = track * DiskIIConstants.BytesPerNibTrack;
            byte[] trackData = new byte[DiskIIConstants.BytesPerNibTrack];
            Array.Copy(diskData, byteOffset, trackData, 0, DiskIIConstants.BytesPerNibTrack);

            // Store at quarter-track index (track * 4)
            int quarterIndex = InternalDiskImage.TrackToQuarterTrackIndex(track);
            quarterTrackBitCounts[quarterIndex] = DiskIIConstants.BitsPerTrack;

            quarterTracks[quarterIndex] = new CircularBitBuffer(
                trackData,
                byteOffset: 0,
                bitOffset: 0,
                bitCount: DiskIIConstants.BitsPerTrack,
                new GroupBool(),
                isReadOnly: false
            );
        }

        Debug.WriteLine($"NibImporter: Imported .nib disk image from {sourcePath ?? "(stream)"} ({DiskIIConstants.TrackCount} tracks, {DiskIIConstants.BytesPerNibTrack} bytes per track)");

        return new InternalDiskImage(DiskIIConstants.TrackCount, quarterTracks, quarterTrackBitCounts)
        {
            SourceFilePath = sourcePath,
            OriginalFormat = DiskFormat.Nib,
            OptimalBitTiming = 32, // Standard timing for NIB (no timing info in format)
            IsWriteProtected = false // Can be changed after import
        };
    }
}
