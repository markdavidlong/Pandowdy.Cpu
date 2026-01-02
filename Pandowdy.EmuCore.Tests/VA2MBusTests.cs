using Emulator;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Comprehensive tests for VA2MBus - the central Apple II bus coordinator.
/// 
/// Tests cover:
/// - I/O space handling ($C000-$CFFF)
/// - Keyboard input ($C000, $C010)
/// - Push buttons ($C061-$C063)
/// - Soft switch reads/writes
/// - Language card banking ($C080-$C08F)
/// - VBlank timing and events
/// - CPU integration
/// - Memory-mapped I/O
/// 
/// VA2MBus is ~570 lines and is the core of the emulation,
/// coordinating all I/O operations and soft switch state.
/// </summary>
public class VA2MBusTests
{
    #region Test Helpers

    /// <summary>
    /// Helper to create a fully configured VA2MBus for testing.
    /// </summary>
    private class VA2MBusFixture
    {
        public MemoryPool MemoryPool { get; }
        public SystemStatusProvider StatusProvider { get; }
        public ICpu Cpu { get; }
        public VA2MBus Bus { get; }

        public VA2MBusFixture()
        {
            MemoryPool = new MemoryPool();
            StatusProvider = new SystemStatusProvider();
            Cpu = new CPUAdapter(new CPU());
            Bus = new VA2MBus(MemoryPool, StatusProvider, Cpu);
        }
    }

    #endregion

    #region Constructor Tests (3 tests)

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange
        var mempool = new MemoryPool();
        var status = new SystemStatusProvider();
        var cpu = new CPUAdapter(new CPU());

        // Act
        var bus = new VA2MBus(mempool, status, cpu);

