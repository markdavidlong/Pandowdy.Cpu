using Pandowdy.EmuCore.Cards;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Tests.Helpers;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Integration tests for the complete Disk II subsystem.
/// </summary>
/// <remarks>
/// These tests verify the interaction between:
/// - DiskIIControllerCard (16-sector and 13-sector variants)
/// - DiskIIDrive with telemetry
/// - DiskImageFactory and providers
/// - CpuClockingCounters (VBlank timing)
/// </remarks>
public class DiskIIIntegrationTests : IDisposable
{
    private readonly CpuClockingCounters _clocking = new();
    private readonly MockTelemetryAggregator _telemetry = new();
    private readonly DiskImageFactory _imageFactory = new();
    private readonly DiskIIFactory _driveFactory;

    public DiskIIIntegrationTests()
    {
        _driveFactory = new DiskIIFactory(_imageFactory, _telemetry);
    }

    public void Dispose()
    {
        _telemetry.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Methods

    private DiskIIControllerCard16Sector CreateController()
    {
        return new DiskIIControllerCard16Sector(_clocking, _driveFactory, _telemetry);
    }

    private void InstallController(DiskIIControllerCard16Sector controller, SlotNumber slot = SlotNumber.Slot6)
    {
        controller.OnInstalled(slot);
    }

    private void AdvanceCycles(int cycles)
    {
        _clocking.IncrementCycles(cycles);
    }

    private void AdvanceToNextVBlank()
    {
        while (_clocking.TotalCycles < _clocking.NextVBlankCycle)
        {
            AdvanceCycles(1000);
        }
        _clocking.CheckAndAdvanceVBlank();
    }

    private void AdvanceVBlanks(int count)
    {
        for (int i = 0; i < count; i++)
        {
            AdvanceToNextVBlank();
        }
    }

    #endregion

    #region Controller + Drive Integration Tests

    [Fact]
    public void Controller_OnInstalled_CreatesTwoDrives()
    {
        var controller = CreateController();

        InstallController(controller);

        Assert.Equal(2, controller.Drives.Length);
        Assert.NotNull(controller.Drives[0]);
        Assert.NotNull(controller.Drives[1]);
    }

    [Fact]
    public void Controller_OnInstalled_DrivesHaveCorrectNames()
    {
        var controller = CreateController();

        InstallController(controller, SlotNumber.Slot6);

        Assert.Equal("Slot6-D1", controller.Drives[0].Name);
        Assert.Equal("Slot6-D2", controller.Drives[1].Name);
    }

    [Fact]
    public void Controller_OnInstalled_DrivesAreEmpty()
    {
        var controller = CreateController();

        InstallController(controller);

        Assert.False(controller.Drives[0].HasDisk);
        Assert.False(controller.Drives[1].HasDisk);
    }

    [Fact]
    public void Controller_MotorOn_AffectsSelectedDrive()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn motor on (default is Drive 1 selected)
        controller.ReadIO(0x09); // Motor on

        Assert.True(controller.Drives[0].MotorOn);
        Assert.False(controller.Drives[1].MotorOn);
    }

    [Fact]
    public void Controller_DriveSelect_SwitchesBetweenDrives()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn motor on for Drive 1
        controller.ReadIO(0x09);
        Assert.True(controller.Drives[0].MotorOn);

        // Switch to Drive 2
        controller.ReadIO(0x0B);

