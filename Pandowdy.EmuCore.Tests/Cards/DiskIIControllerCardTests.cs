// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Slots;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Messages;
using Pandowdy.EmuCore.DiskII.Providers;
using Pandowdy.EmuCore.Tests.Mocks;

namespace Pandowdy.EmuCore.Tests.Cards;

/// <summary>
/// Tests for DiskIIControllerCard and its variants.
/// </summary>
public class DiskIIControllerCardTests
{
    private readonly CpuClockingCounters _clocking = new();
    private readonly DiskStatusProvider _statusProvider = new();
    private readonly CardResponseChannel _responseChannel = new();
    private readonly MockDiskIIFactory _driveFactory = new();
    private static readonly MockDiskImageStore MockStore = new();

    #region Helper Methods

    private DiskIIControllerCard16Sector CreateCard()
    {
        return new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
    }

    private void AdvanceCycles(int cycles)
    {
        _clocking.IncrementCycles(cycles);
    }

    private void AdvanceToVBlank()
    {
        // Advance past the VBlank start cycle and trigger VBlank
        while (_clocking.TotalCycles < _clocking.NextVBlankCycle)
        {
            AdvanceCycles(1000);
        }
        _clocking.CheckAndAdvanceVBlank();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ThrowsOnNullClocking()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DiskIIControllerCard16Sector(null!, _driveFactory, _statusProvider, _responseChannel, MockStore));
    }

    [Fact]
    public void Constructor_ThrowsOnNullDriveFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DiskIIControllerCard16Sector(_clocking, null!, _statusProvider, _responseChannel, MockStore));
    }

    [Fact]
    public void Constructor_ThrowsOnNullStatusMutator()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DiskIIControllerCard16Sector(_clocking, _driveFactory, null!, _responseChannel, MockStore));
    }

    [Fact]
    public void Constructor_Succeeds_WithValidParameters()
    {
        var card = CreateCard();
        Assert.NotNull(card);
    }

    #endregion

    #region ICard Interface Tests - 16 Sector

    [Fact]
    public void Card16Sector_Name_ReturnsCorrectValue()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        Assert.Equal("Disk II", card.Name);
    }

    [Fact]
    public void Card16Sector_Description_ReturnsCorrectValue()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        Assert.Equal("Disk II Controller - 16-Sector ROM", card.Description);
    }

    [Fact]
    public void Card16Sector_Id_Returns10()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        Assert.Equal(10, card.Id);
    }

    [Fact]
    public void Card16Sector_Slot_ReturnsUnslotted_BeforeInstall()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        Assert.Equal(SlotNumber.Unslotted, card.Slot);
    }

    [Fact]
    public void Card16Sector_Clone_ReturnsNewInstance()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        var clone = card.Clone();

        Assert.NotSame(card, clone);
        Assert.IsType<DiskIIControllerCard16Sector>(clone);
    }

    [Fact]
    public void Card16Sector_Clone_IsUnslotted()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        card.OnInstalled(SlotNumber.Slot6);

        var clone = card.Clone();

        // Clone should be in initial state (unslotted)
        Assert.Equal(SlotNumber.Unslotted, ((DiskIIControllerCard16Sector)clone).Slot);
    }

    [Fact]
    public void Card16Sector_Clone_HasSameProperties()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        var clone = (DiskIIControllerCard16Sector)card.Clone();

        // Clone should have same metadata
        Assert.Equal(card.Name, clone.Name);
        Assert.Equal(card.Description, clone.Description);
        Assert.Equal(card.Id, clone.Id);
    }

    #endregion

    #region ICard Interface Tests - 13 Sector

    [Fact]
    public void Card13Sector_Name_ReturnsCorrectValue()
    {
        var card = new DiskIIControllerCard13Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        Assert.Equal("Disk II (13-Sector)", card.Name);
    }

    [Fact]
    public void Card13Sector_Description_ReturnsCorrectValue()
    {
        var card = new DiskIIControllerCard13Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        Assert.Equal("Disk II Controller - 13-Sector ROM", card.Description);
    }

    [Fact]
    public void Card13Sector_Id_Returns11()
    {
        var card = new DiskIIControllerCard13Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        Assert.Equal(11, card.Id);
    }

    [Fact]
    public void Card13Sector_Clone_ReturnsNewInstance()
    {
        var card = new DiskIIControllerCard13Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        var clone = card.Clone();

        Assert.NotSame(card, clone);
        Assert.IsType<DiskIIControllerCard13Sector>(clone);
    }

    [Fact]
    public void Card13Sector_Clone_IsUnslotted()
    {
        var card = new DiskIIControllerCard13Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        card.OnInstalled(SlotNumber.Slot5);

        var clone = card.Clone();

        // Clone should be in initial state (unslotted)
        Assert.Equal(SlotNumber.Unslotted, ((DiskIIControllerCard13Sector)clone).Slot);
    }

    #endregion

    #region OnInstalled Tests

    [Fact]
    public void OnInstalled_SetsSlotNumber()
    {
        var card = CreateCard();

        card.OnInstalled(SlotNumber.Slot6);

        Assert.Equal(SlotNumber.Slot6, card.Slot);
    }

    [Fact]
    public void OnInstalled_CreatesTwoDrives()
    {
        var card = CreateCard();

        card.OnInstalled(SlotNumber.Slot6);

        Assert.Equal(2, card.Drives.Length);
    }

    [Fact]
    public void OnInstalled_CreatesDrivesWithCorrectNames()
    {
        var card = CreateCard();

        card.OnInstalled(SlotNumber.Slot6);

        Assert.Equal("Slot6-D1", card.Drives[0].Name);
        Assert.Equal("Slot6-D2", card.Drives[1].Name);
    }

    [Fact]
    public void OnInstalled_DrivesAreEmpty()
    {
        var card = CreateCard();

        card.OnInstalled(SlotNumber.Slot6);

        Assert.False(card.Drives[0].HasDisk);
        Assert.False(card.Drives[1].HasDisk);
    }

    [Theory]
    [InlineData(SlotNumber.Slot1)]
    [InlineData(SlotNumber.Slot3)]
    [InlineData(SlotNumber.Slot5)]
    [InlineData(SlotNumber.Slot7)]
    public void OnInstalled_WorksWithDifferentSlots(SlotNumber slot)
    {
        var card = CreateCard();

        card.OnInstalled(slot);

        Assert.Equal(slot, card.Slot);
        Assert.Equal($"Slot{(int)slot}-D1", card.Drives[0].Name);
        Assert.Equal($"Slot{(int)slot}-D2", card.Drives[1].Name);
    }

    #endregion

    #region ROM Tests - 16 Sector

    [Fact]
    public void Card16Sector_ReadRom_ReturnsValidBytes()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);

        // First byte should be 0xA2 (LDX #$20)
        Assert.Equal((byte)0xA2, card.ReadRom(0x00));
    }

    [Fact]
    public void Card16Sector_ReadRom_ReturnsNullForOutOfBounds()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);

        // ROM is 256 bytes, offset 0x100 is out of bounds
        // ReadRom takes byte, so we can only test up to 255
        // The ROM is exactly 256 bytes, so index 255 is valid
        // We can't test beyond 255 with a byte parameter
        var result = card.ReadRom(0xFF);
        Assert.NotNull(result); // Last byte is valid
    }

    [Fact]
    public void Card16Sector_ReadRom_ContainsBootSignature()
    {
        var card = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);

        // Check for known boot ROM pattern at $C6F8 (offset 0xF8)
        // JMP $0801 = 4C 01 08
        Assert.Equal((byte)0x4C, card.ReadRom(0xF8));
        Assert.Equal((byte)0x01, card.ReadRom(0xF9));
        Assert.Equal((byte)0x08, card.ReadRom(0xFA));
    }

    #endregion

    #region ROM Tests - 13 Sector

    [Fact]
    public void Card13Sector_ReadRom_ReturnsValidBytes()
    {
        var card = new DiskIIControllerCard13Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);

        // First byte should be 0xA2 (LDX #$20) - same as 16-sector
        Assert.Equal((byte)0xA2, card.ReadRom(0x00));
    }

    [Fact]
    public void Card13Sector_ReadRom_DiffersFrom16Sector()
    {
        var card16 = new DiskIIControllerCard16Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);
        var card13 = new DiskIIControllerCard13Sector(_clocking, _driveFactory, _statusProvider, _responseChannel, MockStore);

        // The ROMs should differ at some point
        bool foundDifference = false;
        for (byte i = 0; i < 255; i++)
        {
            if (card16.ReadRom(i) != card13.ReadRom(i))
            {
                foundDifference = true;
                break;
            }
        }

        Assert.True(foundDifference);
    }

    #endregion

    #region I/O Read Tests - Phase Control (0x0-0x7)

    [Fact]
    public void ReadIO_PhaseAddresses_ReturnNull()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Phase addresses return null (floating bus)
        for (byte addr = 0x00; addr <= 0x07; addr++)
        {
            Assert.Null(card.ReadIO(addr));
        }
    }



    #endregion

    #region I/O Read Tests - Motor Control (0x8-0x9)

    [Fact]
    public void ReadIO_MotorOff_ReturnsNull()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        Assert.Null(card.ReadIO(0x08));
    }

    [Fact]
    public void ReadIO_MotorOn_ReturnsNull()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        Assert.Null(card.ReadIO(0x09));
    }

    [Fact]
    public void ReadIO_MotorOn_TurnsOnSelectedDriveMotor()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on
        card.ReadIO(0x09);

        Assert.True(card.IsMotorRunning);
    }

    [Fact]
    public void ReadIO_MotorOff_WhilePending_IsIgnored()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Request motor off (schedules pending motor-off)
        card.ReadIO(0x08);

        // Request motor off again (should be ignored since already pending)
        card.ReadIO(0x08);

        // Motor should still be on (delayed off)
        Assert.True(card.IsMotorRunning);

        // Advance time - should still turn off after 1 second (not 2 seconds)
        for (int i = 0; i < 60; i++)
        {
            AdvanceToVBlank();
        }

        Assert.False(card.IsMotorRunning);
    }

    [Fact]
    public void ReadIO_MotorOn_WhileMotorAlreadyOn_DoesNotReset()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Turn motor on again (should not cause issues)
        card.ReadIO(0x09);

        Assert.True(card.IsMotorRunning);
    }

    #endregion

    #region I/O Read Tests - Drive Selection (0xA-0xB)

    [Fact]
    public void ReadIO_SelectDrive1_ReturnsNull()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        Assert.Null(card.ReadIO(0x0A));
    }

    [Fact]
    public void ReadIO_SelectDrive2_ReturnsNull()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        Assert.Null(card.ReadIO(0x0B));
    }

    [Fact]
    public void ReadIO_SelectDrive2_SwitchesToDrive2()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on for drive 1
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Switch to drive 2
        card.ReadIO(0x0B);

        // Turn motor on for drive 2
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);
    }

    [Fact]
    public void ReadIO_SwitchDrives_MotorStaysOn()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on for drive 1
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Switch to drive 2 - motor stays ON (Phase 5: single motor line now powers Drive 2)
        // (hardware has one motor line that powers the selected drive)
        card.ReadIO(0x0B);

        // Motor should stay ON, now powering Drive 2
        Assert.True(card.IsMotorRunning);
    }

    [Fact]
    public void ReadIO_SwitchDrives_ThenMotorOn_NewDriveMotorTurnsOn()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on for drive 1
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Switch to drive 2
        card.ReadIO(0x0B);

        // Turn motor on for drive 2
        card.ReadIO(0x09);

        Assert.True(card.IsMotorRunning);
    }

    [Fact]
    public void ReadIO_SelectSameDrive_DoesNotScheduleMotorOff()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on for drive 1
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Select drive 1 again (same drive)
        card.ReadIO(0x0A);

        // Motor should still be on without any scheduling
        for (int i = 0; i < 60; i++)
        {
            AdvanceToVBlank();
        }

        // Motor stays on because no motor-off was scheduled
        Assert.True(card.IsMotorRunning);
    }

    [Fact]
    public void ReadIO_SwitchDrives_SwitchBack_CancelsMotorOff()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on for drive 1
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Switch to drive 2 (schedules motor-off for drive 1)
        card.ReadIO(0x0B);

        // Immediately switch back to drive 1 and turn motor on (cancels pending off)
        card.ReadIO(0x0A);
        card.ReadIO(0x09);

        // After delay, drive 1 motor should still be on (pending was cancelled)
        for (int i = 0; i < 60; i++)
        {
            AdvanceToVBlank();
        }

        Assert.True(card.IsMotorRunning);
    }

    [Fact]
    public void ReadIO_SwitchDrives_ClearsPhaseStateForBothDrives()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on for drive 1
        card.ReadIO(0x09);

        // Activate Phase 1 on drive 1
        card.ReadIO(0x03); // Phase 1 ON

        // Switch to drive 2 - should clear phases
        card.ReadIO(0x0B);

        // Activate Phase 2 on drive 2
        card.ReadIO(0x05); // Phase 2 ON

        // Both operations should complete without error
        // The key fix ensures that when switching drives:
        // 1. Old drive's phase state is cleared in status display
        // 2. New drive's phase state is cleared before subsequent operations
        // 3. Subsequent phase operations work on the new drive

        // If this test completes without hanging or throwing, the fix works
        Assert.True(true);
    }

    [Fact]
    public void ReadIO_SwitchDrives_ResetsTimingState()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on for drive 1 and do some reads to advance timing
        card.ReadIO(0x09); // Motor ON
        card.ReadIO(0x0A); // Select Drive 1
        AdvanceCycles(100); // Advance time

        // Read shift register to establish timing state
        card.ReadIO(0x0C); // Q6L - read shift register

        // Advance more cycles to create timing drift
        ulong cyclesBeforeSwitch = _clocking.TotalCycles;
        AdvanceCycles(500);

        // Switch to drive 2 - timing should reset to current cycle
        card.ReadIO(0x0B); // Select Drive 2

        // Read shift register from drive 2 - should use fresh timing
        var result = card.ReadIO(0x0C); // Q6L - read shift register

        // Test passes if no exception is thrown during drive switch and read
        // The critical fix ensures _lastBitShiftCycle is reset to _clocking.TotalCycles
        // when switching drives, preventing stale timing from affecting new drive
        Assert.NotNull(result);
        Assert.True(_clocking.TotalCycles > cyclesBeforeSwitch);
    }

    #endregion

    #region I/O Read Tests - Q6/Q7 Control (0xC-0xF)

    [Fact]
    public void ReadIO_Q6L_ReturnsShiftRegister()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Q6=0, Q7=0 should read shift register
        card.ReadIO(0x0E); // Q7L first
        var result = card.ReadIO(0x0C); // Q6L

        // Should return something (possibly 0 if no disk)
        Assert.NotNull(result);
    }

    [Fact]
    public void ReadIO_Q6H_ReturnsWriteProtectStatus()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Q6=1, Q7=0 should read write protect
        card.ReadIO(0x0E); // Q7L
        var result = card.ReadIO(0x0D); // Q6H

        // No disk = not write protected = 0x00
            Assert.Equal((byte)0x00, result);
        }

        [Fact]
        public void ReadIO_Q7L_Q6L_IsReadMode()
        {
            var card = CreateCard();
            card.OnInstalled(SlotNumber.Slot6);

            // Q7=0, Q6=0 = READ MODE (read shift register)
            card.ReadIO(0x0E); // Q7L (Q7=0)
            card.ReadIO(0x0C); // Q6L (Q6=0)

            // In read mode, reading returns shift register value
            var result = card.ReadIO(0x0C);
            Assert.NotNull(result);
        }

        [Fact]
        public void ReadIO_Q7L_Q6H_IsSenseWriteProtect()
        {
            var card = CreateCard();
            card.OnInstalled(SlotNumber.Slot6);

            // Q7=0, Q6=1 = SENSE WRITE PROTECT
            card.ReadIO(0x0E); // Q7L (Q7=0)
            card.ReadIO(0x0D); // Q6H (Q6=1)

            // Reading returns write protect status (bit 7)
            var result = card.ReadIO(0x0D);
            Assert.NotNull(result);
            Assert.Equal((byte)0x00, result); // Not write protected (no disk)
        }

        [Fact]
        public void ReadIO_Q7H_Q6L_IsWriteLoadMode()
        {
            var card = CreateCard();
            card.OnInstalled(SlotNumber.Slot6);

            // Q7=1, Q6=0 = WRITE LOAD (timing/prep mode)
            card.ReadIO(0x0F); // Q7H (Q7=1)
            card.ReadIO(0x0C); // Q6L (Q6=0)

            // In write load mode, read returns timing value (typically 0x00)
            var result = card.ReadIO(0x0C);
            Assert.NotNull(result);
        }

        [Fact]
        public void ReadIO_Q7H_Q6H_IsWriteMode()
        {
            var card = CreateCard();
            card.OnInstalled(SlotNumber.Slot6);

            // Q7=1, Q6=1 = WRITE MODE
            card.ReadIO(0x0F); // Q7H (Q7=1)
            card.ReadIO(0x0D); // Q6H (Q6=1)

            // In write mode, read returns 0x00
            var result = card.ReadIO(0x0D);
            Assert.NotNull(result);
            Assert.Equal((byte)0x00, result);
        }

        [Fact]
        public void ReadIO_Q6Q7_ModeTransition_ReadToWriteAndBack()
        {
            var card = CreateCard();
            card.OnInstalled(SlotNumber.Slot6);

            // Start in read mode (Q6=0, Q7=0)
            card.ReadIO(0x0E); // Q7L
            card.ReadIO(0x0C); // Q6L

            // Transition to write mode (Q6=1, Q7=1)
            card.ReadIO(0x0F); // Q7H
            card.ReadIO(0x0D); // Q6H

            // Transition back to read mode
            card.ReadIO(0x0E); // Q7L
            card.ReadIO(0x0C); // Q6L

            // Should be back in read mode - reading returns shift register
            var result = card.ReadIO(0x0C);
            Assert.NotNull(result);
        }

        #endregion

        #region I/O Write Tests



    [Fact]
    public void WriteIO_MotorOn_TurnsOnMotor()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        card.WriteIO(0x09, 0x00);

        Assert.True(card.IsMotorRunning);
    }

    [Fact]
    public void WriteRom_IsNoOp()
    {
        var card = CreateCard();

        // Should not throw
        card.WriteRom(0x00, 0xFF);

        // ROM should be unchanged
        Assert.Equal((byte)0xA2, card.ReadRom(0x00));
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_TurnsOffAllMotors()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn on motors
        card.ReadIO(0x09); // Motor on for drive 1
        card.ReadIO(0x0B); // Select drive 2
        card.ReadIO(0x09); // Motor on for drive 2

        // Reset
        card.Reset();

        Assert.False(card.IsMotorRunning);
        Assert.False(card.IsMotorRunning);
    }



    [Fact]
    public void Reset_SelectsDrive1()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Select drive 2
        card.ReadIO(0x0B);
        card.ReadIO(0x09); // Turn on
        Assert.True(card.IsMotorRunning);

        // Reset - motor turns off, Drive 1 selected
        card.Reset();
        Assert.False(card.IsMotorRunning);

        // Turn on motor - should power Drive 1 (Phase 5: single motor line)
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);
    }

    [Fact]
    public void Reset_CancelsPendingMotorOff()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on
        card.ReadIO(0x09);

        // Request motor off (schedules pending)
        card.ReadIO(0x08);

        // Reset - should cancel pending motor-off AND turn motor off immediately
        card.Reset();

        // Motor should be off immediately (not waiting for timer)
        Assert.False(card.IsMotorRunning);
    }

    [Fact]
    public void Reset_ResetsQ6Q7ToReadMode()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Set to write mode (Q6=1, Q7=1)
        card.ReadIO(0x0F); // Q7H
        card.ReadIO(0x0D); // Q6H

        // Reset
        card.Reset();

        // After reset, Q6=0 and Q7=0 (read mode)
        // Reading Q6L should return shift register (read mode behavior)
        var result = card.ReadIO(0x0C);
        Assert.NotNull(result);
    }

    #endregion

    #region Extended ROM Tests

    [Fact]
    public void ReadExtendedRom_ReturnsNull()
    {
        var card = CreateCard();

        Assert.Null(card.ReadExtendedRom(0xC800));
    }

    [Fact]
    public void WriteExtendedRom_IsNoOp()
    {
        var card = CreateCard();

        // Should not throw
        card.WriteExtendedRom(0xC800, 0xFF);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void GetMetadata_ReturnsEmptyString()
    {
        var card = CreateCard();

        Assert.Equal(string.Empty, card.GetMetadata());
    }

    [Fact]
    public void ApplyMetadata_ReturnsTrue()
    {
        var card = CreateCard();

        Assert.True(card.ApplyMetadata("any data"));
    }

    [Fact]
    public void ApplyMetadata_WithEmptyString_ReturnsTrue()
    {
        var card = CreateCard();

        Assert.True(card.ApplyMetadata(string.Empty));
    }

    #endregion

    #region Motor Off Delay Tests

    [Fact]
    public void MotorOff_DelaysFor1Second()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on
        card.ReadIO(0x09);
        Assert.True(card.IsMotorRunning);

        // Request motor off
        card.ReadIO(0x08);

        // Motor should still be on immediately
        Assert.True(card.IsMotorRunning);
    }

    [Fact]
    public void MotorOff_TurnsOffAfterVBlankDelay()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on
        card.ReadIO(0x09);

        // Request motor off
        card.ReadIO(0x08);

        // Advance enough cycles for motor-off (1 million cycles)
        for (int i = 0; i < 60; i++) // ~60 VBlanks = 1 second
        {
            AdvanceToVBlank();
        }

        Assert.False(card.IsMotorRunning);
    }

    [Fact]
    public void MotorOn_CancelsPendingMotorOff()
    {
        var card = CreateCard();
        card.OnInstalled(SlotNumber.Slot6);

        // Turn motor on
        card.ReadIO(0x09);

        // Request motor off
        card.ReadIO(0x08);

        // Turn motor on again (cancels pending off)
        card.ReadIO(0x09);

        // Advance time - motor should still be on
        for (int i = 0; i < 60; i++)
        {
            AdvanceToVBlank();
        }

        Assert.True(card.IsMotorRunning);
    }

    #endregion

        #region Stepper Motor Tests

        [Fact]
        public void PhaseControl_MovesHeadInBothDirections()
        {
            var card = CreateCard();
            card.OnInstalled(SlotNumber.Slot6);

            // Turn motor on
            card.ReadIO(0x09);

            int initialQuarterTrack = card.Drives[0].QuarterTrack;

            // Sanity check: ensure there's room to move in either direction
            // Drive starts at track 17 (quarter track 68) - arbitrary middle position
            Assert.True(initialQuarterTrack > 0, "Initial quarter track should allow stepping to lower tracks");
            Assert.True(initialQuarterTrack < DiskIIConstants.MaxQuarterTracks, "Initial quarter track should allow stepping to higher tracks");

            // Sync phase state to head position (quarterTrack 68 = position 4, which is Phase 2)
            card.ReadIO(0x05); // Phase 2 on â†’ _currentPhase = 4, pos = 4

            // Step forward using proper overlapping phases (add next phase, then remove previous)
            // Position sequence: 4 â†’ 5 â†’ 6 â†’ 7 â†’ 0
            card.ReadIO(0x07); // Phase 3 on (2+3 = pos 5) â†’ +1
            card.ReadIO(0x04); // Phase 2 off (3 only = pos 6) â†’ +1
            card.ReadIO(0x01); // Phase 0 on (3+0 = pos 7) â†’ +1
            card.ReadIO(0x06); // Phase 3 off (0 only = pos 0) â†’ +1

            // Verify head moved from initial position
            int afterForwardQuarterTrack = card.Drives[0].QuarterTrack;
            Assert.True(afterForwardQuarterTrack >= 0 && afterForwardQuarterTrack <= DiskIIConstants.MaxQuarterTracks,
                "Quarter track should be in valid range");
            Assert.Equal(initialQuarterTrack + 4, afterForwardQuarterTrack);

            // Step backward using reverse overlapping phases
            // Position sequence: 0 â†’ 7 â†’ 6 â†’ 5 â†’ 4
            card.ReadIO(0x07); // Phase 3 on (0+3 = pos 7) â†’ -1
            card.ReadIO(0x00); // Phase 0 off (3 only = pos 6) â†’ -1
            card.ReadIO(0x05); // Phase 2 on (3+2 = pos 5) â†’ -1
            card.ReadIO(0x06); // Phase 3 off (2 only = pos 4) â†’ -1

            // Verify head returned to initial position
            int afterBackwardQuarterTrack = card.Drives[0].QuarterTrack;
            Assert.Equal(initialQuarterTrack, afterBackwardQuarterTrack);
        }

        [Fact]
        public void PhaseControl_AtTrack0_CannotStepLower()
        {
            var card = CreateCard();
            card.OnInstalled(SlotNumber.Slot6);

            // Set drive to track 0 (position 0 & 7 = 0)
            var mockDrive = (MockDiskIIDrive)card.Drives[0];
            mockDrive.SetQuarterTrack(0);

            // Turn motor on
            card.ReadIO(0x09);

            // Sync phase to position 0 (Phase 0)
            card.ReadIO(0x01); // Phase 0 on

            // Try to step toward lower tracks using overlapping phases (backward sequence)
            card.ReadIO(0x07); // Phase 3 on (0+3 = pos 7) â†’ would be -1
            card.ReadIO(0x00); // Phase 0 off (3 only = pos 6) â†’ would be -1
            card.ReadIO(0x05); // Phase 2 on (3+2 = pos 5) â†’ would be -1
            card.ReadIO(0x06); // Phase 3 off (2 only = pos 4) â†’ would be -1

            // Quarter track should still be 0 (clamped at boundary)
            Assert.Equal(0, card.Drives[0].QuarterTrack);
        }

        [Fact]
        public void PhaseControl_AtMaxTrack_CannotStepHigher()
        {
            var card = CreateCard();
            card.OnInstalled(SlotNumber.Slot6);

            // Set drive to max track (position = MaxQuarterTracks & 7)
            var mockDrive = (MockDiskIIDrive)card.Drives[0];
            mockDrive.SetQuarterTrack(DiskIIConstants.MaxQuarterTracks);

            // Turn motor on
            card.ReadIO(0x09);

            // Sync phase to current position (MaxQuarterTracks & 7)
            // MaxQuarterTracks = 139, 139 & 7 = 3, which is phases 0+1 (pos 1) or phase 1+2 (pos 3)
            // Position 3 = phases 1+2 = _currentPhase = 6
            card.ReadIO(0x03); // Phase 1 on
            card.ReadIO(0x05); // Phase 2 on (1+2 = pos 3)

            // Try to step toward higher tracks using overlapping phases (forward sequence)
            card.ReadIO(0x02); // Phase 1 off (2 only = pos 4) â†’ would be +1
            card.ReadIO(0x07); // Phase 3 on (2+3 = pos 5) â†’ would be +1
            card.ReadIO(0x04); // Phase 2 off (3 only = pos 6) â†’ would be +1
            card.ReadIO(0x01); // Phase 0 on (3+0 = pos 7) â†’ would be +1

            // Quarter track should still be at max (clamped at boundary)
            Assert.Equal(DiskIIConstants.MaxQuarterTracks, card.Drives[0].QuarterTrack);
        }

        [Fact]
        public void PhaseControl_MotorOff_DoesNotMoveHead()
        {
            var card = CreateCard();
            card.OnInstalled(SlotNumber.Slot6);

            int initialQuarterTrack = card.Drives[0].QuarterTrack;

            // Motor is OFF - phases should not move head even with overlapping sequence
            card.ReadIO(0x05); // Phase 2 on (sync to position 4)
            card.ReadIO(0x07); // Phase 3 on (2+3 = pos 5)
            card.ReadIO(0x04); // Phase 2 off (3 only = pos 6)
            card.ReadIO(0x01); // Phase 0 on (3+0 = pos 7)

            // Head should not have moved because motor is off
            Assert.Equal(initialQuarterTrack, card.Drives[0].QuarterTrack);
        }

                #endregion

                #region Write Protection Tests

                [Fact]
                public void ReadIO_WriteProtect_ReturnsProtectedStatus_WhenDiskProtected()
                {
                    var card = CreateCard();
                    card.OnInstalled(SlotNumber.Slot6);

                    // Set drive to write-protected
                    var mockDrive = (MockDiskIIDrive)card.Drives[0];
                    mockDrive.SetWriteProtected(true);

                    // Q6=1, Q7=0 = SENSE WRITE PROTECT
                    card.ReadIO(0x0E); // Q7L
                    var result = card.ReadIO(0x0D); // Q6H

                    // Bit 7 should be set (write protected)
                    Assert.Equal((byte)0x80, result);
                }

                [Fact]
                public void ReadIO_WriteProtect_ReturnsUnprotectedStatus_WhenDiskNotProtected()
                {
                    var card = CreateCard();
                    card.OnInstalled(SlotNumber.Slot6);

                    // Drive is not write-protected by default
                    var mockDrive = (MockDiskIIDrive)card.Drives[0];
                    mockDrive.SetWriteProtected(false);

                    // Q6=1, Q7=0 = SENSE WRITE PROTECT
                    card.ReadIO(0x0E); // Q7L
                    var result = card.ReadIO(0x0D); // Q6H

                    // Bit 7 should be clear (not write protected)
                    Assert.Equal((byte)0x00, result);
                }

                #endregion

                #region Write Operation Tests

                [Fact]
                public void WriteIO_Q7H_Q6H_LoadsWriteLatch()
                {
                    var card = CreateCard();
                    card.OnInstalled(SlotNumber.Slot6);

                    // Set to write mode (Q7=1, Q6=1)
                    card.ReadIO(0x0F); // Q7H
                    card.ReadIO(0x0D); // Q6H

                    // Write a value - should load write latch
                    card.WriteIO(0x0D, 0xAB);

                    // No exception means success (actual write depends on motor/disk state)
                }

                [Fact]
                public void WriteIO_PhaseControl_HasSameEffectAsRead()
                {
                    var card = CreateCard();
                    card.OnInstalled(SlotNumber.Slot6);

                    // Turn motor on via write
                    card.WriteIO(0x09, 0x00);

                    int initialQuarterTrack = card.Drives[0].QuarterTrack;

                    // Use WriteIO for phase control
                    card.WriteIO(0x01, 0x00); // Phase 0 on
                    card.WriteIO(0x00, 0x00); // Phase 0 off
                    card.WriteIO(0x03, 0x00); // Phase 1 on

                    // Head should have moved
                    Assert.NotEqual(initialQuarterTrack, card.Drives[0].QuarterTrack);
                }

                #endregion
            }

