// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.DataTypes;

/// <summary>
/// Manages CPU cycle counting and timing-related counters for the emulator.
/// This provides a centralized location for timing information accessible to all components.
/// </summary>
[Capability(typeof(Interfaces.IRestartable))]
public class CpuClockingCounters : Interfaces.IRestartable
{
    private long _vblankBlackoutCounter = 0;
    private ulong _totalCycleCount = 0;
    private ulong _nextVblankCycle;

    /// <summary>
    /// Event fired when a VBlank transition occurs (~60 Hz).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribers can use this for timing-dependent operations that need to run
    /// periodically but don't require cycle-accurate precision. Examples include:
    /// <list type="bullet">
    /// <item>Disk II motor-off timeout checking (1-second delay)</item>
    /// <item>Periodic status updates</item>
    /// <item>Animation timing</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Threading:</strong> This event fires on the emulator thread during
    /// <see cref="CheckAndAdvanceVBlank"/>. Subscribers should avoid blocking operations.
    /// </para>
    /// </remarks>
    public event Action? VBlankOccurred;

    /// <summary>
    /// Number of CPU cycles between VBlank events (17,030 = 262 scanlines Ã— 65 cycles/scanline).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Apple IIe NTSC Timing:</strong>
    /// <list type="bullet">
    /// <item>192 visible scanlines (cycles 0-12,479)</item>
    /// <item>70 vertical blanking scanlines (cycles 12,480-17,029)</item>
    /// <item>Total: 262 scanlines Ã— 65 cycles/scanline = 17,030 cycles/frame</item>
    /// <item>Frame rate: 1,023,000 Hz / 17,030 cycles â‰ˆ 60.06 Hz (NTSC)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public const int CyclesPerVBlank = 17030;

    /// <summary>
    /// Number of cycles the VBlank blackout period lasts (4,550 cycles).
    /// </summary>
    /// <remarks>
    /// During this period, RD_VERTBLANK_ ($C019) reads as $80 (bit 7 set).
    /// </remarks>
    public const int VBlankBlackoutCycles = 4550;

    /// <summary>
    /// Cycle count within frame at which VBlank starts (scanline 192 begins).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Visible Display Ends:</strong> After 192 visible scanlines Ã— 65 cycles/scanline
    /// = 12,480 cycles, the video scanner enters vertical blanking. This is when RD_VERTBLANK_
    /// ($C019) transitions from $00 to $80.
    /// </para>
    /// </remarks>
    public const int VBlankStartCycle = 12480;

    /// <summary>
    /// Gets or sets the VBlank blackout counter. 
    /// When positive, indicates the system is in VBlank period.
    /// </summary>
    public long VBlankCounter
    {
        get => _vblankBlackoutCounter;
        set => _vblankBlackoutCounter = value;
    }

    /// <summary>
    /// Gets the total number of CPU cycles executed since emulator start or last reset.
    /// This counter is suitable for timing-sensitive operations like the No-Slot Clock.
    /// </summary>
    public ulong TotalCycles
    {
        get => _totalCycleCount;
    }

    /// <summary>
    /// Gets the cycle count at which the next VBlank event should fire.
    /// </summary>
    public ulong NextVBlankCycle
    {
        get => _nextVblankCycle;
    }

    /// <summary>
    /// Indicates whether the system is currently in the VBlank period.
    /// </summary>
    public bool InVBlank
    {
        get => VBlankCounter > 0;
    }

    /// <summary>
    /// Initializes a new instance of CpuClockingCounters with proper VBlank timing.
    /// </summary>
    public CpuClockingCounters()
    {
        _nextVblankCycle = VBlankStartCycle;
    }

    /// <summary>
    /// Resets the VBlank counter to the full blackout period.
    /// </summary>
    public void ResetVBlankCounter()
    {
        VBlankCounter = VBlankBlackoutCycles;
    }

    /// <summary>
    /// Increments the total cycle counter by the specified number of cycles.
    /// This should be called by VA2MBus after each CPU instruction execution.
    /// </summary>
    /// <param name="cycles">Number of cycles to add (typically 1-7 for 6502 instructions)</param>
    public void IncrementCycles(int cycles)
    {
        _totalCycleCount += (ulong)cycles;
    }

    /// <summary>
    /// Decrements the VBlank counter by the specified number of cycles.
    /// Should be called alongside IncrementCycles during CPU execution.
    /// </summary>
    /// <param name="cycles">Number of cycles to decrement</param>
    public void DecrementVBlankCounter(int cycles)
    {
        if (_vblankBlackoutCounter > 0)
        {
            _vblankBlackoutCounter -= cycles;
            if (_vblankBlackoutCounter < 0)
            {
                _vblankBlackoutCounter = 0;
            }
        }
    }

    /// <summary>
    /// Checks if a VBlank event should occur and advances the next VBlank cycle if needed.
    /// </summary>
    /// <returns>True if VBlank should fire, false otherwise.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Catch-Up Logic:</strong> If the emulator runs fast (unthrottled batches), multiple
    /// VBlank cycles may be skipped. This method ensures NextVBlankCycle catches up to TotalCycles,
    /// preventing event spam while maintaining proper phase alignment.
    /// </para>
    /// <para>
    /// When VBlank should fire, this method:
    /// <list type="number">
    /// <item>Resets the VBlank blackout counter</item>
    /// <item>Advances NextVBlankCycle by CyclesPerVBlank (17,030)</item>
    /// <item>Repeats until NextVBlankCycle is ahead of TotalCycles</item>
    /// </list>
    /// </para>
    /// </remarks>
    public bool CheckAndAdvanceVBlank()
    {
        if (_totalCycleCount < _nextVblankCycle)
        {
            return false;
        }

        // Catch up if emulator ran fast (unthrottled batches)
        // Advance VBlank cycle by full frame duration (17,030 cycles)
        do
        {
            _nextVblankCycle += CyclesPerVBlank;
            ResetVBlankCounter();
        }
        while (_totalCycleCount >= _nextVblankCycle);

        // Notify subscribers (e.g., disk controller motor-off timing)
        VBlankOccurred?.Invoke();

        return true;
    }

    /// <summary>
    /// Resets all counters to their initial state.
    /// Called during emulator reset/initialization.
    /// </summary>
    public void Reset()
    {
        _vblankBlackoutCounter = 0;
        _totalCycleCount = 0;
        _nextVblankCycle = VBlankStartCycle;
    }

    /// <summary>
    /// Restores counters to their initial power-on state (cold boot).
    /// Equivalent to <see cref="Reset"/> for the counter subsystem.
    /// </summary>
    public void Restart() => Reset();
}
