// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Project.Services;
using Xunit;
using Xunit.Abstractions;

namespace Pandowdy.Project.Tests.Services;

/// <summary>
/// Tests for SkilletProjectManager — create/open/close lifecycle.
/// </summary>
public class SkilletProjectManagerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ValidPath_CreatesFile()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

        try
        {
            // Act
            var project = await manager.CreateAsync(testPath, "Test Project");

            // Assert
            Assert.NotNull(project);
            Assert.Equal(testPath, project.FilePath);
            Assert.Equal("Test Project", project.Metadata.Name);
            Assert.True(File.Exists(testPath));

            _output.WriteLine($"Created test project at: {testPath}");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task CreateAsync_FileAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();
        File.WriteAllText(testPath, "dummy");

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
        var manager = new SkilletProjectManager();
        var testPath1 = GetTempSkilletPath();
        var testPath2 = GetTempSkilletPath();

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
        var manager = new SkilletProjectManager();

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
    public async Task OpenAsync_ValidFile_OpensProject()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

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
    public async Task OpenAsync_NonexistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            manager.OpenAsync(testPath));
    }

    [Fact]
    public async Task OpenAsync_InvalidApplicationId_ThrowsInvalidDataException()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

        // Create a SQLite file with wrong application_id
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={testPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA application_id = 12345;";
            cmd.ExecuteNonQuery();
        }

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                manager.OpenAsync(testPath));
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    #endregion

    #region CloseAsync

    [Fact]
    public async Task CloseAsync_ProjectOpen_CreatesAdHocProject()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

        await manager.CreateAsync(testPath, "Test Project");
        Assert.NotNull(manager.CurrentProject);
        Assert.False(manager.CurrentProject.IsAdHoc);

        // Act
        await manager.CloseAsync();

        // Assert - should have created an ad hoc project (never null)
        Assert.NotNull(manager.CurrentProject);
        Assert.True(manager.CurrentProject.IsAdHoc);
        Assert.Equal("untitled", manager.CurrentProject.Metadata.Name);

        // Clean up ad hoc project
        manager.CurrentProject?.Dispose();
    }

    [Fact]
    public async Task CloseAsync_NoProjectOpen_DoesNotThrow()
    {
        // Arrange
        var manager = new SkilletProjectManager();

        // Act & Assert (should not throw)
        await manager.CloseAsync();

        // When closing with no project open, it just returns - no ad hoc is created
        Assert.Null(manager.CurrentProject);
    }

    #endregion

    #region SaveAsAsync

    [Fact]
    public async Task SaveAsAsync_AdHocProject_UpdatesProjectNameToFileName()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();
        var expectedName = Path.GetFileNameWithoutExtension(testPath);

        try
        {
            await manager.CreateAdHocAsync();
            Assert.Equal("untitled", manager.CurrentProject!.Metadata.Name);

            // Act
            await manager.SaveAsAsync(testPath);

            // Assert — project name should now reflect the file name
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
    public async Task SaveAsAsync_FileBasedProject_UpdatesProjectNameToNewFileName()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var originalPath = GetTempSkilletPath();
        var newPath = GetTempSkilletPath();
        var expectedName = Path.GetFileNameWithoutExtension(newPath);

        try
        {
            await manager.CreateAsync(originalPath, "Original Name");
            Assert.Equal("Original Name", manager.CurrentProject!.Metadata.Name);

            // Act
            await manager.SaveAsAsync(newPath);

            // Assert — project name should now reflect the new file name
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
        // Arrange — verify the name persists in the DB, not just in memory
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();
        var expectedName = Path.GetFileNameWithoutExtension(testPath);

        try
        {
            await manager.CreateAdHocAsync();
            await manager.SaveAsAsync(testPath);
            await manager.CloseAsync();

            // Act — reopen the saved file
            var reopened = await manager.OpenAsync(testPath);

            // Assert — name should still be the file-derived name, not "untitled"
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
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

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
        var manager = new SkilletProjectManager();
        var originalPath = GetTempSkilletPath();
        var newPath = GetTempSkilletPath();

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
    public async Task SaveAsAsync_ProducesNoSidecarFiles()
    {
        // Arrange — .skillet should be a single portable file with no WAL or SHM
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

        try
        {
            await manager.CreateAdHocAsync();
            await manager.SaveAsAsync(testPath);

            // Assert — no sidecar files
            Assert.True(File.Exists(testPath), ".skillet file should exist");
            Assert.False(File.Exists(testPath + "-wal"), "WAL sidecar should not exist");
            Assert.False(File.Exists(testPath + "-shm"), "SHM sidecar should not exist");

            _output.WriteLine("No sidecar files created — single portable .skillet file");
        }
        finally
        {
            await manager.CloseAsync();
        }
    }

    [Fact]
    public async Task SaveAsAsync_ProjectRemainsOperationalAfterTransition()
    {
        // Arrange — verify the project can still perform IO after the connection swap
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

        try
        {
            await manager.CreateAdHocAsync();
            await manager.SaveAsAsync(testPath);

            // Act — perform operations on the transitioned project
            var settings = await manager.CurrentProject!.GetSettingAsync("project_settings", "test_key");
            await manager.CurrentProject.SetSettingAsync("project_settings", "test_key", "test_value");
            var readBack = await manager.CurrentProject.GetSettingAsync("project_settings", "test_key");

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

    [Fact]
    public async Task CreateAsync_ProducesNoSidecarFiles()
    {
        // Arrange — new file-based projects should also use DELETE journal mode
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

        try
        {
            await manager.CreateAsync(testPath, "Test");

            // Assert — no sidecar files
            Assert.True(File.Exists(testPath), ".skillet file should exist");
            Assert.False(File.Exists(testPath + "-wal"), "WAL sidecar should not exist");
            Assert.False(File.Exists(testPath + "-shm"), "SHM sidecar should not exist");

            _output.WriteLine("No sidecar files for newly created project");
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
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

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

    // Helper methods

    private static string GetTempSkilletPath()
    {
        return Path.Combine(Path.GetTempPath(), $"pandowdy_test_{Guid.NewGuid():N}.skillet");
    }
}
