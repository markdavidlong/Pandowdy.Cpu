// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.UI.Models;
using System.Threading.Tasks;

namespace Pandowdy.UI.Interfaces;

/// <summary>
/// Service for managing disk drive state persistence.
/// </summary>
public interface IDriveStateService
{
    /// <summary>
    /// Loads drive state configuration from persistent storage.
    /// Returns empty configuration if file doesn't exist or fails to load.
    /// </summary>
    Task<DriveStateConfig> LoadDriveStateAsync();

    /// <summary>
    /// Saves drive state configuration to persistent storage.
    /// </summary>
    Task SaveDriveStateAsync(DriveStateConfig config);

    /// <summary>
    /// Gets the file path where drive state is stored.
    /// </summary>
    string GetDriveStateFilePath();

    /// <summary>
    /// Captures current drive states from the emulator and saves them.
    /// </summary>
    Task CaptureDriveStateAsync();

    /// <summary>
    /// Captures current drive states from the emulator and returns them as DriveStateSettings
    /// for inclusion in the master GUI settings file (does NOT save to file).
    /// </summary>
    /// <returns>DriveStateSettings populated with currently inserted disks.</returns>
    DriveStateSettings CaptureDriveStateSettings();

    /// <summary>
    /// Restores drive states from DriveStateSettings (from master GUI settings file).
    /// </summary>
    /// <param name="settings">Drive state settings to restore.</param>
    void RestoreDriveState(DriveStateSettings? settings);

    /// <summary>
    /// Loads drive state from persistent storage and restores disk images to drives.
    /// </summary>
    Task LoadAndRestoreDriveStateAsync();
}
