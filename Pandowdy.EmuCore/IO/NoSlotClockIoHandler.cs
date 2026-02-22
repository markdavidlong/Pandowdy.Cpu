// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Runtime.CompilerServices;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.IO;

/// <summary>
/// Implements the Dallas Semiconductor DS1216 "SmartWatch" (No-Slot Clock) functionality.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Hardware Protocol:</strong> The No-Slot Clock (NSC) is a real-time clock that
/// piggybacks on ROM chips. It uses a bit-serial protocol where address line A0 (odd/even)
/// transmits data bits, and bit 0 of read data returns clock information.
/// </para>
/// <para>
/// <strong>Recognition Pattern:</strong> To unlock the clock, software must read 64 addresses
/// where A0 matches the 64-bit pattern: $5C $A3 $3A $C5 $5C $A3 $3A $C5 (LSB first per byte).
/// </para>
/// <para>
/// <strong>Data Format:</strong> After unlocking, the next 64 reads return clock data in bit 0:
/// <list type="bullet">
/// <item>Byte 0: Hundredths of seconds (00-99 BCD)</item>
/// <item>Byte 1: Seconds (00-59 BCD)</item>
/// <item>Byte 2: Minutes (00-59 BCD)</item>
/// <item>Byte 3: Hours (00-23 BCD, 24-hour mode)</item>
/// <item>Byte 4: Day of week (01-07, 1=Sunday)</item>
/// <item>Byte 5: Day of month (01-31 BCD)</item>
/// <item>Byte 6: Month (01-12 BCD)</item>
/// <item>Byte 7: Year (00-99 BCD)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Decorator Pattern:</strong> This class wraps another ISystemIoHandler (or ROM provider)
/// and intercepts reads to implement the NSC protocol. The downstream handler provides the
/// base ROM data, and this class modifies bit 0 when clock data is being read.
/// </para>
/// </remarks>
public class NoSlotClockIoHandler : ISystemIoHandler
{
    private readonly ISystemIoHandler _downstream;
    private readonly CpuClockingCounters _clockingCounters;

    // NSC state machine
    private NscState _state;
    private int _bitIndex;  // 0-63 for recognition/data bits

    // Time offset: stores difference between emulator time and system time
    private long _timeOffsetTicks;

    // Cached 64-bit clock data for current read cycle
    private ulong _clockData;

    // The 64-bit recognition pattern (DS1216 specification)
    // Pattern: $5C $A3 $3A $C5 $5C $A3 $3A $C5 (read LSB first within each byte)
    // Binary: 01011100 10100011 00111010 11000101 01011100 10100011 00111010 11000101
    private const ulong RecognitionPattern = 0xC53AA35CC53AA35C;

    // Current pattern being matched (built up bit by bit)
    private ulong _patternAccumulator;

    private enum NscState
    {
        /// <summary>Waiting for recognition pattern.</summary>
        Matching,
        /// <summary>Pattern matched, returning clock data in bit 0.</summary>
        ReadingData,
        /// <summary>Pattern matched, accepting write data in A0.</summary>
        WritingData
    }

    /// <summary>
    /// Initializes a new No-Slot Clock handler wrapping the specified downstream handler.
    /// </summary>
    /// <param name="downstream">The ROM or I/O handler to wrap.</param>
    /// <param name="clockingCounters">CPU clocking counters (currently unused but reserved for timing).</param>
    public NoSlotClockIoHandler(ISystemIoHandler downstream, CpuClockingCounters clockingCounters)
    {   
        ArgumentNullException.ThrowIfNull(downstream);
        ArgumentNullException.ThrowIfNull(clockingCounters);
        _downstream = downstream;
        _clockingCounters = clockingCounters;
        Reset();
    }

    /// <summary>
    /// Resets the NSC state machine to pattern-matching mode.
    /// </summary>
    /// <remarks>
    /// Time offset is preserved across resets (battery-backed behavior).
    /// </remarks>
    public void Reset()
    {
        _downstream.Reset();
        _state = NscState.Matching;
        _bitIndex = 0;
        _patternAccumulator = 0;
        _clockData = 0;
        // Note: _timeOffsetTicks persists across resets (battery-backed)
    }

    /// <summary>
    /// Restores the NSC to its initial power-on state (cold boot).
    /// </summary>
    /// <remarks>
    /// Same behaviour as <see cref="Reset"/> — the time offset survives both warm reset
    /// and power cycle, modelling battery-backed clock hardware.
    /// </remarks>
    public void Restart()
    {
        _downstream.Restart();
        _state = NscState.Matching;
        _bitIndex = 0;
        _patternAccumulator = 0;
        _clockData = 0;
        // _timeOffsetTicks preserved intentionally — battery-backed RTC
    }

    /// <summary>
    /// Sets the emulator clock time by calculating offset from system time.
    /// </summary>
    /// <param name="emulatorTime">The desired emulator time.</param>
    public void SetClockTime(DateTime emulatorTime)
    {
        _timeOffsetTicks = emulatorTime.Ticks - DateTime.Now.Ticks;
    }

    /// <summary>
    /// Gets the current emulator clock time (system time + offset).
    /// </summary>
    public DateTime GetClockTime()
    {
        return new DateTime(DateTime.Now.Ticks + _timeOffsetTicks);
    }

