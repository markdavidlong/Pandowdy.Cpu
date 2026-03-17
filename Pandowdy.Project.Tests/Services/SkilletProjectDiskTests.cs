// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.Project.Constants;
using Pandowdy.Project.Models;
using Pandowdy.Project.Services;
using Xunit.Abstractions;

namespace Pandowdy.Project.Tests.Services;

/// <summary>
/// Tests for <see cref="SkilletProject"/> disk image CRUD and IDiskImageStore implementation.
/// Uses <see cref="SkilletProject.CreateForTest"/> to inject pre-built PIDI blobs
/// rather than requiring real disk files or a database.
/// </summary>
public class SkilletProjectDiskTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region ImportDiskImageAsync

    [Fact]
    public async Task ImportDiskImageAsync_UnsupportedFormat_ThrowsArgumentException()
    {
        // Arrange
        using var project = SkilletProject.CreateNew("Test Project");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await project.ImportDiskImageAsync("test.xyz", "Test Disk"));
    }

    #endregion

    #region IDiskImageStore (CheckOutAsync / ReturnAsync)

    [Fact]
    public async Task CheckOutAsync_ExistingDiskImage_ReturnsDeserializedImage()
    {
        // Arrange
        var (project, diskId) = CreateTestProjectWithDisk();
        using (project)
        {
            // Act
            var diskImage = await project.CheckOutAsync(diskId);

            // Assert
            Assert.NotNull(diskImage);
            Assert.Equal(35, diskImage.PhysicalTrackCount);
            Assert.Equal(137, diskImage.QuarterTrackCount);
        }
    }

    [Fact]
    public async Task CheckOutAsync_NonexistentDiskImage_ThrowsInvalidOperationException()
    {
        // Arrange
        using var project = SkilletProject.CreateNew("Test Project");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await project.CheckOutAsync(diskImageId: 999));
    }

    [Fact]
    public async Task CheckOutAsync_AlreadyCheckedOut_ThrowsInvalidOperationException()
    {
        // Arrange
        var (project, diskId) = CreateTestProjectWithDisk();
        using (project)
        {
            var _ = await project.CheckOutAsync(diskId);

            // Act & Assert — double checkout is not allowed
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await project.CheckOutAsync(diskId));
        }
    }

    [Fact]
    public async Task ReturnAsync_ModifiedDiskImage_SerializesToWorkingBlob()
    {
        // Arrange
        var (project, diskId) = CreateTestProjectWithDisk();
        using (project)
        {
            var diskImage = await project.CheckOutAsync(diskId);

            // Mark dirty without requiring direct CircularBitBuffer access
            diskImage.MarkDirty();

            // Act
            await project.ReturnAsync(diskId, diskImage);

            // Assert: working blob written, HighestWorkingVersion incremented
            var record = await project.GetDiskImageAsync(diskId);
            Assert.True(record.HighestWorkingVersion > 0,
                "HighestWorkingVersion should be > 0 after returning a dirty image");
        }
    }

    [Fact]
    public async Task ReturnAsync_DiscardChangesDisk_DoesNotIncrementWorkingVersion()
    {
        // Arrange
        var (project, diskId) = CreateTestProjectWithDisk(savePolicy: SavePolicy.DiscardChanges);
        using (project)
        {
            var diskImage = await project.CheckOutAsync(diskId);
            diskImage.MarkDirty();

            // Act
            await project.ReturnAsync(diskId, diskImage);

            // Assert: DiscardChanges policy means blob is never written
            var record = await project.GetDiskImageAsync(diskId);
            Assert.Equal(0, record.HighestWorkingVersion);
        }
    }

    [Fact]
    public async Task ReturnAsync_CleanDiskImage_DoesNotIncrementWorkingVersion()
    {
        // Arrange
        var (project, diskId) = CreateTestProjectWithDisk();
        using (project)
        {
            var diskImage = await project.CheckOutAsync(diskId);
            // Do NOT mark dirty

            // Act
            await project.ReturnAsync(diskId, diskImage);

            // Assert: clean image doesn't bump version
            var record = await project.GetDiskImageAsync(diskId);
            Assert.Equal(0, record.HighestWorkingVersion);
        }
    }

    #endregion

    #region GetDiskImageAsync / GetAllDiskImagesAsync

    [Fact]
    public async Task GetDiskImageAsync_ExistingDisk_ReturnsRecord()
    {
        // Arrange
        var (project, diskId) = CreateTestProjectWithDisk(name: "Test Disk");
        using (project)
        {
            // Act
            var record = await project.GetDiskImageAsync(diskId);

            // Assert
            Assert.NotNull(record);
            Assert.Equal("Test Disk", record.Name);
            Assert.Equal("Nib", record.OriginalFormat);
        }
    }

    [Fact]
    public async Task GetAllDiskImagesAsync_MultipleDiskImages_ReturnsAll()
    {
        // Arrange
        var (project, _) = CreateTestProjectWithDisks(["Disk 1", "Disk 2", "Disk 3"]);
        using (project)
        {
            // Act
            var allDisks = await project.GetAllDiskImagesAsync();

            // Assert
            Assert.Equal(3, allDisks.Count);
            Assert.Contains(allDisks, d => d.Name == "Disk 1");
            Assert.Contains(allDisks, d => d.Name == "Disk 2");
            Assert.Contains(allDisks, d => d.Name == "Disk 3");
        }
    }

    [Fact]
    public async Task GetAllDiskImagesAsync_EmptyProject_ReturnsEmpty()
    {
        using var project = SkilletProject.CreateNew("Test Project");
        var allDisks = await project.GetAllDiskImagesAsync();
        Assert.Empty(allDisks);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test project containing a single pre-serialized mock disk.
    /// Uses <see cref="SkilletProject.CreateForTest"/> — no file system or real import needed.
    /// </summary>
    private static (SkilletProject Project, long DiskId) CreateTestProjectWithDisk(
        string name = "Mock Disk",
        SavePolicy savePolicy = SavePolicy.OverwriteLatest)
    {
        const long diskId = 1L;
        var blob = DiskBlobStore.Serialize(new InternalDiskImage(35));
        var record = BuildDiskRecord(diskId, name, savePolicy);
        var manifest = BuildManifest([record]);
        var blobs = new Dictionary<(long DiskId, int Version), byte[]> { { (diskId, 0), blob } };
        return (SkilletProject.CreateForTest(manifest, blobs), diskId);
    }

    /// <summary>
    /// Creates a test project containing multiple pre-serialized mock disks with distinct names.
    /// </summary>
    private static (SkilletProject Project, IReadOnlyList<long> DiskIds) CreateTestProjectWithDisks(
        string[] names)
    {
        var blob = DiskBlobStore.Serialize(new InternalDiskImage(35));
        var records = names.Select((n, i) => BuildDiskRecord(i + 1, n)).ToList();
        var blobsDict = records.ToDictionary<DiskImageRecord, (long, int), byte[]>(
            r => (r.Id, 0), _ => blob);
        var manifest = BuildManifest(records);
        var ids = records.Select(r => r.Id).ToList();
        return (SkilletProject.CreateForTest(manifest, blobsDict), ids);
    }

    private static DiskImageRecord BuildDiskRecord(
        long id, string name,
        SavePolicy savePolicy = SavePolicy.OverwriteLatest) =>
        new DiskImageRecord
        {
            Id = id,
            Name = name,
            OriginalFilename = "mock.nib",
            OriginalFormat = "Nib",
            WholeTrackCount = 35,
            OptimalBitTiming = 32,
            SavePolicy = savePolicy,
            HighestWorkingVersion = 0,
            CreatedUtc = DateTime.UtcNow,
            ImportedUtc = DateTime.UtcNow
        };

    private static ProjectManifest BuildManifest(List<DiskImageRecord> diskImages) =>
        new ProjectManifest
        {
            Metadata = new ProjectMetadata(
                "Test Project", DateTime.UtcNow, ManifestConstants.CurrentSchemaVersion, "0.0.0"),
            DiskImages = diskImages,
            Attachments = [],
            MountConfigurations = [],
            EmulatorOverrides = new Dictionary<string, string>(),
            DisplayOverrides = new Dictionary<string, string>(),
            ProjectSettings = new Dictionary<string, string>(),
            Notes = string.Empty
        };

    #endregion
}
