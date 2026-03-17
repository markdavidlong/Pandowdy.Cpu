// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Slots;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting a disk image be exported to the filesystem.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="FilePath">Destination file path for the exported image.</param>
/// <param name="Format">Format to export to (Woz, Nib, Dsk, Do, Po).</param>
/// <remarks>
/// <para>
/// Replaces <see cref="SaveDiskAsMessage"/>. Export is non-destructive and does not
/// modify project state. The in-memory <see cref="InternalDiskImage"/> is exported
/// to the specified format using the appropriate <see cref="IDiskImageExporter"/>.
/// </para>
/// </remarks>
public record ExportDiskMessage(int DriveNumber, string FilePath, DiskFormat Format) : ICardMessage;