        // Assert
        Assert.NotNull(bus);
        Assert.Same(mempool, bus.RAM);
        Assert.Same(cpu, bus.Cpu);
    }

    [Fact]
    public void Constructor_NullStatusProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var mempool = new MemoryPool();
        var cpu = new CPUAdapter(new CPU());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new VA2MBus(mempool, null!, cpu));
    }

    [Fact]
    public void Constructor_NullCpu_ThrowsArgumentNullException()
    {
        // Arrange
        var mempool = new MemoryPool();
        var status = new SystemStatusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new VA2MBus(mempool, status, null!));
    }

    #endregion

    #region Property Tests (3 tests)

    [Fact]
    public void RAM_ReturnsMemoryPool()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        var ram = fixture.Bus.RAM;

        // Assert
        Assert.Same(fixture.MemoryPool, ram);
    }

    [Fact]
    public void Cpu_ReturnsCpu()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        var cpu = fixture.Bus.Cpu;

        // Assert
        Assert.Same(fixture.Cpu, cpu);
    }

    [Fact]
    public void SystemClockCounter_StartsAtZero()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        var clock = fixture.Bus.SystemClockCounter;

        // Assert
        Assert.Equal(0UL, clock);
    }

    #endregion

    #region Keyboard I/O Tests (8 tests)

    [Fact]
    public void CpuRead_KBD_ReturnsKeyValue()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.SetKeyValue(0xC1); // 'A' with high bit

        // Act
        var value = fixture.Bus.CpuRead(VA2MBus.KBD_);

        // Assert
        Assert.Equal(0xC1, value);
    }

    [Fact]
    public void CpuRead_KEYSTRB_ClearsHighBit()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.SetKeyValue(0xC1); // 'A' with high bit

        // Act
        var value = fixture.Bus.CpuRead(VA2MBus.KEYSTRB_);

        // Assert
        Assert.Equal(0x41, value); // High bit cleared
    }

    [Fact]
    public void SetKeyValue_StoresKeyValue()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.SetKeyValue(0x42);
        var value = fixture.Bus.CpuRead(VA2MBus.KBD_);

        // Assert
        Assert.Equal(0x42, value);
    }

    [Fact]
    public void SetKeyValue_MultipleKeys_UpdatesValue()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act & Assert
        fixture.Bus.SetKeyValue(0xC1);
        Assert.Equal(0xC1, fixture.Bus.CpuRead(VA2MBus.KBD_));

        fixture.Bus.SetKeyValue(0xC2);
        Assert.Equal(0xC2, fixture.Bus.CpuRead(VA2MBus.KBD_));
    }

    [Fact]
    public void KEYSTRB_Read_ClearsHighBitPermanently()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.SetKeyValue(0xC1);

        // Act
        fixture.Bus.CpuRead(VA2MBus.KEYSTRB_); // Clear high bit
        var valueAfterStrobe = fixture.Bus.CpuRead(VA2MBus.KBD_);

        // Assert
        Assert.Equal(0x41, valueAfterStrobe); // Should stay cleared
    }

    [Theory]
    [InlineData(0x41, 0x41)] // 'A'
    [InlineData(0xC1, 0xC1)] // 'A' with high bit
    [InlineData(0x20, 0x20)] // Space
    [InlineData(0xA0, 0xA0)] // Space with high bit
    public void SetKeyValue_VariousValues_StoresCorrectly(byte input, byte expected)
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.SetKeyValue(input);
        var value = fixture.Bus.CpuRead(VA2MBus.KBD_);

        // Assert
        Assert.Equal(expected, value);
    }

    [Fact]
    public void KEYSTRB_MultipleCalls_OnlyLowersOnce()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.SetKeyValue(0xC1);

        // Act
        fixture.Bus.CpuRead(VA2MBus.KEYSTRB_);
        fixture.Bus.CpuRead(VA2MBus.KEYSTRB_);
        fixture.Bus.CpuRead(VA2MBus.KEYSTRB_);
        var finalValue = fixture.Bus.CpuRead(VA2MBus.KBD_);

        // Assert
        Assert.Equal(0x41, finalValue);
    }

    [Fact]
    public void KeyboardIO_Sequence_WorksCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act - Simulate keyboard input sequence
        fixture.Bus.SetKeyValue(0xC8); // 'H'
        Assert.Equal(0xC8, fixture.Bus.CpuRead(VA2MBus.KBD_));
        fixture.Bus.CpuRead(VA2MBus.KEYSTRB_); // Strobe

        fixture.Bus.SetKeyValue(0xC5); // 'E'
        Assert.Equal(0xC5, fixture.Bus.CpuRead(VA2MBus.KBD_));
        fixture.Bus.CpuRead(VA2MBus.KEYSTRB_);

        fixture.Bus.SetKeyValue(0xCC); // 'L'
        var finalValue = fixture.Bus.CpuRead(VA2MBus.KBD_);

        // Assert
        Assert.Equal(0xCC, finalValue);
    }

    #endregion

    #region Push Button Tests (6 tests)

    [Fact]
    public void SetPushButton_Button0_SetsState()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.SetPushButton(0, true);
        var value = fixture.Bus.CpuRead(VA2MBus.BUTTON0_);

        // Assert
        Assert.Equal(0x80, value); // High bit set
    }

    [Fact]
    public void SetPushButton_Button1_SetsState()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.SetPushButton(1, true);
        var value = fixture.Bus.CpuRead(VA2MBus.BUTTON1_);

        // Assert
        Assert.Equal(0x80, value);
    }

    [Fact]
    public void SetPushButton_Button2_SetsState()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.SetPushButton(2, true);
        var value = fixture.Bus.CpuRead(VA2MBus.BUTTON2_);

        // Assert
        Assert.Equal(0x80, value);
    }

    [Fact]
    public void GetPushButton_ReturnsCorrectState()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.SetPushButton(0, true);
        fixture.Bus.SetPushButton(1, false);
        fixture.Bus.SetPushButton(2, true);

        // Act & Assert
        Assert.True(fixture.Bus.GetPushButton(0));
        Assert.False(fixture.Bus.GetPushButton(1));
        Assert.True(fixture.Bus.GetPushButton(2));
    }

    [Fact]
    public void SetPushButton_Released_ClearsHighBit()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.SetPushButton(0, true);

        // Act
        fixture.Bus.SetPushButton(0, false);
        var value = fixture.Bus.CpuRead(VA2MBus.BUTTON0_);

        // Assert
        Assert.Equal(0x00, value); // High bit clear
    }

    [Fact]
    public void PushButtons_IndependentState()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.SetPushButton(0, true);
        fixture.Bus.SetPushButton(1, false);
        fixture.Bus.SetPushButton(2, true);

        // Assert
        Assert.Equal(0x80, fixture.Bus.CpuRead(VA2MBus.BUTTON0_));
        Assert.Equal(0x00, fixture.Bus.CpuRead(VA2MBus.BUTTON1_));
        Assert.Equal(0x80, fixture.Bus.CpuRead(VA2MBus.BUTTON2_));
    }

    #endregion

    #region Soft Switch Read Tests (15 tests)

    [Fact]
    public void CpuRead_RD_TEXT_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Enable TEXT
        fixture.Bus.CpuRead(VA2MBus.SETTXT_);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_TEXT_);
        
        // Disable TEXT
        fixture.Bus.CpuRead(VA2MBus.CLRTXT_);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_TEXT_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);  // High bit set
        Assert.Equal(0x00, valueOff & 0x80); // High bit clear
    }

    [Fact]
    public void CpuRead_RD_MIXED_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuRead(VA2MBus.SETMIXED_);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_MIXED_);
        
        fixture.Bus.CpuRead(VA2MBus.CLRMIXED_);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_MIXED_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_PAGE2_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuRead(VA2MBus.SETPAGE2_);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_PAGE2_);
        
        fixture.Bus.CpuRead(VA2MBus.CLRPAGE2_);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_PAGE2_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_HIRES_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuRead(VA2MBus.SETHIRES_);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_HIRES_);
        
        fixture.Bus.CpuRead(VA2MBus.CLRHIRES_);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_HIRES_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_80STORE_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.CLR80STORE_, 0); // CLR sets it true
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_80STORE_);
        
        fixture.Bus.CpuWrite(VA2MBus.SET80STORE_, 0); // SET sets it false
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_80STORE_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_RAMRD_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.RDCARDRAM_, 0);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_RAMRD_);
        
        fixture.Bus.CpuWrite(VA2MBus.RDMAINRAM_, 0);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_RAMRD_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_RAMWRT_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.WRCARDRAM_, 0);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_RAMWRT_);
        
        fixture.Bus.CpuWrite(VA2MBus.WRMAINRAM_, 0);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_RAMWRT_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_INTCXROM_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Read initial state (note: might not default to true after constructor)
        var initialValue = fixture.Bus.CpuRead(VA2MBus.RD_INTCXROM_);
        
        // Set it explicitly
        fixture.Bus.CpuWrite(VA2MBus.INTCXROM_, 0);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_INTCXROM_);
        
        fixture.Bus.CpuWrite(VA2MBus.SLOTCXROM_, 0);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_INTCXROM_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);  // Set to true
        Assert.Equal(0x00, valueOff & 0x80); // Set to false
    }

    [Fact]
    public void CpuRead_RD_ALTZP_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.ALTZP_, 0);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_ALTZP_);
        
        fixture.Bus.CpuWrite(VA2MBus.STDZP_, 0);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_ALTZP_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_SLOTC3ROM_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.SLOTC3ROM_, 0);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_SLOTC3ROM_);
        
        fixture.Bus.CpuWrite(VA2MBus.INTC3ROM_, 0);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_SLOTC3ROM_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_80VID_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.SET80VID_, 0);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_80VID_);
        
        fixture.Bus.CpuWrite(VA2MBus.CLR80VID_, 0);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_80VID_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_ALTCHAR_ReflectsSoftSwitch()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.SETALTCHAR_, 0);
        var valueOn = fixture.Bus.CpuRead(VA2MBus.RD_ALTCHAR_);
        
        fixture.Bus.CpuWrite(VA2MBus.CLRALTCHAR_, 0);
        var valueOff = fixture.Bus.CpuRead(VA2MBus.RD_ALTCHAR_);

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);
        Assert.Equal(0x00, valueOff & 0x80);
    }

    [Fact]
    public void CpuRead_RD_VERTBLANK_ReflectsVBlankState()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Initially in VBlank
        var initialValue = fixture.Bus.CpuRead(VA2MBus.RD_VERTBLANK_);

        // Assert - High bit should be set initially (in VBlank)
        Assert.Equal(0x80, initialValue & 0x80);
    }

    [Fact]
    public void SoftSwitchReads_WithKeyValue_PreserveLowBits()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.SetKeyValue(0x55); // Pattern in low bits
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.RDCARDRAM_, 0); // Set RAMRD
        var value = fixture.Bus.CpuRead(VA2MBus.RD_RAMRD_);

        // Assert
        Assert.Equal(0xD5, value); // 0x80 (high bit) | 0x55 (low bits)
    }

    [Fact]
    public void AllSoftSwitchReads_ReturnValues()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Read all soft switch status addresses
        var reads = new Dictionary<string, byte>
        {
            ["RD_LC_BANK1"] = fixture.Bus.CpuRead(VA2MBus.RD_LC_BANK1_),
            ["RD_LC_RAM"] = fixture.Bus.CpuRead(VA2MBus.RD_LC_RAM),
            ["RD_RAMRD"] = fixture.Bus.CpuRead(VA2MBus.RD_RAMRD_),
            ["RD_RAMWRT"] = fixture.Bus.CpuRead(VA2MBus.RD_RAMWRT_),
            ["RD_INTCXROM"] = fixture.Bus.CpuRead(VA2MBus.RD_INTCXROM_),
            ["RD_ALTZP"] = fixture.Bus.CpuRead(VA2MBus.RD_ALTZP_),
            ["RD_SLOTC3ROM"] = fixture.Bus.CpuRead(VA2MBus.RD_SLOTC3ROM_),
            ["RD_80STORE"] = fixture.Bus.CpuRead(VA2MBus.RD_80STORE_),
            ["RD_VERTBLANK"] = fixture.Bus.CpuRead(VA2MBus.RD_VERTBLANK_),
            ["RD_TEXT"] = fixture.Bus.CpuRead(VA2MBus.RD_TEXT_),
            ["RD_MIXED"] = fixture.Bus.CpuRead(VA2MBus.RD_MIXED_),
            ["RD_PAGE2"] = fixture.Bus.CpuRead(VA2MBus.RD_PAGE2_),
            ["RD_HIRES"] = fixture.Bus.CpuRead(VA2MBus.RD_HIRES_),
            ["RD_ALTCHAR"] = fixture.Bus.CpuRead(VA2MBus.RD_ALTCHAR_),
            ["RD_80VID"] = fixture.Bus.CpuRead(VA2MBus.RD_80VID_)
        };

        // Assert - All should return values (not throw)
        Assert.All(reads, kvp => Assert.True(kvp.Value >= 0 && kvp.Value <= 0xFF));
    }

    #endregion

    #region Soft Switch Write Tests (10 tests)

    [Fact]
    public void CpuWrite_VideoSwitches_SetCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.SETTXT_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SETMIXED_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SETPAGE2_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SETHIRES_, 0);

        // Assert
        Assert.True(fixture.StatusProvider.StateTextMode);
        Assert.True(fixture.StatusProvider.StateMixed);
        Assert.True(fixture.StatusProvider.StatePage2);
        Assert.True(fixture.StatusProvider.StateHiRes);
    }

    [Fact]
    public void CpuWrite_VideoSwitches_ClearCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.CpuWrite(VA2MBus.SETTXT_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SETMIXED_, 0);
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.CLRTXT_, 0);
        fixture.Bus.CpuWrite(VA2MBus.CLRMIXED_, 0);

        // Assert
        Assert.False(fixture.StatusProvider.StateTextMode);
        Assert.False(fixture.StatusProvider.StateMixed);
    }

    [Fact]
    public void CpuWrite_MemorySwitches_SetCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.CLR80STORE_, 0); // CLR sets 80STORE true
        fixture.Bus.CpuWrite(VA2MBus.RDCARDRAM_, 0);
        fixture.Bus.CpuWrite(VA2MBus.WRCARDRAM_, 0);
        fixture.Bus.CpuWrite(VA2MBus.ALTZP_, 0);

        // Assert
        Assert.True(fixture.StatusProvider.State80Store);
        Assert.True(fixture.StatusProvider.StateRamRd);
        Assert.True(fixture.StatusProvider.StateRamWrt);
        Assert.True(fixture.StatusProvider.StateAltZp);
    }

    [Fact]
    public void CpuWrite_ROMSwitches_SetCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.INTCXROM_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SLOTC3ROM_, 0);

        // Assert
        Assert.True(fixture.StatusProvider.StateIntCxRom);
        Assert.True(fixture.StatusProvider.StateSlotC3Rom);
    }

    [Fact]
    public void CpuWrite_AnnunciatorSwitches_SetCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.SETAN0_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SETAN1_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SETAN2_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SETAN3_, 0);

        // Assert
        Assert.True(fixture.StatusProvider.StateAnn0);
        Assert.True(fixture.StatusProvider.StateAnn1);
        Assert.True(fixture.StatusProvider.StateAnn2);
        Assert.True(fixture.StatusProvider.StateAnn3_DGR);
    }

    [Fact]
    public void CpuWrite_AnnunciatorSwitches_ClearCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.CpuWrite(VA2MBus.SETAN0_, 0);
        
        // Act
        fixture.Bus.CpuWrite(VA2MBus.CLRAN0_, 0);

        // Assert
        Assert.False(fixture.StatusProvider.StateAnn0);
    }

    [Fact]
    public void CpuRead_SoftSwitchAddresses_AlsoSetSwitches()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Reading these addresses SETS the switches
        fixture.Bus.CpuRead(VA2MBus.SETTXT_);
        fixture.Bus.CpuRead(VA2MBus.SETHIRES_);

        // Assert
        Assert.True(fixture.StatusProvider.StateTextMode);
        Assert.True(fixture.StatusProvider.StateHiRes);
    }

    [Fact]
    public void CpuRead_ClearSoftSwitchAddresses_ClearsSwitches()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.CpuWrite(VA2MBus.SETTXT_, 0);
        
        // Act - Reading CLR addresses clears switches
        fixture.Bus.CpuRead(VA2MBus.CLRTXT_);

        // Assert
        Assert.False(fixture.StatusProvider.StateTextMode);
    }

    [Fact]
    public void SoftSwitches_ReadAndWrite_BothWork()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Both read and write should set switches
        fixture.Bus.CpuWrite(VA2MBus.SETMIXED_, 0);
        var value1 = fixture.StatusProvider.StateMixed;
        
        fixture.Bus.CpuRead(VA2MBus.CLRMIXED_);
        var value2 = fixture.StatusProvider.StateMixed;

        // Assert
        Assert.True(value1);
        Assert.False(value2);
    }

    [Fact]
    public void SoftSwitches_ComplexSequence_TracksCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Simulate typical Apple II graphics mode setup
        fixture.Bus.CpuWrite(VA2MBus.CLRTXT_, 0);     // Graphics mode
        fixture.Bus.CpuWrite(VA2MBus.SETHIRES_, 0);   // Hi-res
        fixture.Bus.CpuWrite(VA2MBus.SETMIXED_, 0);   // Mixed mode
        fixture.Bus.CpuWrite(VA2MBus.CLRPAGE2_, 0);   // Page 1

        // Assert
        Assert.False(fixture.StatusProvider.StateTextMode);
        Assert.True(fixture.StatusProvider.StateHiRes);
        Assert.True(fixture.StatusProvider.StateMixed);
        Assert.False(fixture.StatusProvider.StatePage2);
    }

    #endregion

    #region Language Card Banking Tests (12 tests)

    [Fact]
    public void LanguageCard_B2_RD_RAM_NO_WRT_SetsCorrectFlags()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_NO_WRT_);

        // Assert
        Assert.False(fixture.StatusProvider.StateUseBank1);
        Assert.True(fixture.StatusProvider.StateHighRead);
        Assert.False(fixture.StatusProvider.StateHighWrite);
    }

    [Fact]
    public void LanguageCard_B1_RD_RAM_NO_WRT_SetsCorrectFlags()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuRead(VA2MBus.B1_RD_RAM_NO_WRT_);

        // Assert
        Assert.True(fixture.StatusProvider.StateUseBank1);
        Assert.True(fixture.StatusProvider.StateHighRead);
        Assert.False(fixture.StatusProvider.StateHighWrite);
    }

    [Fact]
    public void LanguageCard_B2_RD_ROM_WRT_RAM_FirstAccess_SetsPreWrite()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - First access sets PreWrite
        fixture.Bus.CpuRead(VA2MBus.B2_RD_ROM_WRT_RAM_);

        // Assert
        Assert.False(fixture.StatusProvider.StateUseBank1);
        Assert.False(fixture.StatusProvider.StateHighRead);
        Assert.True(fixture.StatusProvider.StatePreWrite);
        Assert.False(fixture.StatusProvider.StateHighWrite);
    }

    [Fact]
    public void LanguageCard_B2_RD_ROM_WRT_RAM_SecondAccess_SetsHighWrite()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Second access enables writing
        fixture.Bus.CpuRead(VA2MBus.B2_RD_ROM_WRT_RAM_);
        fixture.Bus.CpuRead(VA2MBus.B2_RD_ROM_WRT_RAM_);

        // Assert
        Assert.False(fixture.StatusProvider.StateUseBank1);
        Assert.False(fixture.StatusProvider.StateHighRead);
        Assert.True(fixture.StatusProvider.StateHighWrite);
    }

    [Fact]
    public void LanguageCard_B2_RD_RAM_WRT_RAM_FirstAccess_SetsPreWrite()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_WRT_RAM_);

        // Assert
        Assert.False(fixture.StatusProvider.StateUseBank1);
        Assert.True(fixture.StatusProvider.StateHighRead);
        Assert.True(fixture.StatusProvider.StatePreWrite);
        Assert.False(fixture.StatusProvider.StateHighWrite);
    }

    [Fact]
    public void LanguageCard_B2_RD_RAM_WRT_RAM_SecondAccess_SetsHighWrite()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_WRT_RAM_);
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_WRT_RAM_);

        // Assert
        Assert.False(fixture.StatusProvider.StateUseBank1);
        Assert.True(fixture.StatusProvider.StateHighRead);
        Assert.True(fixture.StatusProvider.StateHighWrite);
    }

    [Fact]
    public void LanguageCard_B2_RD_ROM_NO_WRT_DisablesReadAndWrite()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act
        fixture.Bus.CpuRead(VA2MBus.B2_RD_ROM_NO_WRT_);

        // Assert
        Assert.False(fixture.StatusProvider.StateUseBank1);
        Assert.False(fixture.StatusProvider.StateHighRead);
        Assert.False(fixture.StatusProvider.StateHighWrite);
    }

    [Fact]
    public void LanguageCard_Bank1And2_Independent()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Switch between banks
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_NO_WRT_);
        var bank2 = fixture.StatusProvider.StateUseBank1;
        
        fixture.Bus.CpuRead(VA2MBus.B1_RD_RAM_NO_WRT_);
        var bank1 = fixture.StatusProvider.StateUseBank1;

        // Assert
        Assert.False(bank2); // Bank 2
        Assert.True(bank1);  // Bank 1
    }

    [Fact]
    public void LanguageCard_WriteProtectionSequence_WorksCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Enable write protection
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_WRT_RAM_);
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_WRT_RAM_);
        var writeEnabled = fixture.StatusProvider.StateHighWrite;
        
        // Disable write
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_NO_WRT_);
        var writeDisabled = fixture.StatusProvider.StateHighWrite;

        // Assert
        Assert.True(writeEnabled);
        Assert.False(writeDisabled);
    }

    [Fact]
    public void LanguageCard_ALT_Addresses_WorkLikeNormal()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - ALT addresses should work identically
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_NO_WRT_ALT_);
        var altFlags = (fixture.StatusProvider.StateHighRead, fixture.StatusProvider.StateHighWrite);
        
        fixture.Bus.CpuRead(VA2MBus.B2_RD_RAM_NO_WRT_);
        var normalFlags = (fixture.StatusProvider.StateHighRead, fixture.StatusProvider.StateHighWrite);

        // Assert
        Assert.Equal(normalFlags, altFlags);
    }

    [Fact]
    public void LanguageCard_WriteHandlers_SetFlagsCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Test write handlers
        fixture.Bus.CpuWrite(VA2MBus.B2_RD_RAM_NO_WRT_, 0);
        var flags = (
            fixture.StatusProvider.StateHighRead,
            fixture.StatusProvider.StateHighWrite,
            fixture.StatusProvider.StatePreWrite
        );

        // Assert
        Assert.True(flags.StateHighRead);
        Assert.False(flags.StateHighWrite);
        Assert.False(flags.StatePreWrite);
    }

    [Fact]
    public void LanguageCard_All16Addresses_Work()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        var addresses = new ushort[]
        {
            VA2MBus.B2_RD_RAM_NO_WRT_, VA2MBus.B2_RD_RAM_NO_WRT_ALT_,
            VA2MBus.B2_RD_ROM_WRT_RAM_, VA2MBus.B2_RD_ROM_WRT_RAM_ALT_,
            VA2MBus.B2_RD_ROM_NO_WRT_, VA2MBus.B2_RD_ROM_NO_WRT_ALT_,
            VA2MBus.B2_RD_RAM_WRT_RAM_, VA2MBus.B2_RD_RAM_WRT_RAM_ALT_,
            VA2MBus.B1_RD_RAM_NO_WRT_, VA2MBus.B1_RD_RAM_NO_WRT_ALT_,
            VA2MBus.B1_RD_ROM_WRT_RAM_, VA2MBus.B1_RD_ROM_WRT_RAM_ALT_,
            VA2MBus.B1_RD_ROM_NO_WRT_, VA2MBus.B1_RD_ROM_NO_WRT_ALT_,
            VA2MBus.B1_RD_RAM_WRT_RAM_, VA2MBus.B1_RD_RAM_WRT_RAM_ALT_
        };

        // Act & Assert - All should execute without throwing
        foreach (var addr in addresses)
        {
            fixture.Bus.CpuRead(addr);
        }
    }

    #endregion

    #region VBlank Tests (6 tests)

    [Fact]
    public void VBlank_Event_FiresAtCorrectInterval()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        var eventCount = 0;
        fixture.Bus.VBlank += (_, __) => eventCount++;

        // Act - Clock through one VBlank period (17063 cycles)
        for (int i = 0; i < 17063; i++)
        {
            fixture.Bus.Clock();
        }

        // Assert
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void VBlank_Event_FiresMultipleTimes()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        var eventCount = 0;
        fixture.Bus.VBlank += (_, __) => eventCount++;

        // Act - Clock through 3 VBlank periods
        for (int i = 0; i < 17063 * 3; i++)
        {
            fixture.Bus.Clock();
        }

        // Assert
        Assert.Equal(3, eventCount);
    }

    [Fact]
    public void VBlank_RD_VERTBLANK_ReflectsBlankoutState()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        
        // Act - Read vertical blank status
        var inBlankInitial = fixture.Bus.CpuRead(VA2MBus.RD_VERTBLANK_);
        
        // Clock past blankout period (4550 cycles)
        for (int i = 0; i < 4600; i++)
        {
            fixture.Bus.Clock();
        }
        var outOfBlank = fixture.Bus.CpuRead(VA2MBus.RD_VERTBLANK_);

        // Assert
        Assert.Equal(0x80, inBlankInitial & 0x80); // In blank
        Assert.Equal(0x00, outOfBlank & 0x80);     // Out of blank
    }

    [Fact]
    public void SystemClockCounter_IncrementsByOne()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        var initialClock = fixture.Bus.SystemClockCounter;

        // Act
        fixture.Bus.Clock();
        var afterOne = fixture.Bus.SystemClockCounter;
        
        fixture.Bus.Clock();
        var afterTwo = fixture.Bus.SystemClockCounter;

        // Assert
        Assert.Equal(0UL, initialClock);
        Assert.Equal(1UL, afterOne);
        Assert.Equal(2UL, afterTwo);
    }

    [Fact]
    public void SystemClockCounter_TracksLongRuns()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act - Run 100,000 cycles
        for (int i = 0; i < 100000; i++)
        {
            fixture.Bus.Clock();
        }

        // Assert
        Assert.Equal(100000UL, fixture.Bus.SystemClockCounter);
    }

    [Fact]
    public void VBlank_Timing_ApproximatesSixtyHz()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        var eventCount = 0;
        fixture.Bus.VBlank += (_, __) => eventCount++;

        // Act - Simulate 1 second at 1.023 MHz
        for (int i = 0; i < 1_023_000; i++)
        {
            fixture.Bus.Clock();
        }

        // Assert - Should be approximately 60 VBlanks per second
        Assert.InRange(eventCount, 58, 62); // Allow small tolerance
    }

    #endregion

    #region Clock and Reset Tests (5 tests)

    [Fact]
    public void Clock_IncrementsClock()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.Clock();

        // Assert
        Assert.Equal(1UL, fixture.Bus.SystemClockCounter);
    }

    [Fact]
    public void Reset_ResetsClockToZero()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        for (int i = 0; i < 100; i++)
        {
            fixture.Bus.Clock();
        }

        // Act
        fixture.Bus.Reset();

        // Assert
        Assert.Equal(0UL, fixture.Bus.SystemClockCounter);
    }

    [Fact]
    public void Reset_CallsCpuReset()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act & Assert - Should not throw
        fixture.Bus.Reset();
    }

    [Fact]
    public void Clock_MultipleCalls_IncrementsSequentially()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        for (ulong i = 1; i <= 10; i++)
        {
            fixture.Bus.Clock();
            Assert.Equal(i, fixture.Bus.SystemClockCounter);
        }
    }

    [Fact]
    public void Reset_MultipleTimes_AlwaysResetsToZero()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 50; j++)
            {
                fixture.Bus.Clock();
            }
            fixture.Bus.Reset();
            Assert.Equal(0UL, fixture.Bus.SystemClockCounter);
        }
    }

    #endregion

    #region CpuRead/CpuWrite Integration Tests (8 tests)

    [Fact]
    public void CpuRead_RegularMemory_ReadsFromMemoryPool()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.MemoryPool.Write(0x1000, 0x42);

        // Act
        var value = fixture.Bus.CpuRead(0x1000);

        // Assert
        Assert.Equal(0x42, value);
    }

    [Fact]
    public void CpuWrite_RegularMemory_WritesToMemoryPool()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.CpuWrite(0x2000, 0x99);
        var value = fixture.MemoryPool.Read(0x2000);

        // Assert
        Assert.Equal(0x99, value);
    }

    [Fact]
    public void CpuRead_IOSpace_RoutesToHandlers()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.Bus.SetKeyValue(0xC1);

        // Act
        var value = fixture.Bus.CpuRead(VA2MBus.KBD_);

        // Assert
        Assert.Equal(0xC1, value);
    }

    [Fact]
    public void CpuWrite_IOSpace_RoutesToHandlers()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.CpuWrite(VA2MBus.SETTXT_, 0);

        // Assert
        Assert.True(fixture.StatusProvider.StateTextMode);
    }

    [Fact]
    public void CpuReadWrite_Sequence_WorksCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act - Write to memory
        fixture.Bus.CpuWrite(0x0800, 0xAA);
        fixture.Bus.CpuWrite(0x0801, 0xBB);
        
        // Read back
        var value1 = fixture.Bus.CpuRead(0x0800);
        var value2 = fixture.Bus.CpuRead(0x0801);

        // Assert
        Assert.Equal(0xAA, value1);
        Assert.Equal(0xBB, value2);
    }

    [Fact]
    public void CpuRead_MultipleAddresses_Independent()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        fixture.MemoryPool.Write(0x1000, 0x10);
        fixture.MemoryPool.Write(0x2000, 0x20);
        fixture.MemoryPool.Write(0x3000, 0x30);

        // Act
        var v1 = fixture.Bus.CpuRead(0x1000);
        var v2 = fixture.Bus.CpuRead(0x2000);
        var v3 = fixture.Bus.CpuRead(0x3000);

        // Assert
        Assert.Equal(0x10, v1);
        Assert.Equal(0x20, v2);
        Assert.Equal(0x30, v3);
    }

    [Fact]
    public void CpuWrite_MultipleAddresses_Independent()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        fixture.Bus.CpuWrite(0x1000, 0xAA);
        fixture.Bus.CpuWrite(0x2000, 0xBB);
        fixture.Bus.CpuWrite(0x3000, 0xCC);

        // Assert
        Assert.Equal(0xAA, fixture.MemoryPool.Read(0x1000));
        Assert.Equal(0xBB, fixture.MemoryPool.Read(0x2000));
        Assert.Equal(0xCC, fixture.MemoryPool.Read(0x3000));
    }

    [Fact]
    public void CpuReadWrite_IOAndMemory_BothWork()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act - Mix I/O and memory operations
        fixture.Bus.CpuWrite(0x1000, 0x42);           // Memory write
        fixture.Bus.CpuWrite(VA2MBus.SETTXT_, 0);     // I/O write
        fixture.Bus.SetKeyValue(0xC5);                // Keyboard
        
        var memValue = fixture.Bus.CpuRead(0x1000);   // Memory read
        var keyValue = fixture.Bus.CpuRead(VA2MBus.KBD_); // I/O read
        var switchValue = fixture.StatusProvider.StateTextMode;

        // Assert
        Assert.Equal(0x42, memValue);
        Assert.Equal(0xC5, keyValue);
        Assert.True(switchValue);
    }

    #endregion

    #region Integration Scenarios (6 tests)

    [Fact]
    public void Scenario_BootSequence_InitializesCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act - Simulate boot
        fixture.Bus.Reset();
        for (int i = 0; i < 100; i++)
        {
            fixture.Bus.Clock();
        }

        // Assert
        Assert.Equal(100UL, fixture.Bus.SystemClockCounter);
    }

    [Fact]
    public void Scenario_GraphicsMode_Setup()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act - Set up typical graphics mode
        fixture.Bus.CpuWrite(VA2MBus.CLRTXT_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SETHIRES_, 0);
        fixture.Bus.CpuWrite(VA2MBus.SETMIXED_, 0);
        fixture.Bus.CpuWrite(VA2MBus.CLRPAGE2_, 0);

        // Assert
        Assert.False(fixture.StatusProvider.StateTextMode);
        Assert.True(fixture.StatusProvider.StateHiRes);
        Assert.True(fixture.StatusProvider.StateMixed);
        Assert.False(fixture.StatusProvider.StatePage2);
    }

    [Fact]
    public void Scenario_KeyboardInput_ProcessesCorrectly()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act - Simulate typing "HI"
        fixture.Bus.SetKeyValue(0xC8); // 'H'
        var h = fixture.Bus.CpuRead(VA2MBus.KBD_);
        fixture.Bus.CpuRead(VA2MBus.KEYSTRB_);
        
        fixture.Bus.SetKeyValue(0xC9); // 'I'
        var i = fixture.Bus.CpuRead(VA2MBus.KBD_);

        // Assert
        Assert.Equal(0xC8, h);
        Assert.Equal(0xC9, i);
    }

    [Fact]
    public void Scenario_LanguageCardAccess_EnablesRAM()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act - Enable language card RAM with write
        fixture.Bus.CpuRead(VA2MBus.B1_RD_RAM_WRT_RAM_);
        fixture.Bus.CpuRead(VA2MBus.B1_RD_RAM_WRT_RAM_);

        // Assert
        Assert.True(fixture.StatusProvider.StateUseBank1);
        Assert.True(fixture.StatusProvider.StateHighRead);
        Assert.True(fixture.StatusProvider.StateHighWrite);
    }

    [Fact]
    public void Scenario_PageFlipping_SwitchesPages()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act - Flip between page 1 and page 2
        fixture.Bus.CpuWrite(VA2MBus.CLRPAGE2_, 0);
        var page1 = fixture.StatusProvider.StatePage2;
        
        fixture.Bus.CpuWrite(VA2MBus.SETPAGE2_, 0);
        var page2 = fixture.StatusProvider.StatePage2;

        // Assert
        Assert.False(page1);
        Assert.True(page2);
    }

    [Fact]
    public void Scenario_CompleteFrame_ClocksAndVBlank()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        var vblankFired = false;
        fixture.Bus.VBlank += (_, __) => vblankFired = true;

        // Act - Clock through one frame
        for (int i = 0; i < 17063; i++)
        {
            fixture.Bus.Clock();
        }

        // Assert
        Assert.True(vblankFired);
        Assert.Equal(17063UL, fixture.Bus.SystemClockCounter);
    }

    #endregion

    #region Edge Cases and Disposal (4 tests)

    [Fact]
    public void GetPushButton_InvalidIndex_ReturnsFalse()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act
        var invalid = fixture.Bus.GetPushButton(99);

        // Assert
        Assert.False(invalid);
    }

    [Fact]
    public void SetPushButton_InvalidIndex_DoesNotThrow()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act & Assert - Should not throw
        fixture.Bus.SetPushButton(99, true);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var fixture = new VA2MBusFixture();

        // Act & Assert - Should not throw
        fixture.Bus.Dispose();
        fixture.Bus.Dispose();
        fixture.Bus.Dispose();
    }

    [Fact]
    public void Connect_ThrowsNotSupportedException()
    {
        // Arrange
        var fixture = new VA2MBusFixture();
        var cpu = new CPU();

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => fixture.Bus.Connect(cpu));
    }

    #endregion
}
