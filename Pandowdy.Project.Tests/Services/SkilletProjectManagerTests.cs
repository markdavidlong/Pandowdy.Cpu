// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Project.Models;
using Pandowdy.Project.Services;
using Pandowdy.Project.Stores;
using Xunit.Abstractions;

namespace Pandowdy.Project.Tests.Services;

/// <summary>
/// Tests for SkilletProjectManager — create/open/close lifecycle.
/// Uses <see cref="DirectoryProjectStoreFactory"/> for all file-backed operations.
/// </summary>
public class SkilletProjectManagerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    // ─── Factory helper ───────────────────────────────────────────────────
    private static SkilletProjectManager MakeManager() =>
        new(new DirectoryProjectStoreFactory());

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ValidPath_CreatesDirectory()
    {
        // Arrange
        var manager = MakeManager();
        var testPath = GetTempStorePath();

        try
        {
            // Act
            var project = await manager.CreateAsync(testPath, "Test Project");

            // Assert
            Assert.NotNull(project);
            Assert.Equal(testPath, project.FilePath);
            Assert.Equal("Test Project", project.Metadata.Name);
            Assert.True(Directory.Exists(testPath));
            Assert.True(File.Exists(Path.Combine(testPath, "manifest.json")));

            _output.WriteLine($"Created test project at: {testPath}");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task CreateAsync_DirectoryAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var manager = MakeManager();
        var testPath = GetTempStorePath();
        Directory.CreateDirectory(testPath);   // Pre-create so factory rejects it

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.CreateAsync(testPath, "Test Project"));
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task CreateAsync_ProjectAlreadyOpen_ClosesCurrentAndCreatesNew()
    {
        // Arrange
        var manager = MakeManager();
        var testPath1 = GetTempStorePath();
        var testPath2 = GetTempStorePath();

        try
        {
            var project1 = await manager.CreateAsync(testPath1, "Project 1");
            Assert.NotNull(project1);
            Assert.Equal(testPath1, manager.CurrentProject?.FilePath);

            // Act - create another project while first is still open
            var project2 = await manager.CreateAsync(testPath2, "Project 2");

            // Assert - should have closed first and created second
            Assert.NotNull(project2);
            Assert.Equal(testPath2, manager.CurrentProject?.FilePath);
            Assert.Equal("Project 2", manager.CurrentProject?.Metadata.Name);
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task CreateAdHocAsync_CreatesInMemoryProject()
    {
        // Arrange
        var manager = MakeManager();

        try
        {
            // Act
            var project = await manager.CreateAdHocAsync();

            // Assert
            Assert.NotNull(project);
            Assert.Null(project.FilePath);
            Assert.True(project.IsAdHoc);
            Assert.Equal("untitled", project.Metadata.Name);
            Assert.Equal(1, project.Metadata.SchemaVersion);

            _output.WriteLine($"Created ad hoc project: {project.Metadata.Name}");
        }
        finally
        {
            manager.CurrentProject?.Dispose();
        }
    }

    #endregion

    #region OpenAsync

    [Fact]
    public async Task OpenAsync_ValidDirectory_OpensProject()
    {
        // Arrange
        var manager = MakeManager();
        var testPath = GetTempStorePath();

        try
        {
            // Create then close
            var created = await manager.CreateAsync(testPath, "Test Project");
            await manager.CloseAsync();

            // Act
            var opened = await manager.OpenAsync(testPath);

            // Assert
            Assert.NotNull(opened);
            Assert.Equal(testPath, opened.FilePath);
            Assert.Equal("Test Project", opened.Metadata.Name);

            _output.WriteLine($"Opened test project from: {testPath}");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task OpenAsync_NonexistentDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var manager = MakeManager();
        var testPath = GetTempStorePath();

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            manager.OpenAsync(testPath));
    }

    #endregion

    #region CloseAsync

    [Fact]
    public async Task CloseAsync_ProjectOpen_CreatesAdHocProject()
    {
        // Arrange
        var manager = MakeManager();
        var testPath = GetTempStorePath();

        await manager.CreateAsync(testPath, "Test Project");
        Assert.NotNull(manager.CurrentProject);
        Assert.False(manager.CurrentProject.IsAdHoc);

        // Act
        await manager.CloseAsync();

        // Assert - CloseAsync always creates a fresh ad hoc project (CurrentProject is never null)
        Assert.NotNull(manager.CurrentProject);
        Assert.True(manager.CurrentProject.IsAdHoc);
        Assert.Equal("untitled", manager.CurrentProject.Metadata.Name);

        // Clean up
        manager.CurrentProject?.Dispose();
    }

    [Fact]
    public async Task CloseAsync_NoProjectOpen_CreatesAdHocProject()
    {
        // Arrange
        var manager = MakeManager();

        // Act (should not throw - CloseAsync always produces an ad hoc project afterwards)
        await manager.CloseAsync();

        // Assert - CurrentProject is always non-null after CloseAsync
        Assert.NotNull(manager.CurrentProject);
        Assert.True(manager.CurrentProject.IsAdHoc);

        manager.CurrentProject?.Dispose();
    }

    #endregion

    #region SaveAsAsync

    [Fact]
    public async Task SaveAsAsync_AdHocProject_UpdatesProjectNameToDirectoryName()
    {
        // Arrange
        var manager = MakeManager();
        var testPath = GetTempStorePath();
        var expectedName = GetProjectNameFromStorePath(testPath);

        try
        {
            await manager.CreateAdHocAsync();
            Assert.Equal("untitled", manager.CurrentProject!.Metadata.Name);

            // Act
            await manager.SaveAsAsync(testPath);

            // Assert — project name should now reflect the directory name (sans _skilletdir suffix)
            Assert.NotNull(manager.CurrentProject);
            Assert.False(manager.CurrentProject.IsAdHoc);
            Assert.Equal(expectedName, manager.CurrentProject.Metadata.Name);
            Assert.Equal(testPath, manager.CurrentProject.FilePath);

            _output.WriteLine($"Save As updated project name to: {manager.CurrentProject.Metadata.Name}");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task SaveAsAsync_FileBasedProject_UpdatesProjectNameToNewDirectoryName()
    {
        // Arrange
        var manager = MakeManager();
        var originalPath = GetTempStorePath();
        var newPath = GetTempStorePath();
        var expectedName = GetProjectNameFromStorePath(newPath);

        try
        {
            await manager.CreateAsync(originalPath, "Original Name");
            Assert.Equal("Original Name", manager.CurrentProject!.Metadata.Name);

            // Act
            await manager.SaveAsAsync(newPath);

            // Assert — project name should now reflect the new directory name
            Assert.NotNull(manager.CurrentProject);
            Assert.Equal(expectedName, manager.CurrentProject.Metadata.Name);
            Assert.Equal(newPath, manager.CurrentProject.FilePath);

            _output.WriteLine($"Save As updated project name from 'Original Name' to: {manager.CurrentProject.Metadata.Name}");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task SaveAsAsync_ReopenedProject_HasUpdatedName()
    {
        // Arrange — verify the name persists in the manifest, not just in memory
        var manager = MakeManager();
        var testPath = GetTempStorePath();
        var expectedName = GetProjectNameFromStorePath(testPath);

        try
        {
            await manager.CreateAdHocAsync();
            await manager.SaveAsAsync(testPath);
            await manager.CloseAsync();

            // Act — reopen the saved project
            var reopened = await manager.OpenAsync(testPath);

            // Assert — name should still be the directory-derived name, not "untitled"
            Assert.Equal(expectedName, reopened.Metadata.Name);

            _output.WriteLine($"Reopened project has name: {reopened.Metadata.Name}");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task SaveAsAsync_AdHocProject_PreservesProjectInstance()
    {
        // Arrange — the SkilletProject instance must survive Save As so that
        // external references (IDiskImageStore held by DiskIIControllerCard,
        // checked-out images) remain valid.
        var manager = MakeManager();
        var testPath = GetTempStorePath();

        try
        {
            var adHocProject = await manager.CreateAdHocAsync();
            var projectBefore = manager.CurrentProject;

            // Act
            await manager.SaveAsAsync(testPath);

            // Assert — same object reference, not a new instance
            Assert.Same(projectBefore, manager.CurrentProject);
            Assert.False(manager.CurrentProject!.IsAdHoc);
            Assert.Equal(testPath, manager.CurrentProject.FilePath);

            _output.WriteLine("Project instance preserved across ad hoc → file transition");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task SaveAsAsync_FileBasedProject_PreservesProjectInstance()
    {
        // Arrange
        var manager = MakeManager();
        var originalPath = GetTempStorePath();
        var newPath = GetTempStorePath();

        try
        {
            await manager.CreateAsync(originalPath, "Original");
            var projectBefore = manager.CurrentProject;

            // Act
            await manager.SaveAsAsync(newPath);

            // Assert — same object reference
            Assert.Same(projectBefore, manager.CurrentProject);
            Assert.Equal(newPath, manager.CurrentProject!.FilePath);

            _output.WriteLine("Project instance preserved across file → file transition");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task SaveAsAsync_CreatesDirectoryWithCorrectStructure()
    {
        // Arrange — directory store should always produce manifest.json + disks/ + attachments/
        var manager = MakeManager();
        var testPath = GetTempStorePath();

        try
        {
            await manager.CreateAdHocAsync();
            await manager.SaveAsAsync(testPath);

            // Assert — correct directory structure, no SQLite sidecar files
            Assert.True(Directory.Exists(testPath), "Project directory should exist");
            Assert.True(File.Exists(Path.Combine(testPath, "manifest.json")), "manifest.json should exist");
            Assert.True(Directory.Exists(Path.Combine(testPath, "disks")), "disks/ subdir should exist");
            Assert.True(Directory.Exists(Path.Combine(testPath, "attachments")), "attachments/ subdir should exist");

            _output.WriteLine("Correct directory structure created by SaveAsAsync");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task CreateAsync_CreatesDirectoryWithCorrectStructure()
    {
        // Arrange — CreateAsync should also produce the correct directory layout
        var manager = MakeManager();
        var testPath = GetTempStorePath();

        try
        {
            await manager.CreateAsync(testPath, "Test");

            // Assert — correct directory structure
            Assert.True(Directory.Exists(testPath), "Project directory should exist");
            Assert.True(File.Exists(Path.Combine(testPath, "manifest.json")), "manifest.json should exist");
            Assert.True(Directory.Exists(Path.Combine(testPath, "disks")), "disks/ subdir should exist");
            Assert.True(Directory.Exists(Path.Combine(testPath, "attachments")), "attachments/ subdir should exist");

            _output.WriteLine("Correct directory structure created by CreateAsync");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task SaveAsAsync_ProjectRemainsOperationalAfterTransition()
    {
        // Arrange — verify the project can still perform IO after the store swap
        var manager = MakeManager();
        var testPath = GetTempStorePath();

        try
        {
            await manager.CreateAdHocAsync();
            await manager.SaveAsAsync(testPath);

            // Act — perform operations on the transitioned project
            var settings = await manager.CurrentProject!.GetSettingAsync(SettingsScope.ProjectSettings, "test_key");
            await manager.CurrentProject.SetSettingAsync(SettingsScope.ProjectSettings, "test_key", "test_value");
            var readBack = await manager.CurrentProject.GetSettingAsync(SettingsScope.ProjectSettings, "test_key");

            // Assert
            Assert.Null(settings);
            Assert.Equal("test_value", readBack);

            _output.WriteLine("Project IO operational after Save As transition");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    #endregion

    #region Round-trip

    [Fact]
    public async Task CreateCloseReopen_MetadataPersists()
    {
        // Arrange
        var manager = MakeManager();
        var testPath = GetTempStorePath();

        try
        {
            // Create
            var created = await manager.CreateAsync(testPath, "Round Trip Test");
            var createdTime = created.Metadata.CreatedUtc;
            await manager.CloseAsync();

            // Reopen
            var reopened = await manager.OpenAsync(testPath);

            // Assert
            Assert.Equal("Round Trip Test", reopened.Metadata.Name);
            Assert.Equal(createdTime, reopened.Metadata.CreatedUtc);
            Assert.Equal(1, reopened.Metadata.SchemaVersion);

            _output.WriteLine($"Round-trip test passed for: {testPath}");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    #endregion

    // ─── Helper methods ───────────────────────────────────────────────────

    /// <summary>
    /// Returns a path to a non-existent temp directory with the <c>_skilletdir</c> suffix
    /// so that <see cref="SkilletProject.SaveAsAsync"/> derives a clean project name from it.
    /// </summary>
    private static string GetTempStorePath() =>
        Path.Combine(Path.GetTempPath(), $"pandowdy_test_{Guid.NewGuid():N}_skilletdir");

    /// <summary>
    /// Derives the expected project name from a store path the same way
    /// <see cref="SkilletProject.SaveAsAsync"/> does — strips the <c>_skilletdir</c> suffix.
    /// </summary>
    private static string GetProjectNameFromStorePath(string path)
    {
        const string suffix = "_skilletdir";
        var dirName = Path.GetFileName(
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return dirName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? dirName[..^suffix.Length]
            : dirName;
    }
}
