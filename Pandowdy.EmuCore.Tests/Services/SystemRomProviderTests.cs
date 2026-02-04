// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for the SystemRomProvider class - Apple IIe system ROM management.
/// </summary>
public class SystemRomProviderTests
{
    private const string TestRomsDirectory = "TestRoms";
    private const int RomSize = 0x4000; // 16KB ($C000-$FFFF)

    #region Test Setup

    private static string GetTestRomPath(string filename) => 
        Path.Combine(TestRomsDirectory, filename);

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidRom_Succeeds()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");

        // Act
        var rom = new SystemRomProvider(romPath);

        // Assert
        Assert.NotNull(rom);
        Assert.Equal(RomSize, rom.Size);
    }

    [Fact]
    public void Constructor_WithResourcePrefix_LoadsFromResource()
    {
        // Arrange - The actual 16KB Apple IIe ROM embedded in EmuCore
        string resourceId = "res:Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom";

        // Act
        var rom = new SystemRomProvider(resourceId);

        // Assert
        Assert.NotNull(rom);
        Assert.Equal(RomSize, rom.Size);
        // Verify ROM contains expected reset vector at end (offset 0x3FFC-0x3FFD = $FFFC-$FFFD)
        byte vectorLo = rom[0x3FFC]; // $FFFC
        byte vectorHigh = rom[0x3FFD]; // $FFFD
        // Reset vector should point into Monitor/BASIC ROM area ($C000+)
        ushort resetVector = (ushort)(vectorLo | (vectorHigh << 8));
        Assert.True(resetVector >= 0xC000, $"Reset vector should be >= $C000, was ${resetVector:X4}");
    }

    [Fact]
    public void Constructor_WithNullFilename_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SystemRomProvider(null!));
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        string nonExistentPath = GetTestRomPath("nonexistent.rom");

        // Act & Assert
        var ex = Assert.Throws<FileNotFoundException>(
            () => new SystemRomProvider(nonExistentPath));
        Assert.Contains(nonExistentPath, ex.Message);
    }

    [Fact]
    public void Constructor_WithNonExistentResource_ThrowsFileNotFoundException()
    {
        // Arrange
        string nonExistentResource = "res:Pandowdy.EmuCore.Resources.DoesNotExist.rom";

        // Act & Assert
        var ex = Assert.Throws<FileNotFoundException>(
            () => new SystemRomProvider(nonExistentResource));
        Assert.Contains("DoesNotExist", ex.Message);
    }

    [Fact]
    public void Constructor_WithTooSmallRom_ThrowsInvalidDataException()
    {
        // Arrange
        string romPath = GetTestRomPath("toosmall.rom"); // 8KB

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(
            () => new SystemRomProvider(romPath));
        Assert.Contains("16384", ex.Message); // Expected 16KB
        Assert.Contains("0x4000", ex.Message);
        Assert.Contains("8192", ex.Message);  // Actual 8KB
        Assert.Contains("0x2000", ex.Message);
    }

    [Fact]
    public void Constructor_WithTooLargeRom_ThrowsInvalidDataException()
    {
        // Arrange
        string romPath = GetTestRomPath("toolarge.rom"); // 20KB (too large)

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(
            () => new SystemRomProvider(romPath));
        Assert.Contains("16384", ex.Message); // Expected 16KB
        Assert.Contains("0x4000", ex.Message);
        Assert.Contains("20480", ex.Message); // Actual 20KB
        Assert.Contains("0x5000", ex.Message);
    }

    [Fact]
    public void Constructor_LoadsRomData()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");

        // Act
        var rom = new SystemRomProvider(romPath);

        // Assert - Verify ROM data was loaded (valid16kb.rom has 0x00-0xFE pattern)
        Assert.Equal(0x00, rom.Read(0x0000)); // First byte
        Assert.Equal(0x01, rom.Read(0x0001)); // Second byte
        Assert.Equal(0xFE, rom.Read(0x00FE)); // Byte 254
        Assert.Equal(0x00, rom.Read(0x00FF)); // Byte 255 wraps to 0
        Assert.Equal(0x01, rom.Read(0x0100)); // Page 1 starts with 1
        Assert.Equal(0x10, rom.Read(0x1000)); // Page at 0x1000 starts with 0x10
        Assert.Equal(0x20, rom.Read(0x2000)); // Page at 0x2000 starts with 0x20
    }

    #endregion

    #region Size Property Tests

    [Fact]
    public void Size_Returns16KB()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act & Assert
        Assert.Equal(0x4000, rom.Size);
        Assert.Equal(16384, rom.Size);
    }

    #endregion

    #region Read Tests

    [Fact]
    public void Read_ReturnsCorrectData()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act & Assert - Verify pattern (0x00-0xFE repeating)
        for (ushort i = 0; i < 512; i++)
        {
            byte expected = (byte)(i % 255);
            Assert.Equal(expected, rom.Read(i));
        }
    }

    [Fact]
    public void Read_FirstByte_ReturnsCorrectValue()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act
        byte value = rom.Read(0x0000);

        // Assert
        Assert.Equal(0x00, value);
    }

    [Fact]
    public void Read_LastByte_ReturnsCorrectValue()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act
        byte value = rom.Read((ushort)(RomSize - 1));

        // Assert - 0x3FFF % 255 = 63 (0x3F)
        Assert.Equal(0x3F, value);
    }

    [Fact]
    public void Read_MultipleReads_ReturnsSameValue()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act
        byte value1 = rom.Read(0x1000);
        byte value2 = rom.Read(0x1000);
        byte value3 = rom.Read(0x1000);

        // Assert - All reads should be consistent (0x1000 % 255 = 16 = 0x10)
        Assert.Equal(0x10, value1);
        Assert.Equal(value1, value2);
        Assert.Equal(value2, value3);
    }

    [Theory]
    [InlineData(0x0000, 0x00)]  // Start ($C000)
    [InlineData(0x0100, 0x01)]  // Page 1 starts with 0x01
    [InlineData(0x1000, 0x10)]  // Monitor ROM start ($D000) - page starts with 0x10
    [InlineData(0x2000, 0x20)]  // BASIC ROM start ($E000) - page starts with 0x20
    [InlineData(0x3F00, 0x3F)]  // Near end - page starts with 0x3F
    [InlineData(0x3FFF, 0x3F)]  // End ($FFFF) - 0x3FFF % 255 = 63 = 0x3F
    public void Read_AtVariousAddresses_ReturnsExpectedPattern(ushort address, byte expected)
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act
        byte value = rom.Read(address);

        // Assert
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Read_PageBoundaries_HaveUniqueStartValues()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act & Assert - Each page (256 bytes) starts with a unique value
        // This helps catch off-by-page errors
        for (int page = 0; page < 64; page++) // 16KB = 64 pages
        {
            ushort pageStart = (ushort)(page * 0x100);
            byte expectedStart = (byte)(page % 255);
            byte actualStart = rom.Read(pageStart);
            
            Assert.Equal(expectedStart, actualStart);
            // Alternative with custom message if needed:
            // if (expectedStart != actualStart)
            //     Assert.Fail($"Page {page:X2} (offset 0x{pageStart:X4}) should start with 0x{expectedStart:X2} but got 0x{actualStart:X2}");
        }
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_IsNoOp()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);
        byte originalValue = rom.Read(0x0000);

        // Act
        rom.Write(0x0000, 0xFF);

        // Assert - ROM should remain unchanged
        Assert.Equal(originalValue, rom.Read(0x0000));
    }

    [Fact]
    public void Write_DoesNotThrow()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act & Assert - Should not throw
        rom.Write(0x1000, 0x42);
    }

    #endregion

    #region Indexer Tests

    [Fact]
    public void Indexer_Get_ReadsValue()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act
        byte value = rom[0x0500];

        // Assert - 0x0500 % 255 = 5
        Assert.Equal(0x05, value);
    }

    [Fact]
    public void Indexer_Set_IsNoOp()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);
        byte originalValue = rom[0x0500];

        // Act
        rom[0x0500] = 0xBB;

        // Assert - ROM unchanged
        Assert.Equal(originalValue, rom[0x0500]);
    }

    #endregion

    #region LoadRomFile Tests (Interface Method)

    [Fact]
    public void LoadRomFile_WithValidFile_ReloadsData()
    {
        // Arrange
        string romPath1 = GetTestRomPath("valid16kb.rom");
        string romPath2 = GetTestRomPath("valid16kb_ff.rom"); // All 0xFF

        var rom = new SystemRomProvider(romPath1) as Interfaces.ISystemRomProvider;
        Assert.Equal(0x00, rom.Read(0x0000)); // valid16kb starts with 0x00

        // Act
        rom.LoadRomFile(romPath2);

        // Assert - Should now have data from valid16kb_ff.rom
        Assert.Equal(0xFF, rom.Read(0x0000));
    }

    [Fact]
    public void LoadRomFile_WithNullFilename_ThrowsArgumentNullException()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath) as Interfaces.ISystemRomProvider;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => rom.LoadRomFile(null!));
    }

    [Fact]
    public void LoadRomFile_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath) as Interfaces.ISystemRomProvider;

        // Act & Assert
        Assert.Throws<FileNotFoundException>(
            () => rom.LoadRomFile("nonexistent.rom"));
    }

    [Fact]
    public void LoadRomFile_WithWrongSize_ThrowsInvalidDataException()
    {
        // Arrange
        string romPath1 = GetTestRomPath("valid16kb.rom"); // 16KB (correct)
        string romPath2 = GetTestRomPath("toosmall.rom"); // 8KB (wrong)
        var rom = new SystemRomProvider(romPath1) as Interfaces.ISystemRomProvider;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => rom.LoadRomFile(romPath2));
    }

    #endregion

    #region Apple IIe ROM Pattern Tests

    [Fact]
    public void Read_ResetVector_Location()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act - Read reset vector location
        byte vectorLo = rom[0x3FFC];   // $FFFC in ROM space (offset 0x3FFC)
        byte vectorHigh = rom[0x3FFD];  // $FFFD in ROM space (offset 0x3FFD)

        // Assert - Verify pattern (0x3FFC % 255 = 60 = 0x3C, 0x3FFD % 255 = 61 = 0x3D)
        Assert.Equal(0x3C, vectorLo);   // 16380 % 255 = 60 = 0x3C
        Assert.Equal(0x3D, vectorHigh); // 16381 % 255 = 61 = 0x3D
    }

    [Fact]
    public void Read_MonitorROM_Area()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act & Assert - Monitor ROM area ($D000 = offset 0x1000)
        // Should start with 0x10 (0x1000 % 255 = 16 = 0x10)
        byte value = rom[0x1000];
        Assert.Equal(0x10, value);
    }

    [Fact]
    public void Read_LanguageCardMapping_Offsets()
    {
        // Arrange
        string romPath = GetTestRomPath("valid16kb.rom");
        var rom = new SystemRomProvider(romPath);

        // Act & Assert - Verify Language Card uses correct offsets
        // Each page should have unique starting value for error detection
        
        // $D000 -> ROM offset 0x1000 (page 0x10)
        Assert.Equal(0x10, rom[0x1000]);
        
        // $E000 -> ROM offset 0x2000 (page 0x20)
        Assert.Equal(0x20, rom[0x2000]);
        
        // $FFFF -> ROM offset 0x3FFF (0x3FFF % 255 = 63 = 0x3F)
        Assert.Equal(0x3F, rom[0x3FFF]);
        
        // Verify different pages have different values
        Assert.NotEqual(rom[0x1000], rom[0x2000]);
        Assert.NotEqual(rom[0x2000], rom[0x3000]);
    }

    #endregion
}