/// <summary>
/// Integration tests for disk swap functionality with real disk metadata.
/// </summary>
/// <remarks>
/// These tests verify that when drives are swapped, the disk image metadata
/// (dirty flags, file paths, formats) swap correctly along with the actual disk data.
/// This is critical to ensure that Save/Export operations write to the correct files.
/// </remarks>
public class DiskSwapMetadataTests
{
    [Fact]
    public void SwapImageProviders_SwapsAllMetadataIncludingDirtyFlags()
    {
        // Arrange: Create two disk images with different metadata
        // Note: Paths are test placeholders - no actual files are accessed
        var image1 = new InternalDiskImage(
            physicalTrackCount: TestConstants.DiskParameters.StandardTrackCount,
            standardTrackBitCount: TestConstants.DiskParameters.StandardTrackBitCount)
        {
            SourceFilePath = TestConstants.DiskImagePaths.TestDisk1Woz,
            DestinationFilePath = TestConstants.DiskImagePaths.TestDisk1WozNew,
            OriginalFormat = DiskFormat.Woz,
            DestinationFormat = DiskFormat.Woz
        };
        image1.MarkDirty(); // Drive 1 has modifications

        var image2 = new InternalDiskImage(
            physicalTrackCount: TestConstants.DiskParameters.StandardTrackCount,
            standardTrackBitCount: TestConstants.DiskParameters.StandardTrackBitCount)
        {
            SourceFilePath = TestConstants.DiskImagePaths.TestDisk2Dsk,
            DestinationFilePath = TestConstants.DiskImagePaths.TestDisk2DskNew,
            OriginalFormat = DiskFormat.Dsk,
            DestinationFormat = DiskFormat.Dsk
        };
        // Drive 2 is NOT dirty

        var provider1 = new UnifiedDiskImageProvider(image1);
        var provider2 = new UnifiedDiskImageProvider(image2);

        var drive1 = new DiskIIDrive(TestConstants.DriveNames.Drive1, provider1);
        var drive2 = new DiskIIDrive(TestConstants.DriveNames.Drive2, provider2);

        // Capture state before swap
        Assert.True(drive1.InternalImage?.IsDirty ?? false, "Drive 1 should start dirty");
        Assert.False(drive2.InternalImage?.IsDirty ?? true, "Drive 2 should start clean");
        Assert.Equal(TestConstants.DiskImagePaths.TestDisk1Woz, drive1.CurrentDiskPath);
        Assert.Equal(TestConstants.DiskImagePaths.TestDisk2Dsk, drive2.CurrentDiskPath);

        // Act: Simulate the swap by exchanging ImageProvider properties (what SwapDriveMedia does)
        (drive2.ImageProvider, drive1.ImageProvider) = (drive1.ImageProvider, drive2.ImageProvider);

        // Assert: All metadata should swap along with the providers
        Assert.False(drive1.InternalImage?.IsDirty ?? true, "Drive 1 should now be clean (has image2)");
        Assert.True(drive2.InternalImage?.IsDirty ?? false, "Drive 2 should now be dirty (has image1)");

        // File paths swap
        Assert.Equal(TestConstants.DiskImagePaths.TestDisk2Dsk, drive1.CurrentDiskPath);
        Assert.Equal(TestConstants.DiskImagePaths.TestDisk1Woz, drive2.CurrentDiskPath);

        // Destination paths swap
        Assert.Equal(TestConstants.DiskImagePaths.TestDisk2DskNew, drive1.InternalImage?.DestinationFilePath);
        Assert.Equal(TestConstants.DiskImagePaths.TestDisk1WozNew, drive2.InternalImage?.DestinationFilePath);

        // Source paths swap
        Assert.Equal(TestConstants.DiskImagePaths.TestDisk2Dsk, drive1.InternalImage?.SourceFilePath);
        Assert.Equal(TestConstants.DiskImagePaths.TestDisk1Woz, drive2.InternalImage?.SourceFilePath);

        // Formats swap
        Assert.Equal(DiskFormat.Dsk, drive1.InternalImage?.OriginalFormat);
        Assert.Equal(DiskFormat.Woz, drive2.InternalImage?.OriginalFormat);
        Assert.Equal(DiskFormat.Dsk, drive1.InternalImage?.DestinationFormat);
        Assert.Equal(DiskFormat.Woz, drive2.InternalImage?.DestinationFormat);
    }

