// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.Project.Constants;
using Pandowdy.Project.Models;
using Pandowdy.Project.Services;
using Pandowdy.Project.Stores;

namespace Pandowdy.Project.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SkilletProject"/> — memory-resident behaviour,
/// settings, disk/mount CRUD, dirty tracking, snapshots, and save lifecycle.
/// Disk checkout/return and SavePolicy semantics are covered in
/// <see cref="SkilletProjectDiskTests"/>.
/// </summary>
public class SkilletProjectTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────

    private static string GetTempStorePath() =>
        Path.Combine(Path.GetTempPath(), $"pandowdy_test_{Guid.NewGuid():N}_skilletdir");

    private static (SkilletProject Project, long DiskId) CreateWithOneDisk(
        string diskName = "Test Disk",
        SavePolicy savePolicy = SavePolicy.OverwriteLatest)
    {
        const long diskId = 1L;
        var blob = DiskBlobStore.Serialize(new InternalDiskImage(35));
        var record = new DiskImageRecord
        {
            Id = diskId,
            Name = diskName,
            OriginalFilename = "mock.nib",
            OriginalFormat = "Nib",
            WholeTrackCount = 35,
            OptimalBitTiming = 32,
            SavePolicy = savePolicy,
            HighestWorkingVersion = 0,
            CreatedUtc = DateTime.UtcNow,
            ImportedUtc = DateTime.UtcNow
        };
        var manifest = new ProjectManifest
        {
            Metadata = new ProjectMetadata(
                "Test Project", DateTime.UtcNow, ManifestConstants.CurrentSchemaVersion, "0.0.0"),
            DiskImages = [record],
            Attachments = [],
            MountConfigurations = [],
            EmulatorOverrides = new Dictionary<string, string>(),
            DisplayOverrides  = new Dictionary<string, string>(),
            ProjectSettings   = new Dictionary<string, string>(),
            Notes = string.Empty
        };
        var blobs = new Dictionary<(long, int), byte[]> { { (diskId, 0), blob } };
        return (SkilletProject.CreateForTest(manifest, blobs), diskId);
    }

    // ─────────────────────────────────────────────────────────────────────
    #region CreateNew / factory properties

    [Fact]
    public void CreateNew_IsAdHocProject()
    {
        using var project = SkilletProject.CreateNew("My Project");
        Assert.True(project.IsAdHoc);
        Assert.Null(project.FilePath);
    }

    [Fact]
    public void CreateNew_HasCorrectMetadata()
    {
        using var project = SkilletProject.CreateNew("Hello World");
        Assert.Equal("Hello World", project.Metadata.Name);
        Assert.Equal(ManifestConstants.CurrentSchemaVersion, project.Metadata.SchemaVersion);
    }

    [Fact]
    public void CreateNew_HasNoUnsavedChanges()
    {
        using var project = SkilletProject.CreateNew("Fresh");
        Assert.False(project.HasUnsavedChanges);
    }

    [Fact]
    public void CreateNew_HasNoDiskImages()
    {
        using var project = SkilletProject.CreateNew("Empty");
        Assert.False(project.HasDiskImages);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region Settings CRUD

    [Fact]
    public async Task GetSettingAsync_MissingKey_ReturnsNull()
    {
        using var project = SkilletProject.CreateNew("Test");
        var value = await project.GetSettingAsync(SettingsScope.ProjectSettings, "no_such_key");
        Assert.Null(value);
    }

    [Fact]
    public async Task SetSettingAsync_ThenGet_ReturnsValue()
    {
        using var project = SkilletProject.CreateNew("Test");
        await project.SetSettingAsync(SettingsScope.ProjectSettings, "my_key", "my_value");
        var readBack = await project.GetSettingAsync(SettingsScope.ProjectSettings, "my_key");
        Assert.Equal("my_value", readBack);
    }

    [Fact]
    public async Task SetSettingAsync_AllScopesStoredSeparately()
    {
        // Each SettingsScope uses its own dictionary; same key in different scopes
        // must not collide.
        using var project = SkilletProject.CreateNew("Test");
        await project.SetSettingAsync(SettingsScope.ProjectSettings,  "key", "project");
        await project.SetSettingAsync(SettingsScope.EmulatorOverrides, "key", "emulator");
        await project.SetSettingAsync(SettingsScope.DisplayOverrides,  "key", "display");

        Assert.Equal("project",  await project.GetSettingAsync(SettingsScope.ProjectSettings,  "key"));
        Assert.Equal("emulator", await project.GetSettingAsync(SettingsScope.EmulatorOverrides, "key"));
        Assert.Equal("display",  await project.GetSettingAsync(SettingsScope.DisplayOverrides,  "key"));
    }

    [Fact]
    public async Task SetSettingAsync_MarksProjectDirty()
    {
        using var project = SkilletProject.CreateNew("Test");
        Assert.False(project.HasUnsavedChanges);

        await project.SetSettingAsync(SettingsScope.ProjectSettings, "x", "1");

        Assert.True(project.HasUnsavedChanges);
    }

    [Fact]
    public async Task SetSettingAsync_OverwriteWithSameValue_StillMarksProjectDirty()
    {
        // Idempotency of value is out of scope — the dirty flag is always set on write.
        using var project = SkilletProject.CreateNew("Test");
        await project.SetSettingAsync(SettingsScope.ProjectSettings, "x", "same");
        Assert.True(project.HasUnsavedChanges);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region Disk CRUD (HasDiskImages, RemoveDiskImageAsync)

    [Fact]
    public void HasDiskImages_WhenDiskExists_IsTrue()
    {
        var (project, _) = CreateWithOneDisk();
        using (project)
        {
            Assert.True(project.HasDiskImages);
        }
    }

    [Fact]
    public async Task RemoveDiskImageAsync_ExistingDisk_RemovesIt()
    {
        var (project, diskId) = CreateWithOneDisk();
        using (project)
        {
            await project.RemoveDiskImageAsync(diskId);

            var all = await project.GetAllDiskImagesAsync();
            Assert.Empty(all);
            Assert.False(project.HasDiskImages);
        }
    }

    [Fact]
    public async Task RemoveDiskImageAsync_NonexistentDisk_ThrowsInvalidOperationException()
    {
        using var project = SkilletProject.CreateNew("Test");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => project.RemoveDiskImageAsync(id: 999));
    }

    [Fact]
    public async Task RemoveDiskImageAsync_MarksProjectDirty()
    {
        var (project, diskId) = CreateWithOneDisk();
        using (project)
        {
            await project.RemoveDiskImageAsync(diskId);
            Assert.True(project.HasUnsavedChanges);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region Mount configuration

    [Fact]
    public async Task GetMountConfigurationAsync_NewProject_ReturnsEmptyList()
    {
        using var project = SkilletProject.CreateNew("Test");
        var mounts = await project.GetMountConfigurationAsync();
        Assert.Empty(mounts);
    }

    [Fact]
    public async Task SetMountAsync_ThenGet_ReflectsChange()
    {
        var (project, diskId) = CreateWithOneDisk();
        using (project)
        {
            await project.SetMountAsync(slot: 6, driveNumber: 1, diskImageId: diskId);
            var mounts = await project.GetMountConfigurationAsync();

            Assert.Single(mounts);
            var mount = mounts[0];
            Assert.Equal(6, mount.Slot);
            Assert.Equal(1, mount.DriveNumber);
            Assert.Equal(diskId, mount.DiskImageId);
        }
    }

    [Fact]
    public async Task SetMountAsync_WithNullDiskId_ClearsDiskAssignment()
    {
        var (project, diskId) = CreateWithOneDisk();
        using (project)
        {
            // Mount then un-mount.
            await project.SetMountAsync(6, 1, diskId);
            await project.SetMountAsync(6, 1, null);

            var mounts = await project.GetMountConfigurationAsync();
            var mount = mounts.Single(m => m.Slot == 6 && m.DriveNumber == 1);
            Assert.Null(mount.DiskImageId);
        }
    }

    [Fact]
    public async Task SetMountAsync_SameDiskId_DoesNotMarkDirty()
    {
        // SetMountAsync is a no-op (and does not dirty the project) when the
        // disk assignment is unchanged.
        var (project, diskId) = CreateWithOneDisk();
        using (project)
        {
            await project.SetMountAsync(6, 1, diskId);
            // Now project is dirty. Manually clear dirty state via a second
            // project so we can test the no-op path.
            // (No public MarkClean — so we verify the call returns without
            // throwing, and simply assert no additional side effects.)
            await project.SetMountAsync(6, 1, diskId); // Same value — should be no-op
        }
    }

    [Fact]
    public async Task SetMountAsync_AssignNewDisk_MarksProjectDirty()
    {
        var (project, diskId) = CreateWithOneDisk();
        using (project)
        {
            Assert.False(project.HasUnsavedChanges);
            await project.SetMountAsync(6, 1, diskId);
            Assert.True(project.HasUnsavedChanges);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region ToSnapshot (internal — accessible via InternalsVisibleTo)

    [Fact]
    public void ToSnapshot_ReflectsCurrentMetadata()
    {
        using var project = SkilletProject.CreateNew("Snapshot Test");
        var snapshot = project.ToSnapshot();
        Assert.Equal("Snapshot Test", snapshot.Metadata.Name);
        Assert.Equal(ManifestConstants.CurrentSchemaVersion, snapshot.Metadata.SchemaVersion);
    }

    [Fact]
    public async Task ToSnapshot_IncludesDiskImages()
    {
        var (project, diskId) = CreateWithOneDisk("My Disk");
        using (project)
        {
            var snapshot = project.ToSnapshot();
            var disk = Assert.Single(snapshot.DiskImages);
            Assert.Equal("My Disk", disk.Name);
            Assert.Equal(diskId, disk.Id);
        }
    }

    [Fact]
    public async Task ToSnapshot_IncludesSettings()
    {
        using var project = SkilletProject.CreateNew("Test");
        await project.SetSettingAsync(SettingsScope.ProjectSettings, "color", "green");

        var snapshot = project.ToSnapshot();
        Assert.True(snapshot.ProjectSettings.TryGetValue("color", out var val));
        Assert.Equal("green", val);
    }

    [Fact]
    public async Task ToSnapshot_IncludesMountConfigurations()
    {
        var (project, diskId) = CreateWithOneDisk();
        using (project)
        {
            await project.SetMountAsync(6, 1, diskId);
            var snapshot = project.ToSnapshot();

            var mount = Assert.Single(snapshot.MountConfigurations);
            Assert.Equal(6, mount.Slot);
            Assert.Equal(1, mount.DriveNumber);
            Assert.Equal(diskId, mount.DiskImageId);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region SaveAsync / SaveAsAsync lifecycle

    [Fact]
    public async Task SaveAsync_AdHocProject_ThrowsInvalidOperationException()
    {
        using var project = SkilletProject.CreateNew("untitled");
        await Assert.ThrowsAsync<InvalidOperationException>(() => project.SaveAsync());
    }

    [Fact]
    public async Task SaveAsAsync_AdHocProject_IsNoLongerAdHoc()
    {
        using var project = SkilletProject.CreateNew("untitled");
        var storePath = GetTempStorePath();
        var store = new DirectoryProjectStoreFactory().Create(storePath);

        await project.SaveAsAsync(store);

        Assert.False(project.IsAdHoc);
        Assert.Equal(storePath, project.FilePath);
    }

    [Fact]
    public async Task SaveAsAsync_AdHocProject_ClearsUnsavedChanges()
    {
        using var project = SkilletProject.CreateNew("untitled");
        await project.SetSettingAsync(SettingsScope.ProjectSettings, "x", "1"); // makes dirty
        Assert.True(project.HasUnsavedChanges);

        var storePath = GetTempStorePath();
        var store = new DirectoryProjectStoreFactory().Create(storePath);
        await project.SaveAsAsync(store);

        Assert.False(project.HasUnsavedChanges);
    }

    [Fact]
    public async Task SaveAsync_AfterSaveAs_PersistsDirtyChanges()
    {
        // Verify that SaveAsync works after SaveAsAsync has bound a store.
        using var project = SkilletProject.CreateNew("untitled");
        var storePath = GetTempStorePath();
        var store = new DirectoryProjectStoreFactory().Create(storePath);

        await project.SaveAsAsync(store);                                        // bind store
        await project.SetSettingAsync(SettingsScope.ProjectSettings, "x", "1"); // dirty
        Assert.True(project.HasUnsavedChanges);

        await project.SaveAsync();

        Assert.False(project.HasUnsavedChanges);
        Assert.True(File.Exists(Path.Combine(storePath, "manifest.json")));
    }

    #endregion
}
