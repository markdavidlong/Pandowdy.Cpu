using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Pandowdy.EmuCore.Services;
using ReactiveUI;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// ViewModel for the disk status sidebar panel.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Observable Collection:</strong> Manages an <see cref="ObservableCollection{T}"/>
/// of <see cref="DiskStatusWidgetViewModel"/> instances, one per drive in the system.
/// </para>
/// <para>
/// <strong>Auto-Update:</strong> Subscribes to <see cref="IDiskStatusProvider.Stream"/>
/// and automatically updates widget ViewModels when drive state changes.
/// </para>
/// <para>
/// <strong>Ordering:</strong> Widgets are ordered by slot (1-7), then drive (1-2),
/// matching the physical layout of an Apple IIe.
/// </para>
/// </remarks>
public class DiskStatusPanelViewModel : ReactiveObject
{
    private readonly IDiskStatusProvider _statusProvider;

    /// <summary>
    /// Gets the collection of disk status widgets (14 drives: 7 slots Ã— 2 drives).
    /// </summary>
    public ObservableCollection<DiskStatusWidgetViewModel> Drives { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskStatusPanelViewModel"/> class.
    /// </summary>
    /// <param name="statusProvider">Status provider for observing drive state changes.</param>
    public DiskStatusPanelViewModel(IDiskStatusProvider statusProvider)
    {
        _statusProvider = statusProvider;

        // Initialize widgets for all 14 drives (7 slots Ã— 2 drives)
        Drives = new ObservableCollection<DiskStatusWidgetViewModel>();

        var initialSnapshot = _statusProvider.Current;
        foreach (var driveSnapshot in initialSnapshot.Drives.OrderBy(d => d.SlotNumber).ThenBy(d => d.DriveNumber))
        {
            Drives.Add(new DiskStatusWidgetViewModel(driveSnapshot));
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
        for (int i = 0; i < snapshot.Drives.Length && i < Drives.Count; i++)
        {
            Drives[i].UpdateSnapshot(snapshot.Drives[i]);
        }
    }
}
