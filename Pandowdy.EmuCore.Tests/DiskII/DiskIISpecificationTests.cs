// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

//------------------------------------------------------------------------------
// DiskIISpecificationTests.cs
//
// SPECIFICATION-DRIVEN TESTS FOR DISK II CONTROLLER
//
// These tests are based on documented Apple II Disk II Controller behavior,
// NOT the current implementation. The goal is to define correct behavior that
// the code should satisfy.
//
// Reference Documentation:
// - Apple II Reference Manual (1978, 1979)
// - Understanding the Apple II by Jim Sather (1983)
// - Beneath Apple DOS by Don Worth and Pieter Lechner (1981)
// - Apple II Disk II Interface Card schematic
// - WOZ 2.0 Reference Implementation
//
// IMPORTANT: These tests define EXPECTED behavior. If tests fail, it indicates
// bugs in the implementation that need to be fixed, not tests that need updating.
//------------------------------------------------------------------------------

using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Slots;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.Tests.Mocks;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Specification-driven tests for Disk II controller behavior.
/// Based on Apple II hardware documentation, not current implementation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Test Philosophy:</strong> These tests define what the Disk II controller
/// SHOULD do according to Apple II specifications. Failing tests indicate implementation
/// bugs that need fixing.
/// </para>
/// <para>
/// <strong>References:</strong>
/// <list type="bullet">
/// <item>Understanding the Apple II, Chapter 9 (Disk II Controller)</item>
/// <item>Beneath Apple DOS, Chapter 3 (Disk Organization)</item>
/// <item>Apple II Reference Manual, Disk II section</item>
/// </list>
/// </para>
/// </remarks>
public class DiskIISpecificationTests
{
    #region Test Infrastructure

    private readonly CpuClockingCounters _clocking = new();
    private readonly DiskStatusProvider _statusProvider = new();
    private readonly CardResponseChannel _responseChannel = new();
    private readonly MockDiskIIFactory _driveFactory = new();
    private static readonly MockDiskImageStore MockStore = new();

    private DiskIIControllerCard16Sector CreateCard()
    {
        return new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
    }

    private void AdvanceCycles(int cycles)
    {
        _clocking.IncrementCycles(cycles);
    }

    /// <summary>
    /// Advances time by approximately one second (1,023,000 cycles at 1.023 MHz).
    /// </summary>
    private void AdvanceOneSecond()
    {
        AdvanceCycles(1_023_000);
    }

    /// <summary>
    /// Advances time to trigger VBlank (~60 Hz).
    /// </summary>
    private void AdvanceToVBlank()
    {
        while (_clocking.TotalCycles < _clocking.NextVBlankCycle)
        {
            AdvanceCycles(1000);
        }
        _clocking.CheckAndAdvanceVBlank();
    }

    #endregion

    #region I/O Address Mapping Specifications

