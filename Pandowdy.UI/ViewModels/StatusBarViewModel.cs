// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using ReactiveUI;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// ViewModel for the StatusBar control, aggregating CPU status and system status for display.
/// </summary>
public class StatusBarViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the CPU status view model for displaying CPU state.
    /// </summary>
    public CpuStatusPanelViewModel CpuStatus { get; }

    /// <summary>
    /// Gets the system status view model for displaying system state (including MHz).
    /// </summary>
    public SystemStatusViewModel SystemStatus { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusBarViewModel"/> class.
    /// </summary>
    /// <param name="cpuStatus">The CPU status view model.</param>
    /// <param name="systemStatus">The system status view model.</param>
    public StatusBarViewModel(CpuStatusPanelViewModel cpuStatus, SystemStatusViewModel systemStatus)
    {
        CpuStatus = cpuStatus;
        SystemStatus = systemStatus;
    }
}
