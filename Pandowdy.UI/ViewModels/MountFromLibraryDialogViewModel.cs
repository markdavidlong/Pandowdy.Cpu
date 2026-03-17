// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Pandowdy.Project.Interfaces;
using Pandowdy.Project.Models;
using ReactiveUI;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// ViewModel for the Mount from Library dialog.
/// </summary>
/// <remarks>
/// <para>
/// Displays all disk images from the current project's library and allows the user
/// to select one for mounting into a drive. Replaces the filesystem-based disk insertion
/// workflow with project-based disk selection.
/// </para>
/// <para>
/// <strong>Commands:</strong>
/// - <see cref="SelectDiskCommand"/>: Enabled when a disk image is selected. Closes the dialog with OK result.
/// - <see cref="CancelCommand"/>: Always enabled. Closes the dialog with Cancel result.
/// </para>
/// </remarks>
public sealed class MountFromLibraryDialogViewModel : ReactiveObject
{
    private readonly ISkilletProjectManager _projectManager;
    private DiskImageRecord? _selectedDiskImage;

    /// <summary>
    /// Gets the collection of disk images available in the current project.
    /// </summary>
    public ObservableCollection<DiskImageRecord> DiskImages { get; }

    /// <summary>
    /// Gets or sets the currently selected disk image.
    /// </summary>
    public DiskImageRecord? SelectedDiskImage
    {
        get => _selectedDiskImage;
        set => this.RaiseAndSetIfChanged(ref _selectedDiskImage, value);
    }

    /// <summary>
    /// Gets a value indicating whether a disk image is selected.
    /// </summary>
    public bool HasSelection => SelectedDiskImage != null;

    /// <summary>
    /// Gets the command to select the current disk image and close the dialog.
    /// Enabled only when a disk image is selected.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SelectDiskCommand { get; }

    /// <summary>
    /// Gets the command to cancel the dialog without selecting a disk.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    /// <summary>
    /// Gets a value indicating whether the dialog was completed with OK (SelectDisk).
    /// </summary>
    public bool DialogResult { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MountFromLibraryDialogViewModel"/> class.
    /// </summary>
    /// <param name="projectManager">Project manager for accessing the current project's disk images.</param>
    public MountFromLibraryDialogViewModel(ISkilletProjectManager projectManager)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));

        DiskImages = [];

        // Create observable for command enablement
        var hasSelectionObservable = this.WhenAnyValue(x => x.SelectedDiskImage)
            .Select(selected => selected != null);

        // Initialize commands
        SelectDiskCommand = ReactiveCommand.Create(
            () =>
            {
                DialogResult = true;
            },
            hasSelectionObservable);

        CancelCommand = ReactiveCommand.Create(
            () =>
            {
                DialogResult = false;
            });

        // Load disk images from current project
        _ = LoadDiskImagesAsync();
    }

    /// <summary>
    /// Loads all disk images from the current project into the <see cref="DiskImages"/> collection.
    /// </summary>
    private async Task LoadDiskImagesAsync()
    {
        var currentProject = _projectManager.CurrentProject;
        if (currentProject == null)
        {
            return;
        }

        var diskImages = await currentProject.GetAllDiskImagesAsync();
        DiskImages.Clear();
        foreach (var diskImage in diskImages)
        {
            DiskImages.Add(diskImage);
        }
    }
}