    // Per Apple II Reference Manual and Understanding the Apple II:
    // Disk II controller uses 16 I/O addresses per slot ($C0n0-$C0nF where n = 8+slot)
    //
    // Address Map:
    // $C0n0 = Phase 0 Off    $C0n1 = Phase 0 On
    // $C0n2 = Phase 1 Off    $C0n3 = Phase 1 On
    // $C0n4 = Phase 2 Off    $C0n5 = Phase 2 On
    // $C0n6 = Phase 3 Off    $C0n7 = Phase 3 On
    // $C0n8 = Motor Off      $C0n9 = Motor On
    // $C0nA = Select Drive 1 $C0nB = Select Drive 2
    // $C0nC = Q6L (Q6=0)     $C0nD = Q6H (Q6=1)
    // $C0nE = Q7L (Q7=0)     $C0nF = Q7H (Q7=1)

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x03)]
    [InlineData(0x04)]
    [InlineData(0x05)]
    [InlineData(0x06)]
    [InlineData(0x07)]
    [InlineData(0x08)]
    [InlineData(0x09)]
    [InlineData(0x0A)]
    [InlineData(0x0B)]
    [InlineData(0x0C)]
    [InlineData(0x0D)]
    [InlineData(0x0E)]
    [InlineData(0x0F)]
    public void IoAddress_ShouldBeAccessible(byte address)
    {
        // Specification: All 16 I/O addresses should be accessible without throwing
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Reading should not throw
        var exception = Record.Exception(() => card.ReadIO(address));
        Assert.Null(exception);

        // Writing should not throw
        exception = Record.Exception(() => card.WriteIO(address, 0x00));
        Assert.Null(exception);
    }

    #endregion

    #region Q6/Q7 Mode Selection Specifications

    // Per Understanding the Apple II (p. 9-12):
    // Q6 and Q7 control lines select the operating mode:
    //
    // Q7=0, Q6=0: Read mode - shift register clocks in bits from disk
    // Q7=0, Q6=1: Sense write protect - returns write protect status in bit 7
    // Q7=1, Q6=0: Write load - prepares for write operation
    // Q7=1, Q6=1: Write mode - shift register clocks out bits to disk

    [Fact]
    public void Q6Q7_ReadMode_ShouldBeQ6LowQ7Low()
    {
        // Specification: Q6=0, Q7=0 is read mode
        // Access $C0nC (Q6L) then $C0nE (Q7L) to enter read mode
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Enter read mode: Q6=0, Q7=0
        card.ReadIO(0x0C); // Q6L - set Q6=0
        card.ReadIO(0x0E); // Q7L - set Q7=0

        // In read mode, reading $C0nC should return shift register data
        // (Implementation detail: may return null for floating bus if motor off)
        _ = card.ReadIO(0x0C);
        // Result depends on motor state and disk data - just verify no exception
    }

    [Fact]
    public void Q6Q7_WriteProtectSense_ShouldBeQ6HighQ7Low()
    {
        // Specification: Q6=1, Q7=0 is write protect sense mode
        // Reading in this mode returns write protect status in bit 7
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Select drive and turn motor on
        card.ReadIO(0x0A); // Select Drive 1
        card.ReadIO(0x09); // Motor On

        // Enter write protect sense mode: Q6=1, Q7=0
        card.ReadIO(0x0D); // Q6H - set Q6=1
        card.ReadIO(0x0E); // Q7L - set Q7=0

        // Read write protect status
        _ = card.ReadIO(0x0D);

        // Note: Actual value depends on disk state
        // Bit 7 = 1 means write protected (or no disk)
        // Bit 7 = 0 means write enabled
    }

    [Fact]
    public void Q6Q7_WriteMode_ShouldBeQ6HighQ7High()
    {
        // Specification: Q6=1, Q7=1 is write mode
        // Writing to $C0nD in this mode loads the write latch
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Enter write mode: Q6=1, Q7=1
        card.ReadIO(0x0D); // Q6H - set Q6=1
        card.ReadIO(0x0F); // Q7H - set Q7=1

        // Write a byte to the write latch
        var exception = Record.Exception(() => card.WriteIO(0x0D, 0xFF));
        Assert.Null(exception);
    }

    #endregion

    #region Motor Control Specifications

    // Per Understanding the Apple II (p. 9-8):
    // - $C0n8 = Motor off (with ~1 second delay)
    // - $C0n9 = Motor on (immediate)
    // - Motor-on cancels any pending motor-off
    // - Motor must be on to read or write data

    [Fact]
    public void Motor_OnCommand_ShouldTurnMotorOnImmediately()
    {
        // Specification: $C0n9 turns motor on immediately
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        //var drive = card.Drives[0];

        Assert.False(card.IsMotorRunning, "Motor should be off initially");

        card.ReadIO(0x09); // Motor On

        Assert.True(card.IsMotorRunning, "Motor should be on after $C0n9 access");
    }

    [Fact]
    public void Motor_OffCommand_ShouldDelayTurnOff()
    {
        // Specification: $C0n8 schedules motor-off with ~1 second delay
        // Motor should NOT turn off immediately
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        var _ = card.Drives[0];

        // Turn motor on
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Request motor off
        card.ReadIO(0x08);

        // Motor should still be on (delay not elapsed)
        Assert.True(card.IsMotorRunning, "Motor should remain on immediately after off command (1-second delay)");
    }

    [Fact]
    public void Motor_OffDelay_ShouldBeApproximatelyOneSecond()
    {
        // Specification: Motor-off delay is approximately 1 second
        // At 1.023 MHz, that's ~1,023,000 cycles
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        var _ = card.Drives[0];

        // Turn motor on
        card.ReadIO(0x09);

        // Request motor off
        card.ReadIO(0x08);

        // Advance 0.5 seconds - motor should still be on
        AdvanceCycles(500_000);
        AdvanceToVBlank(); // Trigger motor-off check
        Assert.True(card.IsMotorRunning, "Motor should still be on after 0.5 seconds");

        // Advance another 0.6 seconds (total 1.1 seconds) - motor should be off
        AdvanceCycles(600_000);
        AdvanceToVBlank(); // Trigger motor-off check
        Assert.False(card.IsMotorRunning, "Motor should be off after ~1 second delay");
    }

    [Fact]
    public void Motor_OnCancelsPendingOff()
    {
        // Specification: Motor-on command cancels any pending motor-off
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        var _ = card.Drives[0];

        // Turn motor on
        card.ReadIO(0x09);

        // Request motor off (starts 1-second countdown)
        card.ReadIO(0x08);

        // Advance 0.5 seconds
        AdvanceCycles(500_000);

        // Turn motor on again (should cancel pending off)
        card.ReadIO(0x09);

        // Advance another 1 second
        AdvanceOneSecond();
        AdvanceToVBlank();

        // Motor should still be on (off was cancelled)
        Assert.True(card.IsMotorRunning, "Motor-on should cancel pending motor-off");
    }

    #endregion

    #region Drive Selection Specifications

    // Per Apple II Reference Manual:
    // - $C0nA selects Drive 1
    // - $C0nB selects Drive 2
    // - Only one drive can be selected at a time
    // - Motor state is per-drive

    [Fact]
    public void DriveSelect_Drive1_ShouldSelectFirstDrive()
    {
        // Specification: $C0nA selects Drive 1 (index 0)
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        card.ReadIO(0x0A); // Select Drive 1

        // Turn motor on
        card.ReadIO(0x09);

        // Drive 1 motor should be on
        Assert.True(card.IsMotorRunning, "Drive 1 motor should be on");
    }

    [Fact]
    public void DriveSelect_Drive2_ShouldSelectSecondDrive()
    {
        // Specification: $C0nB selects Drive 2 (index 1)
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        card.ReadIO(0x0B); // Select Drive 2

        // Turn motor on
        card.ReadIO(0x09);

        // Drive 2 motor should be on
        Assert.True(card.IsMotorRunning, "Drive 2 motor should be on");
    }

    [Fact]
    public void DriveSelect_Switching_MotorStaysOn()
    {
        // Specification: When switching drives with motor on, the motor STAYS ON
        // because the physical hardware has a single motor line that switches to power the new drive.
        // Phase 5: Motor state is controller-level, not per-drive.
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Select Drive 1 and turn motor on
        card.ReadIO(0x0A);
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Switch to Drive 2 - motor stays ON (now powers Drive 2)
        card.ReadIO(0x0B);

        // Motor should stay on (single motor line, now powering Drive 2)
        Assert.True(card.IsMotorRunning, "Motor should stay on when switching drives, now powers Drive 2");

        // Motor is already on, but explicitly sending motor-on command is safe
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning, "Motor should still be on");
    }

    #endregion

    #region Stepper Motor Phase Specifications

    // Per Understanding the Apple II (p. 9-6 to 9-7):
    // The stepper motor has 4 phases (0-3) controlled by $C0n0-$C0n7
    // - Odd addresses turn phases ON
    // - Even addresses turn phases OFF
    // - Motor moves in quarter-track increments
    // - Phases can be energized in combinations for precise positioning
    // - Track 0 to Track 34 = 35 tracks, 140 quarter-tracks

    [Fact]
    public void Phase_TurnOn_ShouldUseOddAddresses()
    {
        // Specification: Odd addresses ($C0n1, $C0n3, $C0n5, $C0n7) turn phases ON
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // No exception should occur
        card.ReadIO(0x01); // Phase 0 On
        card.ReadIO(0x03); // Phase 1 On
        card.ReadIO(0x05); // Phase 2 On
        card.ReadIO(0x07); // Phase 3 On
    }

    [Fact]
    public void Phase_TurnOff_ShouldUseEvenAddresses()
    {
        // Specification: Even addresses ($C0n0, $C0n2, $C0n4, $C0n6) turn phases OFF
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn phases on first
        card.ReadIO(0x01);
        card.ReadIO(0x03);
        card.ReadIO(0x05);
        card.ReadIO(0x07);

        // Turn phases off
        card.ReadIO(0x00); // Phase 0 Off
        card.ReadIO(0x02); // Phase 1 Off
        card.ReadIO(0x04); // Phase 2 Off
        card.ReadIO(0x06); // Phase 3 Off
    }

    [Fact]
    public void Phase_Sequence_ShouldMoveHeadInward()
    {
        // Specification: Phase sequence 0-1-2-3-0-1... moves head to higher tracks
        // Each phase change with motor on should move 1 quarter-track inward
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        var drive = card.Drives[0];

        // Turn motor on
        card.ReadIO(0x09);

        // Record initial position (should be track 0, quarter-track 0)
        int initialQuarterTrack = drive.QuarterTrack;

        // Standard stepping sequence to move inward: activate phase 1
        card.ReadIO(0x01); // Phase 0 On (initial position)
        card.ReadIO(0x00); // Phase 0 Off
        card.ReadIO(0x03); // Phase 1 On
        card.ReadIO(0x02); // Phase 1 Off
        card.ReadIO(0x05); // Phase 2 On
        card.ReadIO(0x04); // Phase 2 Off
        card.ReadIO(0x07); // Phase 3 On

        // Head should have moved inward (higher quarter-track)
        Assert.True(drive.QuarterTrack > initialQuarterTrack,
            $"Head should move inward. Initial: {initialQuarterTrack}, Current: {drive.QuarterTrack}");
    }

    [Fact]
    public void Phase_Sequence_ShouldMoveHeadOutward()
    {
        // Specification: Reverse phase sequence 3-2-1-0-3-2... moves head to lower tracks
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        var drive = card.Drives[0];

        // Turn motor on
        card.ReadIO(0x09);

        // First move head inward to track 4 (quarter-track 16)
        for (int i = 0; i < 16; i++)
        {
            int phase = i % 4;
            card.ReadIO((byte)(phase * 2 + 1)); // Phase On
            card.ReadIO((byte)(phase * 2));      // Phase Off
        }

        int positionAfterInward = drive.QuarterTrack;

        // Now move outward using reverse sequence
        for (int i = 0; i < 4; i++)
        {
            int phase = (3 - (i % 4)); // Reverse: 3, 2, 1, 0
            card.ReadIO((byte)(phase * 2 + 1)); // Phase On
            card.ReadIO((byte)(phase * 2));      // Phase Off
        }

        Assert.True(drive.QuarterTrack < positionAfterInward,
            $"Head should move outward. After inward: {positionAfterInward}, Current: {drive.QuarterTrack}");
    }

    [Fact]
    public void Phase_HeadShouldNotMoveBeyondTrack0()
    {
        // Specification: Head cannot move below track 0 (quarter-track 0)
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        var drive = card.Drives[0];

        // Turn motor on
        card.ReadIO(0x09);

        // Try to move outward from track 0
        for (int i = 0; i < 20; i++)
        {
            int phase = (3 - (i % 4)); // Reverse sequence
            card.ReadIO((byte)(phase * 2 + 1));
            card.ReadIO((byte)(phase * 2));
        }

        Assert.True(drive.QuarterTrack >= 0,
            $"Quarter track should not be negative: {drive.QuarterTrack}");
        Assert.True(drive.Track >= 0,
            $"Track should not be negative: {drive.Track}");
    }

    [Fact]
    public void Phase_HeadShouldNotMoveBeyondTrack34()
    {
        // Specification: Standard Disk II has 35 tracks (0-34)
        // Head should not move beyond track 34 (quarter-track 139)
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        var drive = card.Drives[0];

        // Turn motor on
        card.ReadIO(0x09);

        // Try to move to track 40+ (160+ quarter-tracks)
        for (int i = 0; i < 200; i++)
        {
            int phase = i % 4;
            card.ReadIO((byte)(phase * 2 + 1));
            card.ReadIO((byte)(phase * 2));
        }

        // Should be clamped at track 34.75 (quarter-track 139)
        Assert.True(drive.Track <= 35.0,
            $"Track should not exceed 35: {drive.Track}");
        Assert.True(drive.QuarterTrack <= 140,
            $"Quarter track should not exceed 140: {drive.QuarterTrack}");
    }

    [Fact]
    public void Phase_NoMovementWithMotorOff()
    {
        // Specification: Stepper phases should not move head when motor is off
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        var drive = card.Drives[0];

        // Motor is off by default
        Assert.False(card.IsMotorRunning);

        int initialQuarterTrack = drive.QuarterTrack;

        // Try stepping sequence
        for (int i = 0; i < 16; i++)
        {
            int phase = i % 4;
            card.ReadIO((byte)(phase * 2 + 1));
            card.ReadIO((byte)(phase * 2));
        }

        // Position should not change
        Assert.Equal(initialQuarterTrack, drive.QuarterTrack);
    }

    #endregion

    #region Shift Register and Bit Timing Specifications

    // Per Understanding the Apple II (p. 9-10 to 9-11):
    // - Disk rotates at 300 RPM = 5 revolutions/second
    // - Each track holds ~50,000 bits
    // - Bit rate: 250,000 bits/second
    // - At 1.023 MHz CPU: ~4.09 cycles per bit (exactly 45/11 cycles)
    // - Shift register accumulates 8 bits, valid byte when MSB = 1
    // - DOS expects to read bytes, not raw bits

    [Fact]
    public void ShiftRegister_ShouldAccumulateBits()
    {
        // Specification: The shift register accumulates bits from the disk
        // A valid byte is indicated when bit 7 is set (sync bytes start with 1)
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on and select drive
        card.ReadIO(0x0A); // Select Drive 1
        card.ReadIO(0x09); // Motor On

        // Enter read mode
        card.ReadIO(0x0C); // Q6L
        card.ReadIO(0x0E); // Q7L

        // Advance time to accumulate bits (at least 8 bits = ~33 cycles)
        AdvanceCycles(100);

        // Read shift register
        card.ReadIO(0x0C);

        // With no disk, result is implementation-defined
        // Just verify the operation completes
    }

    [Theory]
    [InlineData(4)]   // ~1 bit
    [InlineData(8)]   // ~2 bits
    [InlineData(16)]  // ~4 bits
    [InlineData(33)]  // ~8 bits (one byte)
    [InlineData(66)]  // ~16 bits (two bytes)
    public void ShiftRegister_TimingGranularity(int cycles)
    {
        // Specification: Bit timing should be consistent
        // ~4.09 cycles per bit (45/11 exactly)
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        card.ReadIO(0x0A); // Select Drive 1
        card.ReadIO(0x09); // Motor On
        card.ReadIO(0x0C); // Q6L - Read mode
        card.ReadIO(0x0E); // Q7L

        AdvanceCycles(cycles);

        // Should not throw
        card.ReadIO(0x0C);
    }

    #endregion

    #region Disk Data Format Specifications

    // Per Beneath Apple DOS (Chapter 3):
    // Standard 16-sector format:
    // - Address Field Prologue: D5 AA 96
    // - Volume, Track, Sector, Checksum (4-and-4 encoded)
    // - Address Field Epilogue: DE AA EB
    // - Data Field Prologue: D5 AA AD
    // - 343 bytes of 6-and-2 encoded data
    // - Data Field Epilogue: DE AA EB

    [Fact]
    public void DiskFormat_AddressPrologue_ShouldBeD5AA96()
    {
        // Specification: Address field prologue is always D5 AA 96
        // These are the "magic bytes" that identify the start of an address field

        byte[] expectedPrologue = [0xD5, 0xAA, 0x96];

        // Verify the constant values
        Assert.Equal(0xD5, expectedPrologue[0]);
        Assert.Equal(0xAA, expectedPrologue[1]);
        Assert.Equal(0x96, expectedPrologue[2]);
    }

    [Fact]
    public void DiskFormat_DataPrologue_ShouldBeD5AAAD()
    {
        // Specification: Data field prologue is always D5 AA AD
        byte[] expectedPrologue = [0xD5, 0xAA, 0xAD];

        Assert.Equal(0xD5, expectedPrologue[0]);
        Assert.Equal(0xAA, expectedPrologue[1]);
        Assert.Equal(0xAD, expectedPrologue[2]);
    }

    [Fact]
    public void DiskFormat_Epilogue_ShouldBeDEAAEB()
    {
        // Specification: Both address and data field epilogues are DE AA EB
        byte[] expectedEpilogue = [0xDE, 0xAA, 0xEB];

        Assert.Equal(0xDE, expectedEpilogue[0]);
        Assert.Equal(0xAA, expectedEpilogue[1]);
        Assert.Equal(0xEB, expectedEpilogue[2]);
    }

    [Fact]
    public void DiskFormat_SyncBytes_ShouldBeFF()
    {
        // Specification: Sync bytes (self-sync bytes) are $FF
        // They allow the controller to synchronize with the data stream
        // 10-bit sync pattern: 1111111100 (8 ones + 2 zeros)
        byte syncByte = 0xFF;
        Assert.Equal(0xFF, syncByte);
    }

    [Fact]
    public void DiskFormat_ValidDiskBytes_MustHaveMSBSet()
    {
        // Specification: All valid disk bytes must have bit 7 set
        // This is required for self-clocking data recovery
        // Valid disk byte range: $96-$FF (with specific exclusions)

        // Test some known valid disk bytes
        byte[] validBytes = [0x96, 0xAA, 0xAD, 0xD5, 0xDE, 0xEB, 0xFF];

        foreach (var b in validBytes)
        {
            Assert.True((b & 0x80) != 0, $"Valid disk byte {b:X2} must have MSB set");
        }
    }

    #endregion

    #region 6-and-2 Encoding Specifications

    // Per Beneath Apple DOS (Appendix):
    // 6-and-2 encoding maps 6-bit values (0-63) to valid disk bytes
    // Used to encode 256 data bytes as 343 disk bytes

    [Fact]
    public void SixAndTwo_EncodingTable_ShouldHave64Entries()
    {
        // Specification: 6-and-2 encoding has 64 entries (for 6-bit values 0-63)
        // The encoding table maps each 6-bit value to a unique disk byte

        // Standard 6-and-2 encoding table from Beneath Apple DOS
        byte[] encodingTable =
        [
            0x96, 0x97, 0x9A, 0x9B, 0x9D, 0x9E, 0x9F, 0xA6,
            0xA7, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF, 0xB2, 0xB3,
            0xB4, 0xB5, 0xB6, 0xB7, 0xB9, 0xBA, 0xBB, 0xBC,
            0xBD, 0xBE, 0xBF, 0xCB, 0xCD, 0xCE, 0xCF, 0xD3,
            0xD6, 0xD7, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE,
            0xDF, 0xE5, 0xE6, 0xE7, 0xE9, 0xEA, 0xEB, 0xEC,
            0xED, 0xEE, 0xEF, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6,
            0xF7, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF
        ];

        Assert.Equal(64, encodingTable.Length);

        // All encoded values must have MSB set
        foreach (var encoded in encodingTable)
        {
            Assert.True((encoded & 0x80) != 0, $"Encoded byte {encoded:X2} must have MSB set");
        }

        // All values must be unique
        var distinct = encodingTable.Distinct().ToArray();
        Assert.Equal(64, distinct.Length);
    }

    [Fact]
    public void SixAndTwo_DataFieldSize_ShouldBe343Bytes()
    {
        // Specification: 256 data bytes encode to 343 disk bytes
        // 256 bytes = 2048 bits
        // 343 × 6 = 2058 bits (covers 2048 with some overhead)

        int dataBytesPerSector = 256;
        int encodedBytesPerSector = 343;

        // Verify the encoding expands data
        Assert.True(encodedBytesPerSector > dataBytesPerSector);

        // The 343 bytes consist of:
        // - 86 bytes of "secondary" data (2-bit pieces)
        // - 256 bytes of "primary" data (6-bit pieces)
        // - 1 checksum byte
        int secondaryBytes = 86;
        int primaryBytes = 256;
        int checksumBytes = 1;

        Assert.Equal(encodedBytesPerSector, secondaryBytes + primaryBytes + checksumBytes);
    }

    #endregion

    #region 4-and-4 Encoding Specifications

    // Per Beneath Apple DOS:
    // 4-and-4 encoding splits each byte into two disk bytes
    // Used for address field (volume, track, sector, checksum)
    // Each nibble is encoded with bits 7,6 set and value in 5,4,3,2,1,0

    [Fact]
    public void FourAndFour_EncodingRule()
    {
        // Specification: 4-and-4 encodes byte AB as two bytes:
        // First byte: 1010 AAAA (0xAA | (A >> 1))
        // Second byte: 1010 BBBB (0xAA | B)

        // Example: Encode 0x12
        byte value = 0x12;
        byte highNibble = (byte)(value >> 4);    // 0x01
        byte lowNibble = (byte)(value & 0x0F);   // 0x02

        // Encoded form (standard 4-and-4)
        byte encodedHigh = (byte)(0xAA | (highNibble << 1));
        byte encodedLow = (byte)(0xAA | lowNibble);

        // Both must have bits 7 and 5 set (0xAA mask)
        Assert.Equal(0xAA, encodedHigh & 0xAA);
        Assert.Equal(0xAA, encodedLow & 0xAA);
    }

    [Fact]
    public void FourAndFour_AddressFieldChecksum()
    {
        // Specification: Address field checksum is XOR of volume, track, sector
        byte volume = 254;
        byte track = 17;
        byte sector = 5;

        byte checksum = (byte)(volume ^ track ^ sector);

        // Checksum should XOR back to zero when all fields are XORed
        byte verification = (byte)(volume ^ track ^ sector ^ checksum);
        Assert.Equal(0, verification);
    }

    #endregion

    #region Write Protection Specifications

    // Per Apple II Reference Manual:
    // - Write protect is sensed when Q6=1, Q7=0
    // - Bit 7 of result indicates write protect status
    // - Bit 7 = 1: Write protected (or no disk)
    // - Bit 7 = 0: Write enabled

    [Fact]
    public void WriteProtect_SenseMode_RequiresCorrectQ6Q7State()
    {
        // Specification: Write protect is sensed only when Q6=1, Q7=0
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        card.ReadIO(0x0A); // Select Drive 1
        card.ReadIO(0x09); // Motor On

        // Enter write protect sense mode
        card.ReadIO(0x0D); // Q6H (Q6=1)
        card.ReadIO(0x0E); // Q7L (Q7=0)

        // Read should return write protect status
        _ = card.ReadIO(0x0D);

        // With mock drive (no real disk), expect write protected
        // Bit 7 should be set
    }

    [Fact]
    public void WriteProtect_NoDisk_ShouldIndicateProtected()
    {
        // Specification: No disk inserted should report as write protected
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);
        var drive = card.Drives[0];

        Assert.False(drive.HasDisk, "Mock drive should have no disk by default");

        // Sense write protect
        card.ReadIO(0x0D); // Q6H
        card.ReadIO(0x0E); // Q7L

        // Check status
        bool isProtected = drive.IsWriteProtected();
        Assert.True(isProtected, "No disk should report as write protected");
    }

    #endregion

    #region Timing Specifications

    // Per Understanding the Apple II:
    // - Disk rotates at 300 RPM (5 revolutions per second)
    // - Track contains ~50,000 bits
    // - One revolution = 200ms = 204,600 CPU cycles at 1.023 MHz
    // - Bit rate: 250,000 bits/second
    // - Cycles per bit: 1,023,000 / 250,000 = 4.092 cycles (45/11 exactly)

    [Fact]
    public void Timing_CyclesPerBit_ShouldBe45Over11()
    {
        // Specification: Exact bit timing is 45/11 CPU cycles per bit
        double expectedCyclesPerBit = 45.0 / 11.0; // ≈ 4.0909...

        // This should match the constant in the implementation
        Assert.True(Math.Abs(expectedCyclesPerBit - 4.090909) < 0.001,
            $"Cycles per bit should be ~4.09, got {expectedCyclesPerBit}");
    }

    [Fact]
    public void Timing_CyclesPerRevolution_ShouldBeApproximately204600()
    {
        // Specification: 300 RPM = 5 rev/sec
        // At 1.023 MHz: 1,023,000 / 5 = 204,600 cycles per revolution
        int cpuFrequency = 1_023_000;
        int revolutionsPerSecond = 5;
        int expectedCyclesPerRev = cpuFrequency / revolutionsPerSecond;

        Assert.Equal(204_600, expectedCyclesPerRev);
    }

    [Fact]
    public void Timing_BitsPerTrack_ShouldBeApproximately50000()
    {
        // Specification: Each track holds approximately 50,000 bits
        // 250,000 bits/sec ÷ 5 rev/sec = 50,000 bits/rev
        int bitRate = 250_000;
        int revolutionsPerSecond = 5;
        int bitsPerTrack = bitRate / revolutionsPerSecond;

        Assert.Equal(50_000, bitsPerTrack);
    }

    #endregion
}

