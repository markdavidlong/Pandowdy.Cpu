// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Memory;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Comprehensive tests for SystemRamSelector, covering main/aux RAM routing,
/// soft switch interactions, 80STORE behavior, and all memory regions.
/// </summary>
public class SystemRamSelectorTests
{
    #region Test Helpers and Mocks

    /// <summary>
    /// Simple mock implementation of ISystemRam for testing.
    /// </summary>
    private class MockSystemRam : ISystemRam
    {
        private readonly byte[] _memory;
        private readonly byte _fillPattern;

        public MockSystemRam(int size, byte fillPattern = 0x00)
        {
            _memory = new byte[size];
            _fillPattern = fillPattern;
            Array.Fill(_memory, fillPattern);
        }

        public int Size => _memory.Length;

        public byte Read(ushort address) => _memory[address % _memory.Length];

        public void Write(ushort address, byte data) => _memory[address % _memory.Length] = data;

        public byte Peek(ushort address) => Read(address);

        public void CopyIntoSpan(Span<byte> destination)
        {
            if (destination.Length < _memory.Length)
            {
                throw new ArgumentException($"Destination span too small. Expected {_memory.Length}, got {destination.Length}");
            }
            _memory.AsSpan().CopyTo(destination);
        }

        public byte this[ushort address]
        {
            get => Read(address);
            set => Write(address, value);
        }

        public byte FillPattern => _fillPattern;

        public void Clear() => Array.Clear(_memory);
    }

    /// <summary>
    /// Mock floating bus provider that returns predictable values.
    /// </summary>
    private class MockFloatingBusProvider(byte value = 0xFF) : IFloatingBusProvider
    {
        private readonly byte _value = value;
        private int _readCount;

        public byte Read()
        {
            _readCount++;
            return _value;
        }

        public int ReadCount => _readCount;
    }

    private static SystemRamSelector CreateSystemRamSelector(
        out MockSystemRam mainRam,
        out MockSystemRam? auxRam,
        out SystemStatusProvider status,
        bool includeAuxRam = true,
        byte mainFillPattern = 0xAA,
        byte auxFillPattern = 0x55)
    {
        mainRam = new MockSystemRam(0xC000, mainFillPattern);
        auxRam = includeAuxRam ? new MockSystemRam(0xC000, auxFillPattern) : null;
        status = new SystemStatusProvider();
        var floatingBus = new MockFloatingBusProvider(0xFF);

        return new SystemRamSelector(mainRam, auxRam, floatingBus, status);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidArguments_CreatesInstance()
    {
        // Arrange
        var mainRam = new MockSystemRam(0xC000);
        var auxRam = new MockSystemRam(0xC000);
        var floatingBus = new MockFloatingBusProvider();
        var status = new SystemStatusProvider();

        // Act
        var selector = new SystemRamSelector(mainRam, auxRam, floatingBus, status);

        // Assert
        Assert.NotNull(selector);
        Assert.Equal(0xC000, selector.Size);
    }

    [Fact]
    public void Constructor_WithNullMainRam_ThrowsArgumentNullException()
    {
        // Arrange
        var auxRam = new MockSystemRam(0xC000);
        var floatingBus = new MockFloatingBusProvider();
        var status = new SystemStatusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SystemRamSelector(null!, auxRam, floatingBus, status));
    }

