// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Project-level metadata from the project_metadata table.
/// </summary>
public sealed record ProjectMetadata(
    string Name,
    DateTime CreatedUtc,
    int SchemaVersion,
    string PandowdyVersion);
