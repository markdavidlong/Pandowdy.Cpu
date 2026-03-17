// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Moq;
using Pandowdy.Project.Interfaces;
using Pandowdy.Project.Models;
using Pandowdy.UI.ViewModels;
using Xunit;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for the <see cref="MountFromLibraryDialogViewModel"/> class.
/// </summary>
public class MountFromLibraryDialogViewModelTests
{
    #region Construction Tests

    [Fact]
    public void Constructor_WithNullProjectManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<System.ArgumentNullException>(() => new MountFromLibraryDialogViewModel(null!));
    }

    [Fact]
    public async Task Constructor_WithValidProjectManager_InitializesSuccessfully()
    {
        // Arrange
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(new List<DiskImageRecord>());

        // Act
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);
        await Task.Delay(50); // Allow async loading to complete

        // Assert
        Assert.NotNull(viewModel);
        Assert.NotNull(viewModel.DiskImages);
        Assert.NotNull(viewModel.SelectDiskCommand);
        Assert.NotNull(viewModel.CancelCommand);
    }

    #endregion

    #region DiskImages Collection Tests

    [Fact]
    public async Task DiskImages_WhenProjectHasNoImages_IsEmpty()
    {
        // Arrange
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(new List<DiskImageRecord>());

        // Act
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);
        await Task.Delay(50); // Allow async loading to complete

        // Assert
        Assert.Empty(viewModel.DiskImages);
    }

    [Fact]
    public async Task DiskImages_WhenProjectHasImages_LoadsSuccessfully()
    {
        // Arrange
        var diskImages = new List<DiskImageRecord>
        {
            CreateDiskImageRecord(1, "Disk 1"),
            CreateDiskImageRecord(2, "Disk 2"),
            CreateDiskImageRecord(3, "Disk 3")
        };
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(diskImages);

        // Act
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);
        await Task.Delay(50); // Allow async loading to complete

        // Assert
        Assert.Equal(3, viewModel.DiskImages.Count);
        Assert.Equal("Disk 1", viewModel.DiskImages[0].Name);
        Assert.Equal("Disk 2", viewModel.DiskImages[1].Name);
        Assert.Equal("Disk 3", viewModel.DiskImages[2].Name);
    }

    [Fact]
    public async Task DiskImages_WhenProjectIsNull_RemainsEmpty()
    {
        // Arrange
        var mockProjectManager = new Mock<ISkilletProjectManager>();
        mockProjectManager.Setup(pm => pm.CurrentProject).Returns((ISkilletProject?)null);

        // Act
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);
        await Task.Delay(50); // Allow async loading to complete

        // Assert
        Assert.Empty(viewModel.DiskImages);
    }

    #endregion

    #region SelectedDiskImage Tests

    [Fact]
    public void SelectedDiskImage_InitialValue_IsNull()
    {
        // Arrange
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(new List<DiskImageRecord>());

        // Act
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);

        // Assert
        Assert.Null(viewModel.SelectedDiskImage);
    }

    [Fact]
    public async Task SelectedDiskImage_WhenSet_RaisesPropertyChanged()
    {
        // Arrange
        var diskImages = new List<DiskImageRecord> { CreateDiskImageRecord(1, "Test Disk") };
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(diskImages);
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);
        await Task.Delay(50); // Allow async loading to complete

        bool propertyChangedRaised = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(viewModel.SelectedDiskImage))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        viewModel.SelectedDiskImage = diskImages[0];

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal("Test Disk", viewModel.SelectedDiskImage?.Name);
    }

    #endregion

    #region SelectDiskCommand Tests

    [Fact]
    public async Task SelectDiskCommand_WhenNoSelection_IsDisabled()
    {
        // Arrange
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(new List<DiskImageRecord>());
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);

        bool canExecute = false;
        viewModel.SelectDiskCommand.CanExecute.Subscribe(value => canExecute = value);
        await Task.Delay(10); // Allow observable to propagate

        // Act & Assert
        Assert.False(canExecute);
    }

    [Fact]
    public async Task SelectDiskCommand_WhenDiskSelected_IsEnabled()
    {
        // Arrange
        var diskImages = new List<DiskImageRecord> { CreateDiskImageRecord(1, "Test Disk") };
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(diskImages);
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);
        await Task.Delay(50); // Allow async loading to complete

        bool canExecute = false;
        viewModel.SelectDiskCommand.CanExecute.Subscribe(value => canExecute = value);

        // Act
        viewModel.SelectedDiskImage = diskImages[0];
        await Task.Delay(10); // Allow observable to propagate

        // Assert
        Assert.True(canExecute);
    }

    [Fact]
    public async Task SelectDiskCommand_WhenExecuted_SetsDialogResultToTrue()
    {
        // Arrange
        var diskImages = new List<DiskImageRecord> { CreateDiskImageRecord(1, "Test Disk") };
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(diskImages);
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);
        await Task.Delay(50); // Allow async loading to complete
        viewModel.SelectedDiskImage = diskImages[0];

        // Act
        viewModel.SelectDiskCommand.Execute(Unit.Default).Subscribe();

        // Assert
        Assert.True(viewModel.DialogResult);
    }

    #endregion

    #region CancelCommand Tests

    [Fact]
    public async Task CancelCommand_IsAlwaysEnabled()
    {
        // Arrange
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(new List<DiskImageRecord>());
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);

        bool canExecute = false;
        viewModel.CancelCommand.CanExecute.Subscribe(value => canExecute = value);
        await Task.Delay(10); // Allow observable to propagate

        // Act & Assert
        Assert.True(canExecute);
    }

    [Fact]
    public void CancelCommand_WhenExecuted_SetsDialogResultToFalse()
    {
        // Arrange
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(new List<DiskImageRecord>());
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);

        // Act
        viewModel.CancelCommand.Execute(Unit.Default).Subscribe();

        // Assert
        Assert.False(viewModel.DialogResult);
    }

    #endregion

    #region DialogResult Tests

    [Fact]
    public void DialogResult_InitialValue_IsFalse()
    {
        // Arrange
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(new List<DiskImageRecord>());

        // Act
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);

        // Assert
        Assert.False(viewModel.DialogResult);
    }

    #endregion

    #region HasSelection Tests

    [Fact]
    public void HasSelection_WhenNoSelection_ReturnsFalse()
    {
        // Arrange
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(new List<DiskImageRecord>());
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);

        // Act & Assert
        Assert.False(viewModel.HasSelection);
    }

    [Fact]
    public async Task HasSelection_WhenDiskSelected_ReturnsTrue()
    {
        // Arrange
        var diskImages = new List<DiskImageRecord> { CreateDiskImageRecord(1, "Test Disk") };
        var mockProjectManager = CreateMockProjectManagerWithDiskImages(diskImages);
        var viewModel = new MountFromLibraryDialogViewModel(mockProjectManager.Object);
        await Task.Delay(50); // Allow async loading to complete

        // Act
        viewModel.SelectedDiskImage = diskImages[0];

        // Assert
        Assert.True(viewModel.HasSelection);
    }

    #endregion

    #region Helper Methods

    private static Mock<ISkilletProjectManager> CreateMockProjectManagerWithDiskImages(List<DiskImageRecord> diskImages)
    {
        var mockProject = new Mock<ISkilletProject>();
        mockProject
            .Setup(p => p.GetAllDiskImagesAsync())
            .ReturnsAsync(diskImages.AsReadOnly());

        var mockProjectManager = new Mock<ISkilletProjectManager>();
        mockProjectManager.Setup(pm => pm.CurrentProject).Returns(mockProject.Object);

        return mockProjectManager;
    }

    private static DiskImageRecord CreateDiskImageRecord(long id, string name)
    {
        return new DiskImageRecord
        {
            Id = id,
            Name = name,
            OriginalFormat = "WOZ2",
            WholeTrackCount = 35,
            OptimalBitTiming = 32,
            IsWriteProtected = false,
            ImportedUtc = System.DateTime.UtcNow,
            CreatedUtc = System.DateTime.UtcNow
        };
    }

    #endregion
}
