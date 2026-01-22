using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore;

/// <summary>
/// Implements the Apple II No-Slot Clock functionality.
/// The No-Slot Clock uses a bit-banging protocol accessed through $C0n0-$C0nF
/// where reads trigger clock/data operations based on the low nibble of the address.
/// </summary>
public class NoSlotClockIoHandler : ISystemIoHandler
{
    private ISystemIoHandler _downstream;
    private CpuClockingCounters _clockingCounters;

    // No-Slot Clock state
    private bool _isUnlocked;
    private int _unlockSequenceIndex;
    private int _bitPosition;
    private int _byteIndex; // Track which byte (0-7) we're currently reading
    private byte _currentByte;
    private ClockMode _mode;
    private bool _writeMode;
    private ulong _lastUnlockAccessTick;

    // Time offset: stores difference between emulator time and system time
    // Positive = emulator time is ahead, Negative = emulator time is behind
    private long _timeOffsetTicks = 0;

    // Cached clock registers (written when clock is set)
    private byte[] _clockRegisters = new byte[8];
    private bool _clockRegistersValid = false;

    // The unlock sequence: reading addresses in this specific pattern
    private static readonly byte[] UnlockSequence = [ 0x5, 0xA, 0x5, 0xA ];
    
    // Timeout for unlock sequence: ~100 cycles between accesses
    // (adjustable based on real hardware behavior)
    private const ulong UnlockTimeoutCycles = 100;

    private enum ClockMode
    {
        Locked,
        ReadClock,
        ReadCompare,
        Write
    }

    public NoSlotClockIoHandler(ISystemIoHandler downstream, CpuClockingCounters clockingCounters)
    {   
        ArgumentNullException.ThrowIfNull(downstream);
        ArgumentNullException.ThrowIfNull(clockingCounters);
        _downstream = downstream;
        _clockingCounters = clockingCounters;
        Reset();
    }

    public void Reset()
    {
        _downstream.Reset();
        _isUnlocked = false;
        _unlockSequenceIndex = 0;
        _bitPosition = 0;
        _byteIndex = 0;
        _currentByte = 0;
        _mode = ClockMode.Locked;
        _writeMode = false;
        _lastUnlockAccessTick = 0ul;
        // Note: We don't reset _timeOffsetTicks - it persists across resets
    }

    /// <summary>
    /// Sets the emulator clock time by calculating offset from system time.
    /// </summary>
    /// <param name="emulatorTime">The desired emulator time.</param>
    public void SetClockTime(DateTime emulatorTime)
    {
        DateTime systemTime = DateTime.Now;
        _timeOffsetTicks = emulatorTime.Ticks - systemTime.Ticks;
        _clockRegistersValid = false; // Invalidate cached registers
    }

    /// <summary>
    /// Gets the current emulator clock time (system time + offset).
    /// </summary>
    /// <returns>The current emulator time.</returns>
    public DateTime GetClockTime()
    {
        DateTime systemTime = DateTime.Now;
        return new DateTime(systemTime.Ticks + _timeOffsetTicks);
    }

    public int Size
    {
        get { return _downstream.Size; }
    }

    public byte Read(ushort loc)
    {
        byte lowNibble = (byte)(loc & 0x0F);

        // No-Slot Clock detection and operation
        if (lowNibble <= 0x0B)
        {
            return HandleNoSlotClockRead(lowNibble);
        }

        // Pass through to downstream handler
        return _downstream.Read(loc);
    }

    public void Write(ushort loc, byte val)
    {
        byte lowNibble = (byte)(loc & 0x0F);

        // No-Slot Clock write operations
        if (lowNibble <= 0x0B && _isUnlocked)
        {
            HandleNoSlotClockWrite(lowNibble, val);
            return;
        }

        // Pass through to downstream handler
        _downstream.Write(loc, val);
    }

