// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Text.Json;
using Pandowdy.Project.Constants;
using Pandowdy.Project.Models;
using Pandowdy.Project.Stores;

namespace Pandowdy.Project.Tests.Stores;

/// <summary>
/// Tests for <see cref="DirectoryProjectStore"/> and <see cref="DirectoryProjectStoreFactory"/>.
/// Each test runs in an isolated temporary directory that is cleaned up on disposal.
/// </summary>
public sealed class DirectoryProjectStoreTests : IDisposable
{
    private readonly string _tempRoot;

    public DirectoryProjectStoreTests()
    {
        _tempRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"pandowdy_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private string NewProjectPath(string name = "proj") =>
        System.IO.Path.Combine(_tempRoot, name);

    private static ProjectMetadata MakeMetadata(string name = "Test Project") =>
        new(name, DateTime.UtcNow, ManifestConstants.CurrentSchemaVersion, "0.0.0");

    private static DiskImageRecord MakeDisk(long id, int hwv = 0) =>
        new()
        {
            Id = id,
            Name = $"Disk {id}",
            OriginalFormat = "nib",
            HighestWorkingVersion = hwv
        };

    private static AttachmentRecord MakeAttachment(long id) =>
        new()
        {
            Id = id,
            Name = $"Attachment {id}",
            CreatedUtc = DateTime.UtcNow
        };

    private static ProjectSnapshot MakeSnapshot(
        string path,
        List<DiskImageRecord>? disks = null,
        Dictionary<(long Id, int Version), byte[]>? blobs = null,
        List<AttachmentRecord>? attachments = null,
        Dictionary<long, byte[]>? attachmentData = null,
        List<MountConfiguration>? mounts = null,
        string notes = "") =>
        new()
        {
            Metadata = MakeMetadata(),
            EmulatorOverrides = new Dictionary<string, string>(),
            DisplayOverrides = new Dictionary<string, string>(),
            ProjectSettings = new Dictionary<string, string>(),
            DiskImages = disks ?? new List<DiskImageRecord>(),
            Blobs = blobs ?? new Dictionary<(long Id, int Version), byte[]>(),
            Attachments = attachments ?? new List<AttachmentRecord>(),
            AttachmentData = attachmentData ?? new Dictionary<long, byte[]>(),
            MountConfigurations = mounts ?? new List<MountConfiguration>(),
            Notes = notes
        };

    private static DirectoryProjectStore OpenStore(string path) =>
        new(path);

    // ─── Manifest round-trip ──────────────────────────────────────────────

    #region LoadManifest / Save round-trip

    [Fact]
    public void Save_ThenLoadManifest_RoundTripsAllFields()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(1);
        var attachment = MakeAttachment(42);
        var mount = new MountConfiguration(Id: 1, Slot: 6, DriveNumber: 1, DiskImageId: 1, AutoMount: true);
        var emulatorOverrides = new Dictionary<string, string> { ["CpuSpeed"] = "FastMode" };
        var displayOverrides  = new Dictionary<string, string> { ["Scanlines"] = "on" };
        var projectSettings   = new Dictionary<string, string> { ["Theme"] = "dark" };

        var snapshot = new ProjectSnapshot
        {
            Metadata = MakeMetadata("My Apple Project"),
            EmulatorOverrides = emulatorOverrides,
            DisplayOverrides = displayOverrides,
            ProjectSettings = projectSettings,
            DiskImages = new List<DiskImageRecord> { disk },
            Blobs = new Dictionary<(long Id, int Version), byte[]>(),
            Attachments = new List<AttachmentRecord> { attachment },
            AttachmentData = new Dictionary<long, byte[]>(),
            MountConfigurations = new List<MountConfiguration> { mount },
            Notes = "Some notes here."
        };

        using var store = OpenStore(projPath);

        // Act
        store.Save(snapshot);
        var manifest = store.LoadManifest();

        // Assert: metadata
        Assert.Equal("My Apple Project", manifest.Metadata.Name);
        Assert.Equal(ManifestConstants.CurrentSchemaVersion, manifest.Metadata.SchemaVersion);

        // Assert: overrides and settings
        Assert.Equal("FastMode", manifest.EmulatorOverrides["CpuSpeed"]);
        Assert.Equal("on", manifest.DisplayOverrides["Scanlines"]);
        Assert.Equal("dark", manifest.ProjectSettings["Theme"]);

        // Assert: disk images
        Assert.Single(manifest.DiskImages);
        Assert.Equal(1L, manifest.DiskImages[0].Id);
        Assert.Equal("Disk 1", manifest.DiskImages[0].Name);

        // Assert: attachments
        Assert.Single(manifest.Attachments);
        Assert.Equal(42L, manifest.Attachments[0].Id);

        // Assert: mount configurations
        Assert.Single(manifest.MountConfigurations);
        Assert.Equal(6, manifest.MountConfigurations[0].Slot);

        // Assert: notes
        Assert.Equal("Some notes here.", manifest.Notes);
    }

