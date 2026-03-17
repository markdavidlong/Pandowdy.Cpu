// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Controls how a disk image's working data is saved.
/// Stored per-disk in the manifest / DiskImageRecord.
/// </summary>
public enum SavePolicy
{
    /// <summary>
    /// Overwrite the single latest working version in place.
    /// Automatic saves always target the highest-numbered working version.
    /// If manual snapshots have created versions v1, v2, v3, the next automatic
    /// save overwrites v3 (or creates v1 if no working versions exist yet).
    /// The version count never decreases -- snapshots are preserved as history.
    /// </summary>
    OverwriteLatest,

    /// <summary>
    /// Preserve all versions created by manual snapshots. Automatic saves
    /// overwrite the current high-water mark (same as <see cref="OverwriteLatest"/>).
    /// New versions are only created by explicit <c>CreateSnapshotAsync</c> calls.
    /// This is NOT a transaction log — it does not auto-increment on every save.
    /// </summary>
    AppendVersion,

    /// <summary>
    /// Prompt the user before saving. The UI must obtain confirmation
    /// before <c>SkilletProject</c> will call <see cref="Interfaces.IProjectStore.SaveBlob"/>.
    /// </summary>
    PromptUser,

    /// <summary>
    /// Discard all working changes. The disk image reverts to its last
    /// persisted state on return. No working version is ever written.
    /// </summary>
    DiscardChanges
}
