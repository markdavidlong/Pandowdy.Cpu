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
    public async Task CreateAsync_ProjectAlreadyOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var testPath1 = GetTempSkilletPath();
        var testPath2 = GetTempSkilletPath();

        try
        {
            await manager.CreateAsync(testPath1, "Project 1");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.CreateAsync(testPath2, "Project 2"));
        }
        finally
        {
            await manager.CloseAsync();
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
    public async Task CloseAsync_ProjectOpen_ClosesSuccessfully()
    {
        // Arrange
        var manager = new SkilletProjectManager();
        var testPath = GetTempSkilletPath();

        await manager.CreateAsync(testPath, "Test Project");
        Assert.NotNull(manager.CurrentProject);

        // Act
        await manager.CloseAsync();

        // Assert
        Assert.Null(manager.CurrentProject);
    }

    [Fact]
    public async Task CloseAsync_NoProjectOpen_DoesNotThrow()
    {
        // Arrange
        var manager = new SkilletProjectManager();

        // Act & Assert (should not throw)
        await manager.CloseAsync();
        Assert.Null(manager.CurrentProject);
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
