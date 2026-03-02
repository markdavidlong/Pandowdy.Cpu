// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Memory;
using System.Reactive.Linq;
using ReactiveUI;
using Pandowdy.UI.Interfaces;
using Pandowdy.Disassembler;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// View model for the CPU status panel displaying real-time processor state.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides reactive properties for displaying the current CPU state
/// including registers (PC, SP), status flags (N, V, B, D, I, Z, C), and execution status.
/// Updates at the base ticker frequency (see <see cref="Constants.RefreshRates.BaseTickerHz"/>)
/// synchronized with the display refresh.
/// </para>
/// <para>
/// <strong>Update Pattern:</strong> Subscribes directly to the <see cref="IRefreshTicker"/> to poll
/// the CPU state at full refresh rate with no sampling overhead. Uses IActivatableViewModel to
/// manage subscription lifecycle.
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
    private bool _isPoweredOn;

    /// <summary>
    /// Gets the view model activator for managing subscription lifecycle.
    /// </summary>
    public ViewModelActivator Activator { get; } = new();

    /// <summary>
    /// Sets the powered-on state, masking all CPU display values when powered off.
    /// </summary>
    /// <param name="poweredOn">True if the emulator is powered on; false to show powered-off placeholders.</param>
    public void SetPoweredOn(bool poweredOn)
    {
        _isPoweredOn = poweredOn;
        UpdateFromCpuState();
    }

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

    #region Display Properties (powered-off masking)

    private string _pcDisplay = "----";
    /// <summary>
    /// Gets the Program Counter as display text ("----" when powered off).
    /// </summary>
    public string PCDisplay
    {
        get => _pcDisplay;
        private set => this.RaiseAndSetIfChanged(ref _pcDisplay, value);
    }

    private string _aDisplay = "--";
    /// <summary>
    /// Gets the Accumulator as display text ("--" when powered off).
    /// </summary>
    public string ADisplay
    {
        get => _aDisplay;
        private set => this.RaiseAndSetIfChanged(ref _aDisplay, value);
    }

    private string _xDisplay = "--";
    /// <summary>
    /// Gets the X register as display text ("--" when powered off).
    /// </summary>
    public string XDisplay
    {
        get => _xDisplay;
        private set => this.RaiseAndSetIfChanged(ref _xDisplay, value);
    }

    private string _yDisplay = "--";
    /// <summary>
    /// Gets the Y register as display text ("--" when powered off).
    /// </summary>
    public string YDisplay
    {
        get => _yDisplay;
        private set => this.RaiseAndSetIfChanged(ref _yDisplay, value);
    }

    private string _spDisplay = "--";
    /// <summary>
    /// Gets the Stack Pointer as display text ("--" when powered off).
    /// </summary>
    public string SPDisplay
    {
        get => _spDisplay;
        private set => this.RaiseAndSetIfChanged(ref _spDisplay, value);
    }

    private string _stackSizeDisplay = "--";
    /// <summary>
    /// Gets the stack depth as display text ("--" when powered off).
    /// </summary>
    public string StackSizeDisplay
    {
        get => _stackSizeDisplay;
        private set => this.RaiseAndSetIfChanged(ref _stackSizeDisplay, value);
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

    private string _statusText = "Normal";
    /// <summary>
    /// Gets the execution status as display text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private string _disassemblyText = "";
    /// <summary>
    /// Gets the disassembled current instruction at PC.
    /// </summary>
    public string DisassemblyText
    {
        get => _disassemblyText;
        private set => this.RaiseAndSetIfChanged(ref _disassemblyText, value);
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuStatusPanelViewModel"/> class.
    /// </summary>
    /// <param name="emulator">The emulator core interface for accessing CPU state.</param>
    /// <param name="refreshTicker">The refresh ticker for polling updates
    /// (see <see cref="Constants.RefreshRates.BaseTickerHz"/>).</param>
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
        if (!_isPoweredOn)
        {
            // Powered-off state: show placeholders for all values
            PC = 0;
            SP = 0;
            StackSize = 0;
            A = 0;
            X = 0;
            Y = 0;

            PCDisplay = "----";
            ADisplay = "--";
            XDisplay = "--";
            YDisplay = "--";
            SPDisplay = "--";
            StackSizeDisplay = "--";

            FlagN = false;
            FlagV = false;
            FlagB = false;
            FlagD = false;
            FlagI = false;
            FlagZ = false;
            FlagC = false;

            StatusText = "Off";
            DisassemblyText = "Powered Off";
            return;
        }

        var cpu = _emulator.CpuState;

        // Registers
        PC = cpu.PC;
        SP = cpu.SP;
        StackSize = 0xFF - cpu.SP;
        A = cpu.A;
        X = cpu.X;
        Y = cpu.Y;

        // Display strings
        PCDisplay = cpu.PC.ToString("X4");
        ADisplay = cpu.A.ToString("X2");
        XDisplay = cpu.X.ToString("X2");
        YDisplay = cpu.Y.ToString("X2");
        SPDisplay = cpu.SP.ToString("X2");
        StackSizeDisplay = (0xFF - cpu.SP).ToString();

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
            CpuExecutionStatus.Running => "Normal",
            CpuExecutionStatus.Stopped => "Stopped",
            CpuExecutionStatus.Jammed => "Jammed",
            CpuExecutionStatus.Waiting => "Waiting",
            CpuExecutionStatus.Bypassed => "Bypassed",
            _ => "Unknown"
        };

        // Disassembly - use captured opcode values instead of reading from PC
        DisassemblyText = GenerateDisassembly(cpu);
    }

    /// <summary>
    /// Generates a disassembled instruction string from the captured CPU state.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="CpuStateSnapshot.CurrentOpcode"/> and <see cref="CpuStateSnapshot.OpcodeAddress"/>
    /// which are captured during instruction fetch. This is correct because PC has already advanced
    /// past the instruction that just executed by the time we can read it.
    /// </remarks>
    private string GenerateDisassembly(CpuStateSnapshot cpu)
    {
        try
        {
            var mem = _emulator.MemoryInspector;

            // Use the captured opcode and address from the CPU state snapshot
            // (PC has already advanced, so reading at PC would give the wrong instruction)
            byte opcode = cpu.CurrentOpcode;
            ushort opcodeAddr = cpu.OpcodeAddress;

            // Read parameter bytes from memory (opcode was already captured during fetch)
            byte p1 = ReadCpuByte(mem, (ushort)(opcodeAddr + 1));
            byte p2 = ReadCpuByte(mem, (ushort)(opcodeAddr + 2));

            // Get opcode info from the disassembler table
            var opcodeInfo = OpcodeTable.Table[opcode];

            // Format the disassembly line using the opcode address (not PC)
            return Disassembler.Disassembler.FormatLine(opcodeInfo, opcodeAddr, p1, p2);
        }
        catch
        {
            return "ERR";
        }
    }

    /// <summary>
    /// Reads a byte from the address space as the CPU would see it.
    /// </summary>
    private static byte ReadCpuByte(IMemoryInspector mem, ushort address)
    {
        // For high memory ($C100-$FFFF), use ReadActiveHighMemory to respect ROM/LC RAM mapping
        if (address >= 0xC100)
        {
            return mem.ReadActiveHighMemory(address);
        }

        // For $C000-$C0FF (I/O space), read from main RAM
        // (disassembling I/O space is tricky but we'll try)
        if (address >= 0xC000)
        {
            return mem.ReadRawMain(address);
        }

        // For everything else ($0000-$BFFF), read from main RAM
        return mem.ReadRawMain(address);
    }
}
