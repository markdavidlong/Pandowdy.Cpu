// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Slots;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting a disk image be mounted from the project's disk image store.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="DiskImageId">ID of the disk image in the project's disk_images table.</param>
/// <remarks>
/// <para>
/// The controller calls <see cref="IDiskImageStore.CheckOutAsync"/> to obtain the
/// <see cref="InternalDiskImage"/>. This is the project-based disk loading workflow
/// that replaced the legacy filesystem-based loading path.
/// </para>
/// </remarks>
public record MountDiskMessage(int DriveNumber, long DiskImageId) : ICardMessage;
