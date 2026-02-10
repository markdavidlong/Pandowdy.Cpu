// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Importers;
using Xunit;

namespace Pandowdy.EmuCore.Tests.DiskII.Importers;

/// <summary>
/// Unit tests for the WozImporter class.
/// </summary>
public class WozImporterTests
{
    [Fact]
    public void SupportedExtensions_ContainsWoz()
    {
        var importer = new WozImporter();
        Assert.Contains(".woz", importer.SupportedExtensions);
    }

    [Fact]
    public void Import_NullFilePath_ThrowsArgumentNullException()
    {
        var importer = new WozImporter();
        Assert.Throws<ArgumentNullException>(() => importer.Import(null!));
    }

    [Fact]
    public void Import_NonExistentFile_ThrowsFileNotFoundException()
    {
        var importer = new WozImporter();
        Assert.Throws<FileNotFoundException>(() => importer.Import("nonexistent.woz"));
    }

    [Fact]
<<<<<<< HEAD
    public void Import_ValidWozFile_ReturnsInternalDiskImage()
    {
        var importer = new WozImporter();
        if (!File.Exists(TestDiskImages.TestWoz))
=======
    [Trait("Category", "FullDiskTests")]
    public void Import_ValidWozFile_ReturnsInternalDiskImage()
    {
        var importer = new WozImporter();
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestWoz);
        if (sourceCopy == null)
>>>>>>> internaldiskimage
        {
            return;
        }

<<<<<<< HEAD
        var diskImage = importer.Import(TestDiskImages.TestWoz);
=======
        var diskImage = importer.Import(sourceCopy.FilePath);
>>>>>>> internaldiskimage

        Assert.NotNull(diskImage);
        Assert.Equal(35, diskImage.TrackCount);
        Assert.Equal(DiskFormat.Woz, diskImage.OriginalFormat);
        Assert.Equal(TestDiskImages.TestWoz, diskImage.SourceFilePath);
        Assert.False(diskImage.IsDirty);
    }

    [Fact]
<<<<<<< HEAD
    public void Import_ValidWozFile_TracksAreReadable()
    {
        var importer = new WozImporter();
        if (!File.Exists(TestDiskImages.TestWoz))
=======
    [Trait("Category", "FullDiskTests")]
    public void Import_ValidWozFile_TracksAreReadable()
    {
        var importer = new WozImporter();
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestWoz);
        if (sourceCopy == null)
>>>>>>> internaldiskimage
        {
            return;
        }

<<<<<<< HEAD
        var diskImage = importer.Import(TestDiskImages.TestWoz);
=======
        var diskImage = importer.Import(sourceCopy.FilePath);
>>>>>>> internaldiskimage

        // Verify tracks are accessible
        for (int track = 0; track < diskImage.TrackCount; track++)
        {
            var trackBuffer = diskImage.Tracks[track];
            Assert.NotNull(trackBuffer);
            Assert.True(diskImage.TrackBitCounts[track] > 0);
        }
    }

    [Fact]
    public void ImportStream_NullStream_ThrowsArgumentNullException()
    {
        var importer = new WozImporter();
        Assert.Throws<ArgumentNullException>(() => importer.Import((Stream)null!, DiskFormat.Woz));
    }

    [Fact]
    public void ImportStream_WrongFormat_ThrowsArgumentException()
    {
        var importer = new WozImporter();
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentException>(() => importer.Import(stream, DiskFormat.Nib));
    }

    [Fact]
<<<<<<< HEAD
    public void ImportStream_ValidWozStream_ReturnsInternalDiskImage()
    {
        var importer = new WozImporter();
        if (!File.Exists(TestDiskImages.TestWoz))
=======
    [Trait("Category", "FullDiskTests")]
    public void ImportStream_ValidWozStream_ReturnsInternalDiskImage()
    {
        var importer = new WozImporter();
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestWoz);
        if (sourceCopy == null)
>>>>>>> internaldiskimage
        {
            return;
        }

<<<<<<< HEAD
        using var stream = File.OpenRead(TestDiskImages.TestWoz);
=======
        using var stream = File.OpenRead(sourceCopy.FilePath);
>>>>>>> internaldiskimage
        var diskImage = importer.Import(stream, DiskFormat.Woz);

        Assert.NotNull(diskImage);
        Assert.Equal(35, diskImage.TrackCount);
        Assert.Equal(DiskFormat.Woz, diskImage.OriginalFormat);
        Assert.Null(diskImage.SourceFilePath); // Stream import
    }
}
