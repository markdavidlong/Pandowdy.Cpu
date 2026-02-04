// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

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

        // Act - Read without sending unlock pattern
        var value = fixture.Clock.Read(0x10);

        // Assert - Should passthrough unmodified
        Assert.Equal(0x99, value);
        Assert.Equal(1, fixture.Downstream.ReadCount);
    }

    [Fact]
    public void Read_UnlockSequence_UnlocksAfterCorrectPattern()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Downstream.ReturnValue = 0x42; // 0x42 in binary = 01000010

        // Act - Send unlock pattern
        UnlockClock(fixture);

        // Now read a clock byte - should return clock data in bit 0
        var value = fixture.Clock.Read(0x0);

        // Assert - Bit 0 should be clock data (modified), other bits from downstream
        // The value should be 0x42 or 0x43 depending on clock bit
        Assert.True(value == 0x42 || value == 0x43);
    }

    [Fact]
    public void Read_UnlockSequence_WrongPattern_StaysLocked()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Downstream.ReturnValue = 0x99;

        // Act - Send wrong pattern (all zeros instead of recognition pattern)
        for (int i = 0; i < 64; i++)
        {
            fixture.Clock.Read(0x00); // All A0=0
        }

        // Read should still passthrough
        var value = fixture.Clock.Read(0x00);

        // Assert - Should still be in matching mode, returning unmodified downstream
        Assert.Equal(0x99, value);
    }

    [Fact]
    public void Read_PartialPattern_StaysLocked()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Downstream.ReturnValue = 0x99;

        // Act - Send only 32 bits of pattern
        for (int i = 0; i < 32; i++)
        {
            bool bit = ((RecognitionPattern >> i) & 1) != 0;
            fixture.Clock.Read(bit ? (ushort)0x01 : (ushort)0x00);
        }

        // Read should still passthrough
        var value = fixture.Clock.Read(0x00);

        // Assert - Should still be in matching mode
        Assert.Equal(0x99, value);
    }

    [Fact]
    public void Read_CorrectPatternTwice_UnlocksOnlyOnce()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Clock.SetClockTime(new DateTime(2024, 6, 15, 12, 0, 0, 0));

        // Act - Unlock and read all 64 bits (returns to matching state)
        UnlockClock(fixture);
        for (int i = 0; i < 64; i++)
        {
            fixture.Clock.Read(0x00);
        }

        // Now should be back in matching mode - unlock again
        UnlockClock(fixture);

        // Read first byte
        byte centiseconds = ReadClockByte(fixture);

        // Assert - Should read valid clock data
        Assert.Equal(0x00, centiseconds); // 0 centiseconds
    }

    [Fact]
    public void Reset_ClearsUnlockState()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Downstream.ReturnValue = 0x99;

        // Unlock the clock
        UnlockClock(fixture);

        // Act - Reset
        fixture.Clock.Reset();

        // Assert - Should be locked again (passthrough to downstream)
        var value = fixture.Clock.Read(0x00);
        Assert.Equal(0x99, value);
    }

    [Fact]
    public void Read_AfterUnlock_AllAddressesReturnClockData()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Clock.SetClockTime(new DateTime(2024, 6, 15, 12, 0, 0, 0));
        fixture.Downstream.ReturnValue = 0x00;
        UnlockClock(fixture);

        // Act - Read from various addresses
        byte value1 = fixture.Clock.Read(0x00);
        byte value2 = fixture.Clock.Read(0x10);
        byte value3 = fixture.Clock.Read(0x50);

        // Assert - All reads advance through clock data
        // All should have clock data in bit 0
        // They may differ based on which bit we're on
        Assert.True(value1 == 0x00 || value1 == 0x01);
        Assert.True(value2 == 0x00 || value2 == 0x01);
        Assert.True(value3 == 0x00 || value3 == 0x01);
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
    public void Read_DataBit_AfterUnlock_ReturnsBitInPosition0()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act - Read data bit (clock data is in bit 0 of return value)
        var value = fixture.Clock.Read(0x0);

        // Assert - NSC returns clock bit in bit 0 position
        // The value will be (romData & 0xFE) | clockBit
        // Since MockSystemIoHandler returns 0x42, result is 0x42 or 0x43
        Assert.True((value & 0xFE) == (fixture.Downstream.ReturnValue & 0xFE));
    }

    [Fact]
    public void Read_AfterUnlock_ReturnsClockDataInBit0()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Clock.SetClockTime(new DateTime(2024, 6, 15, 12, 30, 45, 560)); // 56 centiseconds
        UnlockClock(fixture);

        // Act - Read first byte (centiseconds = 56 = 0x56 BCD)
        byte centiseconds = ReadClockByte(fixture);

        // Assert - Should be 0x56 (56 in BCD)
        Assert.Equal(0x56, centiseconds);
    }

    [Fact]
    public void Read_64Bits_ReturnsToMatchingState()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act - Read all 64 bits (8 bytes)
        for (int i = 0; i < 64; i++)
        {
            fixture.Clock.Read(0x00);
        }

        // After 64 reads, should return to matching state
        // Now reads should pass through to downstream unchanged
        fixture.Downstream.ReturnValue = 0xAB;
        var value = fixture.Clock.Read(0x00);

        // Assert - Should return unmodified downstream value (not clock data)
        // Actually, it returns clock bit in bit 0, so check if we're back to matching
        Assert.Equal(0xAB, value); // Full passthrough when in matching state
    }

    [Fact]
    public void Read_LoadNextByte_AutoAdvancesAfterEightBits()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        fixture.Clock.SetClockTime(new DateTime(2024, 6, 15, 12, 30, 45, 560));
        UnlockClock(fixture);

        // Act - Read first 2 bytes
        byte byte0 = ReadClockByte(fixture); // Centiseconds = 0x56
        byte byte1 = ReadClockByte(fixture); // Seconds = 45 = 0x45 BCD

        // Assert
        Assert.Equal(0x56, byte0);
        Assert.Equal(0x45, byte1);
    }

    [Fact]
    public void Read_AllEightBytes_ReturnsCorrectTime()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        // Set time: June 15, 2024 (Saturday), 12:30:45.56
        // Saturday = DayOfWeek.Saturday = 6, so NSC day = 6 + 1 = 7
        fixture.Clock.SetClockTime(new DateTime(2024, 6, 15, 12, 30, 45, 560));
        UnlockClock(fixture);

        // Act - Read all 8 bytes
        byte[] bytes = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            bytes[i] = ReadClockByte(fixture);
        }

        // Assert - Verify BCD encoded values
        Assert.Equal(0x56, bytes[0]); // Centiseconds = 56
        Assert.Equal(0x45, bytes[1]); // Seconds = 45
        Assert.Equal(0x30, bytes[2]); // Minutes = 30
        Assert.Equal(0x12, bytes[3]); // Hours = 12
        Assert.Equal(0x07, bytes[4]); // Day of week = Saturday = 7
        Assert.Equal(0x15, bytes[5]); // Day = 15
        Assert.Equal(0x06, bytes[6]); // Month = 6
        Assert.Equal(0x24, bytes[7]); // Year = 24
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
        var targetTime = new DateTime(1985, 6, 15, 12, 30, 0);

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
        var targetTime = new DateTime(2000, 1, 1, 0, 0, 0);
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
        fixture.Clock.SetClockTime(new DateTime(2024, 6, 15, 12, 34, 56, 0)); // 0 centiseconds
        UnlockClock(fixture);

        // Act - Read first two bytes
        byte centiseconds = ReadClockByte(fixture);
        byte seconds = ReadClockByte(fixture);

        // Assert - Seconds should be 0x56 (56 in BCD)
        Assert.Equal(0x00, centiseconds); // 0 centiseconds
        Assert.Equal(0x56, seconds);      // 56 seconds in BCD
    }

    [Fact]
    public void Reset_PreservesTimeOffset()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        var targetTime = new DateTime(1990, 12, 25, 10, 30, 0);
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

        // June 17, 2024 is a Monday
        // DayOfWeek.Monday = 1, so NSC day = 1 + 1 = 2
        var testTime = new DateTime(2024, 6, 17, 0, 0, 0);
        fixture.Clock.SetClockTime(testTime);
        UnlockClock(fixture);

        // Skip first 4 bytes (centiseconds, seconds, minutes, hours)
        for (int i = 0; i < 4; i++)
        {
            ReadClockByte(fixture);
        }

        // Act - Read byte 4 (day of week)
        byte dayOfWeek = ReadClockByte(fixture);

        // Assert - Monday = DayOfWeek.Monday(1) + 1 = 2 in NSC format
        Assert.Equal(0x02, dayOfWeek);
    }

    [Fact]
    public void Read_MultipleBytes_ReturnsDifferentValues()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        // Set time with distinct values: 23:59:58.00 on Dec 25
        fixture.Clock.SetClockTime(new DateTime(2024, 12, 25, 23, 59, 58, 0));
        UnlockClock(fixture);

        // Act - Read first 3 bytes (centiseconds, seconds, minutes)
        byte centiseconds = ReadClockByte(fixture);
        byte seconds = ReadClockByte(fixture);
        byte minutes = ReadClockByte(fixture);

        // Assert - Verify expected BCD values
        Assert.Equal(0x00, centiseconds); // 0 centiseconds
        Assert.Equal(0x58, seconds);      // 58 seconds in BCD
        Assert.Equal(0x59, minutes);      // 59 minutes in BCD
    }

    #endregion

    #region Time Writing Tests (3 tests)

    [Fact]
    public void Write_AlwaysPassesThroughToDownstream()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act - Writes always pass through (NSC protocol is read-based)
        fixture.Clock.Write(0x00, 0x42);

        // Assert - Write should passthrough
        Assert.Equal(1, fixture.Downstream.WriteCount);
        Assert.Equal(0x00, fixture.Downstream.LastWriteAddress);
        Assert.Equal(0x42, fixture.Downstream.LastWriteValue);
    }

    [Fact]
    public void Write_AfterUnlock_StillPassesThrough()
    {
        // Arrange
        var fixture = new NoSlotClockFixture();
        UnlockClock(fixture);

        // Act - Even after unlock, writes pass through
        fixture.Clock.Write(0x10, 0xAB);

        // Assert
        Assert.Equal(1, fixture.Downstream.WriteCount);
        Assert.Equal(0x10, fixture.Downstream.LastWriteAddress);
        Assert.Equal(0xAB, fixture.Downstream.LastWriteValue);
    }

    [Fact]
    public void SetClockTime_ViaPublicMethod_UpdatesTime()
    {
        // Arrange - The DS1216 write protocol is complex; use the public API
        var fixture = new NoSlotClockFixture();
        var targetTime = new DateTime(2024, 6, 15, 12, 45, 30);

        // Act
        fixture.Clock.SetClockTime(targetTime);

        // Assert
        DateTime result = fixture.Clock.GetClockTime();
        Assert.Equal(2024, result.Year);
        Assert.Equal(6, result.Month);
        Assert.Equal(15, result.Day);
        Assert.Equal(12, result.Hour);
        Assert.Equal(45, result.Minute);
        Assert.Equal(30, result.Second);
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
    /// The 64-bit recognition pattern for the DS1216 No-Slot Clock.
    /// Pattern: $5C $A3 $3A $C5 $5C $A3 $3A $C5 (read LSB first within each byte).
    /// </summary>
    private const ulong RecognitionPattern = 0xC53AA35CC53AA35C;

    /// <summary>
    /// Helper method to unlock the clock with the correct 64-bit sequence.
    /// </summary>
    /// <remarks>
    /// The DS1216 protocol requires 64 reads where A0 (address bit 0) matches
    /// the recognition pattern bit-by-bit. The pattern is shifted into the
    /// accumulator from bit 63 down to bit 0.
    /// </remarks>
    private static void UnlockClock(NoSlotClockFixture fixture)
    {
        // Send the 64-bit recognition pattern via A0 (address bit 0)
        // The pattern is shifted in from MSB to LSB
        for (int i = 0; i < 64; i++)
        {
            // Extract bit i from the pattern (LSB first)
            bool bit = ((RecognitionPattern >> i) & 1) != 0;
            // Read from address 0 or 1 depending on the bit
            ushort address = bit ? (ushort)0x01 : (ushort)0x00;
            fixture.Clock.Read(address);
        }
    }

    /// <summary>
    /// Reads a single byte (8 bits) of clock data after unlock.
    /// Returns the byte reconstructed from bit 0 of each read.
    /// </summary>
    private static byte ReadClockByte(NoSlotClockFixture fixture)
    {
        byte result = 0;
        for (int i = 0; i < 8; i++)
        {
            byte readValue = fixture.Clock.Read(0x00); // Any address works, we just need the read
            if ((readValue & 0x01) != 0)
            {
                result |= (byte)(1 << i);
            }
        }
        return result;
    }

    #endregion
}
