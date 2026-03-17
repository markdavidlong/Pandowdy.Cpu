// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Slots;
using ReactiveUI;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// ViewModel for the Peripherals menu that dynamically discovers and displays installed cards and their drives.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Dynamic Discovery:</strong> Broadcasts <see cref="IdentifyCardMessage"/> to all slots
/// and builds the menu structure from the responses. Empty slots (NullCard with CardId=0) are filtered out.
/// </para>
/// <para>
/// <strong>Reactive Updates:</strong> Subscribes to <see cref="IDiskStatusProvider.Stream"/> to update
/// drive labels (disk filenames) when disks are inserted, ejected, or swapped.
/// </para>
/// <para>
/// <strong>Menu Structure:</strong> Disk controllers are grouped under "Disks" with each drive
/// showing its current disk image or "(empty)". Menu items can be used to open drive management dialogs.
/// </para>
/// </remarks>
public class PeripheralsMenuViewModel : ReactiveObject, IDisposable
{
    private readonly IEmulatorCoreInterface _emulator;
    private readonly ICardResponseProvider _cardResponseProvider;
    private readonly IDiskStatusProvider _diskStatusProvider;
    private readonly IDisposable _cardResponseSubscription;
    private readonly IDisposable _diskStatusSubscription;
    private readonly List<CardInfo> _discoveredCards = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PeripheralsMenuViewModel"/> class.
    /// </summary>
    /// <param name="emulator">Emulator core interface for sending card messages.</param>
    /// <param name="cardResponseProvider">Provider for card response stream.</param>
    /// <param name="diskStatusProvider">Provider for disk status updates.</param>
    public PeripheralsMenuViewModel(
        IEmulatorCoreInterface emulator,
        ICardResponseProvider cardResponseProvider,
        IDiskStatusProvider diskStatusProvider)
    {
        _emulator = emulator;
        _cardResponseProvider = cardResponseProvider;
        _diskStatusProvider = diskStatusProvider;

        DiskMenuItems = [];

        // Subscribe to card responses to discover cards
        _cardResponseSubscription = _cardResponseProvider.Responses
            .Where(r => r.Payload is CardIdentityPayload)
            .Subscribe(OnCardIdentityReceived);

        // Subscribe to disk status changes to update drive labels
        _diskStatusSubscription = _diskStatusProvider.Stream
            .Subscribe(_ => UpdateDriveLabels());

        // Trigger initial card discovery
        _ = DiscoverCardsAsync();
    }

    /// <summary>
    /// Gets the collection of disk controller menu items.
    /// </summary>
    public ObservableCollection<DiskControllerMenuItem> DiskMenuItems { get; }

    /// <summary>
    /// Broadcasts an IdentifyCardMessage to all slots to discover installed cards.
    /// </summary>
    private async System.Threading.Tasks.Task DiscoverCardsAsync()
    {
        try
        {
            // Clear previous discoveries
            _discoveredCards.Clear();

            // Broadcast to all slots (null slot parameter)
            await _emulator.SendCardMessageAsync(null, new IdentifyCardMessage());

            // Give a brief moment for all responses to arrive
            await System.Threading.Tasks.Task.Delay(50);

            // Rebuild menu structure
            RebuildMenu();
        }
        catch
        {
            // Silently handle errors during discovery
        }
    }

    /// <summary>
    /// Handles incoming card identity responses.
    /// </summary>
    private void OnCardIdentityReceived(CardResponse response)
    {
        if (response.Payload is CardIdentityPayload identity)
        {
            // Filter out NullCard (empty slots)
            if (response.CardId == 0)
            {
                return;
            }

            // Store card info
            var cardInfo = new CardInfo(response.Slot, response.CardId, identity.CardName);
            _discoveredCards.Add(cardInfo);

            // Rebuild menu after receiving responses
            RebuildMenu();
        }
    }

    /// <summary>
    /// Rebuilds the menu structure from discovered cards.
    /// </summary>
    private void RebuildMenu()
    {
        DiskMenuItems.Clear();

        // Group by controller type (for now, all are Disk II controllers)
        var diskControllers = _discoveredCards
            .Where(c => c.CardName.Contains("Disk", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Slot)
            .ToList();

        foreach (var controller in diskControllers)
        {
            var menuItem = new DiskControllerMenuItem
            {
                Header = $"Slot {(int)controller.Slot} — {controller.CardName}",
                Slot = controller.Slot
            };

            // Add drive entries (assuming 2 drives per controller)
            for (int driveNum = 1; driveNum <= 2; driveNum++)
            {
                var driveItem = new DriveMenuItem
                {
                    Slot = controller.Slot,
                    DriveNumber = driveNum,
                    Header = GetDriveLabel(controller.Slot, driveNum)
                };

                menuItem.Drives.Add(driveItem);
            }

            DiskMenuItems.Add(menuItem);
        }
    }

    /// <summary>
    /// Updates all drive labels from current disk status.
    /// </summary>
    private void UpdateDriveLabels()
    {
        foreach (var controller in DiskMenuItems)
        {
            foreach (var drive in controller.Drives)
            {
                drive.Header = GetDriveLabel(drive.Slot, drive.DriveNumber);
            }
        }
    }

    /// <summary>
    /// Gets the display label for a drive (e.g., "S6D1 - disk.woz" or "S6D1 - (empty)").
    /// </summary>
    private string GetDriveLabel(SlotNumber slot, int driveNumber)
    {
        // Use GetDriveStatus to find the drive by slot and drive number
        var drive = _diskStatusProvider.GetDriveStatus((int)slot, driveNumber);

        if (drive != null)
        {
            string diskName = string.IsNullOrEmpty(drive.DiskImageFilename)
                ? "(empty)"
                : drive.DiskImageFilename;

            return $"S{(int)slot}D{driveNumber} - {diskName}";
        }

        return $"S{(int)slot}D{driveNumber} - (empty)";
    }

    /// <summary>
    /// Disposes of subscriptions.
    /// </summary>
    public void Dispose()
    {
        _cardResponseSubscription?.Dispose();
        _diskStatusSubscription?.Dispose();
    }

    /// <summary>
    /// Record for storing discovered card information.
    /// </summary>
    private record CardInfo(SlotNumber Slot, int CardId, string CardName);
}

/// <summary>
/// Menu item for a disk controller.
/// </summary>
public class DiskControllerMenuItem : ReactiveObject
{
    private string _header = "";

    /// <summary>
    /// Gets or sets the menu header text.
    /// </summary>
    public string Header
    {
        get => _header;
        set => this.RaiseAndSetIfChanged(ref _header, value);
    }

    /// <summary>
    /// Gets or sets the slot number for this controller.
    /// </summary>
    public SlotNumber Slot { get; set; }

    /// <summary>
    /// Gets the collection of drive menu items for this controller.
    /// </summary>
    public ObservableCollection<DriveMenuItem> Drives { get; } = [];
}

/// <summary>
/// Menu item for a disk drive.
/// </summary>
public class DriveMenuItem : ReactiveObject
{
    private string _header = "";

    /// <summary>
    /// Gets or sets the menu header text.
    /// </summary>
    public string Header
    {
        get => _header;
        set => this.RaiseAndSetIfChanged(ref _header, value);
    }

    /// <summary>
    /// Gets or sets the slot number for this drive's controller.
    /// </summary>
    public SlotNumber Slot { get; set; }

    /// <summary>
    /// Gets or sets the drive number (1-2).
    /// </summary>
    public int DriveNumber { get; set; }
}
