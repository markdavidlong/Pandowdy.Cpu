// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Controls how a blob save operation is handled.
/// This is the low-level mode passed to <see cref="Pandowdy.Project.Interfaces.IProjectStore.SaveBlob"/>.
/// The per-disk <see cref="SavePolicy"/> determines which mode is used at save time.
/// </summary>
public enum BlobSaveMode
{
    /// <summary>
    /// Overwrite the current active (latest) working version in place.
    /// In normal operation this never targets version 0. A forced v0 overwrite
    /// (e.g., re-import) is permitted but emits a warning to stderr.
    /// </summary>
    OverwriteActive,

    /// <summary>Create a new version, preserving all previous versions.</summary>
    CreateNewVersion
}
