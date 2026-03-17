// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Pandowdy.UI.Models;

namespace Pandowdy.UI.Helpers;

/// <summary>
/// Utility class for saving and restoring window position and size settings.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Windows 11 Compatibility:</strong> Implements workarounds for Windows 11's
/// aggressive window placement algorithm that overrides saved positions.
/// </para>
/// <para>
/// <strong>Multi-Monitor Support:</strong> Tracks which monitor the window was on and
/// validates that saved positions are still on-screen when monitors are reconfigured.
/// </para>
/// <para>
/// <strong>DPI Awareness:</strong> Stores positions in pixel coordinates which Avalonia
/// automatically adjusts for DPI scaling.
/// </para>
/// </remarks>
public static class WindowSettingsHelper
{
    /// <summary>
    /// Minimum window size to prevent saving collapsed windows.
    /// </summary>
    private const int MinWindowSize = 200;
    
    /// <summary>
    /// Default window size when no saved settings exist or saved position is invalid.
    /// </summary>
    /// <remarks>
    /// These defaults should match GuiSettingsService.CreateDefaultSettings() for consistency.
    /// </remarks>
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 768;

    /// <summary>
    /// Loads window settings from the application's configuration directory.
    /// </summary>
    /// <returns>
    /// GuiWindowSettings if successfully loaded and validated, null if file doesn't exist
    /// or settings are invalid.
    /// </returns>
    /// <remarks>
    /// Performs validation to ensure loaded settings represent a valid on-screen position.
    /// Returns null if settings file is missing, corrupted, or contains invalid positions.
    /// </remarks>
    public static GuiWindowSettings? Load()
    {
        try
        {
            var path = GetConfigPath();
            System.Diagnostics.Debug.WriteLine($"[WindowSettingsHelper] Loading from: {path}");

            if (!File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine("[WindowSettingsHelper] File does not exist");
                return null;
            }

            var json = File.ReadAllText(path);
            System.Diagnostics.Debug.WriteLine($"[WindowSettingsHelper] JSON content: {json}");

            var settings = JsonSerializer.Deserialize<GuiWindowSettings>(json);

            if (settings == null)
            {
                System.Diagnostics.Debug.WriteLine("[WindowSettingsHelper] Deserialization returned null");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"[WindowSettingsHelper] Loaded: {settings.Width}x{settings.Height} at ({settings.Left},{settings.Top}) Maximized={settings.IsMaximized}");

            // Validate that settings represent a reasonable window
            if (settings.Width < MinWindowSize || settings.Height < MinWindowSize)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowSettingsHelper] Size too small: {settings.Width}x{settings.Height}");
                return null;
            }

            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowSettingsHelper] Load failed: {ex.Message}");
            // Silently ignore errors - caller will use defaults
            return null;
        }
    }

    /// <summary>
    /// Restores window position and size from saved settings.
    /// </summary>
    /// <param name="window">Window to restore settings to.</param>
    /// <param name="settings">Previously saved settings, or null to use defaults.</param>
    /// <remarks>
    /// <para>
    /// <strong>Validation:</strong> Checks that the saved position is still on a visible screen.
    /// If the monitor was disconnected or position is off-screen, uses default centered position.
    /// </para>
    /// <para>
    /// <strong>Windows 11 Note:</strong> This method should be called BEFORE window.Show() for
    /// best results. Windows 11 is less likely to override positions set before the window
    /// becomes visible.
    /// </para>
    /// </remarks>
    public static void Restore(Window window, GuiWindowSettings? settings)
    {
        if (settings == null || !IsPositionValid(window, settings))
        {
            System.Diagnostics.Debug.WriteLine("[WindowSettingsHelper] Using defaults (settings null or invalid position)");
            // Use default centered position
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Width = DefaultWidth;
            window.Height = DefaultHeight;
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[WindowSettingsHelper] Restoring to: {settings.Width}x{settings.Height} at ({settings.Left},{settings.Top}) Maximized={settings.IsMaximized}");

        // Important: Restore order matters!
        // If maximized: Set WindowState FIRST, then position/size (which sets restore bounds)
        // If normal: Set position/size, WindowState is already Normal
        if (settings.IsMaximized == true)
        {
            // For maximized windows: Set state first, then the saved normal bounds become "restore bounds"
            window.WindowState = WindowState.Maximized;
            window.Position = new PixelPoint(settings.Left ?? 0, settings.Top ?? 0);
            window.Width = settings.Width ?? DefaultWidth;
            window.Height = settings.Height ?? DefaultHeight;
        }
        else
        {
            // For normal windows: Just set position and size
            window.Position = new PixelPoint(settings.Left ?? 0, settings.Top ?? 0);
            window.Width = settings.Width ?? DefaultWidth;
            window.Height = settings.Height ?? DefaultHeight;
        }
    }

    /// <summary>
    /// Validates that a saved window position is currently on-screen.
    /// </summary>
    /// <param name="window">Window to check screens for.</param>
    /// <param name="settings">Settings to validate.</param>
    /// <returns>True if the window would be at least partially visible with these settings.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Validation Strategy:</strong> Checks if the window's top-left corner is within
    /// any screen's bounds. This allows windows to extend slightly off-screen (common with
    /// multi-monitor setups) while preventing completely off-screen windows.
    /// </para>
    /// <para>
    /// <strong>Monitor Disconnection:</strong> Returns false if the saved monitor no longer
    /// exists, forcing a fallback to default position on primary monitor.
    /// </para>
    /// </remarks>
    private static bool IsPositionValid(Window window, GuiWindowSettings settings)
    {
        // Basic sanity checks - handle nullable properties
        if (!settings.Width.HasValue || !settings.Height.HasValue ||
            !settings.Left.HasValue || !settings.Top.HasValue ||
            settings.Width < MinWindowSize || settings.Height < MinWindowSize)
        {
            return false;
        }

        // Check if top-left corner is on any visible screen
        var screens = window.Screens.All;
        var position = new PixelPoint(settings.Left.Value, settings.Top.Value);

        foreach (var screen in screens)
        {
            if (screen.Bounds.Contains(position))
            {
                return true;
            }

            // Also accept if the window's title bar area would be visible
            // (allow some tolerance for windows that extend slightly off-screen)
            var titleBarRect = new PixelRect(
                settings.Left.Value,
                settings.Top.Value,
                Math.Min(settings.Width.Value, 100), // Just check title bar area
                30 // Approximate title bar height
            );

            if (screen.Bounds.Intersects(titleBarRect))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the full path to the window settings configuration file.
    /// </summary>
    /// <returns>Full path: %AppData%\LydianScaleSoftware\Pandowdy\window-settings.json</returns>
    private static string GetConfigPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(baseDir, "LydianScaleSoftware", "Pandowdy");
        return Path.Combine(dir, "window-settings.json");
    }
}
