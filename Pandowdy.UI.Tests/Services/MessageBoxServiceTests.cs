// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Threading.Tasks;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.Services;
using Xunit;

namespace Pandowdy.UI.Tests.Services;

/// <summary>
/// Tests for the <see cref="MessageBoxService"/> class.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Testing Strategy:</strong> Since MessageBoxService displays actual UI dialogs,
/// full integration testing requires a running Avalonia application with a main window.
/// These tests focus on service instantiation and contract verification.
/// </para>
/// <para>
/// <strong>Integration Testing:</strong> Dialog functionality is validated through
/// manual integration testing and end-to-end UI tests where the dialogs are triggered
/// by actual user actions (e.g., Insert Disk, Save As, Eject with unsaved changes).
/// </para>
/// </remarks>
public class MessageBoxServiceTests
{
    #region Construction Tests

    [Fact]
    public void Constructor_CreatesInstance_Successfully()
    {
        // Act
        var service = new MessageBoxService();

        // Assert
        Assert.NotNull(service);
        Assert.IsAssignableFrom<IMessageBoxService>(service);
    }

    #endregion

    #region Interface Contract Tests

    [Fact]
    public void ShowErrorAsync_ReturnsTask_WhenNoWindowAvailable()
    {
        // Arrange
        var service = new MessageBoxService();

        // Act
        var task = service.ShowErrorAsync("Test Title", "Test Message");

        // Assert
        Assert.NotNull(task);
        Assert.True(task.IsCompleted); // Should complete immediately when no window available
    }

    [Fact]
    public async Task ShowConfirmationAsync_ReturnsTask_WhenNoWindowAvailable()
    {
        // Arrange
        var service = new MessageBoxService();

        // Act
        var result = await service.ShowConfirmationAsync("Test Title", "Test Message");

        // Assert
        Assert.False(result); // Should default to false (No) when no window available
    }

    #endregion

    // Note: Full dialog display tests require Avalonia UI thread and a main window.
    // These are tested through integration tests where:
    // 1. User triggers Insert Disk command -> dialog shows -> error handling tested
    // 2. User triggers Save As command -> dialog shows -> path selection tested
    // 3. User ejects dirty disk -> confirmation dialog shows -> Yes/No tested
}