    [Fact]
    public void LoadManifest_WrongSchemaVersion_ThrowsInvalidOperationException()
    {
        // Arrange: write a manifest.json with an unsupported schema version.
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var badManifest = new
        {
            Disclaimer = "",
            Metadata = new
            {
                Name = "Bad",
                CreatedUtc = DateTime.UtcNow,
                SchemaVersion = 99,
                PandowdyVersion = "0.0.0"
            },
            EmulatorOverrides = new { },
            DisplayOverrides = new { },
            ProjectSettings = new { },
            DiskImages = new object[] { },
            Attachments = new object[] { },
            MountConfigurations = new object[] { },
            Notes = ""
        };

        var json = JsonSerializer.Serialize(badManifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(System.IO.Path.Combine(projPath, "manifest.json"), json);

        using var store = OpenStore(projPath);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => store.LoadManifest());
    }

    [Fact]
    public void LoadManifest_MissingFile_ThrowsInvalidOperationException()
    {
        // Arrange: directory exists but no manifest.json
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        using var store = OpenStore(projPath);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => store.LoadManifest());
    }

    #endregion

    // ─── Blob round-trip ──────────────────────────────────────────────────

    #region LoadBlob / Save round-trip

    [Fact]
    public void Save_ThenLoadBlob_ReturnsSameBytes()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(7);
        var blobData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        var snapshot = MakeSnapshot(projPath,
            disks: new List<DiskImageRecord> { disk },
            blobs: new Dictionary<(long Id, int Version), byte[]>
            {
                { (7L, 0), blobData }
            });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        // Act
        var loaded = store.LoadBlob(7L, 0);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(blobData, loaded);
    }

    [Fact]
    public void LoadBlob_WithLatest_ReturnsHighestVersionBytes()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(3, hwv: 1);
        var v0Data = new byte[] { 0x00, 0x01 };
        var v1Data = new byte[] { 0xFF, 0xFE };

        var snapshot = MakeSnapshot(projPath,
            disks: new List<DiskImageRecord> { disk },
            blobs: new Dictionary<(long Id, int Version), byte[]>
            {
                { (3L, 0), v0Data },
                { (3L, 1), v1Data }
            });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        // Act
        var loaded = store.LoadBlob(3L, BlobVersion.Latest);

        // Assert: should be v1, the highest
        Assert.Equal(v1Data, loaded);
    }

    [Fact]
    public void LoadBlob_NonExistentVersion_ReturnsNull()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(5);
        var snapshot = MakeSnapshot(projPath, disks: new List<DiskImageRecord> { disk });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        // Act: request a version that was never written
        var loaded = store.LoadBlob(5L, 99);

        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public void LoadBlob_LatestWithNoFiles_ReturnsNull()
    {
        // Arrange: disk directory exists but no blobs for this id
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);
        Directory.CreateDirectory(System.IO.Path.Combine(projPath, "disks"));

        using var store = OpenStore(projPath);

        // Act
        var loaded = store.LoadBlob(99L, BlobVersion.Latest);

        // Assert
        Assert.Null(loaded);
    }

    #endregion

    // ─── SaveBlob ─────────────────────────────────────────────────────────

    #region SaveBlob

    [Fact]
    public void SaveBlob_CreateNewVersion_IncrementsHighestWorkingVersion()
    {
        // Arrange: start with disk at HWV=0
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(2, hwv: 0);
        var v0Data = new byte[] { 0xAA };

        var snapshot = MakeSnapshot(projPath,
            disks: new List<DiskImageRecord> { disk },
            blobs: new Dictionary<(long Id, int Version), byte[]>
            {
                { (2L, 0), v0Data }
            });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        var newVersionData = new byte[] { 0xBB, 0xCC };

        // Act
        store.SaveBlob(2L, newVersionData, BlobSaveMode.CreateNewVersion);

        // Assert: v1 file exists with the new data
        var v1Path = System.IO.Path.Combine(projPath, "disks", "2_v1.pidi");
        Assert.True(File.Exists(v1Path));
        Assert.Equal(newVersionData, File.ReadAllBytes(v1Path));

        // Assert: HWV updated in manifest
        var manifest = store.LoadManifest();
        Assert.Equal(1, manifest.DiskImages[0].HighestWorkingVersion);
    }

    [Fact]
    public void SaveBlob_OverwriteActive_OverwritesLatestVersion()
    {
        // Arrange: start with disk at HWV=1
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(4, hwv: 1);
        var v0Data = new byte[] { 0x01 };
        var v1Data = new byte[] { 0x02 };

        var snapshot = MakeSnapshot(projPath,
            disks: new List<DiskImageRecord> { disk },
            blobs: new Dictionary<(long Id, int Version), byte[]>
            {
                { (4L, 0), v0Data },
                { (4L, 1), v1Data }
            });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        var updatedData = new byte[] { 0xFF };

        // Act
        store.SaveBlob(4L, updatedData, BlobSaveMode.OverwriteActive);

        // Assert: v1 now has the updated data
        var v1Path = System.IO.Path.Combine(projPath, "disks", "4_v1.pidi");
        Assert.Equal(updatedData, File.ReadAllBytes(v1Path));

        // Assert: v0 is untouched
        var v0Path = System.IO.Path.Combine(projPath, "disks", "4_v0.pidi");
        Assert.Equal(v0Data, File.ReadAllBytes(v0Path));
    }

    [Fact]
    public void SaveBlob_PromptUser_WithoutConfirmation_Throws()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(10);
        disk.SavePolicy = SavePolicy.PromptUser;

        var snapshot = MakeSnapshot(projPath, disks: new List<DiskImageRecord> { disk });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => store.SaveBlob(10L, new byte[] { 0x01 }, userConfirmed: false));
    }

    [Fact]
    public void SaveBlob_PromptUser_WithConfirmation_Succeeds()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(11);
        disk.SavePolicy = SavePolicy.PromptUser;

        var snapshot = MakeSnapshot(projPath, disks: new List<DiskImageRecord> { disk });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        var data = new byte[] { 0xAB };

        // Act (should not throw)
        store.SaveBlob(11L, data, BlobSaveMode.CreateNewVersion, userConfirmed: true);

        // Assert: file written
        Assert.True(File.Exists(System.IO.Path.Combine(projPath, "disks", "11_v1.pidi")));
    }

    #endregion

    // ─── Orphan cleanup ───────────────────────────────────────────────────

    #region Orphan cleanup

    [Fact]
    public void Save_OrphanCleanup_DeletesUnreferencedBlobFiles()
    {
        // Arrange: first Save writes v0 for disk 1.
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(1, hwv: 0);
        var snapshot = MakeSnapshot(projPath,
            disks: new List<DiskImageRecord> { disk },
            blobs: new Dictionary<(long Id, int Version), byte[]>
            {
                { (1L, 0), new byte[] { 0x01 } }
            });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        // Plant an orphan blob file
        var orphanPath = System.IO.Path.Combine(projPath, "disks", "999_v0.pidi");
        File.WriteAllBytes(orphanPath, new byte[] { 0xFF });

        // Act: save again — orphan should be removed
        store.Save(snapshot);

        // Assert
        Assert.False(File.Exists(orphanPath), "Orphan blob should have been deleted.");
        Assert.True(File.Exists(System.IO.Path.Combine(projPath, "disks", "1_v0.pidi")),
                    "Legitimate blob should still exist.");
    }

    [Fact]
    public void Save_OrphanCleanup_DeletesUnreferencedAttachmentFiles()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var attachment = MakeAttachment(5);
        var attachData = new byte[] { 0xAA, 0xBB };

        var snapshot = MakeSnapshot(projPath,
            attachments: new List<AttachmentRecord> { attachment },
            attachmentData: new Dictionary<long, byte[]> { { 5L, attachData } });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        // Plant an orphan attachment file
        var orphanPath = System.IO.Path.Combine(projPath, "attachments", "999.dat");
        File.WriteAllBytes(orphanPath, new byte[] { 0xFF });

        // Act: save again
        store.Save(snapshot);

        // Assert
        Assert.False(File.Exists(orphanPath), "Orphan attachment should have been deleted.");
        Assert.True(File.Exists(System.IO.Path.Combine(projPath, "attachments", "5.dat")),
                    "Legitimate attachment should still exist.");
    }

    #endregion

    // ─── Attachment CRUD ──────────────────────────────────────────────────

    #region Attachment CRUD

    [Fact]
    public void SaveAttachment_ThenLoadAttachment_ReturnsSameBytes()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var data = new byte[] { 0x68, 0x65, 0x6C, 0x6C, 0x6F };
        using var store = OpenStore(projPath);

        // Act
        store.SaveAttachment(77L, data);
        var loaded = store.LoadAttachment(77L);

        // Assert
        Assert.Equal(data, loaded);
    }

    [Fact]
    public void LoadAttachment_MissingId_ReturnsNull()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        using var store = OpenStore(projPath);

        // Act
        var loaded = store.LoadAttachment(999L);

        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public void DeleteAttachment_ExistingId_RemovesFile()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        using var store = OpenStore(projPath);
        store.SaveAttachment(33L, new byte[] { 0x01 });

        // Act
        store.DeleteAttachment(33L);

        // Assert
        Assert.Null(store.LoadAttachment(33L));
    }

    [Fact]
    public void DeleteAttachment_MissingId_DoesNotThrow()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        using var store = OpenStore(projPath);

        // Act & Assert (should not throw)
        var ex = Record.Exception(() => store.DeleteAttachment(999L));
        Assert.Null(ex);
    }

    #endregion

    // ─── Factory ──────────────────────────────────────────────────────────

    #region Factory

    [Fact]
    public void Factory_Open_ValidDirectory_ReturnsStore()
    {
        // Arrange: set up a valid project directory with manifest.json
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(1);
        var snapshot = MakeSnapshot(projPath, disks: new List<DiskImageRecord> { disk });

        using (var setup = OpenStore(projPath))
        {
            setup.Save(snapshot);
        }

        var factory = new DirectoryProjectStoreFactory();

        // Act
        using var store = factory.Open(projPath);

        // Assert
        Assert.NotNull(store);
        Assert.Equal(projPath, store.Path);
    }

    [Fact]
    public void Factory_Open_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var missingPath = System.IO.Path.Combine(_tempRoot, "does_not_exist");
        var factory = new DirectoryProjectStoreFactory();

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => factory.Open(missingPath));
    }

    [Fact]
    public void Factory_Open_DirectoryWithoutManifest_ThrowsInvalidOperationException()
    {
        // Arrange: directory exists but no manifest.json
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);
        var factory = new DirectoryProjectStoreFactory();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => factory.Open(projPath));
    }

    [Fact]
    public void Factory_Create_NewPath_CreatesDirectoryStructureAndReturnsStore()
    {
        // Arrange
        var projPath = NewProjectPath("new_proj");
        var factory = new DirectoryProjectStoreFactory();

        // Act
        using var store = factory.Create(projPath);

        // Assert: directories created
        Assert.True(Directory.Exists(projPath));
        Assert.True(Directory.Exists(System.IO.Path.Combine(projPath, "disks")));
        Assert.True(Directory.Exists(System.IO.Path.Combine(projPath, "attachments")));
        Assert.Equal(projPath, store.Path);
    }

    [Fact]
    public void Factory_Create_ExistingPath_ThrowsInvalidOperationException()
    {
        // Arrange: pre-create the directory
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);
        var factory = new DirectoryProjectStoreFactory();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => factory.Create(projPath));
    }

    #endregion

    // ─── Gap healing ──────────────────────────────────────────────────────

    #region Gap healing

    [Fact]
    public void LoadManifest_GapHealing_RenumbersVersionsAndUpdatesHWV()
    {
        // Arrange: write manifest with HWV=2 but only place v0 and v2 on disk (gap at v1).
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(6, hwv: 2);
        var snapshot = MakeSnapshot(projPath,
            disks: new List<DiskImageRecord> { disk },
            blobs: new Dictionary<(long Id, int Version), byte[]>
            {
                { (6L, 0), new byte[] { 0xA0 } },
                { (6L, 2), new byte[] { 0xA2 } }
            });

        using var store = OpenStore(projPath);

        // Write manifest and v0 / v2 blobsmanually to simulate a gap.
        store.Save(snapshot);

        // Delete v1 that Save() would have written if it existed; it doesn't here because
        // the snapshot only contains v0 and v2. We need to also ensure v1 doesn't exist.
        // Save() only writes blobs IN the snapshot, so v1 was never written. Good.
        // But the HWV in the saved manifest will be 2, meaning LoadManifest will look for
        // v0..v2 on disk — v1 is absent, so gap healing will rename v2 → v1.

        // Act
        var manifest = store.LoadManifest();

        // Assert: v0 and v1 exist; v2 no longer exists
        Assert.True(File.Exists(System.IO.Path.Combine(projPath, "disks", "6_v0.pidi")));
        Assert.True(File.Exists(System.IO.Path.Combine(projPath, "disks", "6_v1.pidi")));
        Assert.False(File.Exists(System.IO.Path.Combine(projPath, "disks", "6_v2.pidi")),
                     "v2 should have been renamed to v1 during gap healing.");

        // Assert: HWV updated to 1
        Assert.Equal(1, manifest.DiskImages[0].HighestWorkingVersion);
    }

    #endregion

    // ─── JSON format ──────────────────────────────────────────────────────

    #region JSON format

    [Fact]
    public void ManifestJson_SavePolicySerializedAsString()
    {
        // Arrange: disk with a non-default SavePolicy
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        var disk = MakeDisk(1);
        disk.SavePolicy = SavePolicy.PromptUser;

        var snapshot = MakeSnapshot(projPath, disks: new List<DiskImageRecord> { disk });

        using var store = OpenStore(projPath);
        store.Save(snapshot);

        // Act: read raw JSON
        var json = File.ReadAllText(System.IO.Path.Combine(projPath, "manifest.json"));

        // Assert: SavePolicy is written as a string, not an integer
        Assert.Contains("\"PromptUser\"", json);
        Assert.DoesNotContain("\"SavePolicy\": 2", json);
    }

    [Fact]
    public void ManifestJson_DisclaimerIsAlwaysWritten()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        using var store = OpenStore(projPath);
        store.Save(MakeSnapshot(projPath));

        // Act: read raw JSON
        var json = File.ReadAllText(System.IO.Path.Combine(projPath, "manifest.json"));

        // Assert: disclaimer text is present
        Assert.Contains(ManifestConstants.Disclaimer, json);
    }

    [Fact]
    public void ManifestJson_IsIndented()
    {
        // Arrange
        var projPath = NewProjectPath();
        Directory.CreateDirectory(projPath);

        using var store = OpenStore(projPath);
        store.Save(MakeSnapshot(projPath));

        // Act
        var json = File.ReadAllText(System.IO.Path.Combine(projPath, "manifest.json"));

        // Assert: indented JSON has newlines
        Assert.Contains('\n', json);
    }

    #endregion
}