    private byte HandleNoSlotClockRead(byte offset)
    {
        // Check for unlock sequence
        if (!_isUnlocked)
        {
            ulong currentTick = _clockingCounters.TotalCycles;
            
            // Check for timeout between unlock sequence accesses
            if (_unlockSequenceIndex > 0)
            {
                ulong cyclesSinceLastAccess = currentTick - _lastUnlockAccessTick;
                if (cyclesSinceLastAccess > UnlockTimeoutCycles)
                {
                    // Timeout - reset unlock sequence
                    _unlockSequenceIndex = 0;
                }
            }
            
            if (offset == UnlockSequence[_unlockSequenceIndex])
            {
                _unlockSequenceIndex++;
                _lastUnlockAccessTick = currentTick;
                
                if (_unlockSequenceIndex >= UnlockSequence.Length)
                {
                    _isUnlocked = true;
                    _unlockSequenceIndex = 0;
                    _bitPosition = 0;
                    _byteIndex = 0;
                    _mode = ClockMode.ReadClock;
                    _writeMode = false;
                }
            }
            else
            {
                // Wrong sequence, reset
                _unlockSequenceIndex = 0;
            }
            return _downstream.Read((ushort)(0xC000 | offset));
        }

        // Once unlocked, handle clock operations
        switch (offset)
        {
            case 0x0: // Read data bit 0
                return ReadClockBit();

            case 0x1: // Shift clock data
                ShiftClockData();
                return 0x00;

            case 0x2: // Enable write mode
                _writeMode = true;
                _byteIndex = 0; // Reset to first byte for writing
                return 0x00;

            case 0x3: // Disable write mode / Enable read mode
                _writeMode = false;
                return 0x00;

            case 0x4: // Load next byte for reading
                _byteIndex = 0; // Reset to first byte
                LoadNextClockByte();
                return 0x00;

            case 0x5: // Part of unlock sequence when locked
            case 0xA: // Part of unlock sequence when locked
                return 0x00;

            default:
                // Other addresses don't affect clock state
                return 0x00;
        }
    }

    private void HandleNoSlotClockWrite(byte offset, byte val)
    {
        if (!_writeMode)
        {
            return;
        }

        switch (offset)
        {
            case 0x0: // Write data bit
                WriteClockBit((val & 0x01) != 0);
                break;

            case 0x1: // Shift clock data
                ShiftClockData();
                break;

            case 0x4: // Store current byte to clock
                StoreClockByte();
                break;
        }
    }

    private byte ReadClockBit()
    {
        // Read the current bit from the current byte
        byte bit = (byte)((_currentByte >> _bitPosition) & 0x01);
        return (byte)(bit != 0 ? 0x80 : 0x00); // NSC returns bit in high bit of byte
    }

    private void ShiftClockData()
    {
        _bitPosition++;
        if (_bitPosition >= 8)
        {
            _bitPosition = 0;
            
            // Only auto-advance byte index in read mode
            if (!_writeMode)
            {
                _byteIndex++; // Move to next byte
                if (_byteIndex >= 8)
                {
                    _byteIndex = 0; // Wrap around after 8 bytes
                }
                // Auto-load next byte after 8 bits
                LoadNextClockByte();
            }
        }
    }

    private void WriteClockBit(bool bitValue)
    {
        if (bitValue)
        {
            _currentByte |= (byte)(1 << _bitPosition);
        }
        else
        {
            _currentByte &= (byte)~(1 << _bitPosition);
        }
    }