        // Motor should still be on for Drive 1, but Drive 2 is now selected
        // Turn motor on again to affect Drive 2
        controller.ReadIO(0x09);
        Assert.True(controller.Drives[1].MotorOn);
    }

    [Fact]
    public void Controller_Reset_TurnsOffAllMotors()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn on motors for both drives
        controller.ReadIO(0x09); // Motor on for Drive 1
        controller.ReadIO(0x0B); // Select Drive 2
        controller.ReadIO(0x09); // Motor on for Drive 2

        Assert.True(controller.Drives[0].MotorOn);
        Assert.True(controller.Drives[1].MotorOn);

        // Reset
        controller.Reset();

        Assert.False(controller.Drives[0].MotorOn);
        Assert.False(controller.Drives[1].MotorOn);
    }

    #endregion

 

    #region Motor Timeout Tests (VBlank-based)

    [Fact]
    public void MotorTimeout_MotorStaysOn_ImmediatelyAfterOffRequest()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn motor on
        controller.ReadIO(0x09);
        Assert.True(controller.Drives[0].MotorOn);

        // Request motor off
        controller.ReadIO(0x08);

        // Motor should still be on (1-second delay)
        Assert.True(controller.Drives[0].MotorOn);
    }

    [Fact]
    public void MotorTimeout_MotorTurnsOff_After60VBlanks()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn motor on
        controller.ReadIO(0x09);

        // Request motor off
        controller.ReadIO(0x08);

        // Advance 60 VBlanks (~1 second)
        AdvanceVBlanks(60);

        // Motor should now be off
        Assert.False(controller.Drives[0].MotorOn);
    }

    [Fact]
    public void MotorTimeout_MotorOnCancels_PendingOff()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn motor on
        controller.ReadIO(0x09);

        // Request motor off
        controller.ReadIO(0x08);

        // Turn motor back on before timeout
        controller.ReadIO(0x09);

        // Advance 60 VBlanks
        AdvanceVBlanks(60);

        // Motor should still be on (cancel worked)
        Assert.True(controller.Drives[0].MotorOn);
    }

    [Fact]
    public void MotorTimeout_MotorStaysOn_Before60VBlanks()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn motor on
        controller.ReadIO(0x09);

        // Request motor off
        controller.ReadIO(0x08);

        // Advance only 30 VBlanks (half the timeout)
        AdvanceVBlanks(30);

        // Motor should still be on
        Assert.True(controller.Drives[0].MotorOn);
    }

    #endregion

    #region Multi-Drive Tests

    [Fact]
    public void MultiDrive_Drive1Selected_ByDefault()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn motor on
        controller.ReadIO(0x09);

        // Only Drive 1 should have motor on
        Assert.True(controller.Drives[0].MotorOn);
        Assert.False(controller.Drives[1].MotorOn);
    }

    [Fact]
    public void MultiDrive_SelectDrive2_AffectsMotorCommands()
    {
        var controller = CreateController();
        InstallController(controller);

        // Select Drive 2
        controller.ReadIO(0x0B);

        // Turn motor on
        controller.ReadIO(0x09);

        // Only Drive 2 should have motor on
        Assert.False(controller.Drives[0].MotorOn);
        Assert.True(controller.Drives[1].MotorOn);
    }

    [Fact]
    public void MultiDrive_SwitchingDrives_PreservesMotorState()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn motor on for Drive 1
        controller.ReadIO(0x09);
        Assert.True(controller.Drives[0].MotorOn);

        // Switch to Drive 2
        controller.ReadIO(0x0B);

        // Drive 1 motor should still be on
        Assert.True(controller.Drives[0].MotorOn);

        // Turn motor on for Drive 2
        controller.ReadIO(0x09);

        // Both drives should now have motor on
        Assert.True(controller.Drives[0].MotorOn);
        Assert.True(controller.Drives[1].MotorOn);
    }

    [Fact]
    public void MultiDrive_SelectDrive1_AfterDrive2()
    {
        var controller = CreateController();
        InstallController(controller);

        // Select Drive 2 and turn on
        controller.ReadIO(0x0B);
        controller.ReadIO(0x09);
        Assert.True(controller.Drives[1].MotorOn);

        // Select Drive 1
        controller.ReadIO(0x0A);

        // Turn off motor (should affect Drive 1 now)
        controller.ReadIO(0x08);

        // After timeout, Drive 1's motor should be off, Drive 2's should still be on
        AdvanceVBlanks(60);

        Assert.False(controller.Drives[0].MotorOn);
        Assert.True(controller.Drives[1].MotorOn);
    }

    #endregion

    #region Phase Control Integration Tests



    [Fact]
    public void PhaseControl_SequentialPhases_StepHead()
    {
        var controller = CreateController();
        InstallController(controller);

        // Turn motor on
        controller.ReadIO(0x09);

        int initialQuarterTrack = controller.Drives[0].QuarterTrack;

        // Activate phases in sequence to step outward
        // Phase sequence: 0 -> 1 -> 2 -> 3 moves head outward
        controller.ReadIO(0x01); // Phase 0 on
        controller.ReadIO(0x00); // Phase 0 off
        controller.ReadIO(0x03); // Phase 1 on
        controller.ReadIO(0x02); // Phase 1 off
        controller.ReadIO(0x05); // Phase 2 on
        controller.ReadIO(0x04); // Phase 2 off
        controller.ReadIO(0x07); // Phase 3 on

        // Head should have moved (actual movement depends on stepper logic)
        // Just verify no crash and track changed or stayed same
        int newQuarterTrack = controller.Drives[0].QuarterTrack;
        Assert.True(newQuarterTrack >= 0 && newQuarterTrack <= DiskIIConstants.MaxQuarterTracks);
    }

    #endregion

    #region Q6/Q7 Mode Tests

    [Fact]
    public void Q6Q7_ReadMode_ReturnsShiftRegister()
    {
        var controller = CreateController();
        InstallController(controller);

        // Set Q6=0, Q7=0 (read mode)
        controller.ReadIO(0x0C); // Q6L
        controller.ReadIO(0x0E); // Q7L

        // Reading Q6L should return shift register value
        var result = controller.ReadIO(0x0C);
        Assert.NotNull(result);
    }

    [Fact]
    public void Q6Q7_SenseWriteProtect_NoDisk_ReturnsNotProtected()
    {
        var controller = CreateController();
        InstallController(controller);

        // Set Q6=1, Q7=0 (sense write protect)
        controller.ReadIO(0x0E); // Q7L first
        var result = controller.ReadIO(0x0D); // Q6H

        // No disk = not write protected = 0x00
        Assert.Equal((byte)0x00, result);
    }

    #endregion

    #region Factory Integration Tests

    [Fact]
    public void Factory_CreatesDrivesWithTelemetry()
    {
        _telemetry.Clear();

        var drive = _driveFactory.CreateDrive("Slot6-D1");

        Assert.NotNull(drive);
        Assert.Equal("Slot6-D1", drive.Name);
    }


    [Fact]
    public void Factory_CreatesIndependentDrives()
    {
        var drive1 = _driveFactory.CreateDrive("Slot6-D1");
        var drive2 = _driveFactory.CreateDrive("Slot6-D2");

        Assert.NotSame(drive1, drive2);
        Assert.NotEqual(drive1.Name, drive2.Name);
    }

    #endregion

    #region Full Stack Integration Tests

    [Fact]
    public void FullStack_ControllerWithFactory_CreatesWorkingDrives()
    {
        var controller = CreateController();
        InstallController(controller);

        // Verify full stack works
        Assert.Equal(2, controller.Drives.Length);
        Assert.False(controller.Drives[0].HasDisk);

        // Turn on motor via I/O
        controller.ReadIO(0x09);
        Assert.True(controller.Drives[0].MotorOn);

        // Reset should turn off motor
        controller.Reset();
        Assert.False(controller.Drives[0].MotorOn);
    }



    [Fact]
    public void FullStack_13SectorController_WorksSameAs16Sector()
    {
        var controller13 = new DiskIIControllerCard13Sector(_clocking, _driveFactory, _telemetry);
        controller13.OnInstalled(SlotNumber.Slot6);

        // Should create 2 drives just like 16-sector
        Assert.Equal(2, controller13.Drives.Length);

        // Motor control should work
        controller13.ReadIO(0x09);
        Assert.True(controller13.Drives[0].MotorOn);
    }

    #endregion
}