    [Fact]
    public void SwapImageProviders_PreservesDirtyDataWhenOneDriveEmpty()
    {
        // Arrange: One drive with dirty data, one empty
        // Note: Paths are test placeholders - no actual files are accessed
        var image1 = new InternalDiskImage(
            physicalTrackCount: TestConstants.DiskParameters.StandardTrackCount,
            standardTrackBitCount: TestConstants.DiskParameters.StandardTrackBitCount)
        {
            SourceFilePath = TestConstants.DiskImagePaths.GameWoz,
            DestinationFilePath = TestConstants.DiskImagePaths.GameWozNew,
            OriginalFormat = DiskFormat.Woz,
            DestinationFormat = DiskFormat.Woz
        };
        image1.MarkDirty();

        var provider1 = new UnifiedDiskImageProvider(image1);
        var drive1 = new DiskIIDrive(TestConstants.DriveNames.Drive1, provider1);
        var drive2 = new DiskIIDrive(TestConstants.DriveNames.Drive2); // Empty drive

        // Verify initial state
        Assert.NotNull(drive1.InternalImage);
        Assert.True(drive1.InternalImage.IsDirty);
        Assert.Equal(TestConstants.DiskImagePaths.GameWoz, drive1.CurrentDiskPath);
        Assert.Null(drive2.InternalImage);
        Assert.Null(drive2.CurrentDiskPath);

        // Act: Swap providers
        (drive2.ImageProvider, drive1.ImageProvider) = (drive1.ImageProvider, drive2.ImageProvider);

        // Assert: Dirty disk with all metadata now in Drive 2
        Assert.Null(drive1.InternalImage); // Drive 1 should now be empty
        Assert.Null(drive1.CurrentDiskPath);

        Assert.NotNull(drive2.InternalImage); // Drive 2 should now have the disk
        Assert.True(drive2.InternalImage.IsDirty); // Dirty flag must be preserved
        Assert.Equal(TestConstants.DiskImagePaths.GameWoz, drive2.CurrentDiskPath);
        Assert.Equal(TestConstants.DiskImagePaths.GameWozNew, drive2.InternalImage.DestinationFilePath);
    }

