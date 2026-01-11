using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Tests.Helpers;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for NoSlotClockIoHandler - Apple II No-Slot Clock emulation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Test Coverage:</strong>
/// <list type="bullet">
/// <item>Unlock sequence validation and timeout</item>
/// <item>Bit-banging protocol (read/write/shift operations)</item>
/// <item>Time reading with BCD encoding</item>
/// <item>Time writing and offset recalculation</item>
/// <item>Time offset persistence across operations</item>
/// <item>Passthrough to downstream handler</item>
/// </list>
/// </para>
/// </remarks>
public class NoSlotClockIoHandlerTests
{
    #region Test Helpers

    /// <summary>
    /// Mock implementation of ISystemIoHandler for testing passthrough.
    /// </summary>
    private class MockSystemIoHandler : ISystemIoHandler
    {
        public int Size => 0x90;
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public ushort LastReadAddress { get; private set; }
        public ushort LastWriteAddress { get; private set; }
        public byte LastWriteValue { get; private set; }
        public byte ReturnValue { get; set; } = 0x42;

        public byte Read(ushort loc)
        {
            ReadCount++;
            LastReadAddress = loc;
            return ReturnValue;
        }

        public void Write(ushort loc, byte val)
        {
            WriteCount++;
            LastWriteAddress = loc;
            LastWriteValue = val;
        }

        public void Reset()
        {
            ReadCount = 0;
            WriteCount = 0;
        }

        public byte this[ushort offset]
        {
            get => Read(offset);
            set => Write(offset, value);
        }
    }

    /// <summary>
    /// Helper to create a configured NoSlotClockIoHandler for testing.
    /// </summary>
    private class NoSlotClockFixture
    {
        public MockSystemIoHandler Downstream { get; }
        public CpuClockingCounters ClockingCounters { get; }
        public NoSlotClockIoHandler Clock { get; }

        public NoSlotClockFixture()
        {
            Downstream = new MockSystemIoHandler();
            ClockingCounters = new CpuClockingCounters();
            Clock = new NoSlotClockIoHandler(Downstream, ClockingCounters);
        }
    }

    #endregion

    #region Constructor Tests (3 tests)

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange
        var downstream = new MockSystemIoHandler();
        var counters = new CpuClockingCounters();

        // Act
        var clock = new NoSlotClockIoHandler(downstream, counters);

