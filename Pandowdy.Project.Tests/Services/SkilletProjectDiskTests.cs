// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Microsoft.Data.Sqlite;
using Pandowdy.Project.Constants;
using Pandowdy.Project.Services;
using Xunit;
using Xunit.Abstractions;

namespace Pandowdy.Project.Tests.Services;

/// <summary>
/// Tests for <see cref="SkilletProject"/> disk image CRUD and IDiskImageStore implementation.
/// </summary>
public class SkilletProjectDiskTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region ImportDiskImageAsync


    [Fact]
    public async Task ImportDiskImageAsync_UnsupportedFormat_ThrowsArgumentException()
    {
        // Arrange
        using var project = await CreateTestProjectAsync();
        var invalidPath = "test.xyz";  // Unsupported extension

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await project.ImportDiskImageAsync(invalidPath, "Test Disk"));
    }

    #endregion

    #region IDiskImageStore (CheckOutAsync / ReturnAsync)

    [Fact]
    public async Task CheckOutAsync_ExistingDiskImage_ReturnsDeserializedImage()
    {
        // Arrange
        using var project = await CreateTestProjectAsync();
        long diskId = await InsertMockDiskImageAsync(project);

        // Act
        var diskImage = await project.CheckOutAsync(diskId);

        // Assert
        Assert.NotNull(diskImage);
        Assert.Equal(35, diskImage.PhysicalTrackCount);
        Assert.Equal(137, diskImage.QuarterTrackCount);
    }

    [Fact]
    public async Task CheckOutAsync_NonexistentDiskImage_ThrowsInvalidOperationException()
    {
        // Arrange
        using var project = await CreateTestProjectAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await project.CheckOutAsync(diskImageId: 999));
    }

    [Fact]
    public async Task ReturnAsync_ModifiedDiskImage_SerializesToWorkingBlob()
    {
        // Arrange
        using var project = await CreateTestProjectAsync();
        long diskId = await InsertMockDiskImageAsync(project);
        var diskImage = await project.CheckOutAsync(diskId);

        // Modify disk (simulate emulator writes)
        diskImage.QuarterTracks[0]!.BitPosition = 0;
        diskImage.QuarterTracks[0]!.WriteOctet(0xAA);
        diskImage.MarkDirty();

        // Act: Return the modified image
        await project.ReturnAsync(diskId, diskImage);

        // Assert: working_blob should be updated, working_dirty = 1
        var record = await project.GetDiskImageAsync(diskId);
        Assert.True(record.WorkingDirty);
    }

    #endregion

    #region GetDiskImageAsync / GetAllDiskImagesAsync

    [Fact]
    public async Task GetDiskImageAsync_ExistingDisk_ReturnsRecord()
    {
        // Arrange
        using var project = await CreateTestProjectAsync();
        long diskId = await InsertMockDiskImageAsync(project, name: "Test Disk");

        // Act
        var record = await project.GetDiskImageAsync(diskId);

        // Assert
        Assert.NotNull(record);
        Assert.Equal("Test Disk", record.Name);
        Assert.Equal("Nib", record.OriginalFormat);
    }

    [Fact]
    public async Task GetAllDiskImagesAsync_MultipleDiskImages_ReturnsAll()
    {
        // Arrange
        using var project = await CreateTestProjectAsync();
        await InsertMockDiskImageAsync(project, name: "Disk 1");
        await InsertMockDiskImageAsync(project, name: "Disk 2");
        await InsertMockDiskImageAsync(project, name: "Disk 3");

        // Act
        var allDisks = await project.GetAllDiskImagesAsync();

        // Assert
        Assert.Equal(3, allDisks.Count);
        Assert.Contains(allDisks, d => d.Name == "Disk 1");
        Assert.Contains(allDisks, d => d.Name == "Disk 2");
        Assert.Contains(allDisks, d => d.Name == "Disk 3");
    }

    #endregion

    #region RegenerateWorkingCopyAsync

    [Fact]
    public async Task RegenerateWorkingCopyAsync_DirtyDisk_ClearsWorkingBlob()
    {
        // Arrange
        using var project = await CreateTestProjectAsync();
        long diskId = await InsertMockDiskImageAsync(project);
        
        // Make disk dirty (simulate emulator writes)
        var diskImage = await project.CheckOutAsync(diskId);
        diskImage.MarkDirty();
        await project.ReturnAsync(diskId, diskImage);
        
        // Verify dirty
        var recordBefore = await project.GetDiskImageAsync(diskId);
        Assert.True(recordBefore.WorkingDirty);

        // Act
        await project.RegenerateWorkingCopyAsync(diskId);

        // Assert: working_blob = NULL, working_dirty = 0
        var recordAfter = await project.GetDiskImageAsync(diskId);
        Assert.False(recordAfter.WorkingDirty);
        
        // Next checkout should come from original_blob
        var regeneratedImage = await project.CheckOutAsync(diskId);
        Assert.NotNull(regeneratedImage);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a temporary test project in memory.
    /// </summary>
    private async Task<SkilletProject> CreateTestProjectAsync()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.skillet");
        _output.WriteLine($"Test project: {tempPath}");

        var manager = new SkilletProjectManager();
        var project = await manager.CreateAsync(tempPath, "Test Project");
        return (SkilletProject)project;
    }

    /// <summary>
    /// Inserts a mock disk image (with serialized blob) into the test project.
    /// </summary>
    private async Task<long> InsertMockDiskImageAsync(SkilletProject project, string name = "Mock Disk")
    {
        // Create a minimal InternalDiskImage (35 tracks, sparse — only track 0 populated)
        var quarterTracks = new CommonUtil.CircularBitBuffer?[137];
        var quarterTrackBitCounts = new int[137];
        
        var trackData = new byte[6400];  // 51,200 bits = 6,400 bytes
        Array.Fill(trackData, (byte)0xFF);
        quarterTracks[0] = new CommonUtil.CircularBitBuffer(trackData, 0, 0, 51200);
        quarterTrackBitCounts[0] = 51200;

        var diskImage = new Pandowdy.EmuCore.DiskII.InternalDiskImage(35, quarterTracks, quarterTrackBitCounts);
        var blob = DiskBlobStore.Serialize(diskImage);

        // Insert directly into database (bypass ImportDiskImageAsync which requires real files)
        return await project.EnqueueAsync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO {SkilletConstants.TableDiskImages}
                    (name, original_filename, original_format, import_source_path, imported_utc,
                     whole_track_count, optimal_bit_timing, is_write_protected, persist_working,
                     original_blob, working_blob, working_dirty, created_utc, modified_utc)
                VALUES
                    (@name, 'mock.nib', 'Nib', '/mock/path', @now,
                     35, 32, 0, 1,
                     @blob, @blob, 0, @now, @now);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@blob", blob);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            
            return Convert.ToInt64(cmd.ExecuteScalar());
        });
    }

    #endregion
}
