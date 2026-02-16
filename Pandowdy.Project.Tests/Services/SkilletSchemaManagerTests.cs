// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Microsoft.Data.Sqlite;
using Pandowdy.Project.Services;
using Xunit;
using Xunit.Abstractions;

namespace Pandowdy.Project.Tests.Services;

/// <summary>
/// Tests for SkilletSchemaManager — schema creation, pragmas, migrations.
/// </summary>
public class SkilletSchemaManagerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region InitializeSchema

    [Fact]
    public void InitializeSchema_CreatesAllV1Tables()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Act
        SkilletSchemaManager.InitializeSchema(connection, "Test Project", "0.1.0");

        // Assert
        var tables = GetTableNames(connection);
        Assert.Contains("project_metadata", tables);
        Assert.Contains("disk_images", tables);
        Assert.Contains("mount_configuration", tables);
        Assert.Contains("emulator_overrides", tables);
        Assert.Contains("display_overrides", tables);
        Assert.Contains("project_settings", tables);
    }

    [Fact]
    public void InitializeSchema_SetsCorrectPragmas()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Act
        SkilletSchemaManager.InitializeSchema(connection, "Test Project", "0.1.0");

        // Assert
        var appId = GetPragmaInt(connection, "application_id");
        var userVersion = GetPragmaInt(connection, "user_version");

        Assert.Equal(0x534B494C, appId);
        Assert.Equal(1, userVersion);
    }

    [Fact]
    public void InitializeSchema_SeedsProjectMetadata()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Act
        SkilletSchemaManager.InitializeSchema(connection, "My Project", "0.1.0");

        // Assert
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM project_metadata;";
        using var reader = cmd.ExecuteReader();

        var metadata = new Dictionary<string, string>();
        while (reader.Read())
        {
            metadata[reader.GetString(0)] = reader.GetString(1);
        }

        Assert.Equal("My Project", metadata["name"]);
        Assert.Equal("1", metadata["schema_version"]);
        Assert.Equal("0.1.0", metadata["pandowdy_version"]);
        Assert.True(metadata.ContainsKey("created_utc"));
        Assert.True(metadata.ContainsKey("modified_utc"));
    }

    [Fact]
    public void InitializeSchema_SeedsDefaultMountConfiguration()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Act
        SkilletSchemaManager.InitializeSchema(connection, "Test Project", "0.1.0");

        // Assert
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT slot, drive_number, disk_image_id FROM mount_configuration;";
        using var reader = cmd.ExecuteReader();

        var mounts = new List<(int slot, int drive, long? diskId)>();
        while (reader.Read())
        {
            mounts.Add((reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt64(2)));
        }

        Assert.Equal(2, mounts.Count);
        Assert.Contains(mounts, m => m.slot == 6 && m.drive == 1 && m.diskId == null);
        Assert.Contains(mounts, m => m.slot == 6 && m.drive == 2 && m.diskId == null);
    }

    #endregion

    #region ValidateApplicationId

    [Fact]
    public void ValidateApplicationId_ValidFile_ReturnsTrue()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SkilletSchemaManager.InitializeSchema(connection, "Test", "0.1.0");

        // Act
        var result = SkilletSchemaManager.ValidateApplicationId(connection);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateApplicationId_InvalidFile_ReturnsFalse()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA application_id = 12345;";
        cmd.ExecuteNonQuery();

        // Act
        var result = SkilletSchemaManager.ValidateApplicationId(connection);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetSchemaVersion

    [Fact]
    public void GetSchemaVersion_AfterInit_Returns1()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SkilletSchemaManager.InitializeSchema(connection, "Test", "0.1.0");

        // Act
        var version = SkilletSchemaManager.GetSchemaVersion(connection);

        // Assert
        Assert.Equal(1, version);
    }

    [Fact]
    public void GetSchemaVersion_EmptyDatabase_Returns0()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Act
        var version = SkilletSchemaManager.GetSchemaVersion(connection);

        // Assert
        Assert.Equal(0, version);
    }

    #endregion

    // Helper methods

    private static List<string> GetTableNames(SqliteConnection connection)
    {
        var tables = new List<string>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    private static long GetPragmaInt(SqliteConnection connection, string pragmaName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA {pragmaName};";
        var result = cmd.ExecuteScalar();
        return result is long value ? value : 0;
    }
}