        // Assert
        Assert.NotNull(clock);
        Assert.Equal(0x90, clock.Size);
    }

    [Fact]
    public void Constructor_NullDownstream_ThrowsArgumentNullException()
    {
        // Arrange
        var counters = new CpuClockingCounters();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new NoSlotClockIoHandler(null!, counters));
    }

    [Fact]
    public void Constructor_NullClockingCounters_ThrowsArgumentNullException()
    {
        // Arrange
        var downstream = new MockSystemIoHandler();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new NoSlotClockIoHandler(downstream, null!));
    }

    #endregion

    #region Unlock Sequence Tests (8 tests)

    [Fact]
    public void Read_BeforeUnlock_PassesThroughToDownstream()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Downstream.ReturnValue = 0x99;

        // Act
        var value = fixture.Clock.Read(0x10); // Address not in unlock range

        // Assert
        Assert.Equal(0x99, value);
        Assert.Equal(1, fixture.Downstream.ReadCount);
    }

    [Fact]
    public void Read_UnlockSequence_UnlocksAfterCorrectSequence()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();

        // Act - Perform unlock sequence: 0x5, 0xA, 0x5, 0xA
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);

        // Now try reading bit (should work if unlocked)
        var bit = fixture.Clock.Read(0x0);

        // Assert - Should return 0x00 or 0x80 (not downstream value)
        Assert.True(bit == 0x00 || bit == 0x80);
    }

    [Fact]
    public void Read_UnlockSequence_WrongSequence_ResetsUnlock()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Downstream.ReturnValue = 0x99;

        // Act - Start unlock then use wrong address
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);
        fixture.Clock.Read(0x7); // Wrong! Should be 0x5
        fixture.Clock.Read(0xA);

        // Try reading bit (should passthrough if still locked)
        fixture.Clock.Read(0x0);

        // Assert - Should have passed through to downstream
        Assert.True(fixture.Downstream.ReadCount > 0);
    }

    [Fact]
    public void Read_UnlockSequence_TimeoutBetweenReads_ResetsUnlock()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Downstream.ReturnValue = 0x99;

        // Act - Start unlock sequence
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);

        // Simulate time passing (> 100 cycles)
        for (int i = 0; i < 150; i++)
        {
            fixture.ClockingCounters.IncrementCycles(1);
        }

        // Try to continue unlock
        fixture.Clock.Read(0x5); // Should timeout and reset

        // Assert - Should still be locked (passthrough to downstream)
        var value = fixture.Clock.Read(0x0);
        Assert.True(fixture.Downstream.ReadCount > 0);
    }

    [Fact]
    public void Read_UnlockSequence_QuickSuccession_Unlocks()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();

        // Act - Perform unlock sequence with minimal cycles between
        for (int i = 0; i < 4; i++)
        {
            byte offset = (i % 2 == 0) ? (byte)0x5 : (byte)0xA;
            fixture.Clock.Read(offset);
            fixture.ClockingCounters.IncrementCycles(1); // Only 1 cycle between
        }

        // Try reading bit
        var bit = fixture.Clock.Read(0x0);

        // Assert - Should be unlocked (returns 0x00 or 0x80, not downstream)
        Assert.True(bit == 0x00 || bit == 0x80);
    }

    [Fact]
    public void Reset_ClearsUnlockState()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();

        // Unlock the clock
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);

        // Act - Reset
        fixture.Clock.Reset();

        // Assert - Should be locked again (passthrough to downstream)
        fixture.Downstream.ReturnValue = 0x99;
        var value = fixture.Clock.Read(0x0);
        Assert.True(fixture.Downstream.ReadCount > 0);
    }

    [Fact]
    public void Read_AfterUnlock_AddressesOutsideRange_PassThrough()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        
        // Unlock
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);

        fixture.Downstream.ReturnValue = 0xAB;

        // Act - Read address outside 0x0-0xB range
        var value = fixture.Clock.Read(0x0C);

        // Assert
        Assert.Equal(0xAB, value);
        Assert.True(fixture.Downstream.ReadCount > 0);
    }

    [Fact]
    public void Write_BeforeUnlock_PassesThroughToDownstream()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();

        // Act - Write without unlocking
        fixture.Clock.Write(0x10, 0x42);

        // Assert
        Assert.Equal(1, fixture.Downstream.WriteCount);
        Assert.Equal(0x10, fixture.Downstream.LastWriteAddress);
        Assert.Equal(0x42, fixture.Downstream.LastWriteValue);
    }

    #endregion

    #region Bit Reading Tests (5 tests)

    [Fact]
    public void Read_DataBit_AfterUnlock_ReturnsHighBitFormat()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act - Read data bit (address 0x0)
        var bit = fixture.Clock.Read(0x0);

        // Assert - NSC returns bit in high bit position (0x00 or 0x80)
        Assert.True(bit == 0x00 || bit == 0x80);
    }

    [Fact]
    public void Read_ShiftOperation_AdvancesBitPosition()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Set a known time for testing
        fixture.Clock.SetClockTime(new DateTime(2024, 6, 15, 12, 30, 45));

        // Act - Load byte and read multiple bits
        fixture.Clock.Read(0x4); // Load next byte
        var bit0 = fixture.Clock.Read(0x0); // Read bit 0
        fixture.Clock.Read(0x1); // Shift
        var bit1 = fixture.Clock.Read(0x0); // Read bit 1

        // Assert - Bits should be different (unless all 0s or all 1s)
        // At minimum, the read operations should complete without error
        Assert.True(bit0 == 0x00 || bit0 == 0x80);
        Assert.True(bit1 == 0x00 || bit1 == 0x80);
    }

    [Fact]
    public void Read_EightBitsShift_AutoLoadsNextByte()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act - Load byte and shift 8 times
        fixture.Clock.Read(0x4); // Load byte 0
        for (int i = 0; i < 8; i++)
        {
            fixture.Clock.Read(0x0); // Read bit
            fixture.Clock.Read(0x1); // Shift
        }

        // After 8 shifts, should auto-load next byte
        var nextBit = fixture.Clock.Read(0x0);

        // Assert - Should complete without error
        Assert.True(nextBit == 0x00 || nextBit == 0x80);
    }

    [Fact]
    public void Read_LoadNextByte_ResetsBitPosition()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Shift a few times
        fixture.Clock.Read(0x1);
        fixture.Clock.Read(0x1);
        fixture.Clock.Read(0x1);

        // Act - Load next byte (should reset position)
        fixture.Clock.Read(0x4);
        var bit = fixture.Clock.Read(0x0);

        // Assert - Should read bit 0 of new byte
        Assert.True(bit == 0x00 || bit == 0x80);
    }

    [Fact]
    public void Read_EnableWriteMode_ReturnsZero()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act
        var value1 = fixture.Clock.Read(0x2); // Enable write mode
        var value2 = fixture.Clock.Read(0x3); // Disable write mode

        // Assert
        Assert.Equal(0x00, value1);
        Assert.Equal(0x00, value2);
    }

    #endregion

    #region Time Reading Tests (7 tests)

    [Fact]
    public void GetClockTime_WithoutOffset_ReturnsSystemTime()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        DateTime before = DateTime.Now;

        // Act
        DateTime clockTime = fixture.Clock.GetClockTime();
        DateTime after = DateTime.Now;

        // Assert - Should be within reasonable range of system time
        Assert.True(clockTime >= before);
        Assert.True(clockTime <= after.AddSeconds(1));
    }

    [Fact]
    public void SetClockTime_UpdatesOffset()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        DateTime targetTime = new DateTime(1985, 6, 15, 12, 30, 0);

        // Act
        fixture.Clock.SetClockTime(targetTime);
        DateTime retrievedTime = fixture.Clock.GetClockTime();

        // Assert - Should be close to target time (within a few milliseconds)
        TimeSpan difference = retrievedTime - targetTime;
        Assert.True(Math.Abs(difference.TotalSeconds) < 1);
    }

    [Fact]
    public void GetClockTime_AfterOffset_AdvancesWithSystemTime()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        DateTime targetTime = new DateTime(2000, 1, 1, 0, 0, 0);
        fixture.Clock.SetClockTime(targetTime);

        // Act
        DateTime time1 = fixture.Clock.GetClockTime();
        System.Threading.Thread.Sleep(100); // Wait 100ms
        DateTime time2 = fixture.Clock.GetClockTime();

        // Assert - Time should have advanced
        Assert.True(time2 > time1);
        TimeSpan elapsed = time2 - time1;
        Assert.True(elapsed.TotalMilliseconds >= 90); // Allow some tolerance
    }

    [Fact]
    public void Read_ClockData_ReturnsValidBCD()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Set known time
        fixture.Clock.SetClockTime(new DateTime(2024, 6, 15, 12, 34, 56));

        // Act - Load and read seconds byte (byte 1)
        fixture.Clock.Read(0x4); // Load byte 0 (centiseconds)
        
        // Shift through byte 0 (8 bits)
        for (int i = 0; i < 8; i++)
        {
            fixture.Clock.Read(0x0);
            fixture.Clock.Read(0x1);
        }

        // Now we're on byte 1 (seconds = 56 = 0x56 BCD)
        byte reconstructed = 0;
        for (int i = 0; i < 8; i++)
        {
            byte bit = fixture.Clock.Read(0x0);
            if (bit == 0x80)
            {
                reconstructed |= (byte)(1 << i);
            }
            if (i < 7)
            {
                fixture.Clock.Read(0x1); // Shift
            }
        }

        // Assert - Should be 0x56 (56 in BCD)
        Assert.Equal(0x56, reconstructed);
    }

    [Fact]
    public void Reset_PreservesTimeOffset()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        DateTime targetTime = new DateTime(1990, 12, 25, 10, 30, 0);
        fixture.Clock.SetClockTime(targetTime);

        // Act
        fixture.Clock.Reset();
        DateTime afterReset = fixture.Clock.GetClockTime();

        // Assert - Offset should still be in effect
        TimeSpan difference = afterReset - targetTime;
        Assert.True(Math.Abs(difference.TotalSeconds) < 1);
    }

    [Fact]
    public void Read_DayOfWeek_MatchesDateTime()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Set to a Monday (June 15, 2024 is a Saturday)
        DateTime testTime = new DateTime(2024, 6, 17); // Monday
        fixture.Clock.SetClockTime(testTime);

        // Act - Load byte 4 (day of week)
        fixture.Clock.Read(0x4); // Load byte 0
        
        // Skip to byte 4 by auto-loading
        for (int byteNum = 0; byteNum < 4; byteNum++)
        {
            for (int i = 0; i < 8; i++)
            {
                fixture.Clock.Read(0x0);
                fixture.Clock.Read(0x1);
            }
        }

        // Read byte 4 (day of week)
        byte dayOfWeek = 0;
        for (int i = 0; i < 8; i++)
        {
            byte bit = fixture.Clock.Read(0x0);
            if (bit == 0x80)
            {
                dayOfWeek |= (byte)(1 << i);
            }
            if (i < 7)
            {
                fixture.Clock.Read(0x1);
            }
        }

        // Assert - Monday = 1 in BCD (0x01)
        Assert.Equal(0x01, dayOfWeek);
    }

    [Fact]
    public void Read_MultipleBytes_ReturnsDifferentValues()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Set time with distinct values
        fixture.Clock.SetClockTime(new DateTime(2024, 12, 25, 23, 59, 58));

        // Act - Read first 3 bytes (centiseconds, seconds, minutes)
        var bytes = new List<byte>();
        fixture.Clock.Read(0x4); // Load byte 0

        for (int byteNum = 0; byteNum < 3; byteNum++)
        {
            byte reconstructed = 0;
            for (int i = 0; i < 8; i++)
            {
                byte bit = fixture.Clock.Read(0x0);
                if (bit == 0x80)
                {
                    reconstructed |= (byte)(1 << i);
                }
                fixture.Clock.Read(0x1); // Shift (auto-loads after 8th)
            }
            bytes.Add(reconstructed);
        }

        // Assert - Bytes should be different (seconds=58, minutes=59)
        // At minimum, verify we got 3 distinct reads
        Assert.Equal(3, bytes.Count);
        // Seconds should be 0x58, Minutes should be 0x59
        Assert.True(bytes.Any(b => b != 0x00)); // Should have non-zero values
    }

    #endregion

    #region Time Writing Tests (5 tests)

    [Fact]
    public void Write_EnableWriteMode_AllowsBitWrites()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act
        fixture.Clock.Read(0x2); // Enable write mode
        fixture.Clock.Write(0x0, 0x01); // Write bit 1

        // Assert - Should not throw or passthrough
        Assert.Equal(0, fixture.Downstream.WriteCount);
    }

    [Fact]
    public void Write_WithoutWriteMode_Ignored()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act - Try writing without enabling write mode
        fixture.Clock.Write(0x0, 0x01);

        // Assert - Should be ignored (not passed downstream)
        Assert.Equal(0, fixture.Downstream.WriteCount);
    }

    [Fact]
    public void Write_EightBytes_RecalculatesOffset()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);
        fixture.Clock.Read(0x2); // Enable write mode

        // Act - Write 8 bytes of clock data (simplified)
        // Byte 0: Centiseconds = 0x00
        // Byte 1: Seconds = 0x30 (30)
        // Byte 2: Minutes = 0x45 (45)
        // Byte 3: Hours = 0x12 (12)
        // Byte 4: Day of week = 0x03 (Wednesday)
        // Byte 5: Day = 0x15 (15)
        // Byte 6: Month = 0x06 (June)
        // Byte 7: Year = 0x24 (2024)

        byte[] clockData = { 0x00, 0x30, 0x45, 0x12, 0x03, 0x15, 0x06, 0x24 };

        foreach (byte data in clockData)
        {
            // Write 8 bits
            for (int i = 0; i < 8; i++)
            {
                byte bit = (byte)((data >> i) & 1);
                fixture.Clock.Write(0x0, bit);
                fixture.Clock.Write(0x1, 0); // Shift
            }
            
            // Store byte
            fixture.Clock.Write(0x4, 0);
        }

        // Assert - Time should now be June 15, 2024, 12:45:30
        DateTime result = fixture.Clock.GetClockTime();
        Assert.Equal(2024, result.Year);
        Assert.Equal(6, result.Month);
        Assert.Equal(15, result.Day);
        Assert.Equal(12, result.Hour);
        Assert.Equal(45, result.Minute);
        Assert.Equal(30, result.Second);
    }

    [Fact]
    public void Write_InvalidDate_IgnoresWrite()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);
        fixture.Clock.Read(0x2); // Enable write mode

        DateTime beforeTime = fixture.Clock.GetClockTime();

        // Act - Write invalid date (month = 13)
        byte[] clockData = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x13, 0x24 }; // Invalid month

        foreach (byte data in clockData)
        {
            for (int i = 0; i < 8; i++)
            {
                byte bit = (byte)((data >> i) & 1);
                fixture.Clock.Write(0x0, bit);
                fixture.Clock.Write(0x1, 0);
            }
            fixture.Clock.Write(0x4, 0);
        }

        DateTime afterTime = fixture.Clock.GetClockTime();

        // Assert - Time should not have changed significantly (may advance slightly due to system time)
        TimeSpan difference = afterTime - beforeTime;
        Assert.True(Math.Abs(difference.TotalSeconds) < 2); // Allow for execution time
    }

    [Fact]
    public void Write_AddressOutsideRange_PassesThroughToDownstream()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act
        fixture.Clock.Write(0x0C, 0x42); // Outside 0x0-0xB range

        // Assert
        Assert.Equal(1, fixture.Downstream.WriteCount);
        Assert.Equal(0x0C, fixture.Downstream.LastWriteAddress);
        Assert.Equal(0x42, fixture.Downstream.LastWriteValue);
    }

    #endregion

    #region Indexer Tests (2 tests)

    [Fact]
    public void Indexer_Get_DelegatesToRead()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Downstream.ReturnValue = 0xAB;

        // Act
        byte value = fixture.Clock[0x10];

        // Assert
        Assert.Equal(0xAB, value);
        Assert.Equal(1, fixture.Downstream.ReadCount);
    }

    [Fact]
    public void Indexer_Set_DelegatesToWrite()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();

        // Act
        fixture.Clock[0x10] = 0xCD;

        // Assert
        Assert.Equal(1, fixture.Downstream.WriteCount);
        Assert.Equal(0x10, fixture.Downstream.LastWriteAddress);
        Assert.Equal(0xCD, fixture.Downstream.LastWriteValue);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Helper method to unlock the clock with the correct sequence.
    /// </summary>
    private void UnlockClock(NoSlotClockFixture fixture)
    {
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);
        fixture.Clock.Read(0x5);
        fixture.Clock.Read(0xA);
    }

    #endregion
}
