// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;


namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for the LanguageCard class - Apple IIe memory banking for $D000-$FFFF.
/// </summary>
public class LanguageCardTests
{
    #region Test Fixtures and Helpers

    private class MockFloatingBusProvider : IFloatingBusProvider
    {
        private byte _returnValue;
        
        public int ReadCount { get; private set; }
        
        public void SetReturnValue(byte value) => _returnValue = value;
        
        public byte Read()
        {
            ReadCount++;
            return _returnValue;
        }
    }

    private class MockSystemRomProvider(int size) : ISystemRomProvider
    {
#pragma warning disable CA1859 // Use concrete types for improved performance
        private readonly IPandowdyMemory _memory = new MemoryBlock(size);
#pragma warning restore CA1859

        public int Size => _memory.Size;

        public byte this[ushort address]
        {
            get => _memory[address];
            set => _memory[address] = value;
        }

        public byte Read(ushort address) => _memory[address];

        public void Write(ushort address, byte value) => _memory[address] = value;

        public void LoadRomFile(string filename)
        {
            throw new NotImplementedException("LoadRomFile not needed for tests");
        }
    }

    private class MockSystemStatusProvider : ISystemStatusProvider
    {
        public bool StateHighRead { get; set; }
        public bool StateHighWrite { get; set; }
        public bool StateUseBank1 { get; set; }
        public bool StateRamRd { get; set; }
        public bool StateRamWrt { get; set; }
        public bool StateAltZp { get; set; }


        // Other required properties (not used in LanguageCard tests)
        public bool State80Store => false;
        public bool StateIntCxRom => false;
        public bool StateSlotC3Rom => false;
        public bool StatePb0 => false;
        public bool StatePb1 => false;
        public bool StatePb2 => false;
        public bool StateAnn0 => false;
        public bool StateAnn1 => false;
        public bool StateAnn2 => false;
        public bool StateAnn3_DGR => false;
        public bool StatePage2 => false;
        public bool StateHiRes => false;
        public bool StateMixed => false;
        public bool StateTextMode => false;
        public bool StateShow80Col => false;
        public bool StateAltCharSet => false;
        public bool StateFlashOn => false;
        public bool StatePreWrite => false;
        public bool StateVBlank => false;
        public bool StateIntC8Rom => false;
        public byte StateIntC8RomSlot => 0;
        public double StateCurrentMhz => 1.023;
        public byte CurrentKey { get; set; }
        public byte Pdl0 { get; set; }
        public byte Pdl1 { get; set; }
        public byte Pdl2 { get; set; }
        public byte Pdl3 { get; set; }
        
        // Event and Stream (not used in tests, but required by interface)
#pragma warning disable CS0067 // Event is never used - required by interface but not needed for LanguageCard tests
        public event EventHandler<SystemStatusSnapshot>? Changed;
        public event EventHandler<SystemStatusSnapshot>? MemoryMappingChanged;
#pragma warning restore CS0067
        public IObservable<SystemStatusSnapshot> Stream => 
            throw new NotImplementedException("Stream not needed for LanguageCard tests");
        public SystemStatusSnapshot Current => 
            throw new NotImplementedException("Current not needed for LanguageCard tests");
        public void Mutate(Action<SystemStatusSnapshotBuilder> mutator) =>
            throw new NotImplementedException("Mutate not needed for LanguageCard tests");
    }

    private class TestFixture
    {
        public ISystemRam MainRam { get; }
        public ISystemRam AuxRam { get; }
        public ISystemRomProvider SystemRom { get; }
        public MockFloatingBusProvider FloatingBus { get; }
        public MockSystemStatusProvider Status { get; }

        public TestFixture()
        {
            MainRam = new MemoryBlock(0x4000); // 16KB
            AuxRam = new MemoryBlock(0x4000);  // 16KB
            SystemRom = new MockSystemRomProvider(0x4000); // 16KB ($C000-$FFFF)
            FloatingBus = new MockFloatingBusProvider();
            Status = new MockSystemStatusProvider();
        }

