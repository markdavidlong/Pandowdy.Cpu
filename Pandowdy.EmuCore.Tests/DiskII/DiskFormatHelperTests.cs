// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Exporters;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Tests for DiskFormatHelper - validates format mapping, exporter selection, and export support checking.
/// </summary>
public class DiskFormatHelperTests
{
    #region GetFormatFromExtension Tests

    [Fact]
    public void GetFormatFromExtension_Woz_ReturnsWozFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension(".woz");
        
        Assert.Equal(DiskFormat.Woz, format);
    }

    [Fact]
    public void GetFormatFromExtension_Nib_ReturnsNibFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension(".nib");
        
        Assert.Equal(DiskFormat.Nib, format);
    }

    [Fact]
    public void GetFormatFromExtension_Dsk_ReturnsDskFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension(".dsk");
        
        Assert.Equal(DiskFormat.Dsk, format);
    }

    [Fact]
    public void GetFormatFromExtension_Do_ReturnsDoFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension(".do");
        
        Assert.Equal(DiskFormat.Do, format);
    }

    [Fact]
    public void GetFormatFromExtension_Po_ReturnsPoFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension(".po");
        
        Assert.Equal(DiskFormat.Po, format);
    }

    [Fact]
    public void GetFormatFromExtension_UpperCase_ReturnsCorrectFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension(".WOZ");
        
        Assert.Equal(DiskFormat.Woz, format);
    }

    [Fact]
    public void GetFormatFromExtension_NoDot_ReturnsCorrectFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension("woz");
        
        Assert.Equal(DiskFormat.Woz, format);
    }

    [Fact]
    public void GetFormatFromExtension_Unknown_ReturnsUnknownFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension(".xyz");
        
        Assert.Equal(DiskFormat.Unknown, format);
    }

    [Fact]
    public void GetFormatFromExtension_EmptyString_ReturnsUnknownFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension("");
        
        Assert.Equal(DiskFormat.Unknown, format);
    }

    [Fact]
    public void GetFormatFromExtension_Null_ReturnsUnknownFormat()
    {
        var format = DiskFormatHelper.GetFormatFromExtension(null);
        
        Assert.Equal(DiskFormat.Unknown, format);
    }

    #endregion

    #region GetFormatFromPath Tests

    [Fact]
    public void GetFormatFromPath_WozFile_ReturnsWozFormat()
    {
        var format = DiskFormatHelper.GetFormatFromPath("E:\\disks\\game.woz");
        
        Assert.Equal(DiskFormat.Woz, format);
    }

    [Fact]
    public void GetFormatFromPath_NibFile_ReturnsNibFormat()
    {
        var format = DiskFormatHelper.GetFormatFromPath("/home/user/disks/game.nib");
        
        Assert.Equal(DiskFormat.Nib, format);
    }

    [Fact]
    public void GetFormatFromPath_EmptyPath_ReturnsUnknownFormat()
    {
        var format = DiskFormatHelper.GetFormatFromPath("");
        
        Assert.Equal(DiskFormat.Unknown, format);
    }

    [Fact]
    public void GetFormatFromPath_NoExtension_ReturnsUnknownFormat()
    {
        var format = DiskFormatHelper.GetFormatFromPath("E:\\disks\\game");
        
        Assert.Equal(DiskFormat.Unknown, format);
    }

    #endregion

    #region GetExporterForFormat Tests

    [Fact]
    public void GetExporterForFormat_Woz_ReturnsWozExporter()
    {
        var exporter = DiskFormatHelper.GetExporterForFormat(DiskFormat.Woz);
        
        Assert.IsType<WozExporter>(exporter);
    }

    [Fact]
    public void GetExporterForFormat_Nib_ReturnsNibExporter()
    {
        var exporter = DiskFormatHelper.GetExporterForFormat(DiskFormat.Nib);
        
        Assert.IsType<NibExporter>(exporter);
    }

    [Fact]
    public void GetExporterForFormat_Dsk_ReturnsSectorExporter()
    {
        var exporter = DiskFormatHelper.GetExporterForFormat(DiskFormat.Dsk);
        
        Assert.IsType<SectorExporter>(exporter);
    }

    [Fact]
    public void GetExporterForFormat_Do_ReturnsSectorExporter()
    {
        var exporter = DiskFormatHelper.GetExporterForFormat(DiskFormat.Do);
        
        Assert.IsType<SectorExporter>(exporter);
    }

    [Fact]
    public void GetExporterForFormat_Po_ReturnsSectorExporter()
    {
        var exporter = DiskFormatHelper.GetExporterForFormat(DiskFormat.Po);
        
        Assert.IsType<SectorExporter>(exporter);
    }

    [Fact]
    public void GetExporterForFormat_Unknown_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DiskFormatHelper.GetExporterForFormat(DiskFormat.Unknown));
        
        Assert.Contains("Unknown format", exception.Message);
    }

    [Fact]
    public void GetExporterForFormat_Internal_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DiskFormatHelper.GetExporterForFormat(DiskFormat.Internal));
        
        Assert.Contains("Internal format", exception.Message);
    }

    #endregion

    #region GetExporterForPath Tests

    [Fact]
    public void GetExporterForPath_WozFile_ReturnsWozExporter()
    {
        var exporter = DiskFormatHelper.GetExporterForPath("E:\\disks\\game.woz");
        
        Assert.IsType<WozExporter>(exporter);
    }

    [Fact]
    public void GetExporterForPath_NibFile_ReturnsNibExporter()
    {
        var exporter = DiskFormatHelper.GetExporterForPath("E:\\disks\\game.nib");
        
        Assert.IsType<NibExporter>(exporter);
    }

    [Fact]
    public void GetExporterForPath_UnknownExtension_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DiskFormatHelper.GetExporterForPath("E:\\disks\\game.xyz"));
        
        Assert.Contains("Unknown format", exception.Message);
    }

    #endregion

    #region IsExportSupported Tests

    [Fact]
    public void IsExportSupported_Woz_ReturnsTrue()
    {
        var supported = DiskFormatHelper.IsExportSupported(DiskFormat.Woz);
        
        Assert.True(supported);
    }

    [Fact]
    public void IsExportSupported_Nib_ReturnsTrue()
    {
        var supported = DiskFormatHelper.IsExportSupported(DiskFormat.Nib);
        
        Assert.True(supported);
    }

    [Fact]
    public void IsExportSupported_Dsk_ReturnsTrue()
    {
        var supported = DiskFormatHelper.IsExportSupported(DiskFormat.Dsk);
        
        Assert.True(supported);
    }

    [Fact]
    public void IsExportSupported_Do_ReturnsTrue()
    {
        var supported = DiskFormatHelper.IsExportSupported(DiskFormat.Do);
        
        Assert.True(supported);
    }

    [Fact]
    public void IsExportSupported_Po_ReturnsTrue()
    {
        var supported = DiskFormatHelper.IsExportSupported(DiskFormat.Po);
        
        Assert.True(supported);
    }

    [Fact]
    public void IsExportSupported_Internal_ReturnsFalse()
    {
        var supported = DiskFormatHelper.IsExportSupported(DiskFormat.Internal);
        
        Assert.False(supported);
    }

    [Fact]
    public void IsExportSupported_Unknown_ReturnsFalse()
    {
        var supported = DiskFormatHelper.IsExportSupported(DiskFormat.Unknown);
        
        Assert.False(supported);
    }

    #endregion
}