    /// <inheritdoc />
    public int Size => _downstream.Size;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Peek(ushort address) => Read(address);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort offset)
    {
        // Get the base ROM/IO data from downstream
        byte romData = _downstream.Read(offset);

        // Extract A0 (address bit 0) - this is the input bit from the Apple II
        bool a0 = (offset & 0x01) != 0;

        switch (_state)
        {
            case NscState.Matching:
                return HandlePatternMatching(romData, a0);

            case NscState.ReadingData:
                return HandleDataRead(romData);

            case NscState.WritingData:
                HandleDataWrite(a0);
                return romData;

            default:
                return romData;
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort offset, byte val)
    {
        // NSC doesn't intercept writes in the traditional sense
        // Write operations still trigger the A0 protocol on reads
        _downstream.Write(offset, val);
    }

    /// <summary>
    /// Handles pattern matching state - looking for the 64-bit recognition sequence.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte HandlePatternMatching(byte romData, bool a0)
    {
        // Shift in the new bit (A0) to the pattern accumulator
        _patternAccumulator = (_patternAccumulator >> 1) | (a0 ? 0x8000000000000000UL : 0UL);
        _bitIndex++;

        // Check if we've matched the full 64-bit pattern
        if (_bitIndex >= 64 && _patternAccumulator == RecognitionPattern)
        {
            // Pattern matched! Transition to reading data
            _state = NscState.ReadingData;
            _bitIndex = 0;
            _clockData = BuildClockData();
        }

        // Return unmodified ROM data during pattern matching
        return romData;
    }

    /// <summary>
    /// Handles data read state - returning clock data in bit 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte HandleDataRead(byte romData)
    {
        // Extract the current clock bit
        bool clockBit = ((_clockData >> _bitIndex) & 1) != 0;

        // Replace bit 0 of ROM data with clock bit
        byte result = (byte)((romData & 0xFE) | (clockBit ? 1 : 0));

        _bitIndex++;
        if (_bitIndex >= 64)
        {
            // All 64 bits read, return to matching state
            _state = NscState.Matching;
            _bitIndex = 0;
            _patternAccumulator = 0;
        }

        return result;
    }

    /// <summary>
    /// Handles data write state - accepting clock data via A0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleDataWrite(bool a0)
    {
        // Set or clear the current bit based on A0
        if (a0)
        {
            _clockData |= (1UL << _bitIndex);
        }
        else
        {
            _clockData &= ~(1UL << _bitIndex);
        }

        _bitIndex++;
        if (_bitIndex >= 64)
        {
            // All 64 bits written, apply the new time
            ApplyWrittenClockData();
            _state = NscState.Matching;
            _bitIndex = 0;
            _patternAccumulator = 0;
        }
    }

    /// <summary>
    /// Builds the 64-bit clock data from the current emulator time.
    /// </summary>
    private ulong BuildClockData()
    {
        DateTime time = GetClockTime();

        // Pack 8 BCD bytes into 64 bits (LSB first within each byte, byte 0 first)
        ulong data = 0;
        data |= (ulong)DecimalToBcd(time.Millisecond / 10);           // Byte 0: Centiseconds
        data |= (ulong)DecimalToBcd(time.Second) << 8;                // Byte 1: Seconds
        data |= (ulong)DecimalToBcd(time.Minute) << 16;               // Byte 2: Minutes
        data |= (ulong)DecimalToBcd(time.Hour) << 24;                 // Byte 3: Hours
        data |= (ulong)DecimalToBcd((int)time.DayOfWeek + 1) << 32;   // Byte 4: Day of week (1=Sunday)
        data |= (ulong)DecimalToBcd(time.Day) << 40;                  // Byte 5: Day of month
        data |= (ulong)DecimalToBcd(time.Month) << 48;                // Byte 6: Month
        data |= (ulong)DecimalToBcd(time.Year % 100) << 56;           // Byte 7: Year

        return data;
    }

    /// <summary>
    /// Applies written clock data to update the time offset.
    /// </summary>
    private void ApplyWrittenClockData()
    {
        try
        {
            // Extract BCD bytes from the 64-bit data
            int centiseconds = BcdToDecimal((byte)(_clockData & 0xFF));
            int seconds = BcdToDecimal((byte)((_clockData >> 8) & 0xFF));
            int minutes = BcdToDecimal((byte)((_clockData >> 16) & 0xFF));
            int hours = BcdToDecimal((byte)((_clockData >> 24) & 0xFF));
            int dayOfWeek = BcdToDecimal((byte)((_clockData >> 32) & 0xFF)); // Not used
            int day = BcdToDecimal((byte)((_clockData >> 40) & 0xFF));
            int month = BcdToDecimal((byte)((_clockData >> 48) & 0xFF));
            int year = BcdToDecimal((byte)((_clockData >> 56) & 0xFF));

            // Convert 2-digit year (assume 2000-2099 for Y2K compliance)
            year += (year < 80) ? 2000 : 1900;

            DateTime writtenTime = new(year, month, day, hours, minutes, seconds, centiseconds * 10);
            _timeOffsetTicks = writtenTime.Ticks - DateTime.Now.Ticks;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Invalid BCD values - ignore the write
        }
    }

    /// <summary>
    /// Converts a decimal value (0-99) to BCD format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte DecimalToBcd(int value)
    {
        return (byte)(((value / 10) << 4) | (value % 10));
    }

    /// <summary>
    /// Converts a BCD byte to decimal value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BcdToDecimal(byte bcd)
    {
        return ((bcd >> 4) * 10) + (bcd & 0x0F);
    }
}