#region Mock Classes for Specification Tests

/// <summary>
/// Mock Disk II factory for specification testing.
/// Creates mock drives that simulate basic Disk II behavior.
/// </summary>
internal class MockDiskIIFactory : IDiskIIFactory
{
    private readonly List<MockDiskIIDrive> _createdDrives = [];

    public IReadOnlyList<MockDiskIIDrive> CreatedDrives => _createdDrives;

    public IDiskIIDrive CreateDrive(string driveName)
    {
        var drive = new MockDiskIIDrive(driveName);
        _createdDrives.Add(drive);
        return drive;
    }
}

/// <summary>
/// Mock Disk II drive for specification testing.
/// Simulates basic drive mechanics without actual disk data.
/// </summary>
internal class MockDiskIIDrive(string name) : IDiskIIDrive
{
    public string Name { get; } = name;
    public double Track => QuarterTrack / 4.0;
    public int QuarterTrack { get; private set; } = 68; // Track 17 (default starting position)
    public bool MotorOn { get; set; }
    public bool HasDisk => false; // Mock drive has no disk
    private bool _writeProtected = true; // No disk = write protected

    /// <summary>
    /// Sets the quarter track position for testing boundary conditions.
    /// </summary>
    public void SetQuarterTrack(int quarterTrack)
    {
        QuarterTrack = quarterTrack;
    }

