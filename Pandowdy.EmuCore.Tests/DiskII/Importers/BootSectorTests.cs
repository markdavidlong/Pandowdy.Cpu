// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

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
/// Tests to verify boot sector and track 0 are correctly formatted for DOS booting.
/// </summary>
public class BootSectorTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void ShowTrack0PhysicalLayout_LegacyProvider()
    {
        if (!File.Exists(TestDiskImages.TestDo))
        {
            _output.WriteLine("test.do not found");
            return;
        }

        _output.WriteLine("=== Track 0 Physical Layout - Legacy SectorDiskImageProvider ===");
        _output.WriteLine("");

        using var provider = new SectorDiskImageProvider(TestDiskImages.TestDo);
        provider.SetQuarterTrack(0);
        provider.GetBit(0); // Trigger synthesis

        var trackCacheField = typeof(SectorDiskImageProvider)
            .GetField("_trackCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var trackCache = (CircularBitBuffer?[])trackCacheField!.GetValue(provider)!;
        CircularBitBuffer track = trackCache[0]!;

        SectorCodec codec = StdSectorCodec.GetCodec(StdSectorCodec.CodecIndex525.Std_525_16);
        track.BitPosition = 0;
        var sectors = codec.FindSectors(0, 0, track);
        var sorted = sectors.OrderBy(s => s.AddrPrologBitOffset).ToList();

        _output.WriteLine("Physical Pos | Logical Sector | First 32 bytes of data");
        _output.WriteLine("-------------|----------------|------------------------");

        for (int i = 0; i < sorted.Count; i++)
        {
            var sector = sorted[i];
            byte[] data = new byte[256];
            track.BitPosition = sector.DataFieldBitOffset;
            codec.DecodeSector62_256(track, sector.DataFieldBitOffset, data, 0);

            string hex = BitConverter.ToString(data, 0, Math.Min(32, data.Length)).Replace("-", " ");
            _output.WriteLine($"    {i,2}       |       {sector.Sector,2}       | {hex}");
        }
    }

    [Fact]
    public void ShowTrack0PhysicalLayout_NewImporter()
    {
        if (!File.Exists(TestDiskImages.TestDo))
        {
            _output.WriteLine("test.do not found");
            return;
        }

        _output.WriteLine("=== Track 0 Physical Layout - New SectorImporter ===");
        _output.WriteLine("");

        var importer = new SectorImporter();
        InternalDiskImage image = importer.Import(TestDiskImages.TestDo);
        CircularBitBuffer track = image.QuarterTracks[0]!;

        SectorCodec codec = StdSectorCodec.GetCodec(StdSectorCodec.CodecIndex525.Std_525_16);
        track.BitPosition = 0;
        var sectors = codec.FindSectors(0, 0, track);
        var sorted = sectors.OrderBy(s => s.AddrPrologBitOffset).ToList();

        _output.WriteLine("Physical Pos | Logical Sector | First 32 bytes of data");
        _output.WriteLine("-------------|----------------|------------------------");

        for (int i = 0; i < sorted.Count; i++)
        {
            var sector = sorted[i];
            byte[] data = new byte[256];
            track.BitPosition = sector.DataFieldBitOffset;
            codec.DecodeSector62_256(track, sector.DataFieldBitOffset, data, 0);

            string hex = BitConverter.ToString(data, 0, Math.Min(32, data.Length)).Replace("-", " ");
            _output.WriteLine($"    {i,2}       |       {sector.Sector,2}       | {hex}");
        }
    }

    [Fact]
    public void CompareBootSectorData_DirectFromFile()
    {
        if (!File.Exists(TestDiskImages.TestDo))
        {
            _output.WriteLine("test.do not found");
            return;
        }

        _output.WriteLine("=== Reading Boot Sector Directly from File ===");
        _output.WriteLine("");

        // Read the first 256 bytes of the file (physical sector 0)
        byte[] physicalSector0 = new byte[256];
        using (var fs = File.OpenRead(TestDiskImages.TestDo))
        {
            fs.ReadExactly(physicalSector0, 0, 256);
        }

        _output.WriteLine("First 64 bytes of physical sector 0 from file:");
        _output.WriteLine(BitConverter.ToString(physicalSector0, 0, 64).Replace("-", " "));
        _output.WriteLine("");

        // Check if it looks like a DOS boot sector (should start with 01 and have some recognizable patterns)
        if (physicalSector0[0] == 0x01)
        {
            _output.WriteLine("✓ Starts with 0x01 (looks like a DOS boot sector)");
        }
        else
        {
            _output.WriteLine($"✗ Starts with 0x{physicalSector0[0]:X2} (expected 0x01 for DOS boot sector)");
        }
    }
}
