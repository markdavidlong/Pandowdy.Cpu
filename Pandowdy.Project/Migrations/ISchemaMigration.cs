// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Microsoft.Data.Sqlite;

namespace Pandowdy.Project.Migrations;

/// <summary>
/// Defines a schema migration step from one version to another.
/// </summary>
internal interface ISchemaMigration
{
    /// <summary>
    /// Gets the schema version this migration applies to (before migration).
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// Gets the schema version after this migration is applied.
    /// </summary>
    int ToVersion { get; }

    /// <summary>
    /// Applies the migration to the given connection.
    /// Executes within a transaction managed by the caller.
    /// </summary>
    void Apply(SqliteConnection connection);
}