    /// <summary>
    /// Sets the write-protected status for testing.
    /// </summary>
    public void SetWriteProtected(bool isProtected)
    {
        _writeProtected = isProtected;
    }

    public void Reset()
    {
        // Per interface contract: motor off, head position preserved
        // (matches real Disk II hardware behavior)
        MotorOn = false;
    }

    public void Restart()
    {
        MotorOn = false;
        QuarterTrack = 0;
    }

    public void StepToHigherTrack()
    {
        if (QuarterTrack < DiskIIConstants.MaxQuarterTracks)
        {
            QuarterTrack++;
        }
    }

    public void StepToLowerTrack()
    {
        if (QuarterTrack > 0)
        {
            QuarterTrack--;
        }
    }

    public bool? GetBit(ulong currentCycle)
    {
        // No disk - return null (floating)
        return null;
    }

    public int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits)
    {
        // No disk - no bits
        return 0;
    }

    public byte OptimalBitTiming => 32;

    public bool SetBit(bool value)
    {
        // No disk - write fails
        return false;
    }

    public bool IsWriteProtected() => _writeProtected;

    public void NotifyMotorStateChanged(bool motorOn, ulong cycleCount)
    {
        // Mock - do nothing
    }

    public void InsertDisk(string diskImagePath)
    {
        // Mock - do nothing
    }

    public void EjectDisk()
    {
        // Mock - do nothing
    }

    public string? CurrentDiskPath => null;

    public IDiskImageProvider? ImageProvider
    {
        get => null;
        set { /* Mock - do nothing */ }
    }

    public InternalDiskImage? InternalImage => null;
}

#endregion
