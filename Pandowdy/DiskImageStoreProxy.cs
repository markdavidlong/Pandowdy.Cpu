// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.Project.Interfaces;

namespace Pandowdy;

/// <summary>
/// Proxy that delegates <see cref="IDiskImageStore"/> calls to the current project.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Cards receive <see cref="IDiskImageStore"/> at creation time
/// and hold the reference for the emulator's lifetime. Without this proxy, that reference
/// would become stale when the user opens a different project (the old project's I/O thread
/// is disposed, causing "Cannot access a disposed object" errors).
/// </para>
/// <para>
/// The proxy always resolves the store from <see cref="ISkilletProjectManager.CurrentProject"/>,
/// so cards transparently use whichever project is currently open.
/// </para>
/// </remarks>
internal class DiskImageStoreProxy(ISkilletProjectManager projectManager) : IDiskImageStore
{
    private IDiskImageStore CurrentStore =>
        (IDiskImageStore?)projectManager.CurrentProject
        ?? throw new InvalidOperationException("No project is currently open.");

    /// <inheritdoc />
    public Task<InternalDiskImage> CheckOutAsync(long diskImageId)
    {
        return CurrentStore.CheckOutAsync(diskImageId);
    }

    /// <inheritdoc />
    public Task ReturnAsync(long diskImageId, InternalDiskImage image)
    {
        return CurrentStore.ReturnAsync(diskImageId, image);
    }
}
