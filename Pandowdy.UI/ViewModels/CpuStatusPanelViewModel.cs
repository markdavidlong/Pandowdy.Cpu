// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Reactive.Linq;
using ReactiveUI;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.UI.Interfaces;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// View model for the CPU status panel displaying real-time processor state.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides reactive properties for displaying the current CPU state
/// including registers (PC, SP), status flags (N, V, B, D, I, Z, C), and execution status.
/// Updates at 60Hz synchronized with the display refresh.
/// </para>
/// <para>
/// <strong>Update Pattern:</strong> Subscribes to the <see cref="IRefreshTicker"/> to poll
/// the CPU state at 60Hz. Uses IActivatableViewModel to manage subscription lifecycle.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> All property updates are marshaled to the UI thread via
/// RxApp.MainThreadScheduler, ensuring safe binding to UI elements.
/// </para>
/// </remarks>
public sealed class CpuStatusPanelViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly IEmulatorCoreInterface _emulator;
    private readonly IRefreshTicker _refreshTicker;

    /// <summary>
    /// Gets the view model activator for managing subscription lifecycle.
    /// </summary>
    public ViewModelActivator Activator { get; } = new();

    #region Register Properties

    private ushort _pc;
    /// <summary>
    /// Gets the current Program Counter (PC) value.
    /// </summary>
    public ushort PC
    {
        get => _pc;
        private set => this.RaiseAndSetIfChanged(ref _pc, value);
    }

    private byte _sp;
    /// <summary>
    /// Gets the current Stack Pointer (SP) value.
    /// </summary>
    public byte SP
    {
        get => _sp;
        private set => this.RaiseAndSetIfChanged(ref _sp, value);
    }

    private int _stackSize;
    /// <summary>
    /// Gets the current stack depth (number of bytes on stack).
    /// </summary>
    /// <remarks>
    /// Calculated as 0xFF - SP. When SP is $FF, stack is empty (0 bytes).
    /// When SP is $00, stack is full (255 bytes).
    /// </remarks>
    public int StackSize
    {
        get => _stackSize;
        private set => this.RaiseAndSetIfChanged(ref _stackSize, value);
    }

    private byte _a;
    /// <summary>
    /// Gets the current Accumulator (A) value.
    /// </summary>
    public byte A
    {
        get => _a;
        private set => this.RaiseAndSetIfChanged(ref _a, value);
    }

    private byte _x;
    /// <summary>
    /// Gets the current X Index Register value.
    /// </summary>
    public byte X
    {
        get => _x;
        private set => this.RaiseAndSetIfChanged(ref _x, value);
    }

    private byte _y;
    /// <summary>
    /// Gets the current Y Index Register value.
    /// </summary>
    public byte Y
    {
        get => _y;
        private set => this.RaiseAndSetIfChanged(ref _y, value);
    }

    #endregion

    #region Flag Properties

    private bool _flagN;
    /// <summary>
    /// Gets the Negative flag (N) state.
    /// </summary>
    public bool FlagN
    {
        get => _flagN;
        private set => this.RaiseAndSetIfChanged(ref _flagN, value);
    }

    private bool _flagV;
    /// <summary>
    /// Gets the Overflow flag (V) state.
    /// </summary>
    public bool FlagV
    {
        get => _flagV;
        private set => this.RaiseAndSetIfChanged(ref _flagV, value);
    }

    private bool _flagB;
    /// <summary>
    /// Gets the Break flag (B) state.
    /// </summary>
    public bool FlagB
    {
        get => _flagB;
        private set => this.RaiseAndSetIfChanged(ref _flagB, value);
    }

    private bool _flagD;
    /// <summary>
    /// Gets the Decimal mode flag (D) state.
    /// </summary>
    public bool FlagD
    {
        get => _flagD;
        private set => this.RaiseAndSetIfChanged(ref _flagD, value);
    }

    private bool _flagI;
    /// <summary>
    /// Gets the Interrupt disable flag (I) state.
    /// </summary>
    public bool FlagI
    {
        get => _flagI;
        private set => this.RaiseAndSetIfChanged(ref _flagI, value);
    }

    private bool _flagZ;
    /// <summary>
    /// Gets the Zero flag (Z) state.
    /// </summary>
    public bool FlagZ
    {
        get => _flagZ;
        private set => this.RaiseAndSetIfChanged(ref _flagZ, value);
    }

    private bool _flagC;
    /// <summary>
    /// Gets the Carry flag (C) state.
    /// </summary>
    public bool FlagC
    {
        get => _flagC;
        private set => this.RaiseAndSetIfChanged(ref _flagC, value);
    }

    #endregion

    #region Status Properties

    private CpuExecutionStatus _status;
    /// <summary>
    /// Gets the current CPU execution status.
    /// </summary>
    public CpuExecutionStatus Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private string _statusText = "Running";
    /// <summary>
    /// Gets the execution status as display text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuStatusPanelViewModel"/> class.
    /// </summary>
    /// <param name="emulator">The emulator core interface for accessing CPU state.</param>
    /// <param name="refreshTicker">The 60Hz refresh ticker for polling updates.</param>
    public CpuStatusPanelViewModel(IEmulatorCoreInterface emulator, IRefreshTicker refreshTicker)
    {
        _emulator = emulator ?? throw new ArgumentNullException(nameof(emulator));
        _refreshTicker = refreshTicker ?? throw new ArgumentNullException(nameof(refreshTicker));

        // Use WhenActivated to manage subscription lifecycle
        // ReactiveUserControl triggers activation when view is loaded
        this.WhenActivated(disposables =>
        {
            var sub = _refreshTicker.Stream
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateFromCpuState());
            disposables.Add(sub);
        });
    }

    /// <summary>
    /// Updates all properties from the current CPU state snapshot.
    /// </summary>
    private void UpdateFromCpuState()
    {
        var cpu = _emulator.CpuState;

        // Registers
        PC = cpu.PC;
        SP = cpu.SP;
        StackSize = 0xFF - cpu.SP;
        A = cpu.A;
        X = cpu.X;
        Y = cpu.Y;

        // Flags
        FlagN = cpu.FlagN;
        FlagV = cpu.FlagV;
        FlagB = cpu.FlagB;
        FlagD = cpu.FlagD;
        FlagI = cpu.FlagI;
        FlagZ = cpu.FlagZ;
        FlagC = cpu.FlagC;

        // Status
        Status = cpu.Status;
        StatusText = cpu.Status switch
        {
            CpuExecutionStatus.Running => "Running",
            CpuExecutionStatus.Stopped => "Stopped",
            CpuExecutionStatus.Jammed => "Jammed",
            CpuExecutionStatus.Waiting => "Waiting",
            CpuExecutionStatus.Bypassed => "Bypassed",
            _ => "Unknown"
        };
    }
}
