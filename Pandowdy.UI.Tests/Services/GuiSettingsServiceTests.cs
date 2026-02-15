// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Moq;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.Models;
using Pandowdy.UI.Services;
using Pandowdy.UI.ViewModels;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Pandowdy.UI.Tests.Services;

/// <summary>
/// Tests for GuiSettingsService that consolidates all GUI configuration.
/// </summary>
public class GuiSettingsServiceTests : IDisposable
{
    private readonly string _testSettingsDir;
    private readonly TestableGuiSettingsService _service;

    public GuiSettingsServiceTests()
    {
        // Create a unique temp directory for each test
        _testSettingsDir = Path.Combine(Path.GetTempPath(), "PandowdyTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testSettingsDir);
        _service = new TestableGuiSettingsService(_testSettingsDir);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testSettingsDir))
        {
            Directory.Delete(_testSettingsDir, recursive: true);
        }
    }

    #region Test Helpers

    // Note: ViewModelApply/Capture tests are simplified since they don't need full MainWindowViewModel construction  
    // The Apply/Capture methods can be tested in MainWindowViewModelTests where the full fixture already exists

    #endregion

    #region Load/Save Tests

    [Fact]
    public async Task LoadAsync_WhenNoFileExists_ReturnsDefaultSettings()
    {
        // Act
        var settings = await _service.LoadAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.NotNull(settings.Window);
        Assert.NotNull(settings.Display);
        Assert.NotNull(settings.Panels);
        Assert.NotNull(settings.Emulator);
        Assert.NotNull(settings.DriveState);
        Assert.Equal(1280, settings.Window.Width);
        Assert.Equal(768, settings.Window.Height);
        Assert.True(settings.Display.ShowScanLines);
        Assert.True(settings.Display.DecreaseFringing);
        Assert.Equal(200.0, settings.Panels.DiskPanelWidth);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsSuccessfully()
    {
        // Arrange
        var originalSettings = new GuiSettings
        {
            Window = new GuiWindowSettings
            {
                Left = 100,
                Top = 200,
                Width = 1024,
                Height = 768,
                IsMaximized = true
            },
            Display = new DisplaySettings
            {
                ShowScanLines = true,
                ForceMonochrome = false,
                MonoMixed = true,
                DecreaseFringing = false
            },
            Panels = new PanelSettings
            {
                ShowSoftSwitchStatus = true,
                ShowDiskStatus = false,
                DiskPanelWidth = 350.0
            },
            Emulator = new EmulatorSettings
            {
                ThrottleEnabled = false,
                CapsLockEnabled = true
            }
        };

        // Act
        await _service.SaveAsync(originalSettings);
        var loadedSettings = await _service.LoadAsync();

        // Assert - Window settings
        Assert.Equal(100, loadedSettings.Window?.Left);
        Assert.Equal(200, loadedSettings.Window?.Top);
        Assert.Equal(1024, loadedSettings.Window?.Width);
        Assert.Equal(768, loadedSettings.Window?.Height);
        Assert.True(loadedSettings.Window?.IsMaximized);

        // Assert - Display settings
        Assert.True(loadedSettings.Display?.ShowScanLines);
        Assert.False(loadedSettings.Display?.ForceMonochrome);
        Assert.True(loadedSettings.Display?.MonoMixed);
        Assert.False(loadedSettings.Display?.DecreaseFringing);

        // Assert - Panel settings
        Assert.True(loadedSettings.Panels?.ShowSoftSwitchStatus);
        Assert.False(loadedSettings.Panels?.ShowDiskStatus);
        Assert.Equal(350.0, loadedSettings.Panels?.DiskPanelWidth);

        // Assert - Emulator settings
        Assert.False(loadedSettings.Emulator?.ThrottleEnabled);
        Assert.True(loadedSettings.Emulator?.CapsLockEnabled);
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsCorrupted_ReturnsDefaultSettings()
    {
        // Arrange - Write invalid JSON
        var filePath = _service.GetMasterSettingsFilePath();
        await File.WriteAllTextAsync(filePath, "{ invalid json }");

        // Act
        var settings = await _service.LoadAsync();

        // Assert - Should return defaults without throwing
        Assert.NotNull(settings);
        Assert.NotNull(settings.Window);
        Assert.Equal(1280, settings.Window.Width);
    }

    [Fact]
    public async Task LoadAsync_WhenFileHasOnlyWindowSection_MergesWithDefaults()
    {
        // Arrange - Write partial JSON (only Window section)
        var filePath = _service.GetMasterSettingsFilePath();
        var partialJson = """
        {
          "Window": {
            "Width": 1024,
            "Height": 768,
            "IsMaximized": true
          }
        }
        """;
        await File.WriteAllTextAsync(filePath, partialJson);

        // Act
        var settings = await _service.LoadAsync();

        // Assert - Window section should have loaded values
        Assert.NotNull(settings.Window);
        Assert.Equal(1024, settings.Window.Width);
        Assert.Equal(768, settings.Window.Height);
        Assert.True(settings.Window.IsMaximized);

        // Assert - Missing sections should have defaults
        Assert.NotNull(settings.Display);
        Assert.True(settings.Display.ShowScanLines); // Default value

        Assert.NotNull(settings.Panels);
        Assert.Equal(200.0, settings.Panels.DiskPanelWidth); // Default value

        Assert.NotNull(settings.Emulator);
        Assert.True(settings.Emulator.ThrottleEnabled); // Default value

        Assert.NotNull(settings.DriveState);
        Assert.Empty(settings.DriveState.Controllers); // Default value
    }

    #endregion

    #region Testable Service

    /// <summary>
    /// Testable version of GuiSettingsService that uses a custom settings directory.
    /// </summary>
    private class TestableGuiSettingsService(string testDirectory) : GuiSettingsService
    {
        private readonly string _testDirectory = testDirectory;

        protected override string GetSettingsDirectory()
        {
            return _testDirectory;
        }
    }

    #endregion
}
