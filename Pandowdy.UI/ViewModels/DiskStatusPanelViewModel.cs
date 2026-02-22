// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Slots;
using Pandowdy.Project.Interfaces;
using Pandowdy.UI.Interfaces;
using ReactiveUI;

using Pandowdy.EmuCore.DiskII;
namespace Pandowdy.UI.ViewModels;

/// <summary>
/// ViewModel for the disk status sidebar panel.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Card-Level Grouping:</strong> Manages an <see cref="ObservableCollection{T}"/>
/// of <see cref="DiskCardPanelViewModel"/> instances, one per disk controller card.
/// Each card panel contains 1-2 drive widgets.
/// </para>
/// <para>
/// <strong>Auto-Update:</strong> Subscribes to <see cref="IDiskStatusProvider.Stream"/>
/// and automatically updates widget ViewModels when drive state changes.
/// </para>
/// <para>
/// <strong>Ordering:</strong> Card panels are ordered by slot (1-7), and drives within
/// each card are ordered by drive number (1-2), matching the physical layout of an Apple IIe.
/// </para>
/// </remarks>
public class DiskStatusPanelViewModel : ReactiveObject
{
    private readonly IDiskStatusProvider _statusProvider;
    private readonly IEmulatorCoreInterface _emulator;
    private readonly IDiskFileDialogService _fileDialogService;
    private readonly IMessageBoxService _messageBoxService;
    private readonly ISkilletProjectManager _projectManager;

    /// <summary>
    /// Gets the collection of disk card panels, grouped by expansion slot.
    /// </summary>
    public ObservableCollection<DiskCardPanelViewModel> Cards { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskStatusPanelViewModel"/> class.
    /// </summary>
    /// <param name="emulator">Emulator core interface for sending card messages.</param>
    /// <param name="statusProvider">Status provider for observing drive state changes.</param>
    /// <param name="fileDialogService">Service for displaying file picker dialogs.</param>
    /// <param name="messageBoxService">Service for displaying error and confirmation dialogs.</param>
    /// <param name="projectManager">Project manager for checking library state.</param>
    public DiskStatusPanelViewModel(
        IEmulatorCoreInterface emulator,
        IDiskStatusProvider statusProvider,
        IDiskFileDialogService fileDialogService,
        IMessageBoxService messageBoxService,
        ISkilletProjectManager projectManager)
    {
        _emulator = emulator;
        _statusProvider = statusProvider;
        _fileDialogService = fileDialogService;
        _messageBoxService = messageBoxService;
        _projectManager = projectManager;

        // Initialize card panels grouped by slot
        Cards = [];

        var initialSnapshot = _statusProvider.Current;
        var drivesBySlot = initialSnapshot.Drives
            .OrderBy(d => d.SlotNumber)
            .ThenBy(d => d.DriveNumber)
            .GroupBy(d => d.SlotNumber);

        foreach (var slotGroup in drivesBySlot)
        {
            var slotNumber = (SlotNumber)slotGroup.Key;
            var drives = new ObservableCollection<DiskStatusWidgetViewModel>();

            foreach (var driveSnapshot in slotGroup)
            {
                drives.Add(new DiskStatusWidgetViewModel(_emulator, _fileDialogService, _messageBoxService, _projectManager, driveSnapshot));
            }

            // TODO: Card name should come from card identification (Phase 3B)
            // For now, hardcode "Disk II Controller" for slots with drives
            var cardPanel = new DiskCardPanelViewModel(
                _emulator,
                _messageBoxService,
                slotNumber,
                "Disk II Controller",
                drives);

            Cards.Add(cardPanel);
        }

        // Subscribe to status updates
        _statusProvider.Stream
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(snapshot => UpdateDrives(snapshot));
    }

    /// <summary>
    /// Updates all drive widgets with new snapshot data.
    /// </summary>
    private void UpdateDrives(DiskStatusSnapshot snapshot)
    {
        // Update each widget with its corresponding drive snapshot
        // Flatten the card panels to get all drive widgets
        var allDrives = Cards.SelectMany(card => card.Drives).ToList();

        for (int i = 0; i < snapshot.Drives.Length && i < allDrives.Count; i++)
        {
            allDrives[i].UpdateSnapshot(snapshot.Drives[i]);
        }
    }
}
