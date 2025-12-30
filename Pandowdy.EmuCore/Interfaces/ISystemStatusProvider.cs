using System;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides read-only access to Apple II system status (soft switches, annunciators, etc.).
/// Supports both event-based and reactive subscriptions for status changes.
/// </summary>
public interface ISystemStatusProvider
{
    // Memory configuration switches
    bool State80Store { get; }
    bool StateRamRd { get; }
    bool StateRamWrt { get; }
    bool StateIntCxRom { get; }
    bool StateAltZp { get; }
    bool StateSlotC3Rom { get; }
    
    // Pushbuttons (game controller)
    bool StatePb0 { get; }
    bool StatePb1 { get; }
    bool StatePb2 { get; }
    
    // Annunciators
    bool StateAnn0 { get; }
    bool StateAnn1 { get; }
    bool StateAnn2 { get; }
    bool StateAnn3_DGR { get; }
    
    // Video mode switches
    bool StatePage2 { get; }
    bool StateHiRes { get; }
    bool StateMixed { get; }
    bool StateTextMode { get; }
    bool StateShow80Col { get; }
    bool StateAltCharSet { get; }
    bool StateFlashOn { get; }
    
    // Language card switches
    bool StatePreWrite { get; }
    bool StateUseBank1 { get; }
    bool StateHighRead { get; }
    bool StateHighWrite { get; }

    /// <summary>
    /// Gets the current system status snapshot.
    /// </summary>
    SystemStatusSnapshot Current { get; }
    
    /// <summary>
    /// Event raised when system status changes (event-style subscription).
    /// </summary>
    event EventHandler<SystemStatusSnapshot>? Changed;
    
    /// <summary>
    /// Observable stream of system status snapshots (reactive subscription).
    /// </summary>
    IObservable<SystemStatusSnapshot> Stream { get; }

    /// <summary>
    /// Mutation hook used by the core (bus) to update status snapshot.
    /// </summary>
    void Mutate(Action<SystemStatusSnapshotBuilder> mutator);
}