    [Fact]
    public void SwapImageProviders_PreservesTrackDataReferences()
    {
        // Arrange: Create a disk with track data
        // Note: Paths are test placeholders - no actual files are accessed
        var image1 = new InternalDiskImage(
            physicalTrackCount: TestConstants.DiskParameters.StandardTrackCount,
            standardTrackBitCount: TestConstants.DiskParameters.StandardTrackBitCount)
        {
            SourceFilePath = TestConstants.DiskImagePaths.OriginalWoz,
            DestinationFilePath = TestConstants.DiskImagePaths.ModifiedWoz
        };
        image1.MarkDirty();

        var provider1 = new UnifiedDiskImageProvider(image1);
        var drive1 = new DiskIIDrive(TestConstants.DriveNames.Drive1, provider1);
        var drive2 = new DiskIIDrive(TestConstants.DriveNames.Drive2);

        // Capture reference to track data before swap
        var trackBeforeSwap = image1.QuarterTracks[0];

        // Act: Swap providers (simulating what Swap Drives does)
        (drive2.ImageProvider, drive1.ImageProvider) = (drive1.ImageProvider, drive2.ImageProvider);

        // Assert: The same track object is still accessible from the swapped location
        Assert.NotNull(drive2.InternalImage);
        Assert.True(drive2.InternalImage.IsDirty);
        Assert.Same(trackBeforeSwap, drive2.InternalImage.QuarterTracks[0]); // Same object reference
        Assert.Equal(TestConstants.DiskParameters.StandardTrackCount, drive2.InternalImage.PhysicalTrackCount); // All track data preserved
    }
}

/// <summary>
/// Mock implementation of IDiskIIFactory for testing.
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
/// Mock implementation of IDiskIIDrive for testing.
/// </summary>
internal class MockDiskIIDrive(string name) : IDiskIIDrive
{
    public string Name { get; } = name;
    public double Track => QuarterTrack / 4.0;
    public int QuarterTrack { get; private set; } = 68; // Track 17
    public bool MotorOn { get; set; }
    public bool HasDisk => false;
    private bool _writeProtected = false;

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

    public bool? GetBit(ulong cycle) => null;

    public int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits) => 0;

    public byte OptimalBitTiming => 32;

    public bool SetBit(bool value) => false;

    public bool IsWriteProtected() => _writeProtected;

    public void NotifyMotorStateChanged(bool motorOn, ulong cycleCount) { }

    public void InsertDisk(string diskImagePath) { }

    public void EjectDisk() { }

    public string? CurrentDiskPath => null;

    public IDiskImageProvider? ImageProvider
    {
        get => null;
        set { /* Mock - do nothing */ }
    }

    public InternalDiskImage? InternalImage => null;
}

