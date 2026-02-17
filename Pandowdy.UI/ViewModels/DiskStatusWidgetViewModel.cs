// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Media;
using Pandowdy.EmuCore.Cards;
using Pandowdy.EmuCore.DiskII.Messages;
using Pandowdy.EmuCore.Exceptions;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Pandowdy.Project.Interfaces;
using Pandowdy.UI.Interfaces;
using ReactiveUI;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// ViewModel for a single disk drive status widget.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Observable Properties:</strong> All properties are reactive and update
/// automatically when the underlying <see cref="DiskDriveStatusSnapshot"/> changes.
/// </para>
/// <para>
/// <strong>Color Coding:</strong> Uses red for read-only indicators to provide
/// visual feedback about write-protection status.
/// </para>
/// <para>
/// <strong>Commands:</strong> Provides reactive commands for disk operations (insert, eject,
/// save, etc.) that are enabled/disabled based on drive state.
/// </para>
/// </remarks>
public class DiskStatusWidgetViewModel : ReactiveObject
{
    private readonly IEmulatorCoreInterface _emulator;
    private readonly IDiskFileDialogService _fileDialogService;
    private readonly IMessageBoxService _messageBoxService;
    private readonly ISkilletProjectManager _projectManager;
    private DiskDriveStatusSnapshot _snapshot;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskStatusWidgetViewModel"/> class.
    /// </summary>
    /// <param name="emulator">Emulator core interface for sending card messages.</param>
    /// <param name="fileDialogService">Service for displaying file picker dialogs.</param>
    /// <param name="messageBoxService">Service for displaying error and confirmation dialogs.</param>
    /// <param name="projectManager">Project manager for checking library state.</param>
    /// <param name="initialSnapshot">Initial drive status snapshot.</param>
    public DiskStatusWidgetViewModel(
        IEmulatorCoreInterface emulator,
        IDiskFileDialogService fileDialogService,
        IMessageBoxService messageBoxService,
        ISkilletProjectManager projectManager,
        DiskDriveStatusSnapshot initialSnapshot)
    {
        _emulator = emulator;
        _fileDialogService = fileDialogService;
        _messageBoxService = messageBoxService;
        _projectManager = projectManager;
        _snapshot = initialSnapshot;

        // Create observables for command enablement
        var hasDiskObservable = this.WhenAnyValue(x => x.HasDisk);
        var canSaveObservable = this.WhenAnyValue(
            x => x.HasDisk,
            x => x.HasDestinationPath,
            x => x.IsDirty,
            (hasDisk, hasDest, isDirty) => hasDisk && hasDest && isDirty);

        // Check if project library has disk images (disable Insert when empty)
        var diskImages = _projectManager.CurrentProject?.GetAllDiskImagesAsync().GetAwaiter().GetResult();
        var hasLibraryImages = diskImages != null && diskImages.Count > 0;
        var canInsertObservable = System.Reactive.Linq.Observable.Return(hasLibraryImages);

        // Commands that show file dialogs
        InsertDiskCommand = ReactiveCommand.CreateFromTask(
            async () => await InsertDiskWithDialogAsync(),
            canInsertObservable);
        InsertBlankDiskCommand = ReactiveCommand.CreateFromTask(async () => await InsertBlankDiskAsync());

        EjectDiskCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                // Show confirmation if disk has unsaved changes
                if (_snapshot.IsDirty)
                {
                    var confirmed = await _messageBoxService.ShowConfirmationAsync(
                        "Unsaved Changes",
                        "Disk has unsaved changes. Eject anyway?");
                    if (!confirmed)
                    {
                        return;
                    }
                }

                await _emulator.SendCardMessageAsync(
                    (SlotNumber)_snapshot.SlotNumber,
                    new EjectDiskMessage(_snapshot.DriveNumber));
            },
            hasDiskObservable);

        SaveCommand = ReactiveCommand.CreateFromTask(
            async () => await _emulator.SendCardMessageAsync(
                (SlotNumber)_snapshot.SlotNumber,
                new SaveDiskMessage(_snapshot.DriveNumber)),
            canSaveObservable);

        SaveAsCommand = ReactiveCommand.CreateFromTask(async () => await SaveAsAsync(), hasDiskObservable);

        ToggleWriteProtectCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                // Toggle the current state
                bool newState = !_snapshot.IsReadOnly;
                await _emulator.SendCardMessageAsync(
                    (SlotNumber)_snapshot.SlotNumber,
                    new SetWriteProtectMessage(_snapshot.DriveNumber, newState));
            },
            hasDiskObservable);
    }

    /// <summary>
    /// Inserts a disk image from a file path (called by drag-and-drop or command).
    /// </summary>
    /// <param name="filePath">The full path to the disk image file.</param>
    /// <remarks>
    /// PHASE 2a: This method is temporarily disabled during the transition to project-based
    /// disk loading. Filesystem-based disk insertion has been removed. Use the "Mount from
    /// Library" workflow instead (import disk to project, then mount via MountDiskMessage).
    /// This method will be replaced with the mount-from-library picker in Step 14.
    /// </remarks>
    [Obsolete("Filesystem-based disk loading removed in Phase 2a. Use Mount from Library workflow.")]
    public async Task InsertDiskAsync(string filePath)
    {
        await Task.CompletedTask; // Silence async warning
        throw new NotSupportedException(
            "Direct filesystem disk insertion is no longer supported. " +
            "Import the disk image to the project first, then mount it from the library.");
    }

    private async Task InsertDiskWithDialogAsync()
    {
        // Show Mount from Library dialog
        var selectedDisk = await _messageBoxService.ShowMountFromLibraryDialogAsync();

        if (selectedDisk != null)
        {
            try
            {
                await _emulator.SendCardMessageAsync(
                    (SlotNumber)_snapshot.SlotNumber,
                    new MountDiskMessage(_snapshot.DriveNumber, selectedDisk.Id));
            }
            catch (CardMessageException ex)
            {
                await _messageBoxService.ShowErrorAsync("Mount Disk Failed", ex.Message);
            }
        }
    }

    private async Task InsertBlankDiskAsync()
    {
        await _emulator.SendCardMessageAsync(
            (SlotNumber)_snapshot.SlotNumber,
            new InsertBlankDiskMessage(_snapshot.DriveNumber));
    }

    private async Task SaveAsAsync()
    {
        var suggestedName = _snapshot.DiskImageFilename;
        var filePath = await _fileDialogService.ShowSaveFileDialogAsync(suggestedName);

        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                await _emulator.SendCardMessageAsync(
                    (SlotNumber)_snapshot.SlotNumber,
                    new SaveDiskAsMessage(_snapshot.DriveNumber, filePath));
            }
            catch (CardMessageException ex)
            {
                await _messageBoxService.ShowErrorAsync("Save Disk Failed", ex.Message);
            }
        }
    }

    /// <summary>
    /// Gets the command for inserting a disk image.
    /// </summary>
    public ReactiveCommand<Unit, Unit> InsertDiskCommand { get; }

    /// <summary>
    /// Gets the command for inserting a blank disk.
    /// </summary>
    public ReactiveCommand<Unit, Unit> InsertBlankDiskCommand { get; }

    /// <summary>
    /// Gets the command for ejecting the current disk.
    /// </summary>
    public ReactiveCommand<Unit, Unit> EjectDiskCommand { get; }

    /// <summary>
    /// Gets the command for saving the disk to its attached destination path.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    /// <summary>
    /// Gets the command for saving the disk to a user-chosen path (Save As).
    /// </summary>
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }

    /// <summary>
    /// Gets the command for toggling write-protect state.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ToggleWriteProtectCommand { get; }

    /// <summary>
    /// Updates the widget with a new snapshot.
    /// </summary>
    public void UpdateSnapshot(DiskDriveStatusSnapshot snapshot)
    {
        _snapshot = snapshot;
        this.RaisePropertyChanged(nameof(DiskId));
        this.RaisePropertyChanged(nameof(HasDisk));
        this.RaisePropertyChanged(nameof(IsDirty));
        this.RaisePropertyChanged(nameof(HasDestinationPath));
        this.RaisePropertyChanged(nameof(Filename));
        this.RaisePropertyChanged(nameof(DiskImagePathTooltip));
        this.RaisePropertyChanged(nameof(FilenameForeground));
        this.RaisePropertyChanged(nameof(TrackSectorText));
        this.RaisePropertyChanged(nameof(TrackSectorForeground));
        this.RaisePropertyChanged(nameof(PhaseText));
        this.RaisePropertyChanged(nameof(MotorText));
    }

    /// <summary>
    /// Gets the disk identifier (e.g., "S6D1").
    /// </summary>
    public string DiskId => _snapshot.DiskId;

    /// <summary>
    /// Gets a value indicating whether a disk is inserted in this drive.
    /// </summary>
    public bool HasDisk => !string.IsNullOrEmpty(_snapshot.DiskImageFilename);

    /// <summary>
    /// Gets a value indicating whether the disk has unsaved changes.
    /// </summary>
    public bool IsDirty => _snapshot.IsDirty;

    /// <summary>
    /// Gets a value indicating whether the disk has an attached destination path for Save operations.
    /// </summary>
    public bool HasDestinationPath => _snapshot.HasDestinationPath;

    /// <summary>
    /// Gets the filename without path, or "(empty)" if no disk.
    /// </summary>
    public string Filename =>
        string.IsNullOrEmpty(_snapshot.DiskImageFilename) ? "(empty)" : _snapshot.DiskImageFilename;

    /// <summary>
    /// Gets the full disk image path for tooltip display.
    /// </summary>
    public string DiskImagePathTooltip =>
        string.IsNullOrEmpty(_snapshot.DiskImagePath) ? "No disk inserted" : _snapshot.DiskImagePath;

    /// <summary>
    /// Gets the foreground color for filename (red if read-only).
    /// </summary>
    public IBrush FilenameForeground =>
        _snapshot.IsReadOnly ? Brushes.Red : Brushes.White;

    /// <summary>
    /// Gets the track/sector display text.
    /// </summary>
    /// <remarks>
    /// Format: "T#.## S##" or "T-- S--" if no disk.
    /// </remarks>
    public string TrackSectorText
    {
        get
        {
            if (string.IsNullOrEmpty(_snapshot.DiskImageFilename))
            {
                return "T-- S--";
            }

            string trackStr = $"T:{_snapshot.Track:F2}";
            string sectorStr = _snapshot.Sector >= 0 ? $"S:{_snapshot.Sector:D2}" : "S:--";
            return $"{trackStr} {sectorStr}";
        }
    }

    /// <summary>
    /// Gets the foreground color for track/sector (red if read-only).
    /// </summary>
    public IBrush TrackSectorForeground =>
        _snapshot.IsReadOnly ? Brushes.Red : Brushes.White;

    /// <summary>
    /// Gets the phase display text.
    /// </summary>
    /// <remarks>
    /// Format: "----" (all off) to "++++" (all on).
    /// Each character represents one phase (0-3).
    /// </remarks>
    public string PhaseText
    {
        get
        {
            char p0 = (_snapshot.PhaseState & 0b0001) != 0 ? '+' : '-';
            char p1 = (_snapshot.PhaseState & 0b0010) != 0 ? '+' : '-';
            char p2 = (_snapshot.PhaseState & 0b0100) != 0 ? '+' : '-';
            char p3 = (_snapshot.PhaseState & 0b1000) != 0 ? '+' : '-';
            return $"ϕ:{p0}{p1}{p2}{p3}";
        }
    }

    /// <summary>
    /// Gets the motor status display text.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>"⌚" = Motor-off scheduled (delayed)</item>
    /// <item>"⚡" = Motor running</item>
    /// </list>
    /// </remarks>
    public string MotorText
    {
        get
        {
            var status = "";
            if (_snapshot.MotorOffScheduled)
            {
                status += "⌚";
            }
            if (_snapshot.MotorOn)
            {
                status += "⚡";
            }
            return status;
        }
    }
}
