// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Microsoft.Data.Sqlite;
using Pandowdy.Project.Constants;

namespace Pandowdy.Project.Migrations;

/// <summary>
/// Creates the initial V1 schema for .skillet project files.
/// </summary>
internal sealed class V1_InitialSchema : ISchemaMigration
{
    public int FromVersion => 0;
    public int ToVersion => 1;

    public void Apply(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();

        cmd.CommandText = $"""
            -- Project metadata (singleton key-value pairs)
            CREATE TABLE {SkilletConstants.TableProjectMetadata} (
                key         TEXT PRIMARY KEY NOT NULL,
                value       TEXT NOT NULL
            );

            -- Disk image storage (original + working copy blobs)
            CREATE TABLE {SkilletConstants.TableDiskImages} (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                name                TEXT NOT NULL,
                original_filename   TEXT,
                original_format     TEXT NOT NULL,
                import_source_path  TEXT,
                imported_utc        TEXT NOT NULL,
                track_count         INTEGER NOT NULL DEFAULT 35,
                optimal_bit_timing  INTEGER NOT NULL DEFAULT 32,
                is_write_protected  INTEGER NOT NULL DEFAULT 0,
                persist_working     INTEGER NOT NULL DEFAULT 1,
                notes               TEXT,
                original_blob       BLOB NOT NULL,
                working_blob        BLOB,
                working_dirty       INTEGER NOT NULL DEFAULT 0,
                created_utc         TEXT NOT NULL,
                modified_utc        TEXT NOT NULL
            );

            CREATE INDEX idx_disk_images_name ON {SkilletConstants.TableDiskImages}(name);

            -- Mount configuration (slot/drive assignments)
            CREATE TABLE {SkilletConstants.TableMountConfiguration} (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                slot            INTEGER NOT NULL,
                drive_number    INTEGER NOT NULL,
                disk_image_id   INTEGER,
                auto_mount      INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (disk_image_id) REFERENCES {SkilletConstants.TableDiskImages}(id) ON DELETE SET NULL,
                UNIQUE(slot, drive_number)
            );

            -- Per-project emulator configuration overrides
            CREATE TABLE {SkilletConstants.TableEmulatorOverrides} (
                key     TEXT PRIMARY KEY NOT NULL,
                value   TEXT NOT NULL
            );

            -- Per-project display configuration overrides
            CREATE TABLE {SkilletConstants.TableDisplayOverrides} (
                key     TEXT PRIMARY KEY NOT NULL,
                value   TEXT NOT NULL
            );

            -- General-purpose project settings
            CREATE TABLE {SkilletConstants.TableProjectSettings} (
                key     TEXT PRIMARY KEY NOT NULL,
                value   TEXT NOT NULL
            );
            """;

        cmd.ExecuteNonQuery();
    }
}
