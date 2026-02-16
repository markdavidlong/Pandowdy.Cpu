// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using System.Text;
using CommonUtil;
using DiskArc;
using DiskArc.Disk;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Importers;
using Pandowdy.EmuCore.DiskII.Providers;
using Xunit;
using Xunit.Abstractions;

namespace Pandowdy.EmuCore.Tests.DiskII.Importers;

/// <summary>
/// Comprehensive sector-level comparison tests to verify disk image importers
/// correctly encode and interleave sectors.
/// </summary>
public class SectorComparisonTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Compares SectorImporter (new) against SectorDiskImageProvider (legacy) 
    /// by decoding all sectors from all tracks and verifying they match.
    /// </summary>
    [Fact]
    public void CompareSectorImporterVsLegacyProvider_AllTracksAllSectors_Match()
    {

        // Use temp copy to avoid file locking conflicts with parallel tests
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestDo);
        if (sourceCopy == null)
        {
            _output.WriteLine("test.do not found, skipping test");
            return;
        }

        _output.WriteLine("=== Comparing SectorImporter vs Legacy SectorDiskImageProvider ===");
        _output.WriteLine("");

        // Import with new SectorImporter
        var importer = new SectorImporter();

        InternalDiskImage newImage = importer.Import(sourceCopy.FilePath);

        // Load with legacy SectorDiskImageProvider
        using var legacyProvider = new SectorDiskImageProvider(sourceCopy.FilePath);

        _output.WriteLine($"SectorImporter: {newImage.PhysicalTrackCount} tracks");
        _output.WriteLine($"Legacy provider: Ready");
        _output.WriteLine("");

        // Get a codec for decoding
        SectorCodec codec = StdSectorCodec.GetCodec(StdSectorCodec.CodecIndex525.Std_525_16);

        int totalMismatches = 0;
        int totalSectorsChecked = 0;

        // Compare each track
        for (int track = 0; track < newImage.PhysicalTrackCount && track < 35; track++)
        {
            _output.WriteLine($"--- Track {track} ---");

            // Decode sectors from new importer
            var newSectors = DecodeSectorsFromTrack(newImage.QuarterTracks[InternalDiskImage.TrackToQuarterTrackIndex(track)]!, track, codec);

            // Get legacy track by triggering synthesis
            legacyProvider.SetQuarterTrack(track * 4);
            legacyProvider.GetBit(0); // Trigger track synthesis

            // Access the legacy track cache via reflection (it's private)
            var legacyTrackCacheField = typeof(SectorDiskImageProvider)
                .GetField("_trackCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var legacyTrackCache = (CircularBitBuffer?[])legacyTrackCacheField!.GetValue(legacyProvider)!;
            CircularBitBuffer? legacyTrack = legacyTrackCache[track];

            if (legacyTrack == null)
            {
                _output.WriteLine($"  ERROR: Legacy provider track {track} is null");
                continue;
            }

            var legacySectors = DecodeSectorsFromTrack(legacyTrack, track, codec);

            _output.WriteLine($"  New importer: Found {newSectors.Count} sectors");
            _output.WriteLine($"  Legacy provider: Found {legacySectors.Count} sectors");

            // Compare sector by sector
            for (int sector = 0; sector < 16; sector++)
            {
                if (!newSectors.ContainsKey(sector))
                {
                    _output.WriteLine($"  ERROR: New importer missing sector {sector}");
                    totalMismatches++;
                    continue;
                }

                if (!legacySectors.ContainsKey(sector))
                {
                    _output.WriteLine($"  ERROR: Legacy provider missing sector {sector}");
                    totalMismatches++;
                    continue;
                }

                byte[] newData = newSectors[sector];
                byte[] legacyData = legacySectors[sector];

                totalSectorsChecked++;

                if (!newData.SequenceEqual(legacyData))
                {
                    totalMismatches++;
                    _output.WriteLine($"  MISMATCH: Sector {sector} differs!");
                    _output.WriteLine($"    New:    {FormatHex(newData, 0, 32)}");
                    _output.WriteLine($"    Legacy: {FormatHex(legacyData, 0, 32)}");

                    // Find first difference
                    for (int i = 0; i < Math.Min(newData.Length, legacyData.Length); i++)
                    {
                        if (newData[i] != legacyData[i])
                        {
                            _output.WriteLine($"    First difference at byte {i}: new=0x{newData[i]:X2}, legacy=0x{legacyData[i]:X2}");
                            break;
                        }
                    }
                }
            }

            _output.WriteLine("");
        }

        _output.WriteLine("=== Summary ===");
        _output.WriteLine($"Total sectors checked: {totalSectorsChecked}");
        _output.WriteLine($"Mismatches found: {totalMismatches}");

        Assert.Equal(0, totalMismatches);
    }

    /// <summary>
    /// Decodes all sectors from a track and returns them as a dictionary.
    /// </summary>
    private Dictionary<int, byte[]> DecodeSectorsFromTrack(CircularBitBuffer track, int trackNum, SectorCodec codec)
    {
        var result = new Dictionary<int, byte[]>();

        // Reset track position to start
        track.BitPosition = 0;

        // Find all sectors on the track
        List<SectorPtr> sectors = codec.FindSectors((uint)trackNum, 0, track);

        foreach (var sectorPtr in sectors)
        {
            if (sectorPtr.IsDataDamaged)
            {
                _output.WriteLine($"    WARNING: Track {trackNum} Sector {sectorPtr.Sector} is damaged");
                continue;
            }

            // Decode the sector data
            byte[] sectorData = new byte[256];
            track.BitPosition = sectorPtr.DataFieldBitOffset;

            bool decoded = codec.DecodeSector62_256(track, sectorPtr.DataFieldBitOffset, sectorData, 0);

            if (decoded)
            {
                result[sectorPtr.Sector] = sectorData;
            }
            else
            {
                _output.WriteLine($"    WARNING: Track {trackNum} Sector {sectorPtr.Sector} failed to decode");
            }
        }

        return result;
    }

    /// <summary>
    /// Formats a byte array as a hex string.
    /// </summary>
    private static string FormatHex(byte[] data, int offset, int length)
    {
        var sb = new StringBuilder();
        int end = Math.Min(offset + length, data.Length);
        for (int i = offset; i < end; i++)
        {
            if (i > offset)
            {
                sb.Append(' ');
            }
            sb.AppendFormat("{0:X2}", data[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Shows the physical sector order for test.do track 17 (catalog track).
    /// </summary>
    [Fact]
    public void ShowTestDoTrack17PhysicalOrder()
    {

        // Use temp copy to avoid file locking conflicts with parallel tests
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestDo);
        if (sourceCopy == null)
        {
            _output.WriteLine("test.do not found, skipping test");
            return;
        }

        _output.WriteLine("=== Track 17 Physical Sector Order (test.do) ===");
        _output.WriteLine("");

        var importer = new SectorImporter();

        InternalDiskImage doImage = importer.Import(sourceCopy.FilePath);

        CircularBitBuffer track = doImage.QuarterTracks[InternalDiskImage.TrackToQuarterTrackIndex(17)]!;
        SectorCodec codec = StdSectorCodec.GetCodec(StdSectorCodec.CodecIndex525.Std_525_16);

        // Reset to start
        track.BitPosition = 0;

        // Find all sectors
        List<SectorPtr> sectors = codec.FindSectors(17, 0, track);

        _output.WriteLine($"Found {sectors.Count} sectors on track 17");
        _output.WriteLine("");

        // Sort by physical position (address prolog offset)
        var sortedSectors = sectors.OrderBy(s => s.AddrPrologBitOffset).ToList();

        for (int physPos = 0; physPos < sortedSectors.Count; physPos++)
        {
            var sector = sortedSectors[physPos];
            byte[] sectorData = new byte[256];

            track.BitPosition = sector.DataFieldBitOffset;
            bool decoded = codec.DecodeSector62_256(track, sector.DataFieldBitOffset, sectorData, 0);

            string preview = decoded ? FormatHex(sectorData, 0, 16) : "DECODE FAILED";

            _output.WriteLine($"Physical position {physPos,2}: Logical sector {sector.Sector,2} " +
                            $"at bit offset {sector.AddrPrologBitOffset,6} - {preview}");
        }
    }

    /// <summary>
    /// Compares raw buffer bytes (not decoded sectors) between SectorImporter and legacy provider.
    /// This is critical because decoded sectors can match while raw encoding differs.
    /// </summary>
    [Fact]
    public void CompareRawBufferBytes_Track0_FindFirstDifference()
    {

        // Use temp copy to avoid file locking conflicts with parallel tests
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestDo);
        if (sourceCopy == null)
        {
            _output.WriteLine("test.do not found, skipping test");
            return;
        }

        _output.WriteLine("=== Raw Buffer Byte Comparison (Track 0) ===");
        _output.WriteLine("");

        // Import with new SectorImporter
        var importer = new SectorImporter();

        InternalDiskImage newImage = importer.Import(sourceCopy.FilePath);

        // Load with legacy SectorDiskImageProvider
        using var legacyProvider = new SectorDiskImageProvider(sourceCopy.FilePath);

        // Force track synthesis on legacy provider
        legacyProvider.SetQuarterTrack(0);
        legacyProvider.GetBit(0); // Trigger synthesis

        // Get legacy track buffer via reflection
        var legacyTrackCacheField = typeof(SectorDiskImageProvider)
            .GetField("_trackCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var legacyTrackCache = (CircularBitBuffer?[])legacyTrackCacheField!.GetValue(legacyProvider)!;
        CircularBitBuffer legacyTrack = legacyTrackCache[0]!;

        CircularBitBuffer newTrack = newImage.QuarterTracks[0]!;

        // Get raw buffer bytes via reflection
        var dataField = typeof(CircularBitBuffer)
            .GetField("mBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        byte[] newData = (byte[])dataField!.GetValue(newTrack)!;
        byte[] legacyData = (byte[])dataField!.GetValue(legacyTrack)!;

        _output.WriteLine($"New buffer length: {newData.Length} bytes");
        _output.WriteLine($"Legacy buffer length: {legacyData.Length} bytes");
        _output.WriteLine($"New bit count: {newImage.QuarterTrackBitCounts[0]}");

        // Get legacy bit count
        var bitCountsField = typeof(SectorDiskImageProvider)
            .GetField("_trackBitCounts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int[] legacyBitCounts = (int[])bitCountsField!.GetValue(legacyProvider)!;
        _output.WriteLine($"Legacy bit count: {legacyBitCounts[0]}");
        _output.WriteLine("");

        // Find first difference
        int minLen = Math.Min(newData.Length, legacyData.Length);
        int firstDiffByte = -1;
        int diffCount = 0;

        for (int i = 0; i < minLen; i++)
        {
            if (newData[i] != legacyData[i])
            {
                if (firstDiffByte == -1)
                {
                    firstDiffByte = i;
                }
                diffCount++;
            }
        }

        if (firstDiffByte == -1)
        {
            _output.WriteLine("SUCCESS: Raw buffer bytes are IDENTICAL!");
        }
        else
        {
            _output.WriteLine($"FOUND DIFFERENCES: {diffCount} bytes differ");
            _output.WriteLine($"First difference at byte {firstDiffByte} (bit {firstDiffByte * 8})");
            _output.WriteLine("");

            // Show context around first difference
            int start = Math.Max(0, firstDiffByte - 16);
            int end = Math.Min(minLen, firstDiffByte + 32);

            _output.WriteLine($"Bytes {start}-{end}:");
            _output.WriteLine($"  New:    {FormatHex(newData, start, end - start)}");
            _output.WriteLine($"  Legacy: {FormatHex(legacyData, start, end - start)}");
            _output.WriteLine("");

            // Show differences marked
            _output.WriteLine("Differences (byte positions):");
            int shown = 0;
            for (int i = 0; i < minLen && shown < 20; i++)
            {
                if (newData[i] != legacyData[i])
                {
                    _output.WriteLine($"  Byte {i,5}: new=0x{newData[i]:X2}, legacy=0x{legacyData[i]:X2}");
                    shown++;
                }
            }
        }

        // Assert they match
        Assert.Equal(-1, firstDiffByte);
    }

    /// <summary>
    /// Compares the actual bit stream returned by AdvanceAndReadBits between providers.
    /// This simulates what the controller actually sees during disk reads.
    /// </summary>
    [Fact]
    public void CompareAdvanceAndReadBits_Track0_FindFirstDifference()
    {

        // Use temp copy to avoid file locking conflicts with parallel tests
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestDo);
        if (sourceCopy == null)
        {
            _output.WriteLine("test.do not found, skipping test");
            return;
        }

        _output.WriteLine("=== AdvanceAndReadBits Comparison (Track 0) ===");
        _output.WriteLine("");

        // Import with new SectorImporter via UnifiedDiskImageProvider
        var importer = new SectorImporter();
        InternalDiskImage newImage = importer.Import(sourceCopy.FilePath);
        var newProvider = new UnifiedDiskImageProvider(newImage);

        // Load with legacy SectorDiskImageProvider
        var legacyProvider = new SectorDiskImageProvider(sourceCopy.FilePath);

        // Set both to track 0
        newProvider.SetQuarterTrack(0);
        legacyProvider.SetQuarterTrack(0);

        // Simulate motor on
        newProvider.NotifyMotorStateChanged(true, 0);
        legacyProvider.NotifyMotorStateChanged(true, 0);

        // Read bits in chunks, simulating what ProcessBits does
        const double cyclesPerRead = 100.0; // ~25 bits at standard timing
        const int totalReads = 500; // Read ~12,500 bits

        Span<bool> newBits = stackalloc bool[64];
        Span<bool> legacyBits = stackalloc bool[64];

        int totalBitsRead = 0;
        int firstDiffBit = -1;
        int diffCount = 0;

        for (int read = 0; read < totalReads; read++)
        {
            int newCount = newProvider.AdvanceAndReadBits(cyclesPerRead, newBits);
            int legacyCount = legacyProvider.AdvanceAndReadBits(cyclesPerRead, legacyBits);

            if (newCount != legacyCount)
            {
                _output.WriteLine($"Read {read}: Bit count mismatch! new={newCount}, legacy={legacyCount}");
                if (firstDiffBit == -1)
                {
                    firstDiffBit = totalBitsRead;
                }
                break;
            }

            for (int i = 0; i < newCount; i++)
            {
                if (newBits[i] != legacyBits[i])
                {
                    if (firstDiffBit == -1)
                    {
                        firstDiffBit = totalBitsRead + i;
                        _output.WriteLine($"First bit difference at bit {firstDiffBit}: new={newBits[i]}, legacy={legacyBits[i]}");
                    }
                    diffCount++;
                }
            }

            totalBitsRead += newCount;
        }

        _output.WriteLine($"Total bits compared: {totalBitsRead}");
        _output.WriteLine($"Total differences: {diffCount}");

        if (firstDiffBit == -1)
        {
            _output.WriteLine("SUCCESS: Bit streams are IDENTICAL!");
        }

        newProvider.Dispose();
        legacyProvider.Dispose();

        Assert.Equal(-1, firstDiffBit);
    }
}
