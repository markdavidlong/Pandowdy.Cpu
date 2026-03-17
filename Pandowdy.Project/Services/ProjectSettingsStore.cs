// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Microsoft.Data.Sqlite;

namespace Pandowdy.Project.Services;

/// <summary>
/// Generic key-value CRUD operations for settings tables in .skillet project files.
/// </summary>
internal static class ProjectSettingsStore
{
    /// <summary>
    /// Gets a setting value from the specified table.
    /// </summary>
    public static string? Get(SqliteConnection connection, string tableName, string key)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT value FROM {tableName} WHERE key = @key;";
        cmd.Parameters.AddWithValue("@key", key);

        var result = cmd.ExecuteScalar();
        return result as string;
    }

    /// <summary>
    /// Sets a setting value in the specified table (upsert).
    /// </summary>
    public static void Set(SqliteConnection connection, string tableName, string key, string value)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO {tableName} (key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets all settings from the specified table.
    /// </summary>
    public static Dictionary<string, string> GetAll(SqliteConnection connection, string tableName)
    {
        var settings = new Dictionary<string, string>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT key, value FROM {tableName};";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            settings[reader.GetString(0)] = reader.GetString(1);
        }

        return settings;
    }

    /// <summary>
    /// Removes a setting from the specified table.
    /// </summary>
    public static void Remove(SqliteConnection connection, string tableName, string key)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM {tableName} WHERE key = @key;";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.ExecuteNonQuery();
    }
}
