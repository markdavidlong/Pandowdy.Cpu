// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.UI.Models;
using Pandowdy.UI.ViewModels;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pandowdy.UI.Services;

/// <summary>
/// Master settings service that consolidates all GUI configuration into a single file.
/// </summary>
/// <remarks>
/// This service handles all GUI settings via a single pandowdy-settings.json file.
/// Pre-release note: Migration logic simplified since no legacy users exist.
/// </remarks>
public class GuiSettingsService
{
    private const string MasterSettingsFileName = "pandowdy-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the full path to the master settings file.
    /// </summary>
    /// <returns>Full path to pandowdy-settings.json in the settings directory.</returns>
    public virtual string GetMasterSettingsFilePath()
    {
        return Path.Combine(GetSettingsDirectory(), MasterSettingsFileName);
    }

    /// <summary>
    /// Gets the directory containing all settings files.
    /// </summary>
    protected virtual string GetSettingsDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "LydianScaleSoftware", "Pandowdy");
    }

    /// <summary>
    /// Loads the master GUI settings file, migrating from legacy files if necessary.
    /// </summary>
    /// <returns>
    /// GuiSettings instance populated with saved values, or default values if no settings file exists.
    /// </returns>
    /// <remarks>
    /// On first run, this will attempt to migrate from legacy settings files and consolidate them.
    /// </remarks>
    public async Task<GuiSettings> LoadAsync()
    {
        var masterFilePath = GetMasterSettingsFilePath();

        if (!File.Exists(masterFilePath))
        {
            System.Diagnostics.Debug.WriteLine("[GuiSettingsService] No settings file found, using defaults");
            return await GetDefaultSettingsAsync();
        }

        try
        {
            var json = await File.ReadAllTextAsync(masterFilePath);
            System.Diagnostics.Debug.WriteLine($"[GuiSettingsService] Loaded master settings from: {masterFilePath}");

            // Start with defaults, then overlay loaded values
            var loadedSettings = JsonSerializer.Deserialize<GuiSettings>(json, JsonOptions);
            if (loadedSettings == null)
            {
                return CreateDefaultSettings();
            }

            return MergeWithDefaults(loadedSettings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GuiSettingsService] Failed to load settings: {ex.Message}");
            // If deserialization fails, return defaults
            return CreateDefaultSettings();
        }
    }

    /// <summary>
    /// Saves the master GUI settings file.
    /// </summary>
    /// <param name="settings">Settings to save.</param>
    public async Task SaveAsync(GuiSettings settings)
    {
        var filePath = GetMasterSettingsFilePath();
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        System.Diagnostics.Debug.WriteLine($"[GuiSettingsService] Saved master settings to: {filePath}");
    }

    /// <summary>
    /// Applies settings to a MainWindowViewModel instance.
    /// </summary>
    /// <param name="viewModel">ViewModel to configure.</param>
    /// <param name="settings">Settings to apply.</param>
    public static void ApplyToViewModel(MainWindowViewModel viewModel, GuiSettings settings)
    {
        // Apply display settings
        if (settings.Display != null)
        {
            if (settings.Display.ShowScanLines.HasValue)
            {
                viewModel.ShowScanLines = settings.Display.ShowScanLines.Value;
            }

            if (settings.Display.ForceMonochrome.HasValue)
            {
                viewModel.ForceMonochrome = settings.Display.ForceMonochrome.Value;
            }

            if (settings.Display.MonoMixed.HasValue)
            {
                viewModel.MonoMixed = settings.Display.MonoMixed.Value;
            }

            if (settings.Display.DecreaseFringing.HasValue)
            {
                viewModel.DecreaseFringing = settings.Display.DecreaseFringing.Value;
            }
        }

        // Apply panel settings
        if (settings.Panels != null)
        {
            if (settings.Panels.ShowSoftSwitchStatus.HasValue)
            {
                viewModel.ShowSoftSwitchStatus = settings.Panels.ShowSoftSwitchStatus.Value;
            }

            if (settings.Panels.ShowDiskStatus.HasValue)
            {
                viewModel.ShowDiskStatus = settings.Panels.ShowDiskStatus.Value;
            }

            if (settings.Panels.DiskPanelWidth.HasValue)
            {
                viewModel.DiskPanelWidth = settings.Panels.DiskPanelWidth.Value;
            }
        }

        // Apply emulator settings
        if (settings.Emulator != null)
        {
            if (settings.Emulator.ThrottleEnabled.HasValue)
            {
                viewModel.ThrottleEnabled = settings.Emulator.ThrottleEnabled.Value;
            }

            if (settings.Emulator.CapsLockEnabled.HasValue)
            {
                viewModel.CapsLockEnabled = settings.Emulator.CapsLockEnabled.Value;
            }
        }
    }

    /// <summary>
    /// Captures current settings from a MainWindowViewModel instance.
    /// </summary>
    /// <param name="viewModel">ViewModel to capture from.</param>
    /// <param name="existingSettings">Existing settings to update (preserves Window and DriveState).</param>
    /// <returns>Updated GuiSettings instance.</returns>
    public static GuiSettings CaptureFromViewModel(MainWindowViewModel viewModel, GuiSettings? existingSettings = null)
    {
        var settings = existingSettings ?? new GuiSettings();

        // Capture display settings
        settings.Display = new DisplaySettings
        {
            ShowScanLines = viewModel.ShowScanLines,
            ForceMonochrome = viewModel.ForceMonochrome,
            MonoMixed = viewModel.MonoMixed,
            DecreaseFringing = viewModel.DecreaseFringing
        };

        // Capture panel settings
        settings.Panels = new PanelSettings
        {
            ShowSoftSwitchStatus = viewModel.ShowSoftSwitchStatus,
            ShowDiskStatus = viewModel.ShowDiskStatus,
            DiskPanelWidth = viewModel.DiskPanelWidth
        };

        // Capture emulator settings
        settings.Emulator = new EmulatorSettings
        {
            ThrottleEnabled = viewModel.ThrottleEnabled,
            CapsLockEnabled = viewModel.CapsLockEnabled
        };

        return settings;
    }

    /// <summary>
    /// Creates default settings when no saved settings exist.
    /// </summary>
    private static GuiSettings CreateDefaultSettings()
    {
        return new GuiSettings
        {
            Window = new GuiWindowSettings
            {
                Width = 1280,
                Height = 768,
                IsMaximized = false
            },
            Display = new DisplaySettings
            {
                ShowScanLines = true,
                ForceMonochrome = false,
                MonoMixed = false,
                DecreaseFringing = true
            },
            Panels = new PanelSettings
            {
                ShowSoftSwitchStatus = true,
                ShowDiskStatus = true,
                DiskPanelWidth = 200.0
            },
            Emulator = new EmulatorSettings
            {
                ThrottleEnabled = true,
                CapsLockEnabled = false
            },
            DriveState = new DriveStateSettings
            {
                Controllers = []
            }
        };
    }

    /// <summary>
    /// Merges loaded settings with defaults, ensuring all null sections get default values.
    /// </summary>
    /// <param name="loaded">Settings loaded from JSON (may have null sections).</param>
    /// <returns>Complete settings with defaults filled in for any null sections.</returns>
    private static GuiSettings MergeWithDefaults(GuiSettings loaded)
    {
        var defaults = CreateDefaultSettings();

        return new GuiSettings
        {
            Window = loaded.Window ?? defaults.Window,
            Display = loaded.Display ?? defaults.Display,
            Panels = loaded.Panels ?? defaults.Panels,
            Emulator = loaded.Emulator ?? defaults.Emulator,
            DriveState = loaded.DriveState ?? defaults.DriveState
        };
    }

    /// <summary>
    /// Returns default settings asynchronously.
    /// </summary>
    /// <remarks>
    /// This is called when no settings file exists on disk.
    /// Pre-release note: Migration logic was removed since no legacy users exist.
    /// </remarks>
    /// <returns>Default GuiSettings instance wrapped in a Task.</returns>
    private static Task<GuiSettings> GetDefaultSettingsAsync()
    {
        System.Diagnostics.Debug.WriteLine("[GuiSettingsService] Creating default settings");
        return Task.FromResult(CreateDefaultSettings());
    }
}
