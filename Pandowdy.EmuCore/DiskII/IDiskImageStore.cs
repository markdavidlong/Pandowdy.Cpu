// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Abstraction for a persistent store that can lend and accept disk images.
/// </summary>
/// <remarks>
/// <para>
/// Defined in EmuCore so the controller card can depend on it without
/// referencing Pandowdy.Project. Implemented by the project system (SkilletProject).
/// </para>
/// <para>
/// <strong>Checkout Model:</strong><br/>
/// When a disk image is mounted, the controller calls <see cref="CheckOutAsync"/>
/// to borrow an <see cref="InternalDiskImage"/> from the store. The emulator owns
/// the image in-memory until eject. On eject, the controller calls
/// <see cref="ReturnAsync"/> to return the image (and persist any modifications).
/// </para>
/// <para>
/// <strong>Lifecycle:</strong><br/>
/// - Mount: <see cref="CheckOutAsync"/> → emulator receives <see cref="InternalDiskImage"/><br/>
/// - Unmount/Eject: <see cref="ReturnAsync"/> → store serializes modifications
/// </para>
/// </remarks>
public interface IDiskImageStore
{
    /// <summary>
    /// Checks out a disk image for use by the emulator.
    /// </summary>
    /// <param name="diskImageId">ID of the disk image to check out.</param>
    /// <returns>
    /// An <see cref="InternalDiskImage"/> owned by the caller until
    /// <see cref="ReturnAsync"/> is called.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the disk image ID does not exist.
    /// </exception>
    Task<InternalDiskImage> CheckOutAsync(long diskImageId);

    /// <summary>
    /// Returns a disk image to the store after ejection.
    /// </summary>
    /// <param name="diskImageId">ID of the disk image being returned.</param>
    /// <param name="image">The <see cref="InternalDiskImage"/> to serialize and persist.</param>
    /// <remarks>
    /// The store serializes the image to its working copy blob if the image has been
    /// modified. This method is called automatically by the controller on eject.
    /// </remarks>
    Task ReturnAsync(long diskImageId, InternalDiskImage image);
}