        public LanguageCard CreateLanguageCard(bool includeAuxRam = true)
        {
            return new LanguageCard(
                MainRam,
                includeAuxRam ? AuxRam : null,
                SystemRom,
                FloatingBus,
                Status);
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        var fixture = new TestFixture();

        // Act
        var lc = fixture.CreateLanguageCard();

        // Assert
        Assert.NotNull(lc);
        Assert.Equal(0x3000, lc.Size);
    }

    [Fact]
    public void Constructor_WithNullMainRam_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new TestFixture();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LanguageCard(null!, fixture.AuxRam, fixture.SystemRom,
                fixture.FloatingBus, fixture.Status));
    }

    [Fact]
    public void Constructor_WithNullSystemRom_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new TestFixture();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LanguageCard(fixture.MainRam, fixture.AuxRam, null!,
                fixture.FloatingBus, fixture.Status));
    }

    [Fact]
    public void Constructor_WithNullFloatingBus_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new TestFixture();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LanguageCard(fixture.MainRam, fixture.AuxRam, fixture.SystemRom,
                null!, fixture.Status));
    }

    [Fact]
    public void Constructor_WithNullStatus_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new TestFixture();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LanguageCard(fixture.MainRam, fixture.AuxRam, fixture.SystemRom,
                fixture.FloatingBus, null!));
    }

    [Fact]
    public void Constructor_WithNullAuxRam_Succeeds()
    {
        // Arrange
        var fixture = new TestFixture();

        // Act
        var lc = fixture.CreateLanguageCard(includeAuxRam: false);

        // Assert
        Assert.NotNull(lc);
    }

    [Fact]
    public void Constructor_WithWrongSizeMainRam_ThrowsArgumentException()
    {
        // Arrange
        var fixture = new TestFixture();
        var wrongSizeRam = new MemoryBlock(0x1000); // Only 4KB

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new LanguageCard(wrongSizeRam, fixture.AuxRam, fixture.SystemRom,
                fixture.FloatingBus, fixture.Status));
        Assert.Contains("16384", ex.Message);
        Assert.Contains("0x4000", ex.Message);
    }

    [Fact]
    public void Constructor_WithWrongSizeRom_ThrowsArgumentException()
    {
        // Arrange
        var fixture = new TestFixture();
        var wrongSizeRom = new MockSystemRomProvider(0x3000); // 12KB instead of 16KB

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new LanguageCard(fixture.MainRam, fixture.AuxRam, wrongSizeRom,
                fixture.FloatingBus, fixture.Status));
        Assert.Contains("16384", ex.Message); // Expected 16KB
        Assert.Contains("0x4000", ex.Message);
    }

    #endregion

    #region Size Property Tests

    [Fact]
    public void Size_Returns12KB()
    {
        // Arrange
        var fixture = new TestFixture();
        var lc = fixture.CreateLanguageCard();

        // Act & Assert
        Assert.Equal(0x3000, lc.Size);
        Assert.Equal(12288, lc.Size);
    }

    #endregion

    #region Read Tests - ROM Mode

    [Fact]
    public void Read_WhenStateHighReadFalse_ReadsFromROM()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = false;
        fixture.SystemRom[0x1000] = 0x4C; // JMP instruction at ROM offset 0x1000 ($D000)
        var lc = fixture.CreateLanguageCard();

        // Act
        byte value = lc.Read(0xD000);

        // Assert
        Assert.Equal(0x4C, value);
    }

    [Fact]
    public void Read_FromROM_IgnoresMainRamContents()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = false;
        fixture.MainRam[0x1000] = 0xAA; // Bank 2 $D000 position in RAM
        fixture.SystemRom[0x1000] = 0xBB; // ROM offset 0x1000 ($D000 position)
        var lc = fixture.CreateLanguageCard();

        // Act
        byte value = lc.Read(0xD000);

        // Assert
        Assert.Equal(0xBB, value); // Should read from ROM, not RAM
    }

    [Theory]
    [InlineData(0xD000, 0x1000)] // Start of Language Card space -> ROM offset 0x1000
    [InlineData(0xD100, 0x1100)] // Within first 4KB
    [InlineData(0xDFFF, 0x1FFF)] // End of first 4KB
    [InlineData(0xE000, 0x2000)] // Start of common 8KB -> ROM offset 0x2000
    [InlineData(0xF000, 0x3000)] // Middle of common 8KB
    [InlineData(0xFFFF, 0x3FFF)] // End of common 8KB -> ROM offset 0x3FFF
    public void Read_FromROM_MapsAddressesCorrectly(ushort lcAddress, ushort romOffset)
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = false;
        fixture.SystemRom[romOffset] = 0x42;
        var lc = fixture.CreateLanguageCard();

        // Act
        byte value = lc.Read(lcAddress);

        // Assert
        Assert.Equal(0x42, value);
    }

    #endregion

    #region Read Tests - RAM Mode (Main)

    [Fact]
    public void Read_WhenStateHighReadTrue_ReadsFromMainRAM()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateRamRd = false; // Main RAM
        fixture.MainRam[0x1000] = 0x60; // RTS at Bank 2 $D000 position
        var lc = fixture.CreateLanguageCard();

        // Act
        byte value = lc.Read(0xD000);

        // Assert
        Assert.Equal(0x60, value);
    }

    [Fact]
    public void Read_FromMainRAM_Bank2_MapsCorrectly()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateRamRd = false;
        fixture.Status.StateUseBank1 = false; // Bank 2
        
        // Bank 2: $D000-$FFFF maps to $1000-$3FFF (12KB contiguous)
        fixture.MainRam[0x1000] = 0xAA; // $D000
        fixture.MainRam[0x2000] = 0xBB; // $E000
        fixture.MainRam[0x3FFF] = 0xCC; // $FFFF
        
        var lc = fixture.CreateLanguageCard();

        // Act & Assert
        Assert.Equal(0xAA, lc.Read(0xD000));
        Assert.Equal(0xBB, lc.Read(0xE000));
        Assert.Equal(0xCC, lc.Read(0xFFFF));
    }

    [Fact]
    public void Read_FromMainRAM_Bank1_MapsCorrectly()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateRamRd = false;
        fixture.Status.StateUseBank1 = true; // Bank 1
        
        // Bank 1: $D000-$DFFF maps to $0000-$0FFF, $E000-$FFFF maps to $2000-$3FFF
        fixture.MainRam[0x0000] = 0xDD; // $D000
        fixture.MainRam[0x0FFF] = 0xEE; // $DFFF
        fixture.MainRam[0x2000] = 0xFF; // $E000
        fixture.MainRam[0x3FFF] = 0x11; // $FFFF
        
        var lc = fixture.CreateLanguageCard();

        // Act & Assert
        Assert.Equal(0xDD, lc.Read(0xD000));
        Assert.Equal(0xEE, lc.Read(0xDFFF));
        Assert.Equal(0xFF, lc.Read(0xE000));
        Assert.Equal(0x11, lc.Read(0xFFFF));
    }

    [Fact]
    public void Read_Bank1_LeavesGapAt0x1000()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateRamRd = false;
        fixture.Status.StateUseBank1 = true;
        
        // The gap at $1000-$1FFF should not be accessed
        // Verify that $D000-$DFFF maps to $0000-$0FFF (not $1000-$1FFF)
        fixture.MainRam[0x0500] = 0x42;
        
        var lc = fixture.CreateLanguageCard();

        // Act
        byte value = lc.Read(0xD500);

        // Assert
        Assert.Equal(0x42, value);
    }

    #endregion

    #region Read Tests - RAM Mode (Aux)

    [Fact]
    public void Read_WhenStateAltZpTrue_ReadsFromAuxRAM()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateAltZp = true; // Aux RAM
        fixture.AuxRam[0x1000] = 0x77;
        var lc = fixture.CreateLanguageCard();

        // Act
        byte value = lc.Read(0xD000);

        // Assert
        Assert.Equal(0x77, value);
    }

    [Fact]
    public void Read_FromAuxRAM_IgnoresMainRAM()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateAltZp = true;
        fixture.MainRam[0x1000] = 0xAA;
        fixture.AuxRam[0x1000] = 0xBB;
        var lc = fixture.CreateLanguageCard();

        // Act
        byte value = lc.Read(0xD000);

        // Assert
        Assert.Equal(0xBB, value); // Should read from aux, not main
    }

    [Fact]
    public void Read_WhenAuxRamNull_ReturnsFloatingBus()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateAltZp = true;
        fixture.FloatingBus.SetReturnValue(0x99);
        var lc = fixture.CreateLanguageCard(includeAuxRam: false);

        // Act
        byte value = lc.Read(0xD000);

        // Assert
        Assert.Equal(0x99, value);
        Assert.Equal(1, fixture.FloatingBus.ReadCount);
    }

    #endregion

    #region Write Tests - Write Protected

    [Fact]
    public void Write_WhenStateHighWriteFalse_DoesNotWrite()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighWrite = false;
        fixture.MainRam[0x1000] = 0x00;
        var lc = fixture.CreateLanguageCard();

        // Act
        lc.Write(0xD000, 0x42);

        // Assert
        Assert.Equal(0x00, fixture.MainRam[0x1000]); // Should remain unchanged
    }

    [Fact]
    public void Write_ToROM_IsNoOp()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighWrite = false;
        fixture.SystemRom[0x0000] = 0xFF;
        var lc = fixture.CreateLanguageCard();

        // Act
        lc.Write(0xD000, 0x00);

        // Assert
        Assert.Equal(0xFF, fixture.SystemRom[0x0000]); // ROM unchanged
    }

    #endregion

    #region Write Tests - Main RAM

    [Fact]
    public void Write_WhenStateHighWriteTrue_WritesToMainRAM()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateRamWrt = false; // Main RAM
        var lc = fixture.CreateLanguageCard();

        // Act
        lc.Write(0xD000, 0x42);

        // Assert
        Assert.Equal(0x42, fixture.MainRam[0x1000]); // Bank 2 $D000 position
    }

    [Fact]
    public void Write_ToMainRAM_Bank2_MapsCorrectly()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateRamWrt = false;
        fixture.Status.StateUseBank1 = false; // Bank 2
        var lc = fixture.CreateLanguageCard();

        // Act
        lc.Write(0xD000, 0xAA);
        lc.Write(0xE000, 0xBB);
        lc.Write(0xFFFF, 0xCC);

        // Assert
        Assert.Equal(0xAA, fixture.MainRam[0x1000]);
        Assert.Equal(0xBB, fixture.MainRam[0x2000]);
        Assert.Equal(0xCC, fixture.MainRam[0x3FFF]);
    }

    [Fact]
    public void Write_ToMainRAM_Bank1_MapsCorrectly()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateRamWrt = false;
        fixture.Status.StateUseBank1 = true; // Bank 1
        var lc = fixture.CreateLanguageCard();

        // Act
        lc.Write(0xD000, 0xDD);
        lc.Write(0xDFFF, 0xEE);
        lc.Write(0xE000, 0xFF);
        lc.Write(0xFFFF, 0x11);

        // Assert
        Assert.Equal(0xDD, fixture.MainRam[0x0000]);
        Assert.Equal(0xEE, fixture.MainRam[0x0FFF]);
        Assert.Equal(0xFF, fixture.MainRam[0x2000]);
        Assert.Equal(0x11, fixture.MainRam[0x3FFF]);
    }

    #endregion

    #region Write Tests - Aux RAM

    [Fact]
    public void Write_WhenStateAltZpTrue_WritesToAuxRAM()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateAltZp = true; // Aux RAM
        var lc = fixture.CreateLanguageCard();

        // Act
        lc.Write(0xD000, 0x77);

        // Assert
        Assert.Equal(0x77, fixture.AuxRam[0x1000]);
        Assert.Equal(0x00, fixture.MainRam[0x1000]); // Main unchanged
    }

    [Fact]
    public void Write_WhenAuxRamNull_IsNoOp()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateAltZp = true;
        var lc = fixture.CreateLanguageCard(includeAuxRam: false);

        // Act (should not throw)
        lc.Write(0xD000, 0x42);

        // Assert - Main RAM should remain unchanged
        Assert.Equal(0x00, fixture.MainRam[0x1000]);
    }

    #endregion

    #region Indexer Tests

    [Fact]
    public void Indexer_Get_CallsRead()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = false;
        fixture.SystemRom[0x1000] = 0x4C; // ROM offset 0x1000 ($D000)
        var lc = fixture.CreateLanguageCard();

        // Act
        byte value = lc[0xD000];

        // Assert
        Assert.Equal(0x4C, value);
    }

    [Fact]
    public void Indexer_Set_CallsWrite()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateRamWrt = false;
        var lc = fixture.CreateLanguageCard();

        // Act
        lc[0xD000] = 0x42;

        // Assert
        Assert.Equal(0x42, fixture.MainRam[0x1000]);
    }

    #endregion

    #region Integration Tests - Read/Write Round Trip

    [Fact]
    public void ReadWrite_RoundTrip_MainRAM_Bank2()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateRamRd = false;
        fixture.Status.StateRamWrt = false;
        fixture.Status.StateUseBank1 = false;
        var lc = fixture.CreateLanguageCard();

        // Act
        lc.Write(0xD500, 0xAB);
        byte value = lc.Read(0xD500);

        // Assert
        Assert.Equal(0xAB, value);
    }

    [Fact]
    public void ReadWrite_RoundTrip_AuxRAM_Bank1()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateRamRd = true;
        fixture.Status.StateRamWrt = true;
        fixture.Status.StateUseBank1 = true;
        var lc = fixture.CreateLanguageCard();

        // Act
        lc.Write(0xD200, 0xCD);
        byte value = lc.Read(0xD200);

        // Assert
        Assert.Equal(0xCD, value);
    }

    [Fact]
    public void ReadWrite_AsymmetricConfig_ReadRomWriteRam()
    {
        // Arrange - Classic "copy from ROM to RAM" configuration
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = false; // Read ROM
        fixture.Status.StateHighWrite = true;  // Write RAM
        fixture.Status.StateRamWrt = false;
        fixture.SystemRom[0x1100] = 0x4C; // ROM offset 0x1100 ($D100)
        var lc = fixture.CreateLanguageCard();

        // Act - Read from ROM, write to RAM
        byte romValue = lc.Read(0xD100);
        lc.Write(0xD100, romValue);

        // Assert
        Assert.Equal(0x4C, romValue);
        Assert.Equal(0x4C, fixture.MainRam[0x1100]); // Copied to RAM
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void BankSwitching_PreservesData()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateRamRd = false;
        fixture.Status.StateRamWrt = false;
        var lc = fixture.CreateLanguageCard();

        // Act - Write to Bank 2
        fixture.Status.StateUseBank1 = false;
        lc.Write(0xD000, 0x22);

        // Switch to Bank 1 and write
        fixture.Status.StateUseBank1 = true;
        lc.Write(0xD000, 0x11);

        // Switch back to Bank 2
        fixture.Status.StateUseBank1 = false;
        byte bank2Value = lc.Read(0xD000);

        // Switch back to Bank 1
        fixture.Status.StateUseBank1 = true;
        byte bank1Value = lc.Read(0xD000);

        // Assert - Each bank should preserve its own data
        Assert.Equal(0x22, bank2Value);
        Assert.Equal(0x11, bank1Value);
    }

    [Fact]
    public void CommonArea_SharedBetweenBanks()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Status.StateHighRead = true;
        fixture.Status.StateHighWrite = true;
        fixture.Status.StateRamRd = false;
        fixture.Status.StateRamWrt = false;
        var lc = fixture.CreateLanguageCard();

        // Act - Write to $E000 (common area) in Bank 1
        fixture.Status.StateUseBank1 = true;
        lc.Write(0xE000, 0xAA);

        // Switch to Bank 2, read $E000
        fixture.Status.StateUseBank1 = false;
        byte value = lc.Read(0xE000);

        // Assert - Common area should be accessible from both banks
        Assert.Equal(0xAA, value);
    }

    #endregion
}
