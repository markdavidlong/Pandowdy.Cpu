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
/// Tests for <see cref="ProjectSnapshot"/> fidelity:
/// <see cref="SkilletProject.ToSnapshot()"/> content, blob dictionary structure,
/// attachment data structure, and <see cref="SkilletProject.FromManifest"/> validation.
/// </summary>
public class ProjectSnapshotTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────

    private static byte[] MakeBlob() =>
        DiskBlobStore.Serialize(new InternalDiskImage(35));

    private static string GetTempStorePath() =>
        Path.Combine(Path.GetTempPath(), $"pandowdy_snap_test_{Guid.NewGuid():N}_skilletdir");

    private static ProjectManifest BuildManifest(
        string projectName = "Snapshot Project",
        List<DiskImageRecord>? disks = null,
        List<MountConfiguration>? mounts = null,
        Dictionary<string, string>? projectSettings = null,
        Dictionary<string, string>? emulatorOverrides = null,
        Dictionary<string, string>? displayOverrides = null,
        string notes = "") =>
        new ProjectManifest
        {
            Metadata = new ProjectMetadata(
                projectName, DateTime.UtcNow, ManifestConstants.CurrentSchemaVersion, "0.0.0"),
            DiskImages           = disks            ?? [],
            Attachments          = [],
            MountConfigurations  = mounts           ?? [],
            EmulatorOverrides    = emulatorOverrides ?? new Dictionary<string, string>(),
            DisplayOverrides     = displayOverrides  ?? new Dictionary<string, string>(),
            ProjectSettings      = projectSettings   ?? new Dictionary<string, string>(),
            Notes                = notes
        };

    private static DiskImageRecord MakeDiskRecord(long id, string name = "Disk") =>
        new DiskImageRecord
        {
            Id = id, Name = name, OriginalFilename = "mock.nib", OriginalFormat = "Nib",
            WholeTrackCount = 35, OptimalBitTiming = 32,
            SavePolicy = SavePolicy.OverwriteLatest,
            HighestWorkingVersion = 0,
            CreatedUtc = DateTime.UtcNow, ImportedUtc = DateTime.UtcNow
        };

    // ─────────────────────────────────────────────────────────────────────
    #region ToSnapshot — metadata fidelity

    [Fact]
    public void ToSnapshot_Metadata_MatchesManifest()
    {
        var manifest = BuildManifest("My Project");
        using var project = SkilletProject.CreateForTest(manifest);

        var snapshot = project.ToSnapshot();

        Assert.Equal("My Project", snapshot.Metadata.Name);
        Assert.Equal(ManifestConstants.CurrentSchemaVersion, snapshot.Metadata.SchemaVersion);
        Assert.Equal(manifest.Metadata.CreatedUtc, snapshot.Metadata.CreatedUtc);
    }

    [Fact]
    public void ToSnapshot_Notes_MatchesManifest()
    {
        var manifest = BuildManifest(notes: "These are my notes.");
        using var project = SkilletProject.CreateForTest(manifest);

        var snapshot = project.ToSnapshot();

        Assert.Equal("These are my notes.", snapshot.Notes);
    }

    [Fact]
    public void ToSnapshot_EmptyProject_HasEmptyCollections()
    {
        using var project = SkilletProject.CreateNew("Empty");

        var snapshot = project.ToSnapshot();

        Assert.Empty(snapshot.DiskImages);
        Assert.Empty(snapshot.MountConfigurations);
        Assert.Empty(snapshot.Attachments);
        Assert.Empty(snapshot.Blobs);
        Assert.Empty(snapshot.AttachmentData);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region ToSnapshot — settings fidelity

    [Fact]
    public async Task ToSnapshot_ProjectSettings_RoundTrip()
    {
        using var project = SkilletProject.CreateNew("Test");
        await project.SetSettingAsync(SettingsScope.ProjectSettings, "color", "blue");

        var snapshot = project.ToSnapshot();

        Assert.True(snapshot.ProjectSettings.TryGetValue("color", out var val));
        Assert.Equal("blue", val);
    }

    [Fact]
    public async Task ToSnapshot_EmulatorOverrides_RoundTrip()
    {
        using var project = SkilletProject.CreateNew("Test");
        await project.SetSettingAsync(SettingsScope.EmulatorOverrides, "speed", "fast");

        var snapshot = project.ToSnapshot();

        Assert.True(snapshot.EmulatorOverrides.TryGetValue("speed", out var val));
        Assert.Equal("fast", val);
    }

    [Fact]
    public async Task ToSnapshot_DisplayOverrides_RoundTrip()
    {
        using var project = SkilletProject.CreateNew("Test");
        await project.SetSettingAsync(SettingsScope.DisplayOverrides, "scanlines", "on");

        var snapshot = project.ToSnapshot();

        Assert.True(snapshot.DisplayOverrides.TryGetValue("scanlines", out var val));
        Assert.Equal("on", val);
    }

    [Fact]
    public async Task ToSnapshot_AllScopesDontCrossContaminate()
    {
        using var project = SkilletProject.CreateNew("Test");
        await project.SetSettingAsync(SettingsScope.ProjectSettings,   "k", "project");
        await project.SetSettingAsync(SettingsScope.EmulatorOverrides, "k", "emulator");
        await project.SetSettingAsync(SettingsScope.DisplayOverrides,  "k", "display");

        var snapshot = project.ToSnapshot();

        Assert.Equal("project",  snapshot.ProjectSettings["k"]);
        Assert.Equal("emulator", snapshot.EmulatorOverrides["k"]);
        Assert.Equal("display",  snapshot.DisplayOverrides["k"]);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region ToSnapshot — disk images and blob dictionary

    [Fact]
    public void ToSnapshot_DiskImages_MatchManifest()
    {
        var disk1 = MakeDiskRecord(1, "Boot Disk");
        var disk2 = MakeDiskRecord(2, "Data Disk");
        var manifest = BuildManifest(disks: [disk1, disk2]);
        var blob = MakeBlob();
        var blobs = new Dictionary<(long, int), byte[]>
        {
            { (1, 0), blob },
            { (2, 0), blob }
        };
        using var project = SkilletProject.CreateForTest(manifest, blobs);

        var snapshot = project.ToSnapshot();

        Assert.Equal(2, snapshot.DiskImages.Count);
        Assert.Contains(snapshot.DiskImages, d => d.Name == "Boot Disk" && d.Id == 1);
        Assert.Contains(snapshot.DiskImages, d => d.Name == "Data Disk" && d.Id == 2);
    }

    [Fact]
    public void ToSnapshot_Blobs_KeyedByDiskIdAndVersion()
    {
        // Blobs dict must use (diskImageId, version) composite key.
        // Version 0 is the original import blob.
        var disk = MakeDiskRecord(42, "Test Disk");
        var manifest = BuildManifest(disks: [disk]);
        var blob = MakeBlob();
        var blobs = new Dictionary<(long, int), byte[]> { { (42, 0), blob } };
        using var project = SkilletProject.CreateForTest(manifest, blobs);

        var snapshot = project.ToSnapshot();

        Assert.True(snapshot.Blobs.ContainsKey((42, 0)),
            "Snapshot blobs should contain key (diskId=42, version=0)");
        Assert.Equal(blob.Length, snapshot.Blobs[(42, 0)].Length);
    }

    [Fact]
    public async Task ToSnapshot_AfterReturn_BlobsContainWorkingVersion()
    {
        // After a dirty checkout is returned, ToSnapshot() should include the new
        // working-version blob in addition to the original.
        var disk = MakeDiskRecord(1);
        var manifest = BuildManifest(disks: [disk]);
        var blob = MakeBlob();
        var blobs = new Dictionary<(long, int), byte[]> { { (1, 0), blob } };
        using var project = SkilletProject.CreateForTest(manifest, blobs);

        var image = await project.CheckOutAsync(1);
        image.MarkDirty();
        await project.ReturnAsync(1, image);

        var snapshot = project.ToSnapshot();

        // After return, the current working version (1) should be in the blobs dict.
        Assert.True(snapshot.Blobs.ContainsKey((1, 0)), "Original blob (v0) must still be present");
        Assert.True(snapshot.Blobs.ContainsKey((1, 1)), "Working blob (v1) must be present after dirty return");
    }

    [Fact]
    public void ToSnapshot_MultipleDisks_BlobsHaveCorrectDiskIds()
    {
        var disk1 = MakeDiskRecord(10);
        var disk2 = MakeDiskRecord(20);
        var manifest = BuildManifest(disks: [disk1, disk2]);
        var blob1 = MakeBlob();
        var blob2 = MakeBlob();
        var blobs = new Dictionary<(long, int), byte[]>
        {
            { (10, 0), blob1 },
            { (20, 0), blob2 }
        };
        using var project = SkilletProject.CreateForTest(manifest, blobs);

        var snapshot = project.ToSnapshot();

        Assert.True(snapshot.Blobs.ContainsKey((10, 0)));
        Assert.True(snapshot.Blobs.ContainsKey((20, 0)));
        Assert.Equal(2, snapshot.Blobs.Count);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region ToSnapshot — attachment data structure

    [Fact]
    public void ToSnapshot_AttachmentData_KeyedByAttachmentId()
    {
        var attachment = new AttachmentRecord
        {
            Id = 7L,
            Name = "readme.txt",
            ContentType = "text/plain",
            SizeBytes = 5,
            CreatedUtc = DateTime.UtcNow
        };
        var attachmentBytes = "hello"u8.ToArray();
        var manifest = BuildManifest();
        manifest = new ProjectManifest
        {
            Metadata            = manifest.Metadata,
            DiskImages          = manifest.DiskImages,
            Attachments         = [attachment],
            MountConfigurations = manifest.MountConfigurations,
            EmulatorOverrides   = manifest.EmulatorOverrides,
            DisplayOverrides    = manifest.DisplayOverrides,
            ProjectSettings     = manifest.ProjectSettings,
            Notes               = manifest.Notes
        };
        var attachmentData = new Dictionary<long, byte[]> { { 7L, attachmentBytes } };
        using var project = SkilletProject.CreateForTest(manifest, attachmentData: attachmentData);

        var snapshot = project.ToSnapshot();

        Assert.True(snapshot.AttachmentData.ContainsKey(7L),
            "AttachmentData should be keyed by attachmentId");
        Assert.Equal(attachmentBytes, snapshot.AttachmentData[7L]);
    }

    [Fact]
    public void ToSnapshot_NoAttachments_AttachmentDataIsEmpty()
    {
        using var project = SkilletProject.CreateNew("Test");
        var snapshot = project.ToSnapshot();
        Assert.Empty(snapshot.AttachmentData);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region ToSnapshot → FromManifest round-trip

    [Fact]
    public async Task ToSnapshot_ThenFromManifest_PreservesAllMetadata()
    {
        // Build a rich manifest, snapshot it, then reconstruct from the snapshot's manifest.
        var disk = MakeDiskRecord(1, "Round-trip Disk");
        var mountConfig = new MountConfiguration(Id: 0, Slot: 6, DriveNumber: 1, DiskImageId: 1, AutoMount: true);
        var original = BuildManifest(
            projectName: "RT Project",
            disks: [disk],
            mounts: [mountConfig],
            projectSettings: new Dictionary<string, string> { { "theme", "dark" } },
            notes: "Some notes");

        var blob = MakeBlob();
        var blobs = new Dictionary<(long, int), byte[]> { { (1, 0), blob } };
        using var source = SkilletProject.CreateForTest(original, blobs);

        var snapshot = source.ToSnapshot();

        // Rebuild a manifest from the snapshot's fields (simulating what DirectoryProjectStore does).
        var rebuilt = new ProjectManifest
        {
            Metadata            = snapshot.Metadata,
            DiskImages          = snapshot.DiskImages,
            Attachments         = snapshot.Attachments,
            MountConfigurations = snapshot.MountConfigurations,
            EmulatorOverrides   = snapshot.EmulatorOverrides,
            DisplayOverrides    = snapshot.DisplayOverrides,
            ProjectSettings     = snapshot.ProjectSettings,
            Notes               = snapshot.Notes ?? string.Empty
        };

        using var restored = SkilletProject.CreateForTest(rebuilt, snapshot.Blobs);

        // Assert — metadata
        Assert.Equal("RT Project", restored.Metadata.Name);
        Assert.Equal(source.Metadata.CreatedUtc, restored.Metadata.CreatedUtc);

        // Assert — disk images
        var allDisks = await restored.GetAllDiskImagesAsync();
        Assert.Single(allDisks);
        Assert.Equal("Round-trip Disk", allDisks[0].Name);

        // Assert — mount config
        var mounts = await restored.GetMountConfigurationAsync();
        Assert.Single(mounts);
        Assert.Equal(6, mounts[0].Slot);
        Assert.Equal(1L, mounts[0].DiskImageId);

        // Assert — settings
        var theme = await restored.GetSettingAsync(SettingsScope.ProjectSettings, "theme");
        Assert.Equal("dark", theme);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region FromManifest — validation

    [Fact]
    public void FromManifest_DuplicateDiskIds_ThrowsInvalidOperationException()
    {
        var disk1 = MakeDiskRecord(5, "Disk A");
        var disk2 = MakeDiskRecord(5, "Disk B");   // same ID as disk1
        var manifest = BuildManifest(disks: [disk1, disk2]);
        var storePath = GetTempStorePath();
        var store = new DirectoryProjectStoreFactory().Create(storePath);

        Assert.Throws<InvalidOperationException>(() =>
            SkilletProject.FromManifest(manifest, store));
    }

    [Fact]
    public void FromManifest_NegativeDiskId_ThrowsInvalidOperationException()
    {
        var badDisk = MakeDiskRecord(-1, "Bad Disk");
        var manifest = BuildManifest(disks: [badDisk]);
        var storePath = GetTempStorePath();
        var store = new DirectoryProjectStoreFactory().Create(storePath);

        Assert.Throws<InvalidOperationException>(() =>
            SkilletProject.FromManifest(manifest, store));
    }

    [Fact]
    public void FromManifest_MountReferencesNonexistentDisk_ThrowsInvalidOperationException()
    {
        var disk = MakeDiskRecord(1);
        var badMount = new MountConfiguration(Id: 0, Slot: 6, DriveNumber: 1, DiskImageId: 999, AutoMount: true);
        var manifest = BuildManifest(disks: [disk], mounts: [badMount]);
        var storePath = GetTempStorePath();
        var store = new DirectoryProjectStoreFactory().Create(storePath);

        Assert.Throws<InvalidOperationException>(() =>
            SkilletProject.FromManifest(manifest, store));
    }

    [Fact]
    public void FromManifest_ValidManifest_CreatesProjectWithCorrectFilePath()
    {
        var manifest = BuildManifest("Valid Project");
        var storePath = GetTempStorePath();
        var store = new DirectoryProjectStoreFactory().Create(storePath);

        using var project = SkilletProject.FromManifest(manifest, store);

        Assert.False(project.IsAdHoc);
        Assert.Equal(storePath, project.FilePath);
        Assert.Equal("Valid Project", project.Metadata.Name);
    }

    #endregion
}
