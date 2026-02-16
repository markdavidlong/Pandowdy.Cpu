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
/// Tests for ProjectSettingsStore — key-value CRUD operations.
/// </summary>
public class ProjectSettingsStoreTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region Get/Set/Remove

    [Fact]
    public void Set_ThenGet_ReturnsValue()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SkilletSchemaManager.InitializeSchema(connection, "Test", "0.1.0");

        // Act
        ProjectSettingsStore.Set(connection, SkilletConstants.TableProjectSettings, "test_key", "test_value");
        var result = ProjectSettingsStore.Get(connection, SkilletConstants.TableProjectSettings, "test_key");

        // Assert
        Assert.Equal("test_value", result);
    }

    [Fact]
    public void Get_NonexistentKey_ReturnsNull()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SkilletSchemaManager.InitializeSchema(connection, "Test", "0.1.0");

        // Act
        var result = ProjectSettingsStore.Get(connection, SkilletConstants.TableProjectSettings, "nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_ExistingKey_UpdatesValue()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SkilletSchemaManager.InitializeSchema(connection, "Test", "0.1.0");

        ProjectSettingsStore.Set(connection, SkilletConstants.TableProjectSettings, "key", "value1");

        // Act
        ProjectSettingsStore.Set(connection, SkilletConstants.TableProjectSettings, "key", "value2");
        var result = ProjectSettingsStore.Get(connection, SkilletConstants.TableProjectSettings, "key");

        // Assert
        Assert.Equal("value2", result);
    }

    [Fact]
    public void Remove_ExistingKey_RemovesValue()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SkilletSchemaManager.InitializeSchema(connection, "Test", "0.1.0");

        ProjectSettingsStore.Set(connection, SkilletConstants.TableProjectSettings, "key", "value");

        // Act
        ProjectSettingsStore.Remove(connection, SkilletConstants.TableProjectSettings, "key");
        var result = ProjectSettingsStore.Get(connection, SkilletConstants.TableProjectSettings, "key");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetAll

    [Fact]
    public void GetAll_ReturnsAllKeyValuePairs()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SkilletSchemaManager.InitializeSchema(connection, "Test", "0.1.0");

        ProjectSettingsStore.Set(connection, SkilletConstants.TableProjectSettings, "key1", "value1");
        ProjectSettingsStore.Set(connection, SkilletConstants.TableProjectSettings, "key2", "value2");
        ProjectSettingsStore.Set(connection, SkilletConstants.TableProjectSettings, "key3", "value3");

        // Act
        var result = ProjectSettingsStore.GetAll(connection, SkilletConstants.TableProjectSettings);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("value1", result["key1"]);
        Assert.Equal("value2", result["key2"]);
        Assert.Equal("value3", result["key3"]);
    }

    [Fact]
    public void GetAll_EmptyTable_ReturnsEmptyDictionary()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SkilletSchemaManager.InitializeSchema(connection, "Test", "0.1.0");

        // Act
        var result = ProjectSettingsStore.GetAll(connection, SkilletConstants.TableProjectSettings);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Multiple Tables

    [Fact]
    public void Set_DifferentTables_ValuesIsolated()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SkilletSchemaManager.InitializeSchema(connection, "Test", "0.1.0");

        // Act
        ProjectSettingsStore.Set(connection, SkilletConstants.TableEmulatorOverrides, "throttle", "true");
        ProjectSettingsStore.Set(connection, SkilletConstants.TableDisplayOverrides, "throttle", "false");

        var emulatorValue = ProjectSettingsStore.Get(connection, SkilletConstants.TableEmulatorOverrides, "throttle");
        var displayValue = ProjectSettingsStore.Get(connection, SkilletConstants.TableDisplayOverrides, "throttle");

        // Assert
        Assert.Equal("true", emulatorValue);
        Assert.Equal("false", displayValue);
    }

    #endregion
}
