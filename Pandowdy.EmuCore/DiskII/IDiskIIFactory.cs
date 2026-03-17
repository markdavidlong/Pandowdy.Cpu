// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Factory for creating Disk II drive instances.
/// </summary>
/// <remarks>
/// <para>
/// This factory creates properly configured <see cref="IDiskIIDrive"/> instances
/// with telemetry integration and optional debug decorators.
/// </para>
/// <para>
/// <strong>Drive Naming Convention:</strong> Drive names follow the pattern "SlotX-DY"
/// where X is the slot number (1-7) and Y is the drive number (1-2).
/// Example: "Slot6-D1" for drive 1 in slot 6.
/// </para>
/// </remarks>
public interface IDiskIIFactory
{
    /// <summary>
    /// Creates a Disk II drive with no disk inserted.
    /// </summary>
    /// <param name="driveName">Name for the drive (e.g., "Slot6-D1").</param>
    /// <returns>A new drive instance ready for disk insertion.</returns>
    IDiskIIDrive CreateDrive(string driveName);
}
