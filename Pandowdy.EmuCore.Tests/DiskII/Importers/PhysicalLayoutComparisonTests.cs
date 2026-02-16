// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

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
/// Diagnostic test to compare physical sector layout between new and legacy providers.
/// </summary>
public class PhysicalLayoutComparisonTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void ComparePhysicalLayout_Track0_NewVsLegacy()
    {

        // Use temp copy to avoid file locking conflicts with parallel tests
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestDo);
        if (sourceCopy == null)
        {
            _output.WriteLine("test.do not found");
            return;
        }

        int track = 0;
        _output.WriteLine($"=== Physical Layout Comparison: Track {track} ===");
        _output.WriteLine("");

        // New importer (using temp copy)
        var importer = new SectorImporter();
        InternalDiskImage newImage = importer.Import(sourceCopy.FilePath);
        CircularBitBuffer newTrack = newImage.QuarterTracks[InternalDiskImage.TrackToQuarterTrackIndex(track)]!;

        // Legacy provider (using temp copy)
        using var legacyProvider = new SectorDiskImageProvider(sourceCopy.FilePath);
        legacyProvider.SetQuarterTrack(track * 4);
        legacyProvider.GetBit(0);

        var legacyTrackCacheField = typeof(SectorDiskImageProvider)
            .GetField("_trackCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var legacyTrackCache = (CircularBitBuffer?[])legacyTrackCacheField!.GetValue(legacyProvider)!;
        CircularBitBuffer legacyTrack = legacyTrackCache[track]!;

        // Decode both
        SectorCodec codec = StdSectorCodec.GetCodec(StdSectorCodec.CodecIndex525.Std_525_16);

        newTrack.BitPosition = 0;
        var newSectors = codec.FindSectors((uint)track, 0, newTrack);
        var newSorted = newSectors.OrderBy(s => s.AddrPrologBitOffset).ToList();

        legacyTrack.BitPosition = 0;
        var legacySectors = codec.FindSectors((uint)track, 0, legacyTrack);
        var legacySorted = legacySectors.OrderBy(s => s.AddrPrologBitOffset).ToList();

        _output.WriteLine("Physical Position | New Logical Sector | Legacy Logical Sector");
        _output.WriteLine("------------------|--------------------|-----------------------");

        for (int i = 0; i < Math.Min(newSorted.Count, legacySorted.Count); i++)
        {
            string match = newSorted[i].Sector == legacySorted[i].Sector ? " " : " <-- MISMATCH";
            _output.WriteLine($"        {i,2}        |         {newSorted[i].Sector,2}         |           {legacySorted[i].Sector,2}          {match}");
        }
    }
}
