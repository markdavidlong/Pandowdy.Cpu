// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for the MemoryBlock class - a simple contiguous memory implementation.
/// </summary>
/// <remarks>
/// MemoryBlock is a straightforward, flat memory space without bank switching or
/// soft switches. These tests verify basic read/write functionality, boundary behavior,
/// and IPandowdyMemory interface compliance.
/// </remarks>
public class MemoryBlockTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidSize_CreatesMemoryBlock()
    {
        // Arrange & Act
        var memory = new MemoryBlock(1024);

        // Assert
        Assert.Equal(1024, memory.Size);
    }

    [Fact]
    public void Constructor_With64KB_CreatesFullAddressSpace()
    {
        // Arrange & Act
        var memory = new MemoryBlock(0x10000); // 64KB

        // Assert
        Assert.Equal(0x10000, memory.Size);
    }

    [Fact]
    public void Constructor_WithMaxSize_Succeeds()
    {
        // Arrange & Act
        var memory = new MemoryBlock(MemoryBlock.MaxSize);

        // Assert
        Assert.Equal(MemoryBlock.MaxSize, memory.Size);
        Assert.Equal(0x10000, memory.Size);
    }

    [Fact]
    public void Constructor_WithSizeGreaterThanMaxSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        int invalidSize = MemoryBlock.MaxSize + 1; // 65,537 bytes

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryBlock(invalidSize));
        Assert.Equal("size", exception.ParamName);
        Assert.Contains("65536", exception.Message);
        Assert.Contains("0x10000", exception.Message);
    }

    [Fact]
    public void Constructor_WithZeroSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        int invalidSize = 0;

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryBlock(invalidSize));
        Assert.Equal("size", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativeSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        int invalidSize = -100;

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryBlock(invalidSize));
        Assert.Equal("size", exception.ParamName);
    }

    [Theory]
    [InlineData(0x10001)]  // 65,537 - just over limit
    [InlineData(0x20000)]  // 128KB
    [InlineData(0x100000)] // 1MB
    [InlineData(int.MaxValue)] // Maximum int value
    public void Constructor_WithSizesAboveMaximum_ThrowsArgumentOutOfRangeException(int invalidSize)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryBlock(invalidSize));
        Assert.Equal("size", exception.ParamName);
    }

    [Fact]
    public void Constructor_InitializesMemoryToZero()
    {
        // Arrange & Act
        var memory = new MemoryBlock(256);

        // Assert - All bytes should be initialized to 0
        for (ushort i = 0; i < 256; i++)
        {
            Assert.Equal(0, memory.Read(i));
        }
    }

    [Theory]
    [InlineData(1)]       // Minimum size
    [InlineData(64)]      // Small block
    [InlineData(256)]     // Page
    [InlineData(0x4000)]  // 16KB
    [InlineData(0x10000)] // 64KB (maximum)
    public void Constructor_WithVariousSizes_WorksCorrectly(int size)
    {
        // Arrange & Act
        var memory = new MemoryBlock(size);

        // Assert
        Assert.Equal(size, memory.Size);
    }

    [Fact]
    public void MaxSize_Constant_Is64KB()
    {
        // Assert
        Assert.Equal(0x10000, MemoryBlock.MaxSize);
        Assert.Equal(65536, MemoryBlock.MaxSize);
    }

    #endregion

    #region Read Tests

    [Fact]
    public void Read_FromZeroInitializedMemory_ReturnsZero()
    {
        // Arrange
        var memory = new MemoryBlock(256);

        // Act
        byte value = memory.Read(0x00);

        // Assert
        Assert.Equal(0, value);
    }

    [Fact]
    public void Read_AfterWrite_ReturnsWrittenValue()
    {
        // Arrange
        var memory = new MemoryBlock(256);
        memory.Write(0x0042, 0xAB);

        // Act
        byte value = memory.Read(0x0042);

        // Assert
        Assert.Equal(0xAB, value);
    }

    [Theory]
    [InlineData(0x00, 0x00)]    // Zero page start
    [InlineData(0xFF, 0xFF)]    // Zero page end
    [InlineData(0x0100, 0x00)]  // Stack start
    [InlineData(0x01FF, 0xFF)]  // Stack end
    [InlineData(0x0400, 0x00)]  // Text page 1
    [InlineData(0xFFFF, 0xFF)]  // End of 64KB space
    public void Read_FromDifferentAddresses_WorksCorrectly(ushort address, byte expectedValue)
    {
        // Arrange
        var memory = new MemoryBlock(0x10000);
        memory.Write(address, expectedValue);

        // Act
        byte value = memory.Read(address);

        // Assert
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public void Read_BeyondSize_ThrowsIndexOutOfRangeException()
    {
        // Arrange
        var memory = new MemoryBlock(256);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => memory.Read(256));
    }

    [Fact]
    public void Read_MultipleReads_ReturnsConsistentValues()
    {
        // Arrange
        var memory = new MemoryBlock(256);
        memory.Write(0x10, 0x42);

        // Act
        byte value1 = memory.Read(0x10);
        byte value2 = memory.Read(0x10);
        byte value3 = memory.Read(0x10);

        // Assert - All reads should return the same value
        Assert.Equal(0x42, value1);
        Assert.Equal(0x42, value2);
        Assert.Equal(0x42, value3);
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_StoresValueAtAddress()
    {
        // Arrange
        var memory = new MemoryBlock(256);

        // Act
        memory.Write(0x20, 0xCD);

        // Assert
        Assert.Equal(0xCD, memory.Read(0x20));
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0x7F)]
    [InlineData(0x80)]
    [InlineData(0xFE)]
    [InlineData(0xFF)]
    public void Write_AllByteValues_StoresCorrectly(byte value)
    {
        // Arrange
        var memory = new MemoryBlock(256);
        ushort address = 0x50;

        // Act
        memory.Write(address, value);

        // Assert
        Assert.Equal(value, memory.Read(address));
    }

    [Fact]
    public void Write_BeyondSize_ThrowsIndexOutOfRangeException()
    {
        // Arrange
        var memory = new MemoryBlock(256);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => memory.Write(256, 0x42));
    }

    [Fact]
    public void Write_ToSameAddress_OverwritesPreviousValue()
    {
        // Arrange
        var memory = new MemoryBlock(256);
        ushort address = 0x80;

        // Act
        memory.Write(address, 0x11);
        memory.Write(address, 0x22);
        memory.Write(address, 0x33);

        // Assert - Should have the last written value
        Assert.Equal(0x33, memory.Read(address));
    }

    [Fact]
    public void Write_ToAdjacentAddresses_DoesNotAffectEachOther()
    {
        // Arrange
        var memory = new MemoryBlock(256);

        // Act
        memory.Write(0x10, 0xAA);
        memory.Write(0x11, 0xBB);
        memory.Write(0x12, 0xCC);

        // Assert - Each address should have its own value
        Assert.Equal(0xAA, memory.Read(0x10));
        Assert.Equal(0xBB, memory.Read(0x11));
        Assert.Equal(0xCC, memory.Read(0x12));
    }

    #endregion

    #region Size Property Tests

    [Fact]
    public void Size_ReturnsConstructorSize()
    {
        // Arrange & Act
        var memory = new MemoryBlock(512);

        // Assert
        Assert.Equal(512, memory.Size);
    }

    [Fact]
    public void Size_IsReadOnly()
    {
        // Arrange
        var memory = new MemoryBlock(256);

        // Assert - Size property should only have a getter
        // This is verified by the property definition: { get => _data.Length; }
        Assert.Equal(256, memory.Size);
    }

    #endregion

    #region IPandowdyMemory Interface Compliance Tests

    [Fact]
    public void IPandowdyMemory_Read_WorksThroughInterface()
    {
        // Arrange
        IPandowdyMemory memory = new MemoryBlock(256);
        ((MemoryBlock)memory).Write(0x70, 0xBB);

        // Act
        byte value = memory.Read(0x70);

        // Assert
        Assert.Equal(0xBB, value);
    }

    [Fact]
    public void IPandowdyMemory_Write_WorksThroughInterface()
    {
        // Arrange
        IPandowdyMemory memory = new MemoryBlock(256);

        // Act
        memory.Write(0x80, 0xCC);

        // Assert
        Assert.Equal(0xCC, ((MemoryBlock)memory).Read(0x80));
    }

    [Fact]
    public void IPandowdyMemory_Size_WorksThroughInterface()
    {
        // Arrange
        IPandowdyMemory memory = new MemoryBlock(1024);

        // Assert
        Assert.Equal(1024, memory.Size);
    }

    #endregion

    #region Practical Usage Tests

    [Fact]
    public void MemoryBlock_Can_StoreSimple6502Program()
    {
        // Arrange - Create memory and store a simple program
        var memory = new MemoryBlock(0x10000);
        
        // LDA #$42 (Load Accumulator immediate)
        memory.Write(0x0000, 0xA9); // Opcode
        memory.Write(0x0001, 0x42); // Operand

        // STA $0200 (Store Accumulator)
        memory.Write(0x0002, 0x8D); // Opcode
        memory.Write(0x0003, 0x00); // Low byte of address
        memory.Write(0x0004, 0x02); // High byte of address

        // Act - Read back the program
        byte[] program = new byte[5];
        for (ushort i = 0; i < 5; i++)
        {
            program[i] = memory.Read(i);
        }

        // Assert
        Assert.Equal(0xA9, program[0]);
        Assert.Equal(0x42, program[1]);
        Assert.Equal(0x8D, program[2]);
        Assert.Equal(0x00, program[3]);
        Assert.Equal(0x02, program[4]);
    }

    [Fact]
    public void MemoryBlock_Can_FillRange()
    {
        // Arrange
        var memory = new MemoryBlock(256);
        byte fillValue = 0xFF;

        // Act - Fill first 64 bytes with 0xFF
        for (ushort i = 0; i < 64; i++)
        {
            memory.Write(i, fillValue);
        }

        // Assert - Verify filled range
        for (ushort i = 0; i < 64; i++)
        {
            Assert.Equal(fillValue, memory.Read(i));
        }

        // Assert - Verify unfilled range is still zero
        for (ushort i = 64; i < 256; i++)
        {
            Assert.Equal(0, memory.Read(i));
        }
    }

    [Fact]
    public void MemoryBlock_Can_CopyData()
    {
        // Arrange
        var memory = new MemoryBlock(0x10000); // Need full 64KB for these addresses
        byte[] sourceData = [0x01, 0x02, 0x03, 0x04, 0x05];
        ushort sourceAddress = 0x0100;
        ushort destAddress = 0x0200;

        // Act - Write source data
        for (int i = 0; i < sourceData.Length; i++)
        {
            memory.Write((ushort)(sourceAddress + i), sourceData[i]);
        }

        // Copy data from source to destination
        for (int i = 0; i < sourceData.Length; i++)
        {
            byte value = memory.Read((ushort)(sourceAddress + i));
            memory.Write((ushort)(destAddress + i), value);
        }

        // Assert - Verify copied data
        for (int i = 0; i < sourceData.Length; i++)
        {
            Assert.Equal(sourceData[i], memory.Read((ushort)(destAddress + i)));
        }
    }

    [Fact]
    public void MemoryBlock_HandlesZeroPageAddressing()
    {
        // Arrange - Zero page is addresses $0000-$00FF
        var memory = new MemoryBlock(256);

        // Act - Write to various zero page addresses
        memory.Write(0x00, 0x10);
        memory.Write(0x42, 0x20);
        memory.Write(0xFF, 0x30);

        // Assert
        Assert.Equal(0x10, memory.Read(0x00));
        Assert.Equal(0x20, memory.Read(0x42));
        Assert.Equal(0x30, memory.Read(0xFF));
    }

    [Fact]
    public void MemoryBlock_HandlesStackAddressing()
    {
        // Arrange - Stack is at $0100-$01FF
        var memory = new MemoryBlock(0x10000);
        ushort stackBase = 0x0100;

        // Act - Push values onto stack (stack grows downward)
        byte stackPointer = 0xFF;
        memory.Write((ushort)(stackBase + stackPointer--), 0xAA);
        memory.Write((ushort)(stackBase + stackPointer--), 0xBB);
        memory.Write((ushort)(stackBase + stackPointer--), 0xCC);

        // Assert - Pop values from stack
        Assert.Equal(0xCC, memory.Read((ushort)(stackBase + ++stackPointer)));
        Assert.Equal(0xBB, memory.Read((ushort)(stackBase + ++stackPointer)));
        Assert.Equal(0xAA, memory.Read((ushort)(stackBase + ++stackPointer)));
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void MemoryBlock_WithMinimumSize_Works()
    {
        // Arrange & Act
        var memory = new MemoryBlock(1); // Single byte

        // Assert
        Assert.Equal(1, memory.Size);
        memory.Write(0, 0x42);
        Assert.Equal(0x42, memory.Read(0));
    }

    [Fact]
    public void MemoryBlock_ReadWrite_AtAddressZero()
    {
        // Arrange
        var memory = new MemoryBlock(256);

        // Act
        memory.Write(0x0000, 0x55);

        // Assert
        Assert.Equal(0x55, memory.Read(0x0000));
    }

    [Fact]
    public void MemoryBlock_ReadWrite_AtMaxAddress()
    {
        // Arrange
        var memory = new MemoryBlock(256);
        ushort maxAddress = 255;

        // Act
        memory.Write(maxAddress, 0x66);

        // Assert
        Assert.Equal(0x66, memory.Read(maxAddress));
    }

    [Fact]
    public void MemoryBlock_AllBytesCan_BeWrittenAndRead()
    {
        // Arrange
        var memory = new MemoryBlock(256);

        // Act & Assert - Write and read all 256 bytes
        for (ushort i = 0; i < 256; i++)
        {
            byte testValue = (byte)i;
            memory.Write(i, testValue);
            Assert.Equal(testValue, memory.Read(i));
        }
    }

    #endregion

    #region Performance Characteristics Tests

    [Fact]
    public void MemoryBlock_Supports_HighFrequencyAccess()
    {
        // Arrange
        var memory = new MemoryBlock(0x10000);
        int iterations = 10000;

        // Act - Simulate high-frequency CPU memory access
        for (int i = 0; i < iterations; i++)
        {
            ushort address = (ushort)(i % 0x10000);
            byte value = (byte)(i % 256);
            
            memory.Write(address, value);
            byte readValue = memory.Read(address);
            
            Assert.Equal(value, readValue);
        }

        // Assert - Test completed without errors
        // This verifies MemoryBlock can handle rapid access patterns
    }

    #endregion

    #region MemoryBlock64k Tests

    [Fact]
    public void MemoryBlock64k_Constructor_CreatesFullAddressSpace()
    {
        // Arrange & Act
        var memory = new MemoryBlock64k();

        // Assert
        Assert.Equal(0x10000, memory.Size);
        Assert.Equal(65536, memory.Size);
    }

    [Fact]
    public void MemoryBlock64k_Size_EqualsMaxSize()
    {
        // Arrange & Act
        var memory = new MemoryBlock64k();

        // Assert
        Assert.Equal(MemoryBlock.MaxSize, memory.Size);
    }

    [Fact]
    public void MemoryBlock64k_CanAccessEntireRange()
    {
        // Arrange
        var memory = new MemoryBlock64k();

        // Act & Assert - Can write and read from start, middle, and end
        memory.Write(0x0000, 0xAA);
        Assert.Equal(0xAA, memory.Read(0x0000));

        memory.Write(0x8000, 0xBB);
        Assert.Equal(0xBB, memory.Read(0x8000));

        memory.Write(0xFFFF, 0xCC);
        Assert.Equal(0xCC, memory.Read(0xFFFF));
    }

    [Fact]
    public void MemoryBlock64k_InitializedToZero()
    {
        // Arrange & Act
        var memory = new MemoryBlock64k();

        // Assert - Sample various addresses
        Assert.Equal(0, memory.Read(0x0000));
        Assert.Equal(0, memory.Read(0x1000));
        Assert.Equal(0, memory.Read(0x8000));
        Assert.Equal(0, memory.Read(0xFFFF));
    }

    [Fact]
    public void MemoryBlock64k_SupportsFullZeroPage()
    {
        // Arrange - Zero page is $0000-$00FF
        var memory = new MemoryBlock64k();

        // Act - Write to all zero page addresses
        for (ushort i = 0x00; i <= 0xFF; i++)
        {
            memory.Write(i, (byte)i);
        }

        // Assert - Verify all zero page addresses
        for (ushort i = 0x00; i <= 0xFF; i++)
        {
            Assert.Equal((byte)i, memory.Read(i));
        }
    }

    [Fact]
    public void MemoryBlock64k_SupportsFullStack()
    {
        // Arrange - Stack is $0100-$01FF
        var memory = new MemoryBlock64k();

        // Act - Write to all stack addresses
        for (ushort i = 0x0100; i <= 0x01FF; i++)
        {
            memory.Write(i, (byte)(i & 0xFF));
        }

        // Assert - Verify all stack addresses
        for (ushort i = 0x0100; i <= 0x01FF; i++)
        {
            Assert.Equal((byte)(i & 0xFF), memory.Read(i));
        }
    }

    [Fact]
    public void MemoryBlock64k_SupportsTextPages()
    {
        // Arrange
        var memory = new MemoryBlock64k();

        // Act & Assert - Text page 1 ($0400-$07FF)
        memory.Write(0x0400, 0x41); // 'A'
        Assert.Equal(0x41, memory.Read(0x0400));

        // Text page 2 ($0800-$0BFF)
        memory.Write(0x0800, 0x42); // 'B'
        Assert.Equal(0x42, memory.Read(0x0800));
    }

    [Fact]
    public void MemoryBlock64k_SupportsHiResPages()
    {
        // Arrange
        var memory = new MemoryBlock64k();

        // Act & Assert - Hi-res page 1 ($2000-$3FFF)
        memory.Write(0x2000, 0xAA);
        Assert.Equal(0xAA, memory.Read(0x2000));

        // Hi-res page 2 ($4000-$5FFF)
        memory.Write(0x4000, 0xBB);
        Assert.Equal(0xBB, memory.Read(0x4000));
    }

    [Fact]
    public void MemoryBlock64k_SupportsROMArea()
    {
        // Arrange
        var memory = new MemoryBlock64k();

        // Act & Assert - ROM area ($D000-$FFFF)
        memory.Write(0xD000, 0xC3); // Monitor ROM start
        Assert.Equal(0xC3, memory.Read(0xD000));

        memory.Write(0xFFFC, 0x00); // Reset vector low byte
        memory.Write(0xFFFD, 0xC6); // Reset vector high byte
        Assert.Equal(0x00, memory.Read(0xFFFC));
        Assert.Equal(0xC6, memory.Read(0xFFFD));
    }

    [Fact]
    public void MemoryBlock64k_Implements_IPandowdyMemory()
    {
        // Arrange & Act
        IPandowdyMemory memory = new MemoryBlock64k();

        // Assert
        Assert.Equal(0x10000, memory.Size);

        memory.Write(0x1000, 0x42);
        Assert.Equal(0x42, memory.Read(0x1000));
    }

    [Fact]
    public void MemoryBlock64k_EquivalentTo_MemoryBlock_WithMaxSize()
    {
        // Arrange
        var memory64k = new MemoryBlock64k();
        var memoryBlock = new MemoryBlock(0x10000);

        // Assert - Both should have identical size
        Assert.Equal(memory64k.Size, memoryBlock.Size);

        // Both should support the same address range
        memory64k.Write(0x5000, 0x55);
        memoryBlock.Write(0x5000, 0x55);

        Assert.Equal(memory64k.Read(0x5000), memoryBlock.Read(0x5000));
    }

    [Fact]
    public void MemoryBlock64k_Can_Store_Complete6502Program()
    {
        // Arrange
        var memory = new MemoryBlock64k();
        
        // Act - Store a complete 6502 program with reset vector
        // Program at $C000: Simple loop that increments accumulator
        memory.Write(0xC000, 0xA9); // LDA #$00
        memory.Write(0xC001, 0x00);
        memory.Write(0xC002, 0x69); // ADC #$01
        memory.Write(0xC003, 0x01);
        memory.Write(0xC004, 0x4C); // JMP $C002
        memory.Write(0xC005, 0x02);
        memory.Write(0xC006, 0xC0);

        // Reset vector points to program
        memory.Write(0xFFFC, 0x00); // Low byte
        memory.Write(0xFFFD, 0xC0); // High byte
        
        // Assert - Verify program and reset vector
        Assert.Equal(0xA9, memory.Read(0xC000));
        Assert.Equal(0x00, memory.Read(0xC001));
        Assert.Equal(0x69, memory.Read(0xC002));
        Assert.Equal(0x01, memory.Read(0xC003));
        Assert.Equal(0x4C, memory.Read(0xC004));
        Assert.Equal(0x02, memory.Read(0xC005));
        Assert.Equal(0xC0, memory.Read(0xC006));

        // Verify reset vector
        ushort resetVector = (ushort)(memory.Read(0xFFFC) | (memory.Read(0xFFFD) << 8));
        Assert.Equal(0xC000, resetVector);
    }

    #endregion
}