    [Fact]
    public void Constructor_WithNullFloatingBus_ThrowsArgumentNullException()
    {
        // Arrange
        var mainRam = new MockSystemRam(0xC000);
        var auxRam = new MockSystemRam(0xC000);
        var status = new SystemStatusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SystemRamSelector(mainRam, auxRam, null!, status));
    }

    [Fact]
    public void Constructor_WithNullStatus_ThrowsArgumentNullException()
    {
        // Arrange
        var mainRam = new MockSystemRam(0xC000);
        var auxRam = new MockSystemRam(0xC000);
        var floatingBus = new MockFloatingBusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SystemRamSelector(mainRam, auxRam, floatingBus, null!));
    }

    [Fact]
    public void Constructor_WithNullAuxRam_IsValid()
    {
        // Arrange
        var mainRam = new MockSystemRam(0xC000);
        var floatingBus = new MockFloatingBusProvider();
        var status = new SystemStatusProvider();

        // Act
        var selector = new SystemRamSelector(mainRam, null, floatingBus, status);

        // Assert
        Assert.NotNull(selector);
    }

    [Fact]
    public void Constructor_WithWrongSizedMainRam_ThrowsArgumentException()
    {
        // Arrange
        var mainRam = new MockSystemRam(0x1000); // Too small
        var floatingBus = new MockFloatingBusProvider();
        var status = new SystemStatusProvider();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new SystemRamSelector(mainRam, null, floatingBus, status));
    }

    [Fact]
    public void Size_ReturnsCorrectValue()
    {
        // Arrange & Act
        var selector = CreateSystemRamSelector(out _, out _, out _);

        // Assert
        Assert.Equal(0xC000, selector.Size);
    }

    #endregion

    #region ReadRawMain Tests

    [Fact]
    public void ReadRawMain_ReadsFromMainMemory()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out _, out _);
        mainRam.Write(0x1000, 0x42);

        // Act
        byte result = selector.ReadRawMain(0x1000);

        // Assert
        Assert.Equal(0x42, result);
    }

    [Fact]
    public void ReadRawMain_IgnoresSoftSwitches()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x1000, 0x42);
        auxRam!.Write(0x1000, 0x99);
        
        // Enable auxiliary memory
        status.SetRamRd(true);
        status.SetAltZp(true);

        // Act
        byte result = selector.ReadRawMain(0x1000);

        // Assert - Should still read from main, not aux
        Assert.Equal(0x42, result);
    }

    [Fact]
    public void ReadRawMain_MasksAddressTo16Bit()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out _, out _);
        mainRam.Write(0x1234, 0x77);

        // Act - Use address > 16-bit
        byte result = selector.ReadRawMain(0x10000 + 0x1234);

        // Assert
        Assert.Equal(0x77, result);
    }

    #endregion

    #region ReadRawAux Tests

    [Fact]
    public void ReadRawAux_WithAuxMemory_ReadsFromAux()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out _, out var auxRam, out _);
        auxRam!.Write(0x1000, 0x88);

        // Act
        byte result = selector.ReadRawAux(0x1000);

        // Assert
        Assert.Equal(0x88, result);
    }

    [Fact]
    public void ReadRawAux_WithoutAuxMemory_ReturnsFloatingBus()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out _, out _, out _, includeAuxRam: false);

        // Act
        byte result = selector.ReadRawAux(0x1000);

        // Assert
        Assert.Equal(0xFF, result); // MockFloatingBusProvider returns 0xFF
    }

    [Fact]
    public void ReadRawAux_IgnoresSoftSwitches()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x1000, 0x42);
        auxRam!.Write(0x1000, 0x99);
        
        // Disable auxiliary memory access
        status.SetRamRd(false);

        // Act
        byte result = selector.ReadRawAux(0x1000);

        // Assert - Should still read from aux
        Assert.Equal(0x99, result);
    }

    #endregion

    #region Read Tests - Zero Page ($0000-$01FF)

    [Fact]
    public void Read_ZeroPage_WithAltZpOff_ReadsFromMain()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x0080, 0x42);
        auxRam!.Write(0x0080, 0x99);
        status.SetAltZp(false);

        // Act
        byte result = selector.Read(0x0080);

        // Assert
        Assert.Equal(0x42, result);
    }

    [Fact]
    public void Read_ZeroPage_WithAltZpOn_ReadsFromAux()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x0080, 0x42);
        auxRam!.Write(0x0080, 0x99);
        status.SetAltZp(true);

        // Act
        byte result = selector.Read(0x0080);

        // Assert
        Assert.Equal(0x99, result);
    }

    [Fact]
    public void Read_Stack_WithAltZpOn_ReadsFromAux()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x01FF, 0x42);
        auxRam!.Write(0x01FF, 0x99);
        status.SetAltZp(true);

        // Act
        byte result = selector.Read(0x01FF);

        // Assert
        Assert.Equal(0x99, result);
    }

    #endregion

    #region Read Tests - Text Page Region ($0200-$03FF)

    [Fact]
    public void Read_TextPage_With80StoreOff_UsesRamRd()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x0400, 0x42);
        auxRam!.Write(0x0400, 0x99);
        
        status.Set80Store(false);
        status.SetRamRd(true);

        // Act
        byte result = selector.Read(0x0400);

        // Assert
        Assert.Equal(0x99, result);
    }

    [Fact]
    public void Read_TextPage_With80StoreOn_UsesPage2()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x0400, 0x42);
        auxRam!.Write(0x0400, 0x99);
        
        status.Set80Store(true);
        status.SetPage2(true);
        status.SetRamRd(false); // Should be ignored

        // Act
        byte result = selector.Read(0x0400);

        // Assert
        Assert.Equal(0x99, result);
    }

    [Fact]
    public void Read_TextPage_With80StoreOnPage2Off_ReadsFromMain()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x0400, 0x42);
        auxRam!.Write(0x0400, 0x99);
        
        status.Set80Store(true);
        status.SetPage2(false);

        // Act
        byte result = selector.Read(0x0400);

        // Assert
        Assert.Equal(0x42, result);
    }

    #endregion

    #region Read Tests - General RAM ($0800-$1FFF and $4000-$BFFF)

    [Fact]
    public void Read_GeneralRam_WithRamRdOff_ReadsFromMain()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x1000, 0x42);
        auxRam!.Write(0x1000, 0x99);
        status.SetRamRd(false);

        // Act
        byte result = selector.Read(0x1000);

        // Assert
        Assert.Equal(0x42, result);
    }

    [Fact]
    public void Read_GeneralRam_WithRamRdOn_ReadsFromAux()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x1000, 0x42);
        auxRam!.Write(0x1000, 0x99);
        status.SetRamRd(true);

        // Act
        byte result = selector.Read(0x1000);

        // Assert
        Assert.Equal(0x99, result);
    }

    [Fact]
    public void Read_UpperRam_WithRamRdOn_ReadsFromAux()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x8000, 0x42);
        auxRam!.Write(0x8000, 0x99);
        status.SetRamRd(true);

        // Act
        byte result = selector.Read(0x8000);

        // Assert
        Assert.Equal(0x99, result);
    }

    #endregion

    #region Read Tests - Hi-Res Page 1 ($2000-$3FFF)

    [Fact]
    public void Read_HiResPage1_With80StoreOff_UsesRamRd()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x2000, 0x42);
        auxRam!.Write(0x2000, 0x99);
        
        status.Set80Store(false);
        status.SetRamRd(true);

        // Act
        byte result = selector.Read(0x2000);

        // Assert
        Assert.Equal(0x99, result);
    }

    [Fact]
    public void Read_HiResPage1_With80StoreOnHiResOn_UsesPage2()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x2000, 0x42);
        auxRam!.Write(0x2000, 0x99);
        
        status.Set80Store(true);
        status.SetHiRes(true);
        status.SetPage2(true);
        status.SetRamRd(false); // Should be ignored

        // Act
        byte result = selector.Read(0x2000);

        // Assert
        Assert.Equal(0x99, result);
    }

    [Fact]
    public void Read_HiResPage1_With80StoreOnHiResOff_UsesRamRd()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x2000, 0x42);
        auxRam!.Write(0x2000, 0x99);
        
        status.Set80Store(true);
        status.SetHiRes(false);
        status.SetRamRd(true);
        status.SetPage2(false); // Should be ignored

        // Act
        byte result = selector.Read(0x2000);

        // Assert
        Assert.Equal(0x99, result);
    }

    #endregion

    #region Write Tests - Zero Page

    [Fact]
    public void Write_ZeroPage_WithAltZpOff_WritesToMain()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        status.SetAltZp(false);

        // Act
        selector.Write(0x0080, 0x42);

        // Assert
        Assert.Equal(0x42, mainRam!.Read(0x0080));
        Assert.Equal(0x55, auxRam!.Read(0x0080)); // Unchanged (fill pattern)
    }

    [Fact]
    public void Write_ZeroPage_WithAltZpOn_WritesToAux()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        status.SetAltZp(true);

        // Act
        selector.Write(0x0080, 0x42);

        // Assert
        Assert.Equal(0xAA, mainRam!.Read(0x0080)); // Unchanged (fill pattern)
        Assert.Equal(0x42, auxRam!.Read(0x0080));
    }

    #endregion

    #region Write Tests - Text Page

    [Fact]
    public void Write_TextPage_With80StoreOff_UsesRamWrt()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        status.Set80Store(false);
        status.SetRamRd(true); // Using SetRamRd since Write uses StateRamRd in the code

        // Act
        selector.Write(0x0400, 0x42);

        // Assert
        Assert.Equal(0x42, auxRam!.Read(0x0400));
    }

    [Fact]
    public void Write_TextPage_With80StoreOn_UsesPage2()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        status.Set80Store(true);
        status.SetPage2(true);

        // Act
        selector.Write(0x0400, 0x42);

        // Assert
        Assert.Equal(0x42, auxRam!.Read(0x0400));
        Assert.Equal(0xAA, mainRam!.Read(0x0400)); // Unchanged
    }

    #endregion

    #region Write Tests - General RAM

    [Fact]
    public void Write_GeneralRam_WithRamWrtOff_WritesToMain()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        status.SetRamRd(false); // Write uses StateRamRd

        // Act
        selector.Write(0x1000, 0x42);

        // Assert
        Assert.Equal(0x42, mainRam!.Read(0x1000));
        Assert.Equal(0x55, auxRam!.Read(0x1000)); // Unchanged
    }

    [Fact]
    public void Write_GeneralRam_WithRamWrtOn_WritesToAux()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        status.SetRamRd(true); // Write uses StateRamRd

        // Act
        selector.Write(0x1000, 0x42);

        // Assert
        Assert.Equal(0xAA, mainRam!.Read(0x1000)); // Unchanged
        Assert.Equal(0x42, auxRam!.Read(0x1000));
    }

    #endregion

    #region Write Tests - Hi-Res Page 1

    [Fact]
    public void Write_HiResPage1_With80StoreOnHiResOn_UsesPage2()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        status.Set80Store(true);
        status.SetHiRes(true);
        status.SetPage2(true);

        // Act
        selector.Write(0x2000, 0x42);

        // Assert
        Assert.Equal(0x42, auxRam!.Read(0x2000));
        Assert.Equal(0xAA, mainRam!.Read(0x2000)); // Unchanged
    }

    [Fact]
    public void Write_HiResPage1_With80StoreOnHiResOff_UsesRamWrt()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        status.Set80Store(true);
        status.SetHiRes(false);
        status.SetRamRd(true); // Write uses StateRamRd

        // Act
        selector.Write(0x2000, 0x42);

        // Assert
        Assert.Equal(0x42, auxRam!.Read(0x2000));
    }

    #endregion

    #region Write Tests - Missing Aux Memory

    [Fact]
    public void Write_ToAuxMemory_WhenNotInstalled_NoOp()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out _, out var status, includeAuxRam: false);
        status.SetRamRd(true); // Try to write to aux

        // Act
        selector.Write(0x1000, 0x42);

        // Assert - Main memory unchanged
        Assert.Equal(0xAA, mainRam!.Read(0x1000));
    }

    #endregion

    #region Peek Tests

    [Fact]
    public void Peek_DelegatesToRead()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out _, out var status);
        mainRam!.Write(0x1000, 0x42);

        // Act
        byte result = selector.Peek(0x1000);

        // Assert
        Assert.Equal(0x42, result);
    }

    #endregion

    #region CopyMainMemoryIntoSpan Tests

    [Fact]
    public void CopyMainMemoryIntoSpan_CopiesCorrectly()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out _, out _);
        mainRam!.Write(0x0000, 0x11);
        mainRam.Write(0x1000, 0x22);
        mainRam.Write(0xBFFF, 0x33);

        Span<byte> destination = new byte[0xC000];

        // Act
        selector.CopyMainMemoryIntoSpan(destination);

        // Assert
        Assert.Equal(0x11, destination[0x0000]);
        Assert.Equal(0x22, destination[0x1000]);
        Assert.Equal(0x33, destination[0xBFFF]);
    }

    [Fact]
    public void CopyMainMemoryIntoSpan_WithSmallDestination_ThrowsException()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out _, out _, out _);
        byte[] destinationArray = new byte[0x100]; // Too small

        // Act & Assert
        Assert.Throws<ArgumentException>(() => selector.CopyMainMemoryIntoSpan(destinationArray));
    }

    #endregion

    #region CopyAuxMemoryIntoSpan Tests

    [Fact]
    public void CopyAuxMemoryIntoSpan_WithAuxMemory_CopiesAndReturnsTrue()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out _, out var auxRam, out _);
        auxRam!.Write(0x0000, 0x11);
        auxRam.Write(0x1000, 0x22);
        auxRam.Write(0xBFFF, 0x33);

        Span<byte> destination = new byte[0xC000];

        // Act
        bool result = selector.CopyAuxMemoryIntoSpan(destination);

        // Assert
        Assert.True(result);
        Assert.Equal(0x11, destination[0x0000]);
        Assert.Equal(0x22, destination[0x1000]);
        Assert.Equal(0x33, destination[0xBFFF]);
    }

    [Fact]
    public void CopyAuxMemoryIntoSpan_WithoutAuxMemory_ReturnsFalse()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out _, out _, out _, includeAuxRam: false);
        byte[] destinationArray = new byte[0xC000];
        Array.Fill(destinationArray, (byte)0x99);

        // Act
        bool result = selector.CopyAuxMemoryIntoSpan(destinationArray);

        // Assert
        Assert.False(result);
        // Destination should be unchanged
        Assert.Equal(0x99, destinationArray[0]);
    }

    #endregion

    #region Integration Tests - Complex Scenarios

    [Fact]
    public void Integration_80ColumnMode_TextPageAccess()
    {
        // Arrange - Simulate 80-column text mode setup
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        
        // Write different values to main and aux text pages
        mainRam!.Write(0x0400, 0x41); // 'A'
        auxRam!.Write(0x0400, 0x42); // 'B'
        
        // Enable 80STORE and PAGE2
        status.Set80Store(true);
        status.SetPage2(true);

        // Act
        byte result = selector.Read(0x0400);

        // Assert - Should read from aux (PAGE2)
        Assert.Equal(0x42, result);
    }

    [Fact]
    public void Integration_DoubleHiRes_PageSwitching()
    {
        // Arrange - Simulate double hi-res mode
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        
        mainRam!.Write(0x2000, 0xAA);
        auxRam!.Write(0x2000, 0x55);
        
        // Enable 80STORE, HIRES, and PAGE2
        status.Set80Store(true);
        status.SetHiRes(true);
        status.SetPage2(true);

        // Act
        byte result = selector.Read(0x2000);

        // Assert - Should read from aux (PAGE2)
        Assert.Equal(0x55, result);
    }

    [Fact]
    public void Integration_AlternateZeroPage_WithFullSetup()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        
        // Set up zero page values
        mainRam!.Write(0x00, 0x11);
        mainRam.Write(0x01, 0x22);
        auxRam!.Write(0x00, 0x33);
        auxRam.Write(0x01, 0x44);
        
        // Enable ALTZP
        status.SetAltZp(true);

        // Act
        byte result1 = selector.Read(0x00);
        byte result2 = selector.Read(0x01);

        // Assert
        Assert.Equal(0x33, result1);
        Assert.Equal(0x44, result2);
    }

    [Fact]
    public void Integration_ReadWriteCycle_MaintenanceData()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out _, out var status);

        // Act - Write and read back
        selector.Write(0x1000, 0x42);
        byte result = selector.Read(0x1000);

        // Assert
        Assert.Equal(0x42, result);
        Assert.Equal(0x42, mainRam!.Read(0x1000));
    }

    [Fact]
    public void Integration_MultipleRegions_DifferentSoftSwitches()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        
        // Set up different switch states
        status.SetAltZp(true);         // Zero page -> aux
        status.Set80Store(true);        // Text page controlled by PAGE2
        status.SetPage2(true);          // Text page -> aux
        status.SetRamRd(false);         // General RAM -> main

        // Write to different regions
        mainRam!.Write(0x0080, 0x11);
        auxRam!.Write(0x0080, 0x22);
        mainRam.Write(0x0400, 0x33);
        auxRam.Write(0x0400, 0x44);
        mainRam.Write(0x1000, 0x55);
        auxRam.Write(0x1000, 0x66);

        // Act
        byte zeroPage = selector.Read(0x0080);    // Should read aux
        byte textPage = selector.Read(0x0400);    // Should read aux
        byte generalRam = selector.Read(0x1000);  // Should read main

        // Assert
        Assert.Equal(0x22, zeroPage);
        Assert.Equal(0x44, textPage);
        Assert.Equal(0x55, generalRam);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Read_AtBoundary_0x01FF_UsesAltZp()
    {
        // Arrange - Boundary between zero page and general RAM
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x01FF, 0x42);
        auxRam!.Write(0x01FF, 0x99);
        status.SetAltZp(true);

        // Act
        byte result = selector.Read(0x01FF);

        // Assert
        Assert.Equal(0x99, result);
    }

    [Fact]
    public void Read_AtBoundary_0x0200_DoesNotUseAltZp()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out var mainRam, out var auxRam, out var status);
        mainRam!.Write(0x0200, 0x42);
        auxRam!.Write(0x0200, 0x99);
        status.SetAltZp(true);
        status.SetRamRd(false);

        // Act
        byte result = selector.Read(0x0200);

        // Assert - Should use RamRd, not AltZp
        Assert.Equal(0x42, result);
    }

    [Fact]
    public void Read_AllMemoryRegions_WithoutAuxRam_ReturnsFloatingBus()
    {
        // Arrange
        var selector = CreateSystemRamSelector(out _, out _, out var status, includeAuxRam: false);
        status.SetRamRd(true);
        status.SetAltZp(true);

        // Act
        byte result1 = selector.Read(0x0080);  // Zero page
        byte result2 = selector.Read(0x1000);  // General RAM

        // Assert
        Assert.Equal(0xFF, result1);
        Assert.Equal(0xFF, result2);
    }

    #endregion
}
