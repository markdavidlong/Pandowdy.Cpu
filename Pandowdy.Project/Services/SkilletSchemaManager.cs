// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Microsoft.Data.Sqlite;
using Pandowdy.Project.Constants;
using Pandowdy.Project.Migrations;

namespace Pandowdy.Project.Services;

/// <summary>
/// Manages SQLite schema creation and migration for .skillet project files.
/// </summary>
internal static class SkilletSchemaManager
{
    private static readonly ISchemaMigration[] s_migrations =
    [
        new V1_InitialSchema()
    ];

    /// <summary>
    /// Initializes a new .skillet database with pragmas, V1 schema, and seed data.
    /// </summary>
    public static void InitializeSchema(SqliteConnection connection, string projectName, string pandowdyVersion)
    {
        SetPragmas(connection);

        // Run V1 migration
        using var transaction = connection.BeginTransaction();
        try
        {
            s_migrations[0].Apply(connection);
            SeedProjectMetadata(connection, projectName, pandowdyVersion);
            SeedDefaultMountConfiguration(connection);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Migrates the schema from the current version to the latest version.
    /// </summary>
    public static void Migrate(SqliteConnection connection, int currentVersion)
    {
        if (currentVersion >= SkilletConstants.SchemaVersion)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var migration in s_migrations)
            {
                if (migration.FromVersion >= currentVersion && migration.ToVersion <= SkilletConstants.SchemaVersion)
                {
                    migration.Apply(connection);
                }
            }

            UpdateSchemaVersion(connection, (int)SkilletConstants.SchemaVersion);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Sets the required pragmas for .skillet database files.
    /// </summary>
    public static void SetPragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            PRAGMA application_id = {SkilletConstants.ApplicationId};
            PRAGMA user_version = {SkilletConstants.SchemaVersion};
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Verifies that the database has the correct application_id pragma.
    /// </summary>
    public static bool ValidateApplicationId(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA application_id;";
        var result = cmd.ExecuteScalar();
        return result is long appId && appId == SkilletConstants.ApplicationId;
    }

    /// <summary>
    /// Gets the current schema version from the user_version pragma.
    /// </summary>
    public static int GetSchemaVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = cmd.ExecuteScalar();
        return result is long version ? (int)version : 0;
    }

    private static void SeedProjectMetadata(SqliteConnection connection, string projectName, string pandowdyVersion)
    {
        var nowUtc = DateTime.UtcNow.ToString("o");

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO {SkilletConstants.TableProjectMetadata} (key, value) VALUES
                ('name', @name),
                ('created_utc', @nowUtc),
                ('modified_utc', @nowUtc),
                ('schema_version', @schemaVersion),
                ('pandowdy_version', @pandowdyVersion);
            """;
        cmd.Parameters.AddWithValue("@name", projectName);
        cmd.Parameters.AddWithValue("@nowUtc", nowUtc);
        cmd.Parameters.AddWithValue("@schemaVersion", SkilletConstants.SchemaVersion);
        cmd.Parameters.AddWithValue("@pandowdyVersion", pandowdyVersion);
        cmd.ExecuteNonQuery();
    }

    private static void SeedDefaultMountConfiguration(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO {SkilletConstants.TableMountConfiguration} (slot, drive_number, disk_image_id, auto_mount) VALUES
                (6, 1, NULL, 1),
                (6, 2, NULL, 1);
            """;
        cmd.ExecuteNonQuery();
    }

    private static void UpdateSchemaVersion(SqliteConnection connection, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }
}
