// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Slots;
using Pandowdy.EmuCore.DiskII.Messages;
using Pandowdy.UI.Interfaces;
using ReactiveUI;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// ViewModel for a disk controller card panel, grouping drives from the same expansion slot.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Card-Level Grouping:</strong> Each DiskCardPanel represents one disk controller card
/// and contains 1-2 DiskStatusWidgetViewModel children (one per drive attached to that controller).
/// </para>
/// <para>
/// <strong>Swap Command:</strong> The "Swap Drives" command is enabled only when the card has
/// 2 drives and at least one drive has a disk inserted.
/// </para>
/// </remarks>
public class DiskCardPanelViewModel : ReactiveObject
{
    private readonly IEmulatorCoreInterface _emulator;
    private readonly IMessageBoxService _messageBoxService;
    private readonly SlotNumber _slot;

    /// <summary>
    /// Gets the slot number for this card (e.g., Slot5, Slot6).
    /// </summary>
    public SlotNumber Slot => _slot;

    /// <summary>
    /// Gets the card name (e.g., "Disk II Controller").
    /// </summary>
    public string CardName { get; }

    /// <summary>
    /// Gets the display title for the card header (e.g., "Disk II — Slot 6").
    /// </summary>
    public string CardTitle => $"{CardName} — Slot {(int)_slot}";

    /// <summary>
    /// Gets the collection of drive status widgets for this card (1 or 2 drives).
    /// </summary>
    public ObservableCollection<DiskStatusWidgetViewModel> Drives { get; }

    /// <summary>
    /// Gets the command for swapping drives on this card.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SwapDrivesCommand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskCardPanelViewModel"/> class.
    /// </summary>
    /// <param name="emulator">Emulator core interface for sending card messages.</param>
    /// <param name="messageBoxService">Service for displaying error dialogs.</param>
    /// <param name="slot">The expansion slot number containing this card.</param>
    /// <param name="cardName">The human-readable card name.</param>
    /// <param name="drives">Collection of drive ViewModels for this card.</param>
    public DiskCardPanelViewModel(
        IEmulatorCoreInterface emulator,
        IMessageBoxService messageBoxService,
        SlotNumber slot,
        string cardName,
        ObservableCollection<DiskStatusWidgetViewModel> drives)
    {
        _emulator = emulator;
        _messageBoxService = messageBoxService;
        _slot = slot;
        CardName = cardName;
        Drives = drives;

        // SwapDrivesCommand enabled when card has 2 drives and at least one has a disk
        // Create an observable that monitors changes to HasDisk on any drive in the collection
        var driveObservables = Drives.Select(drive =>
            drive.WhenAnyValue(d => d.HasDisk));

        var canSwapObservable = Observable.CombineLatest(driveObservables)
            .Select(_ => Drives.Count == 2 && Drives.Any(d => d.HasDisk));

        SwapDrivesCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                try
                {
                    await _emulator.SendCardMessageAsync(_slot, new SwapDrivesMessage());
                }
                catch (CardMessageException ex)
                {
                    await _messageBoxService.ShowErrorAsync("Swap Drives Failed", ex.Message);
                }
            },
            canSwapObservable);
    }
}
