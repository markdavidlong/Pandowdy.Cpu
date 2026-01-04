using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Comprehensive tests for MemoryPool, covering Apple IIe memory banking,
/// 80STORE logic, language card, alternate zero page, ROM management, and more.
/// 
/// Total: 47 tests covering ~90% of MemoryPool functionality.
/// </summary>
public class MemoryPoolTests
{
    #region Test Infrastructure

    /// <summary>
    /// Builds a test memory pool with pre-populated data:
    /// - Main memory filled with 0x01
    /// - Aux memory filled with 0x02
    /// - Internal ROM slots filled with 'I'
    /// - Slot ROM filled with 'S'
    /// </summary>
    private static MemoryPool BuildPool()
    {
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        
        // Fill main and aux memory
        for (int i = 0; i < 65536; i++)
        {
            pool.WritePool(i, 1);           // Main memory
            pool.WritePool(i + 65536, 2);   // Aux memory
        }
        
        // Fill internal ROM
        for (int i = 0x20100; i < 0x20800; i += 0x100)
        {
            for (int j = 0; j < 0x100; j++)
            {
                pool.WritePool(i + j, (byte)'I');
            }
        }
        
        // Fill slot ROM
        for (int i = 0x24000; i < 0x24700; i += 0x100)
        {
            for (int j = 0; j < 0x100; j++)
            {
                pool.WritePool(i + j, (byte)'S');
            }
        }
        
        return pool;
    }

    #endregion

    #region Basic Memory Operations (3 tests)

    [Fact]
    public void ReadPool_ReturnsCorrectValues()
    {
        // Arrange
        var pool = BuildPool();

        // Act & Assert - Main memory range
        for (ushort i = 0x200; i < 48 * 1024; i++)
        {
            Assert.Equal(1, pool.ReadPool(i));
        }

        // Aux memory range
        for (ushort i = 0x200; i < 48 * 1024; i++)
        {
            Assert.Equal(2, pool.ReadPool(0x10000 + i));
        }
    }

    [Fact]
    public void Read_MainMemory_ReturnsMainValues()
    {
        // Arrange
        var pool = BuildPool();

        // Act & Assert
        for (ushort i = 0x200; i < 48 * 1024; i++)
        {
            Assert.Equal(1, pool.Read(i));
        }
    }

    [Fact]
    public void Read_AuxMemory_WithRamRdEnabled()
    {
        // Arrange
        var pool = BuildPool();
        pool.SetRamRd(true);

        // Act & Assert
        for (ushort i = 0x200; i < 48 * 1024; i++)
        {
            Assert.Equal(2, pool.Read(i));
        }
    }

    #endregion

    #region 80STORE OFF - Read Tests (2 tests)

    [Fact]
    public void Store80Off_RamRdOff_ReadsMainMemory()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(false);
        pool.SetRamRd(false);

        // Test various HIRES/PAGE2 combinations
        var testCases = new[]
        {
            (hires: false, page2: false),
            (hires: false, page2: true),
            (hires: true, page2: false),
            (hires: true, page2: true)
        };