    private void LoadNextClockByte()
    {
        // Load the next byte from the clock registers
        // The No-Slot Clock has 8 bytes of time data in BCD format:
        // Byte 0: Centiseconds (00-99)
        // Byte 1: Seconds (00-59)
        // Byte 2: Minutes (00-59)
        // Byte 3: Hours (00-23)
        // Byte 4: Day of week (00-06, 0=Sunday)
        // Byte 5: Day of month (01-31)
        // Byte 6: Month (01-12)
        // Byte 7: Year (00-99)

        // Get current emulator time
        DateTime emulatorTime = GetClockTime();

        // Convert to BCD format based on current byte index
        _currentByte = _byteIndex switch
        {
            0 => DecimalToBcd(emulatorTime.Millisecond / 10), // Centiseconds (0-99)
            1 => DecimalToBcd(emulatorTime.Second),           // Seconds (00-59)
            2 => DecimalToBcd(emulatorTime.Minute),           // Minutes (00-59)
            3 => DecimalToBcd(emulatorTime.Hour),             // Hours (00-23)
            4 => DecimalToBcd((int)emulatorTime.DayOfWeek),   // Day of week (0=Sunday)
            5 => DecimalToBcd(emulatorTime.Day),              // Day of month (01-31)
            6 => DecimalToBcd(emulatorTime.Month),            // Month (01-12)
            7 => DecimalToBcd(emulatorTime.Year % 100),       // Year (00-99)
            _ => 0x00
        };

        _bitPosition = 0;
    }

    private void StoreClockByte()
    {
        // Store the current byte to the clock registers
        // Cache the written byte for later calculation of new time offset

        if (_byteIndex >= 0 && _byteIndex < _clockRegisters.Length)
        {
            _clockRegisters[_byteIndex] = _currentByte;
            
            // If all 8 bytes have been written, recalculate time offset
            if (_byteIndex == 7)
            {
                _clockRegistersValid = true;
                RecalculateTimeOffset();
                _byteIndex = 0; // Reset for next write cycle
            }
            else
            {
                _byteIndex++; // Move to next byte
            }
        }

        _currentByte = 0;
        _bitPosition = 0;
    }

    /// <summary>
    /// Converts a decimal value to BCD (Binary-Coded Decimal) format.
    /// </summary>
    /// <param name="value">Decimal value (0-99).</param>
    /// <returns>BCD-encoded byte.</returns>
    private static byte DecimalToBcd(int value)
    {
        if (value < 0 || value > 99)
        {
            return 0x00; // Invalid value
        }
        
        int tens = value / 10;
        int ones = value % 10;
        return (byte)((tens << 4) | ones);
    }

    /// <summary>
    /// Converts a BCD (Binary-Coded Decimal) value to decimal.
    /// </summary>
    /// <param name="bcd">BCD-encoded byte.</param>
    /// <returns>Decimal value (0-99).</returns>
    private static int BcdToDecimal(byte bcd)
    {
        int tens = (bcd >> 4) & 0x0F;
        int ones = bcd & 0x0F;
        return tens * 10 + ones;
    }

    /// <summary>
    /// Recalculates the time offset based on newly written clock registers.
    /// </summary>
    private void RecalculateTimeOffset()
    {
        if (!_clockRegistersValid)
        {
            return;
        }

        try
        {
            // Parse BCD values from clock registers
            int centiseconds = BcdToDecimal(_clockRegisters[0]);
            int seconds = BcdToDecimal(_clockRegisters[1]);
            int minutes = BcdToDecimal(_clockRegisters[2]);
            int hours = BcdToDecimal(_clockRegisters[3]);
            int dayOfWeek = BcdToDecimal(_clockRegisters[4]); // Not used for DateTime
            int day = BcdToDecimal(_clockRegisters[5]);
            int month = BcdToDecimal(_clockRegisters[6]);
            int year = BcdToDecimal(_clockRegisters[7]);

            // Convert 2-digit year to 4-digit year (assume 2000-2099)
            year += 2000;

            // Build DateTime from registers
            DateTime writtenTime = new (
                year,
                month,
                day,
                hours,
                minutes,
                seconds,
                centiseconds * 10); // Centiseconds to milliseconds

            // Calculate new offset
            DateTime systemTime = DateTime.Now;
            _timeOffsetTicks = writtenTime.Ticks - systemTime.Ticks;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Invalid date/time values - ignore the write
            // This can happen if the software writes invalid BCD values
        }
    }

    public byte this[ushort offset]
    {
        get { return Read(offset); }
        set { Write(offset, value); }
    }
}
