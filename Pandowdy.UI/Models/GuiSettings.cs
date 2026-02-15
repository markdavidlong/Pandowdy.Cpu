// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.Generic;

namespace Pandowdy.UI.Models;

/// <summary>
/// Master settings class containing all GUI configuration.
/// </summary>
/// <remarks>
/// This class consolidates all GUI settings that were previously scattered across
/// multiple JSON files (settings.json, window-settings.json, drive-state.json).
/// All properties are nullable to distinguish between "not set" and explicit false/zero values.
/// </remarks>
public sealed class GuiSettings
{
    /// <summary>
    /// Gets or sets the window position and geometry settings.
    /// </summary>
    public GuiWindowSettings? Window { get; set; }

    /// <summary>
    /// Gets or sets the display rendering settings.
    /// </summary>
    public DisplaySettings? Display { get; set; }

    /// <summary>
    /// Gets or sets the panel visibility and layout settings.
    /// </summary>
    public PanelSettings? Panels { get; set; }

    /// <summary>
    /// Gets or sets the emulator behavior settings.
    /// </summary>
    public EmulatorSettings? Emulator { get; set; }

    /// <summary>
    /// Gets or sets the loaded disk image state per drive.
    /// </summary>
    public DriveStateSettings? DriveState { get; set; }
}

/// <summary>
/// Window position, size, and state settings for GuiSettings.
/// </summary>
public sealed class GuiWindowSettings
{
    /// <summary>Gets or sets the window X position in pixels.</summary>
    public int? Left { get; set; }

    /// <summary>Gets or sets the window Y position in pixels.</summary>
    public int? Top { get; set; }

    /// <summary>Gets or sets the window width in pixels.</summary>
    public int? Width { get; set; }

    /// <summary>Gets or sets the window height in pixels.</summary>
    public int? Height { get; set; }

    /// <summary>Gets or sets whether the window is maximized.</summary>
    public bool? IsMaximized { get; set; }
}

/// <summary>
/// Display rendering and visual effect settings.
/// </summary>
public sealed class DisplaySettings
{
    /// <summary>Gets or sets whether to show CRT scanlines.</summary>
    public bool? ShowScanLines { get; set; }

    /// <summary>Gets or sets whether to force monochrome display.</summary>
    public bool? ForceMonochrome { get; set; }

    /// <summary>Gets or sets whether to use monochrome in mixed mode text.</summary>
    public bool? MonoMixed { get; set; }

    /// <summary>Gets or sets whether to decrease color fringing.</summary>
    public bool? DecreaseFringing { get; set; }
}

/// <summary>
/// Panel visibility and layout settings.
/// </summary>
public sealed class PanelSettings
{
    /// <summary>Gets or sets whether the soft switch status panel is visible.</summary>
    public bool? ShowSoftSwitchStatus { get; set; }

    /// <summary>Gets or sets whether the disk status panel is visible.</summary>
    public bool? ShowDiskStatus { get; set; }

    /// <summary>Gets or sets the width of the disk status panel in pixels.</summary>
    public double? DiskPanelWidth { get; set; }
}

/// <summary>
/// Emulator behavior settings.
/// </summary>
public sealed class EmulatorSettings
{
    /// <summary>Gets or sets whether CPU throttling is enabled.</summary>
    public bool? ThrottleEnabled { get; set; }

    /// <summary>Gets or sets whether caps lock emulation is enabled.</summary>
    public bool? CapsLockEnabled { get; set; }
}

/// <summary>
/// Drive state configuration containing loaded disk images.
/// </summary>
public sealed class DriveStateSettings
{
    /// <summary>Gets or sets the list of disk controller entries with their loaded disks.</summary>
    public List<DiskControllerEntry> Controllers { get; set; } = [];
}

/// <summary>
/// Disk controller entry with loaded disk images per drive.
/// </summary>
public sealed class DiskControllerEntry
{
    /// <summary>Gets or sets the slot number (1-7).</summary>
    public int Slot { get; set; }

    /// <summary>Gets or sets the list of drives (typically 2 per controller).</summary>
    public List<DriveEntry> Drives { get; set; } = [];
}

/// <summary>
/// Drive entry with loaded disk image path.
/// </summary>
public sealed class DriveEntry
{
    /// <summary>Gets or sets the drive number (1 or 2).</summary>
    public int Drive { get; set; }

    /// <summary>Gets or sets the disk image file path (null if no disk loaded).</summary>
    public string? ImagePath { get; set; }
}