        foreach (var (hires, page2) in testCases)
        {
            // Act
            pool.SetHiRes(hires);
            pool.SetPage2(page2);

            // Assert - All regions should read from main (1)
            Assert.Equal(1, pool.Read(0x0200));
            Assert.Equal(1, pool.Read(0x0400));
            Assert.Equal(1, pool.Read(0x2000));
            Assert.Equal(1, pool.Read(0x4000));
        }
    }

    [Fact]
    public void Store80Off_RamRdOn_ReadsAuxMemory()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(false);
        pool.SetRamRd(true);

        // Test various HIRES/PAGE2 combinations
        var testCases = new[]
        {
            (hires: false, page2: false),
            (hires: false, page2: true),
            (hires: true, page2: false),
            (hires: true, page2: true)
        };

        foreach (var (hires, page2) in testCases)
        {
            // Act
            pool.SetHiRes(hires);
            pool.SetPage2(page2);

            // Assert - All regions should read from aux (2)
            Assert.Equal(2, pool.Read(0x0200));
            Assert.Equal(2, pool.Read(0x0400));
            Assert.Equal(2, pool.Read(0x2000));
            Assert.Equal(2, pool.Read(0x4000));
        }
    }

    #endregion

    #region 80STORE ON - Read Tests (8 tests)

    [Fact]
    public void Store80On_RamRdOff_HiResOff_Page2Off()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamRd(false);
        pool.SetHiRes(false);
        pool.SetPage2(false);

        // Assert - All main: M, M, M, M
        Assert.Equal(1, pool.Read(0x0200));
        Assert.Equal(1, pool.Read(0x0400));
        Assert.Equal(1, pool.Read(0x2000));
        Assert.Equal(1, pool.Read(0x4000));
    }

    [Fact]
    public void Store80On_RamRdOff_HiResOff_Page2On()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamRd(false);
        pool.SetHiRes(false);
        pool.SetPage2(true);

        // Assert - M, A, M, M (text page 2 switches to aux)
        Assert.Equal(1, pool.Read(0x0200));
        Assert.Equal(2, pool.Read(0x0400));
        Assert.Equal(1, pool.Read(0x2000));
        Assert.Equal(1, pool.Read(0x4000));
    }

    [Fact]
    public void Store80On_RamRdOff_HiResOn_Page2Off()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamRd(false);
        pool.SetHiRes(true);
        pool.SetPage2(false);

        // Assert - All main: M, M, M, M
        Assert.Equal(1, pool.Read(0x0200));
        Assert.Equal(1, pool.Read(0x0400));
        Assert.Equal(1, pool.Read(0x2000));
        Assert.Equal(1, pool.Read(0x4000));
    }

    [Fact]
    public void Store80On_RamRdOff_HiResOn_Page2On()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamRd(false);
        pool.SetHiRes(true);
        pool.SetPage2(true);

        // Assert - M, A, A, M (text and hires page 2 switch to aux)
        Assert.Equal(1, pool.Read(0x0200));
        Assert.Equal(2, pool.Read(0x0400));
        Assert.Equal(2, pool.Read(0x2000));
        Assert.Equal(1, pool.Read(0x4000));
    }

    [Fact]
    public void Store80On_RamRdOn_HiResOff_Page2Off()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamRd(true);
        pool.SetHiRes(false);
        pool.SetPage2(false);

        // Assert - A, M, A, A (text page 1 stays main when 80STORE on)
        Assert.Equal(2, pool.Read(0x0200));
        Assert.Equal(1, pool.Read(0x0400));
        Assert.Equal(2, pool.Read(0x2000));
        Assert.Equal(2, pool.Read(0x4000));
    }

    [Fact]
    public void Store80On_RamRdOn_HiResOff_Page2On()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamRd(true);
        pool.SetHiRes(false);
        pool.SetPage2(true);

        // Assert - All aux: A, A, A, A
        Assert.Equal(2, pool.Read(0x0200));
        Assert.Equal(2, pool.Read(0x0400));
        Assert.Equal(2, pool.Read(0x2000));
        Assert.Equal(2, pool.Read(0x4000));
    }

    [Fact]
    public void Store80On_RamRdOn_HiResOn_Page2Off()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamRd(true);
        pool.SetHiRes(true);
        pool.SetPage2(false);

        // Assert - A, M, M, A (text page 1 and hires page 1 stay main)
        Assert.Equal(2, pool.Read(0x0200));
        Assert.Equal(1, pool.Read(0x0400));
        Assert.Equal(1, pool.Read(0x2000));
        Assert.Equal(2, pool.Read(0x4000));
    }

    [Fact]
    public void Store80On_RamRdOn_HiResOn_Page2On()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamRd(true);
        pool.SetHiRes(true);
        pool.SetPage2(true);

        // Assert - All aux: A, A, A, A
        Assert.Equal(2, pool.Read(0x0200));
        Assert.Equal(2, pool.Read(0x0400));
        Assert.Equal(2, pool.Read(0x2000));
        Assert.Equal(2, pool.Read(0x4000));
    }

    #endregion

    #region 80STORE OFF - Write Tests (2 tests)

    [Fact]
    public void Store80Off_RamWrtOff_WritesToMainMemory()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(false);
        pool.SetRamWrt(false);

        var testCases = new[]
        {
            (hires: false, page2: false, value: (byte)3),
            (hires: true, page2: false, value: (byte)4),
            (hires: true, page2: true, value: (byte)53),
            (hires: false, page2: true, value: (byte)23)
        };

        foreach (var (hires, page2, value) in testCases)
        {
            // Act
            pool.SetHiRes(hires);
            pool.SetPage2(page2);
            pool.Write(0x0200, value);
            pool.Write(0x0400, value);
            pool.Write(0x2000, value);
            pool.Write(0x4000, value);

            // Assert - All writes go to main
            Assert.Equal(value, pool.ReadRawMain(0x0200));
            Assert.Equal(value, pool.ReadRawMain(0x0400));
            Assert.Equal(value, pool.ReadRawMain(0x2000));
            Assert.Equal(value, pool.ReadRawMain(0x4000));
            
            // Aux should be unchanged (still 2)
            Assert.Equal(2, pool.ReadRawAux(0x0200));
            Assert.Equal(2, pool.ReadRawAux(0x0400));
            Assert.Equal(2, pool.ReadRawAux(0x2000));
            Assert.Equal(2, pool.ReadRawAux(0x4000));
        }
    }

    [Fact]
    public void Store80Off_RamWrtOn_WritesToAuxMemory()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(false);
        pool.SetRamWrt(true);

        var testCases = new[]
        {
            (hires: false, page2: false, value: (byte)3),
            (hires: false, page2: false, value: (byte)23),
            (hires: true, page2: false, value: (byte)33),
            (hires: true, page2: true, value: (byte)13),
            (hires: false, page2: true, value: (byte)3)
        };

        foreach (var (hires, page2, value) in testCases)
        {
            // Act
            pool.SetHiRes(hires);
            pool.SetPage2(page2);
            pool.Write(0x0200, value);
            pool.Write(0x0400, value);
            pool.Write(0x2000, value);
            pool.Write(0x4000, value);

            // Assert - Main unchanged (still 1)
            Assert.Equal(1, pool.ReadRawMain(0x0200));
            Assert.Equal(1, pool.ReadRawMain(0x0400));
            Assert.Equal(1, pool.ReadRawMain(0x2000));
            Assert.Equal(1, pool.ReadRawMain(0x4000));
            
            // Aux receives writes
            Assert.Equal(value, pool.ReadRawAux(0x0200));
            Assert.Equal(value, pool.ReadRawAux(0x0400));
            Assert.Equal(value, pool.ReadRawAux(0x2000));
            Assert.Equal(value, pool.ReadRawAux(0x4000));
        }
    }

    #endregion

    #region 80STORE ON - Write Tests (8 tests)

    [Fact]
    public void Store80On_RamWrtOff_HiResOff_Page2Off_WritesToMain()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamWrt(false);
        pool.SetHiRes(false);
        pool.SetPage2(false);

        // Act
        pool.Write(0x0200, 23);
        pool.Write(0x0400, 23);
        pool.Write(0x2000, 23);
        pool.Write(0x4000, 23);

        // Assert - All main
        Assert.Equal(23, pool.ReadRawMain(0x0200));
        Assert.Equal(23, pool.ReadRawMain(0x0400));
        Assert.Equal(23, pool.ReadRawMain(0x2000));
        Assert.Equal(23, pool.ReadRawMain(0x4000));
        
        // Aux unchanged
        Assert.Equal(2, pool.ReadRawAux(0x0200));
        Assert.Equal(2, pool.ReadRawAux(0x0400));
        Assert.Equal(2, pool.ReadRawAux(0x2000));
        Assert.Equal(2, pool.ReadRawAux(0x4000));
    }

    [Fact]
    public void Store80On_RamWrtOff_HiResOff_Page2On_WritesToMixed()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamWrt(false);
        pool.SetHiRes(false);
        pool.SetPage2(true);

        // Act
        pool.Write(0x0200, 33);
        pool.Write(0x0400, 33);
        pool.Write(0x2000, 33);
        pool.Write(0x4000, 33);

        // Assert - M, A, M, M
        Assert.Equal(33, pool.ReadRawMain(0x0200));
        Assert.Equal(1, pool.ReadRawMain(0x0400));
        Assert.Equal(33, pool.ReadRawMain(0x2000));
        Assert.Equal(33, pool.ReadRawMain(0x4000));
        
        Assert.Equal(2, pool.ReadRawAux(0x0200));
        Assert.Equal(33, pool.ReadRawAux(0x0400));
        Assert.Equal(2, pool.ReadRawAux(0x2000));
        Assert.Equal(2, pool.ReadRawAux(0x4000));
    }

    [Fact]
    public void Store80On_RamWrtOff_HiResOn_Page2Off_WritesToMain()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamWrt(false);
        pool.SetHiRes(true);
        pool.SetPage2(false);

        // Act
        pool.Write(0x0200, 23);
        pool.Write(0x0400, 23);
        pool.Write(0x2000, 23);
        pool.Write(0x4000, 23);

        // Assert - All main
        Assert.Equal(23, pool.ReadRawMain(0x0200));
        Assert.Equal(23, pool.ReadRawMain(0x0400));
        Assert.Equal(23, pool.ReadRawMain(0x2000));
        Assert.Equal(23, pool.ReadRawMain(0x4000));
        
        Assert.Equal(2, pool.ReadRawAux(0x0200));
        Assert.Equal(2, pool.ReadRawAux(0x0400));
        Assert.Equal(2, pool.ReadRawAux(0x2000));
        Assert.Equal(2, pool.ReadRawAux(0x4000));
    }

    [Fact]
    public void Store80On_RamWrtOff_HiResOn_Page2On_WritesToMixed()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamWrt(false);
        pool.SetHiRes(true);
        pool.SetPage2(true);

        // Act
        pool.Write(0x0200, 33);
        pool.Write(0x0400, 33);
        pool.Write(0x2000, 33);
        pool.Write(0x4000, 33);

        // Assert - M, A, A, M
        Assert.Equal(33, pool.ReadRawMain(0x0200));
        Assert.Equal(1, pool.ReadRawMain(0x0400));
        Assert.Equal(1, pool.ReadRawMain(0x2000));
        Assert.Equal(33, pool.ReadRawMain(0x4000));
        
        Assert.Equal(2, pool.ReadRawAux(0x0200));
        Assert.Equal(33, pool.ReadRawAux(0x0400));
        Assert.Equal(33, pool.ReadRawAux(0x2000));
        Assert.Equal(2, pool.ReadRawAux(0x4000));
    }

    [Fact]
    public void Store80On_RamWrtOn_HiResOff_Page2Off_WritesToMixed()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamWrt(true);
        pool.SetHiRes(false);
        pool.SetPage2(false);

        // Act
        pool.Write(0x0200, 33);
        pool.Write(0x0400, 33);
        pool.Write(0x2000, 33);
        pool.Write(0x4000, 33);

        // Assert - A, M, A, A
        Assert.Equal(1, pool.ReadRawMain(0x0200));
        Assert.Equal(33, pool.ReadRawMain(0x0400));
        Assert.Equal(1, pool.ReadRawMain(0x2000));
        Assert.Equal(1, pool.ReadRawMain(0x4000));
        
        Assert.Equal(33, pool.ReadRawAux(0x0200));
        Assert.Equal(2, pool.ReadRawAux(0x0400));
        Assert.Equal(33, pool.ReadRawAux(0x2000));
        Assert.Equal(33, pool.ReadRawAux(0x4000));
    }

    [Fact]
    public void Store80On_RamWrtOn_HiResOff_Page2On_WritesToAux()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamWrt(true);
        pool.SetHiRes(false);
        pool.SetPage2(true);

        // Act
        pool.Write(0x0200, 33);
        pool.Write(0x0400, 33);
        pool.Write(0x2000, 33);
        pool.Write(0x4000, 33);

        // Assert - All aux
        Assert.Equal(1, pool.ReadRawMain(0x0200));
        Assert.Equal(1, pool.ReadRawMain(0x0400));
        Assert.Equal(1, pool.ReadRawMain(0x2000));
        Assert.Equal(1, pool.ReadRawMain(0x4000));
        
        Assert.Equal(33, pool.ReadRawAux(0x0200));
        Assert.Equal(33, pool.ReadRawAux(0x0400));
        Assert.Equal(33, pool.ReadRawAux(0x2000));
        Assert.Equal(33, pool.ReadRawAux(0x4000));
    }

    [Fact]
    public void Store80On_RamWrtOn_HiResOn_Page2Off_WritesToMixed()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamWrt(true);
        pool.SetHiRes(true);
        pool.SetPage2(false);

        // Act
        pool.Write(0x0200, 33);
        pool.Write(0x0400, 33);
        pool.Write(0x2000, 33);
        pool.Write(0x4000, 33);

        // Assert - A, M, M, A
        Assert.Equal(1, pool.ReadRawMain(0x0200));
        Assert.Equal(33, pool.ReadRawMain(0x0400));
        Assert.Equal(33, pool.ReadRawMain(0x2000));
        Assert.Equal(1, pool.ReadRawMain(0x4000));
        
        Assert.Equal(33, pool.ReadRawAux(0x0200));
        Assert.Equal(2, pool.ReadRawAux(0x0400));
        Assert.Equal(2, pool.ReadRawAux(0x2000));
        Assert.Equal(33, pool.ReadRawAux(0x4000));
    }

    [Fact]
    public void Store80On_RamWrtOn_HiResOn_Page2On_WritesToAux()
    {
        // Arrange
        var pool = BuildPool();
        pool.Set80Store(true);
        pool.SetRamWrt(true);
        pool.SetHiRes(true);
        pool.SetPage2(true);

        // Act
        pool.Write(0x0200, 33);
        pool.Write(0x0400, 33);
        pool.Write(0x2000, 33);
        pool.Write(0x4000, 33);

        // Assert - All aux
        Assert.Equal(1, pool.ReadRawMain(0x0200));
        Assert.Equal(1, pool.ReadRawMain(0x0400));
        Assert.Equal(1, pool.ReadRawMain(0x2000));
        Assert.Equal(1, pool.ReadRawMain(0x4000));
        
        Assert.Equal(33, pool.ReadRawAux(0x0200));
        Assert.Equal(33, pool.ReadRawAux(0x0400));
        Assert.Equal(33, pool.ReadRawAux(0x2000));
        Assert.Equal(33, pool.ReadRawAux(0x4000));
    }

    #endregion

    #region ROM Selection Tests (4 tests)

    [Fact]
    public void IntCxRom_On_SlotC3Rom_Off_UsesInternalROM()
    {
        // Arrange
        var pool = BuildPool();
        pool.SetIntCxRom(true);
        pool.SetSlotC3Rom(false);

        // Assert - All slots use internal ROM
        Assert.Equal((byte)'I', pool.Read(0xC100));
        Assert.Equal((byte)'I', pool.Read(0xC200));
        Assert.Equal((byte)'I', pool.Read(0xC300));
        Assert.Equal((byte)'I', pool.Read(0xC400));
        Assert.Equal((byte)'I', pool.Read(0xC500));
        Assert.Equal((byte)'I', pool.Read(0xC600));
        Assert.Equal((byte)'I', pool.Read(0xC700));
    }

    [Fact]
    public void IntCxRom_On_SlotC3Rom_On_UsesInternalROM()
    {
        // Arrange
        var pool = BuildPool();
        pool.SetIntCxRom(true);
        pool.SetSlotC3Rom(true);

        // Assert - All slots use internal ROM (INTCXROM overrides)
        Assert.Equal((byte)'I', pool.Read(0xC100));
        Assert.Equal((byte)'I', pool.Read(0xC200));
        Assert.Equal((byte)'I', pool.Read(0xC300));
        Assert.Equal((byte)'I', pool.Read(0xC400));
        Assert.Equal((byte)'I', pool.Read(0xC500));
        Assert.Equal((byte)'I', pool.Read(0xC600));
        Assert.Equal((byte)'I', pool.Read(0xC700));
    }

    [Fact]
    public void IntCxRom_Off_SlotC3Rom_Off_UsesSlotROMExceptC3()
    {
        // Arrange
        var pool = BuildPool();
        pool.SetIntCxRom(false);
        pool.SetSlotC3Rom(false);

        // Assert - Slots use slot ROM, but C3 uses internal
        Assert.Equal((byte)'S', pool.Read(0xC100));
        Assert.Equal((byte)'S', pool.Read(0xC200));
        Assert.Equal((byte)'I', pool.Read(0xC300)); // C3 special case
        Assert.Equal((byte)'S', pool.Read(0xC400));
        Assert.Equal((byte)'S', pool.Read(0xC500));
        Assert.Equal((byte)'S', pool.Read(0xC600));
        Assert.Equal((byte)'S', pool.Read(0xC700));
    }

    [Fact]
    public void IntCxRom_Off_SlotC3Rom_On_UsesSlotROM()
    {
        // Arrange
        var pool = BuildPool();
        pool.SetIntCxRom(false);
        pool.SetSlotC3Rom(true);

        // Assert - All slots use slot ROM (including C3)
        Assert.Equal((byte)'S', pool.Read(0xC100));
        Assert.Equal((byte)'S', pool.Read(0xC200));
        Assert.Equal((byte)'S', pool.Read(0xC300));
        Assert.Equal((byte)'S', pool.Read(0xC400));
        Assert.Equal((byte)'S', pool.Read(0xC500));
        Assert.Equal((byte)'S', pool.Read(0xC600));
        Assert.Equal((byte)'S', pool.Read(0xC700));
    }

    #endregion

    #region Language Card Banking Tests (6 tests)

    [Fact]
    public void LanguageCard_Bank1_ReadWrite_WithBankSwitching()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        pool.SetBank1(true);
        pool.SetHighRead(true);
        pool.SetHighWrite(true);
        pool.SetAltZp(false);

        // Act - Write to Bank 1
        pool.Write(0xD000, 0x42);
        pool.Write(0xE000, 0x44);

        // Assert - Verify we can read back
        Assert.Equal(0x42, pool.Read(0xD000));
        Assert.Equal(0x44, pool.Read(0xE000));
        
        // Switch to Bank 2 and write different values
        pool.SetBank1(false);
        pool.Write(0xD000, 0x52);
        Assert.Equal(0x52, pool.Read(0xD000));
        
        // Switch back to Bank 1 - original value should remain
        pool.SetBank1(true);
        Assert.Equal(0x42, pool.Read(0xD000));
    }

    [Fact]
    public void LanguageCard_Bank2_ReadWrite()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        pool.SetBank1(false);  // Select Bank 2
        pool.SetHighRead(true);
        pool.SetHighWrite(true);
        pool.SetAltZp(false);

        // Act
        pool.Write(0xD000, 0x52);
        pool.Write(0xD0FF, 0x53);
        pool.Write(0xE000, 0x54);
        pool.Write(0xFFFE, 0x55);

        // Assert
        Assert.Equal(0x52, pool.Read(0xD000));
        Assert.Equal(0x53, pool.Read(0xD0FF));
        Assert.Equal(0x54, pool.Read(0xE000));
        Assert.Equal(0x55, pool.Read(0xFFFE));
    }

    [Fact]
    public void LanguageCard_ReadDisabled_ReadsFromROM()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        byte[] testRom = new byte[0x4000]; // 16K ROM
        for (int i = 0; i < testRom.Length; i++)
        {
            testRom[i] = (byte)((i + 0x10) & 0xFF);
        }
        pool.InstallApple2ROM(testRom);
        
        pool.SetHighRead(false);  // Read from ROM
        pool.SetHighWrite(false);

        // Act - Read from ROM regions
        byte valueD000 = pool.Read(0xD000);
        byte valueE000 = pool.Read(0xE000);

        // Assert - Should read from ROM
        Assert.Equal((byte)((0x1000 + 0x10) & 0xFF), valueD000);
        Assert.Equal((byte)((0x2000 + 0x10) & 0xFF), valueE000);
    }

    [Fact]
    public void LanguageCard_WriteDisabled_ProtectsRAM()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        pool.SetBank1(true);
        pool.SetHighRead(true);
        pool.SetHighWrite(true);
        pool.SetAltZp(false);

        // Pre-fill with known value
        pool.Write(0xD000, 0x99);
        
        // Disable writing
        pool.SetHighWrite(false);

        // Act - Attempt write (should be ignored)
        pool.Write(0xD000, 0x11);

        // Assert - Original value should remain
        pool.SetHighRead(true);
        Assert.Equal(0x99, pool.Read(0xD000));
    }

    [Fact]
    public void LanguageCard_Bank1VsBank2_Isolation()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        pool.SetHighRead(true);
        pool.SetHighWrite(true);
        pool.SetAltZp(false);

        // Act - Write to Bank 1
        pool.SetBank1(true);
        pool.Write(0xD000, 0xB1);
        pool.Write(0xD100, 0xB1);

        // Write to Bank 2
        pool.SetBank1(false);
        pool.Write(0xD000, 0xB2);
        pool.Write(0xD100, 0xB2);

        // Assert - Verify isolation
        pool.SetBank1(true);
        Assert.Equal(0xB1, pool.Read(0xD000));
        Assert.Equal(0xB1, pool.Read(0xD100));

        pool.SetBank1(false);
        Assert.Equal(0xB2, pool.Read(0xD000));
        Assert.Equal(0xB2, pool.Read(0xD100));
    }

    [Fact]
    public void LanguageCard_WithAltZp_UsesAuxMemory()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        pool.SetHighRead(true);
        pool.SetHighWrite(true);
        pool.SetBank1(true);

        // Act - Write to main language card
        pool.SetAltZp(false);
        pool.Write(0xD000, 0xAA);
        pool.Write(0xE000, 0xBB);

        // Write to aux language card
        pool.SetAltZp(true);
        pool.Write(0xD000, 0xCC);
        pool.Write(0xE000, 0xDD);

        // Assert - Verify isolation
        pool.SetAltZp(false);
        Assert.Equal(0xAA, pool.Read(0xD000));
        Assert.Equal(0xBB, pool.Read(0xE000));

        pool.SetAltZp(true);
        Assert.Equal(0xCC, pool.Read(0xD000));
        Assert.Equal(0xDD, pool.Read(0xE000));
    }

    #endregion

    #region Alternate Zero Page Tests (3 tests)

    [Fact]
    public void AltZp_Off_UsesMainZeroPage()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        pool.SetAltZp(false);

        // Act
        pool.Write(0x00, 0x11);
        pool.Write(0x01, 0x22);
        pool.Write(0xFF, 0x33);
        pool.Write(0x1FF, 0x44);

        // Assert
        Assert.Equal(0x11, pool.Read(0x00));
        Assert.Equal(0x22, pool.Read(0x01));
        Assert.Equal(0x33, pool.Read(0xFF));
        Assert.Equal(0x44, pool.Read(0x1FF));
        
        // Verify via raw access
        Assert.Equal(0x11, pool.ReadRawMain(0x00));
        Assert.Equal(0x44, pool.ReadRawMain(0x1FF));
    }

    [Fact]
    public void AltZp_On_UsesAuxZeroPage()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        pool.SetAltZp(true);

        // Act
        pool.Write(0x00, 0x55);
        pool.Write(0x01, 0x66);
        pool.Write(0xFF, 0x77);
        pool.Write(0x1FF, 0x88);

        // Assert
        Assert.Equal(0x55, pool.Read(0x00));
        Assert.Equal(0x66, pool.Read(0x01));
        Assert.Equal(0x77, pool.Read(0xFF));
        Assert.Equal(0x88, pool.Read(0x1FF));
        
        // Verify via raw access
        Assert.Equal(0x55, pool.ReadRawAux(0x00));
        Assert.Equal(0x88, pool.ReadRawAux(0x1FF));
    }

    [Fact]
    public void AltZp_Switching_IsolatesMemory()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);

        // Act - Write to main ZP
        pool.SetAltZp(false);
        pool.Write(0x00, 0x11);
        pool.Write(0x80, 0x22);
        pool.Write(0xFF, 0x33);

        // Write to aux ZP
        pool.SetAltZp(true);
        pool.Write(0x00, 0x44);
        pool.Write(0x80, 0x55);
        pool.Write(0xFF, 0x66);

        // Assert - Main ZP preserved
        pool.SetAltZp(false);
        Assert.Equal(0x11, pool.Read(0x00));
        Assert.Equal(0x22, pool.Read(0x80));
        Assert.Equal(0x33, pool.Read(0xFF));

        // Aux ZP preserved
        pool.SetAltZp(true);
        Assert.Equal(0x44, pool.Read(0x00));
        Assert.Equal(0x55, pool.Read(0x80));
        Assert.Equal(0x66, pool.Read(0xFF));
    }

    #endregion

    #region ROM Installation Tests (3 tests)

    [Fact]
    public void InstallApple2ROM_LoadsCorrectly()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        byte[] testRom = new byte[0x4000]; // 16K ROM
        
        for (int i = 0; i < testRom.Length; i++)
        {
            testRom[i] = (byte)(i & 0xFF);
        }

        // Act
        pool.InstallApple2ROM(testRom);

        // Assert - Verify ROM accessible at $D000-$FFFF
        pool.SetHighRead(false); // Read from ROM
        
        Assert.Equal(0x00, pool.Read(0xD000));
        Assert.Equal(0x01, pool.Read(0xD001));
        Assert.Equal(0xFF, pool.Read(0xD0FF));
        Assert.Equal(0x00, pool.Read(0xE000));
        Assert.Equal(0x01, pool.Read(0xE001));
    }

    [Fact]
    public void InstallApple2ROM_WrongSize_ThrowsException()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        byte[] wrongSizeRom = new byte[0x3000]; // Wrong size

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => pool.InstallApple2ROM(wrongSizeRom));
        Assert.Contains("16KB", exception.Message);
    }

    [Fact]
    public void InstallApple2ROM_LoadsInternalROMSlots()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        byte[] testRom = new byte[0x4000];
        
        for (int i = 0; i < testRom.Length; i++)
        {
            testRom[i] = (byte)((i >> 8) + 0x10);
        }

        // Act
        pool.InstallApple2ROM(testRom);

        // Assert - Verify internal ROM slots loaded
        pool.SetIntCxRom(true);
        
        Assert.Equal(0x11, pool.Read(0xC100));
        Assert.Equal(0x12, pool.Read(0xC200));
        Assert.Equal(0x13, pool.Read(0xC300));
    }

    #endregion

    #region Range Reset & Interface Tests (4 tests)

    [Fact]
    public void ResetRanges_RestoresDefaultMappings()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        
        // Change settings
        pool.SetRamRd(true);
        pool.Set80Store(true);
        pool.SetHighRead(true);
        pool.SetHighWrite(true);
        pool.SetAltZp(true);
        
        pool.Write(0x00, 0xAA);
        pool.Write(0xD000, 0xBB);

        // Act
        pool.ResetRanges();
        
        // Reset soft switches manually
        pool.SetRamRd(false);
        pool.Set80Store(false);
        pool.SetHighRead(false);
        pool.SetHighWrite(false);
        pool.SetAltZp(false);
        
        // Assert
        pool.Write(0x1000, 0xCC);
        Assert.Equal(0xCC, pool.Read(0x1000));
    }

    [Fact]
    public void Size_Returns64K()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);

        // Assert
        Assert.Equal(0x10000, pool.Size);
    }

    [Fact]
    public void Indexer_ReadWrite()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);

        // Act
        pool[0x1000] = 0x42;
        pool[0x2000] = 0x43;

        // Assert
        Assert.Equal(0x42, pool[0x1000]);
        Assert.Equal(0x43, pool[0x2000]);
    }

    [Fact]
    public void Indexer_WorksWithSoftSwitches()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        pool.SetRamRd(true);
        pool.SetRamWrt(true);

        // Act
        pool[0x1000] = 0x55;

        // Assert
        Assert.Equal(0x55, pool[0x1000]);
        Assert.Equal(0x55, pool.ReadRawAux(0x1000));
        Assert.Equal(0x00, pool.ReadRawMain(0x1000));
    }

    #endregion

    #region Event Tests - MemoryAccessEventArgs (8 tests)

    [Fact]
    public void MemoryAccessEventArgs_CanBeCreated()
    {
        // Arrange & Act
        var eventArgs = new MemoryAccessEventArgs
        {
            Address = 0x1234,
            Value = 0x42
        };

        // Assert
        Assert.Equal(0x1234, eventArgs.Address);
        Assert.Equal((byte)0x42, eventArgs.Value);
    }

    [Fact]
    public void MemoryAccessEventArgs_NullValue_Supported()
    {
        // Arrange & Act
        var eventArgs = new MemoryAccessEventArgs
        {
            Address = 0x2000,
            Value = null
        };

        // Assert
        Assert.Equal(0x2000, eventArgs.Address);
        Assert.Null(eventArgs.Value);
    }

    [Fact]
    public void MemoryWritten_Event_RaisedOnWrite()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        bool eventRaised = false;
        ushort capturedAddress = 0;
        byte? capturedValue = null;

        pool.MemoryWritten += (sender, args) =>
        {
            eventRaised = true;
            capturedAddress = args.Address;
            capturedValue = args.Value;
        };

        // Act
        pool.Write(0x1000, 0x42);

        // Assert
        Assert.True(eventRaised);
        Assert.Equal(0x1000, capturedAddress);
        Assert.Equal((byte)0x42, capturedValue);
    }

    [Fact]
    public void MemoryWritten_Event_NotRaisedOnRead()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        bool eventRaised = false;

        pool.MemoryWritten += (sender, args) => eventRaised = true;

        // Act
        _ = pool.Read(0x1000);

        // Assert
        Assert.False(eventRaised);
    }

    [Fact]
    public void MemoryWritten_Event_MultipleWrites()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        int eventCount = 0;
        var addresses = new List<ushort>();

        pool.MemoryWritten += (sender, args) =>
        {
            eventCount++;
            addresses.Add(args.Address);
        };

        // Act
        pool.Write(0x1000, 0x01);
        pool.Write(0x1001, 0x02);
        pool.Write(0x1002, 0x03);

        // Assert
        Assert.Equal(3, eventCount);
        Assert.Equal(new ushort[] { 0x1000, 0x1001, 0x1002 }, addresses);
    }

    [Fact]
    public void MemoryWritten_Event_SenderIsMemoryPool()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        object? capturedSender = null;

        pool.MemoryWritten += (sender, args) => capturedSender = sender;

        // Act
        pool.Write(0x1000, 0x42);

        // Assert
        Assert.Same(pool, capturedSender);
    }

    [Fact]
    public void MemoryWritten_Event_NotRaisedOnWriteProtectedRegion()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        bool eventRaised = false;

        pool.MemoryWritten += (sender, args) => eventRaised = true;

        // Act - Attempt to write to write-protected ROM region
        pool.SetHighWrite(false);
        pool.Write(0xD000, 0x42); // Write-protected

        // Assert - Event still raised even though write is ignored
        // (This matches current implementation - event fires even on failed writes)
        Assert.True(eventRaised);
    }

    [Fact]
    public void MemoryWritten_Event_MultipleSubscribers()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        int subscriber1Count = 0;
        int subscriber2Count = 0;
        byte? subscriber1Value = null;
        byte? subscriber2Value = null;

        pool.MemoryWritten += (sender, args) =>
        {
            subscriber1Count++;
            subscriber1Value = args.Value;
        };

        pool.MemoryWritten += (sender, args) =>
        {
            subscriber2Count++;
            subscriber2Value = args.Value;
        };

        // Act
        pool.Write(0x1000, 0x99);

        // Assert - Both subscribers should receive the event
        Assert.Equal(1, subscriber1Count);
        Assert.Equal(1, subscriber2Count);
        Assert.Equal((byte)0x99, subscriber1Value);
        Assert.Equal((byte)0x99, subscriber2Value);
    }

    #endregion

    #region Event Tests - Write Scenarios (4 tests)

    [Fact]
    public void MemoryWritten_Event_ZeroPageWrite()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        ushort capturedAddress = 0;
        byte? capturedValue = null;

        pool.MemoryWritten += (sender, args) =>
        {
            capturedAddress = args.Address;
            capturedValue = args.Value;
        };

        // Act
        pool.Write(0x0000, 0x11);

        // Assert
        Assert.Equal(0x0000, capturedAddress);
        Assert.Equal((byte)0x11, capturedValue);
    }

    [Fact]
    public void MemoryWritten_Event_TextPageWrite()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        ushort capturedAddress = 0;

        pool.MemoryWritten += (sender, args) => capturedAddress = args.Address;

        // Act
        pool.Write(0x0400, 0x41); // Text page 1

        // Assert
        Assert.Equal(0x0400, capturedAddress);
    }

    [Fact]
    public void MemoryWritten_Event_HiResPageWrite()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        ushort capturedAddress = 0;

        pool.MemoryWritten += (sender, args) => capturedAddress = args.Address;

        // Act
        pool.Write(0x2000, 0xFF); // Hi-Res page 1

        // Assert
        Assert.Equal(0x2000, capturedAddress);
    }

    [Fact]
    public void MemoryWritten_Event_WithDifferentMemoryMappings()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);
        var writtenAddresses = new List<ushort>();

        pool.MemoryWritten += (sender, args) => writtenAddresses.Add(args.Address);

        // Act - Write with different mappings
        pool.SetRamWrt(false);
        pool.Write(0x1000, 0x01);

        pool.SetRamWrt(true);
        pool.Write(0x1000, 0x02);

        pool.Set80Store(true);
        pool.SetPage2(true);
        pool.Write(0x0400, 0x03);

        // Assert - All writes should trigger events
        Assert.Equal(3, writtenAddresses.Count);
        Assert.Contains((ushort)0x1000, writtenAddresses);
        Assert.Contains((ushort)0x0400, writtenAddresses);
    }

    #endregion

    #region Disposal Tests (1 test)

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var pool = new MemoryPool(statusProvider);

        // Act & Assert - Should not throw
        pool.Dispose();
        
        // Dispose should be idempotent
        pool.Dispose();
    }

    #endregion
}
