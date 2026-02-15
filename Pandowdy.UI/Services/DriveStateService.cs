// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pandowdy.UI.Services;

/// <summary>
/// Service for managing disk drive state persistence across application restarts.
/// Captures which disk images are inserted into which drives and restores them on startup.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DriveStateService"/> class.
/// </remarks>
public class DriveStateService(
    EmuCore.Services.IDiskStatusProvider diskStatusProvider,
    EmuCore.Services.IDiskStatusMutator diskStatusMutator,
    ISlots slots) : IDriveStateService
{
    private const string DriveStateFileName = "drive-state.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly EmuCore.Services.IDiskStatusProvider _diskStatusProvider = diskStatusProvider;
    private readonly EmuCore.Services.IDiskStatusMutator _diskStatusMutator = diskStatusMutator;
    private readonly ISlots _slots = slots;

    /// <inheritdoc/>
    public virtual string GetDriveStateFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pandowdyPath = Path.Combine(appDataPath, "LydianScaleSoftware", "Pandowdy");
        return Path.Combine(pandowdyPath, DriveStateFileName);
    }

    /// <inheritdoc/>
    public async Task<DriveStateConfig> LoadDriveStateAsync()
    {
        var filePath = GetDriveStateFilePath();

        // DEBUG: Log the file path being checked
        System.Diagnostics.Debug.WriteLine($"[DriveStateService] Looking for drive state at: {filePath}");

        if (!File.Exists(filePath))
        {
            System.Diagnostics.Debug.WriteLine($"[DriveStateService] Drive state file not found, returning empty config");
            return new DriveStateConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            System.Diagnostics.Debug.WriteLine($"[DriveStateService] Loaded JSON from '{filePath}': {json}");
            var config = JsonSerializer.Deserialize<DriveStateConfig>(json);
            System.Diagnostics.Debug.WriteLine($"[DriveStateService] Successfully decoded JSON from '{filePath}' - found {config?.Drives.Count ?? 0} drive entries");
            return config ?? new DriveStateConfig();
        }
        catch (Exception ex)
        {
            // If deserialization fails, return empty configuration
            System.Diagnostics.Debug.WriteLine($"[DriveStateService] Failed to decode JSON from '{filePath}': {ex.Message}");
            return new DriveStateConfig();
        }
    }

    /// <inheritdoc/>
    public async Task SaveDriveStateAsync(DriveStateConfig config)
    {
        var filePath = GetDriveStateFilePath();
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <inheritdoc/>
    public async Task CaptureDriveStateAsync()
    {
        var config = new DriveStateConfig();

        // Get current disk status from provider
        var snapshot = _diskStatusProvider.Current;

        // Capture each drive that has a disk inserted or is in ghost state
        foreach (var drive in snapshot.Drives)
        {
            // Capture drives with disk images (including ghost disks)
            if (!string.IsNullOrWhiteSpace(drive.DiskImagePath))
            {
                config.Drives.Add(new DriveStateEntry
                {
                    Slot = (byte)drive.SlotNumber,
                    DriveNumber = (byte)drive.DriveNumber,
                    DiskImagePath = drive.DiskImagePath
                });
            }
        }

        await SaveDriveStateAsync(config);
    }

    /// <summary>
    /// Captures current drive states from the emulator and returns them as DriveStateSettings
    /// for inclusion in the master GUI settings file.
    /// </summary>
    /// <returns>DriveStateSettings populated with currently inserted disks.</returns>
    /// <remarks>
    /// This method does NOT save to a file - it returns the settings for the caller to save
    /// via GuiSettingsService. This supports the unified settings architecture where all
    /// settings go to a single master file.
    /// </remarks>
    public DriveStateSettings CaptureDriveStateSettings()
    {
        var settings = new DriveStateSettings
        {
            Controllers = []
        };

        // Get current disk status from provider
        var snapshot = _diskStatusProvider.Current;

        // Group drives by slot
        var drivesBySlot = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<(int DriveNumber, string ImagePath)>>();

        foreach (var drive in snapshot.Drives)
        {
            // Only capture drives with disk images (including ghost disks)
            if (!string.IsNullOrWhiteSpace(drive.DiskImagePath))
            {
                if (!drivesBySlot.ContainsKey(drive.SlotNumber))
                {
                    drivesBySlot[drive.SlotNumber] = [];
                }

                drivesBySlot[drive.SlotNumber].Add((drive.DriveNumber, drive.DiskImagePath));
            }
        }

        // Create controller entries
        foreach (var kvp in drivesBySlot)
        {
            var controller = new DiskControllerEntry
            {
                Slot = kvp.Key,
                Drives = []
            };

            foreach (var (driveNumber, imagePath) in kvp.Value)
            {
                controller.Drives.Add(new DriveEntry
                {
                    Drive = driveNumber,
                    ImagePath = imagePath
                });
            }

            settings.Controllers.Add(controller);
        }

        return settings;
    }

    /// <summary>
    /// Restores drive states from DriveStateSettings (from master GUI settings file).
    /// </summary>
    /// <param name="settings">Drive state settings to restore.</param>
    /// <remarks>
    /// This method restores disk images from the DriveStateSettings section of the master
    /// pandowdy-settings.json file. It should be called during application startup after
    /// loading settings via GuiSettingsService.
    /// </remarks>
    public void RestoreDriveState(DriveStateSettings? settings)
    {
        if (settings == null || settings.Controllers.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[DriveStateService] No drive state to restore");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[DriveStateService] Restoring drive state for {settings.Controllers.Count} controller(s)");

        foreach (var controller in settings.Controllers)
        {
            var slotNumber = (SlotNumber)controller.Slot;
            var card = _slots.GetCardIn(slotNumber);

            // Verify it's a Disk II controller
            if (card is not Pandowdy.EmuCore.Cards.DiskIIControllerCard diskController)
            {
                System.Diagnostics.Debug.WriteLine($"[DriveStateService] Slot {controller.Slot} is not a Disk II controller");
                continue;
            }

            foreach (var driveEntry in controller.Drives)
            {
                // Skip empty entries
                if (string.IsNullOrWhiteSpace(driveEntry.ImagePath))
                {
                    continue;
                }

                System.Diagnostics.Debug.WriteLine($"[DriveStateService] Processing S{controller.Slot}D{driveEntry.Drive}: {driveEntry.ImagePath}");

                // Drive number is 1-based in our model, but 0-based in array
                var driveIndex = driveEntry.Drive - 1;

                if (driveIndex < 0 || driveIndex >= diskController.Drives.Length)
                {
                    System.Diagnostics.Debug.WriteLine($"[DriveStateService] Invalid drive index: {driveIndex}");
                    continue;
                }

                // Check if file exists before attempting to load
                if (File.Exists(driveEntry.ImagePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[DriveStateService] File exists, inserting disk");
                    diskController.Drives[driveIndex].InsertDisk(driveEntry.ImagePath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DriveStateService] File does not exist, skipping (drive will remain empty)");
                }
            }
        }

        System.Diagnostics.Debug.WriteLine("[DriveStateService] Drive state restoration complete");
    }

    /// <inheritdoc/>
    public async Task LoadAndRestoreDriveStateAsync()
    {
        var config = await LoadDriveStateAsync();

        System.Diagnostics.Debug.WriteLine($"[DriveStateService] Restoring {config.Drives.Count} drive(s)");

        // Restore each drive state
        foreach (var entry in config.Drives)
        {
            // Skip empty entries
            if (string.IsNullOrWhiteSpace(entry.DiskImagePath))
            {
                continue;
            }

            System.Diagnostics.Debug.WriteLine($"[DriveStateService] Processing S{entry.Slot}D{entry.DriveNumber}: {entry.DiskImagePath}");

            // Get the slot number and retrieve the card
            var slotNumber = (SlotNumber)entry.Slot;
            var card = _slots.GetCardIn(slotNumber);

            // Verify it's a Disk II controller
            if (card is not Pandowdy.EmuCore.Cards.DiskIIControllerCard diskController)
            {
                System.Diagnostics.Debug.WriteLine($"[DriveStateService] Slot {entry.Slot} is not a Disk II controller");
                continue;
            }

            // Drive number is 1-based in our model, but 0-based in array
            var driveIndex = entry.DriveNumber - 1;

            if (driveIndex < 0 || driveIndex >= diskController.Drives.Length)
            {
                System.Diagnostics.Debug.WriteLine($"[DriveStateService] Invalid drive index: {driveIndex}");
                continue;
            }

            // Check if file exists before attempting to load
            if (File.Exists(entry.DiskImagePath))
            {
                System.Diagnostics.Debug.WriteLine($"[DriveStateService] File exists, inserting disk");
                // File exists - insert the disk normally
                diskController.Drives[driveIndex].InsertDisk(entry.DiskImagePath);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DriveStateService] File does not exist, skipping (drive will remain empty)");
            }
            // If file doesn't exist, skip loading - the drive will remain empty, matching real hardware behavior
        }

        System.Diagnostics.Debug.WriteLine($"[DriveStateService] Drive restoration complete");
    }
}
