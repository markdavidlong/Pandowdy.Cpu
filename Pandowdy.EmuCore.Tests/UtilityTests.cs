using System;
using Xunit;
using Emulator;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for the Utility class - validation and helper methods.
/// </summary>
public class UtilityTests
{
    #region ValidateIMemorySize Tests

    [Fact]
    public void ValidateIMemorySize_WithCorrectSize_ReturnsMemory()
    {
        // Arrange
        var memory = new MemoryBlock(0x4000); // 16KB
        ushort expectedSize = 0x4000;

        // Act
        // Use ISystemRam to test the generic constraint works with derived interfaces
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        ISystemRam result = Utility.ValidateIMemorySize(memory, "testMemory", expectedSize);
#pragma warning restore CA1859

        // Assert
        Assert.NotNull(result);
        Assert.Same(memory, result);
        Assert.Equal(0x4000, result.Size);
    }

    [Fact]
    public void ValidateIMemorySize_WithNullMemory_ThrowsArgumentNullException()
    {
        // Arrange
        MemoryBlock? memory = null;
        ushort expectedSize = 0x4000;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(
            () => Utility.ValidateIMemorySize(memory!, "testMemory", expectedSize));
        Assert.Equal("testMemory", ex.ParamName);
    }

    [Fact]
    public void ValidateIMemorySize_WithWrongSize_ThrowsArgumentException()
    {
        // Arrange
        var memory = new MemoryBlock(0x1000); // 4KB
        ushort expectedSize = 0x4000; // Expecting 16KB

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => Utility.ValidateIMemorySize(memory, "testMemory", expectedSize));
        Assert.Equal("testMemory", ex.ParamName);
        Assert.Contains("16384", ex.Message); // Expected size in decimal
        Assert.Contains("0x4000", ex.Message); // Expected size in hex
        Assert.Contains("4096", ex.Message);   // Actual size in decimal
        Assert.Contains("0x1000", ex.Message); // Actual size in hex
    }

    [Theory]
    [InlineData(0x0001, 0x4000)]  // Too small: 1 byte vs 16KB
    [InlineData(0x1000, 0x4000)]  // Too small: 4KB vs 16KB
    [InlineData(0x2000, 0x4000)]  // Too small: 8KB vs 16KB
    [InlineData(0x8000, 0x4000)]  // Too large: 32KB vs 16KB
    [InlineData(0x4001, 0x4000)]  // Too large: 16KB+1 vs 16KB
    public void ValidateIMemorySize_WithVariousWrongSizes_ThrowsArgumentException(
        ushort actualSize, ushort expectedSize)
    {
        // Arrange
        var memory = new MemoryBlock(actualSize);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => Utility.ValidateIMemorySize(memory, "testParam", expectedSize));
        Assert.Contains(expectedSize.ToString(), ex.Message);
        Assert.Contains(actualSize.ToString(), ex.Message);
    }

    [Theory]
    [InlineData(0x0001)]  // 1 byte
    [InlineData(0x0100)]  // 256 bytes
    [InlineData(0x1000)]  // 4KB
    [InlineData(0x3000)]  // 12KB
    [InlineData(0x4000)]  // 16KB
    [InlineData(0x8000)]  // 32KB
    [InlineData(0xFFFF)]  // 64KB-1
    public void ValidateIMemorySize_WithMatchingSizes_Succeeds(ushort size)
    {
        // Arrange
        var memory = new MemoryBlock(size);

        // Act
        // Use ISystemRam to verify generic constraint works with derived interfaces
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        ISystemRam result = Utility.ValidateIMemorySize(memory, "testMemory", size);
#pragma warning restore CA1859

        // Assert
        Assert.NotNull(result);
        Assert.Equal(size, result.Size);
    }

    [Fact]
    public void ValidateIMemorySize_PreservesMemoryReference()
    {
        // Arrange
        var memory = new MemoryBlock(0x1000);
        memory[0x0100] = 0x42; // Set a value

        // Act
        // Use ISystemRam to verify reference preservation works with interface types
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        ISystemRam result = Utility.ValidateIMemorySize(memory, "testMemory", 0x1000);
#pragma warning restore CA1859

        // Assert - Should be same instance
        Assert.Same(memory, result);
        Assert.Equal(0x42, result[0x0100]); // Value should be preserved
    }

    [Fact]
    public void ValidateIMemorySize_UsesCorrectParameterNameInException()
    {
        // Arrange
        var memory = new MemoryBlock(0x1000);
        string paramName = "mainRam";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => Utility.ValidateIMemorySize(memory, paramName, 0x4000));
        Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void ValidateIMemorySize_ErrorMessageIncludesBothFormats()
    {
        // Arrange
        var memory = new MemoryBlock(0x1234); // 4660 bytes
        ushort expectedSize = 0x4000; // 16384 bytes

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => Utility.ValidateIMemorySize(memory, "test", expectedSize));
        
        // Verify both decimal and hex formats are present
        Assert.Contains("16384", ex.Message);  // Expected in decimal
        Assert.Contains("0x4000", ex.Message); // Expected in hex
        Assert.Contains("4660", ex.Message);   // Actual in decimal
        Assert.Contains("0x1234", ex.Message); // Actual in hex
    }

    #endregion

    #region Real-World Usage Tests

    [Fact]
    public void ValidateIMemorySize_LanguageCardMainRam_16KB()
    {
        // Arrange - Simulate LanguageCard validation
        var mainRam = new MemoryBlock(0x4000);

        // Act
        // Use ISystemRam to match LanguageCard's actual usage
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        ISystemRam result = Utility.ValidateIMemorySize(mainRam, nameof(mainRam), 0x4000);
#pragma warning restore CA1859

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0x4000, result.Size);
    }

    [Fact]
    public void ValidateIMemorySize_LanguageCardAuxRam_16KB()
    {
        // Arrange - Simulate LanguageCard validation
        var auxRam = new MemoryBlock(0x4000);

        // Act
        // Use ISystemRam to match LanguageCard's actual usage
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        ISystemRam result = Utility.ValidateIMemorySize(auxRam, nameof(auxRam), 0x4000);
#pragma warning restore CA1859

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0x4000, result.Size);
    }

    [Fact]
    public void ValidateIMemorySize_SystemRom_16KB()
    {
        // Arrange - Simulate SystemRomProvider validation (now 16KB, not 12KB)
        var systemRom = new MemoryBlock(0x4000);

        // Act
        // Use IMemory to test base interface compatibility
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        IMemory result = Utility.ValidateIMemorySize(systemRom, nameof(systemRom), 0x4000);
#pragma warning restore CA1859

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0x4000, result.Size);
    }

    [Fact]
    public void ValidateIMemorySize_FullMemoryPool_64KB()
    {
        // Arrange - Note: MemoryBlock.MaxSize is 0x10000 (64KB), but ushort max is 0xFFFF
        // So we test with the largest valid ushort value
        var memoryPool = new MemoryBlock(0xFFFF);

        // Act
        // Use ISystemRam to test with derived interface
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        ISystemRam result = Utility.ValidateIMemorySize(memoryPool, nameof(memoryPool), 0xFFFF);
#pragma warning restore CA1859

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0xFFFF, result.Size);
    }

    [Fact]
    public void ValidateIMemorySize_ChainedValidation()
    {
        // Arrange - Test that validation can be chained
        var memory = new MemoryBlock(0x2000);

        // Act - Chain validation result directly
        var validated = Utility.ValidateIMemorySize(
            Utility.ValidateIMemorySize(memory, "param1", 0x2000),
            "param2",
            0x2000);

        // Assert
        Assert.Same(memory, validated);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void ValidateIMemorySize_WithMinimumSize_Succeeds()
    {
        // Arrange
        var memory = new MemoryBlock(1); // Minimum size
        ushort expectedSize = 1;

        // Act
        var result = Utility.ValidateIMemorySize(memory, "testMemory", expectedSize);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Size);
    }

    [Fact]
    public void ValidateIMemorySize_WithNearMaximumSize_Succeeds()
    {
        // Arrange - Test with maximum ushort value
        var memory = new MemoryBlock(0xFFFF); // 64KB-1
        ushort expectedSize = 0xFFFF;

        // Act
        var result = Utility.ValidateIMemorySize(memory, "testMemory", expectedSize);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0xFFFF, result.Size);
    }

    [Fact]
    public void ValidateIMemorySize_OffByOne_Upper_Fails()
    {
        // Arrange
        var memory = new MemoryBlock(0x4001); // 16KB + 1
        ushort expectedSize = 0x4000;

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => Utility.ValidateIMemorySize(memory, "testMemory", expectedSize));
    }

    [Fact]
    public void ValidateIMemorySize_OffByOne_Lower_Fails()
    {
        // Arrange
        var memory = new MemoryBlock(0x3FFF); // 16KB - 1
        ushort expectedSize = 0x4000;

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => Utility.ValidateIMemorySize(memory, "testMemory", expectedSize));
    }

    #endregion

    #region Documentation Tests

    [Fact]
    public void ValidateIMemorySize_DocumentsLanguageCardUsage()
    {
        // This test documents how LanguageCard uses ValidateIMemorySize
        
        // Arrange - Simulating LanguageCard constructor
        var mainRam = new MemoryBlock(0x4000);
        var auxRam = new MemoryBlock(0x4000);
        var systemRom = new MemoryBlock(0x4000);

        // Act - Validation as done in LanguageCard (using interface types)
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        ISystemRam validatedMain = Utility.ValidateIMemorySize(mainRam, "mainRam", 0x4000);
        ISystemRam validatedAux = Utility.ValidateIMemorySize(auxRam, "auxRam", 0x4000);
        IMemory validatedRom = Utility.ValidateIMemorySize(systemRom, "systemRom", 0x4000);
#pragma warning restore CA1859

        // Assert
        Assert.Equal(0x4000, validatedMain.Size);
        Assert.Equal(0x4000, validatedAux.Size);
        Assert.Equal(0x4000, validatedRom.Size);
    }

    [Fact]
    public void ValidateIMemorySize_DocumentsErrorMessage()
    {
        // This test documents the error message format
        
        // Arrange
        var memory = new MemoryBlock(0x2000); // 8KB
        ushort expectedSize = 0x4000; // Expecting 16KB

        // Act
        try
        {
            Utility.ValidateIMemorySize(memory, "exampleParameter", expectedSize);
            Assert.Fail("Should have thrown ArgumentException");
        }
        catch (ArgumentException ex)
        {
            // Assert - Document expected error message format
            Assert.Equal("exampleParameter", ex.ParamName);
            Assert.Contains("Memory size must be exactly", ex.Message);
            Assert.Contains("16384 bytes", ex.Message);
            Assert.Contains("0x4000", ex.Message);
            Assert.Contains("Actual size: 8192", ex.Message);
            Assert.Contains("0x2000", ex.Message);
        }
    }

    #endregion
}
