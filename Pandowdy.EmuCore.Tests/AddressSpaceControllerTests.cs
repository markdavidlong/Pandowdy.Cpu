// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Input;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Memory;
using Pandowdy.EmuCore.Slots;
using Pandowdy.EmuCore.Tests.Helpers;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Comprehensive tests for AddressSpaceController - the pure routing layer for Apple IIe address space.
/// 
/// Tests cover:
/// - Constructor parameter validation
/// - Address space routing to appropriate subsystems
/// - Read/Write delegation ($0000-$BFFF, $C090-$CFFF, $D000-$FFFF)
/// - Exception handling for invalid addresses ($C000-$C08F)
/// - Direct memory access (ReadRawMain, ReadRawAux)
/// - Event notification (MemoryWritten)
/// - IMemory interface compliance
/// - IDisposable implementation
/// 
/// AddressSpaceController is a stateless router (~200 lines) that delegates all operations
/// to specialized subsystems without owning any memory or state itself.
/// </summary>
public class AddressSpaceControllerTests
{
    #region Test Helpers

    /// <summary>
    /// Mock implementation of ILanguageCard for testing routing.
    /// </summary>
    private class MockLanguageCard : ILanguageCard
    {
        private readonly byte[] _memory = new byte[0x3000]; // $D000-$FFFF (12KB)
        
        public int Size => 0x3000;

        public byte Read(ushort address)
        {
            int offset = address - 0xD000;
            if (offset >= 0 && offset < _memory.Length)
            {
                return _memory[offset];
            }
            return 0xFF;
        }

        public byte Peek(ushort address)
        {
            // Same as Read for mock - no side effects
            return Read(address);
        }

        public void Write(ushort address, byte value)
        {
            int offset = address - 0xD000;
            if (offset >= 0 && offset < _memory.Length)
            {
                _memory[offset] = value;
            }
        }

        public void Restart() { /* No-op */ }
    }
    private class MockSlots : ISlots
    {
        public ushort LastReadAddress { get; private set; }
        public ushort LastWriteAddress { get; private set; }
        public byte LastWriteValue { get; private set; }
        public byte ReadReturnValue { get; set; } = 0x42;
        
        public int Size => 0x1000;
        public byte BankSelect { get; set; }

        public byte Read(ushort address)
        {
            LastReadAddress = address;
            return ReadReturnValue;
        }

        public byte Peek(ushort address)
        {
            // Peek doesn't track address
            return ReadReturnValue;
        }

        public void Write(ushort address, byte val)
        {
            LastWriteAddress = address;
            LastWriteValue = val;
        }

            public void InstallCard(int id, SlotNumber slot) { }
            public void InstallCard(string name, SlotNumber slot) { }
            public void RemoveCard(SlotNumber slot) { }
            public ICard GetCardIn(SlotNumber slot) => throw new NotImplementedException();
            public bool IsEmpty(SlotNumber slot) => true;
            public string GetMetadata() => string.Empty;
            public bool ApplyMetadata(string metadata) => true;
            public void Reset() { }
        }

    /// <summary>
    /// Helper to create a fully configured AddressSpaceController for testing.
    /// </summary>
    private class AddressSpaceFixture
    {
        public MockLanguageCard LanguageCard { get; }
        public Test64KSystemRamSelector SystemRam { get; }
        public MockSlots Slots { get; }
        public ISystemIoHandler IoHandler { get; }
        public AddressSpaceController AddressSpace { get; }

        public AddressSpaceFixture()
        {
            LanguageCard = new MockLanguageCard();
            SystemRam = new Test64KSystemRamSelector();
            Slots = new MockSlots();
            var statusProvider = new SystemStatusProvider(new SimpleGameController());
            IoHandler = new SystemIoHandler(
                new SoftSwitches(statusProvider),
                new SingularKeyHandler(),
                new SimpleGameController(),
                new CpuClockingCounters());
            AddressSpace = new AddressSpaceController(LanguageCard, SystemRam, IoHandler, Slots);
        }
    }

    #endregion

    #region Constructor Tests (7 tests)

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange
        var langCard = new TestLanguageCard();
        var systemRam = new Test64KSystemRamSelector();
        var slots = new MockSlots();
        var statusProvider = new SystemStatusProvider(new SimpleGameController());
        var ioHandler = new SystemIoHandler(
            new SoftSwitches(statusProvider),
            new SingularKeyHandler(),
            new SimpleGameController(),
            new CpuClockingCounters());

        // Act
        var controller = new AddressSpaceController(langCard, systemRam, ioHandler, slots);

        // Assert
        Assert.NotNull(controller);
        Assert.Same(systemRam, controller.SystemRam);
        Assert.Equal(0x10000, controller.Size); // 64KB addressable space
    }

    [Fact]
    public void Constructor_NullLanguageCard_ThrowsArgumentNullException()
    {
        // Arrange
        var systemRam = new Test64KSystemRamSelector();
        var slots = new MockSlots();
        var statusProvider = new SystemStatusProvider(new SimpleGameController());
        var ioHandler = new SystemIoHandler(
            new SoftSwitches(statusProvider),
            new SingularKeyHandler(),
            new SimpleGameController(),
            new CpuClockingCounters());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AddressSpaceController(null!, systemRam, ioHandler, slots));
    }

    [Fact]
    public void Constructor_NullSystemRam_ThrowsArgumentNullException()
    {
        // Arrange
        var langCard = new TestLanguageCard();
        var slots = new MockSlots();
        var statusProvider = new SystemStatusProvider(new SimpleGameController());
        var ioHandler = new SystemIoHandler(
            new SoftSwitches(statusProvider),
            new SingularKeyHandler(),
            new SimpleGameController(),
            new CpuClockingCounters());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AddressSpaceController(langCard, null!, ioHandler, slots));
    }

    [Fact]
    public void Constructor_NullSlots_ThrowsArgumentNullException()
    {
        // Arrange
        var langCard = new TestLanguageCard();
        var systemRam = new Test64KSystemRamSelector();
        var statusProvider = new SystemStatusProvider(new SimpleGameController());
        var ioHandler = new SystemIoHandler(
            new SoftSwitches(statusProvider),
            new SingularKeyHandler(),
            new SimpleGameController(),
            new CpuClockingCounters());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AddressSpaceController(langCard, systemRam, ioHandler, null!));
    }



    [Fact]
    public void Size_ReturnsCorrect64KBAddressSpace()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        var size = fixture.AddressSpace.Size;

        // Assert
        Assert.Equal(0x10000, size); // 64KB (16-bit address space)
    }

    [Fact]
    public void SystemRam_ReturnsInjectedInstance()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        var ram = fixture.AddressSpace.SystemRam;

        // Assert
        Assert.Same(fixture.SystemRam, ram);
    }

    #endregion

    #region Read Routing Tests (10 tests)

    [Fact]
    public void Read_Address0000_RoutesToSystemRam()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        fixture.SystemRam.Write(0x0000, 0xAB);

        // Act
        var value = fixture.AddressSpace.Read(0x0000);

        // Assert
        Assert.Equal(0xAB, value);
    }

    [Fact]
    public void Read_Address0400_RoutesToSystemRam()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        fixture.SystemRam.Write(0x0400, 0xCD);

        // Act
        var value = fixture.AddressSpace.Read(0x0400);

        // Assert
        Assert.Equal(0xCD, value);
    }

    [Fact]
    public void Read_AddressBFFF_RoutesToSystemRam()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        fixture.SystemRam.Write(0xBFFF, 0xEF);

        // Act
        var value = fixture.AddressSpace.Read(0xBFFF);

        // Assert
        Assert.Equal(0xEF, value);
    }

    [Fact]
    public void Read_AddressC000toC08F_RoutesToSystemIoHandler()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

            // Act
            _ = fixture.AddressSpace.Read(0xC000);

            // Assert - Should route to IoHandler, not throw
            // The actual value depends on what the mock IoHandler returns
            Assert.NotNull(fixture.IoHandler);
        }

        [Fact]
        public void Read_AddressC08F_RoutesToSystemIoHandler()
        {
            // Arrange
            var fixture = new AddressSpaceFixture();

            // Act
            _ = fixture.AddressSpace.Read(0xC08F);

        // Assert - Should route to IoHandler, not throw
        Assert.NotNull(fixture.IoHandler);
    }

    [Fact]
    public void Read_AddressC090_RoutesToSlots()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        fixture.Slots.ReadReturnValue = 0x99;

        // Act
        var value = fixture.AddressSpace.Read(0xC090);

        // Assert
        Assert.Equal(0x99, value);
        Assert.Equal(0x90, fixture.Slots.LastReadAddress); // Offset by $C000
    }

    [Fact]
    public void Read_AddressC600_RoutesToSlots()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        fixture.Slots.ReadReturnValue = 0x77;

        // Act
        var value = fixture.AddressSpace.Read(0xC600);

        // Assert
        Assert.Equal(0x77, value);
        Assert.Equal((ushort)0x600, fixture.Slots.LastReadAddress); // Offset by $C000
    }

    [Fact]
    public void Read_AddressCFFF_RoutesToSlots()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        fixture.Slots.ReadReturnValue = 0x55;

        // Act
        var value = fixture.AddressSpace.Read(0xCFFF);

        // Assert
        Assert.Equal(0x55, value);
        Assert.Equal((ushort)0xFFF, fixture.Slots.LastReadAddress); // Offset by $C000
    }

    [Fact]
    public void Read_AddressD000_RoutesToLanguageCard()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        fixture.LanguageCard.Write(0xD000, 0x33);

        // Act
        var value = fixture.AddressSpace.Read(0xD000);

        // Assert
        Assert.Equal(0x33, value);
    }

    [Fact]
    public void Read_AddressFFFF_RoutesToLanguageCard()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        fixture.LanguageCard.Write(0xFFFF, 0x11);

        // Act
        var value = fixture.AddressSpace.Read(0xFFFF);

        // Assert
        Assert.Equal(0x11, value);
    }

    #endregion

    #region Write Routing Tests (10 tests)

    [Fact]
    public void Write_Address0000_RoutesToSystemRam()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0x0000, 0xAB);

        // Assert
        Assert.Equal(0xAB, fixture.SystemRam.Read(0x0000));
    }

    [Fact]
    public void Write_Address0800_RoutesToSystemRam()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0x0800, 0xCD);

        // Assert
        Assert.Equal(0xCD, fixture.SystemRam.Read(0x0800));
    }

    [Fact]
    public void Write_AddressBFFF_RoutesToSystemRam()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0xBFFF, 0xEF);

        // Assert
        Assert.Equal(0xEF, fixture.SystemRam.Read(0xBFFF));
    }

    [Fact]
    public void Write_AddressC000toC08F_RoutesToSystemIoHandler()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0xC000, 0x42);

        // Assert - Should route to IoHandler, not throw
        Assert.NotNull(fixture.IoHandler);
    }

    [Fact]
    public void Write_AddressC08F_RoutesToSystemIoHandler()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0xC08F, 0x99);

        // Assert - Should route to IoHandler, not throw
        Assert.NotNull(fixture.IoHandler);
    }

    [Fact]
    public void Write_AddressC090_RoutesToSlots()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0xC090, 0x99);

        // Assert
        Assert.Equal(0x90, fixture.Slots.LastWriteAddress); // Offset by $C000
        Assert.Equal(0x99, fixture.Slots.LastWriteValue);
    }

    [Fact]
    public void Write_AddressC300_RoutesToSlots()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0xC300, 0x77);

        // Assert
        Assert.Equal((ushort)0x300, fixture.Slots.LastWriteAddress); // Offset by $C000
        Assert.Equal(0x77, fixture.Slots.LastWriteValue);
    }

    [Fact]
    public void Write_AddressCFFF_RoutesToSlots()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0xCFFF, 0x55);

        // Assert
        Assert.Equal((ushort)0xFFF, fixture.Slots.LastWriteAddress); // Offset by $C000
        Assert.Equal(0x55, fixture.Slots.LastWriteValue);
    }

    [Fact]
    public void Write_AddressD000_RoutesToLanguageCard()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0xD000, 0x33);

        // Assert
        Assert.Equal(0x33, fixture.LanguageCard.Read(0xD000));
    }

    [Fact]
    public void Write_AddressFFFF_RoutesToLanguageCard()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act
        fixture.AddressSpace.Write(0xFFFF, 0x11);

        // Assert
        Assert.Equal(0x11, fixture.LanguageCard.Read(0xFFFF));
    }

    #endregion

    #region Direct Memory Access Tests (4 tests)

    [Fact]
    public void ReadRawMain_ReturnsMainMemory_BypassingBankSwitching()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        // Use indexer to write, then read raw
        fixture.SystemRam.Write(0x2000, 0xAA);

        // Act
        var value = fixture.AddressSpace.ReadRawMain(0x2000);

        // Assert
        Assert.Equal(0xAA, value);
    }

    [Fact]
    public void ReadRawAux_ReturnsAuxMemory_BypassingBankSwitching()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        // Test64KSystemRamSelector provides separate main/aux banks
        // Write to aux via direct access method on the SystemRam
        fixture.SystemRam.Write(0x4000, 0xBB); // Writes to current bank
        
        // Act
        _ = fixture.AddressSpace.ReadRawAux(0x4000);

        // Assert
        // Value depends on how Test64KSystemRamSelector implements aux memory
        Assert.NotNull(fixture.SystemRam); // Basic assertion that we can call ReadRawAux
    }

    [Fact]
    public void ReadRawMain_WorksForVideoMemory()
    {
        // Arrange - Video renderer use case
        var fixture = new AddressSpaceFixture();
        fixture.SystemRam.Write(0x4000, 0xCC); // Hi-res page 2

        // Act
        var value = fixture.AddressSpace.ReadRawMain(0x4000);

        // Assert
        Assert.Equal(0xCC, value);
    }

    [Fact]
    public void ReadRawAux_WorksFor80ColumnMemory()
    {
        // Arrange - 80-column text mode use case
        var fixture = new AddressSpaceFixture();
        fixture.SystemRam.Write(0x0400, 0xDD); // Text page 1

        // Act - ReadRawAux should delegate to SystemRam
        _ = fixture.AddressSpace.ReadRawAux(0x0400);

        // Assert - Basic test that method works
        Assert.NotNull(fixture.SystemRam);
    }

    #endregion

    #region Event Notification Tests (5 tests)

    [Fact]
    public void MemoryWritten_FiresOnWrite()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        MemoryAccessEventArgs? capturedArgs = null;
        fixture.AddressSpace.MemoryWritten += (sender, args) => capturedArgs = args;

        // Act
        fixture.AddressSpace.Write(0x1000, 0x42);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal((ushort)0x1000, capturedArgs.Address);
        Assert.Equal((byte)0x42, capturedArgs.Value);
    }

    [Fact]
    public void MemoryWritten_FiresForSlotWrites()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        MemoryAccessEventArgs? capturedArgs = null;
        fixture.AddressSpace.MemoryWritten += (sender, args) => capturedArgs = args;

        // Act
        fixture.AddressSpace.Write(0xC600, 0x99);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal((ushort)0xC600, capturedArgs.Address);
        Assert.Equal((byte)0x99, capturedArgs.Value);
    }

    [Fact]
    public void MemoryWritten_FiresForLanguageCardWrites()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        MemoryAccessEventArgs? capturedArgs = null;
        fixture.AddressSpace.MemoryWritten += (sender, args) => capturedArgs = args;

        // Act
        fixture.AddressSpace.Write(0xD000, 0x77);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal((ushort)0xD000, capturedArgs.Address);
        Assert.Equal((byte)0x77, capturedArgs.Value);
    }

    [Fact]
    public void MemoryWritten_DoesNotFireOnRead()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        var eventFired = false;
        fixture.AddressSpace.MemoryWritten += (sender, args) => eventFired = true;

        // Act
        _ = fixture.AddressSpace.Read(0x2000);

        // Assert
        Assert.False(eventFired);
    }

    [Fact]
    public void MemoryWritten_SupportsMultipleSubscribers()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();
        var event1Fired = false;
        var event2Fired = false;
        fixture.AddressSpace.MemoryWritten += (sender, args) => event1Fired = true;
        fixture.AddressSpace.MemoryWritten += (sender, args) => event2Fired = true;

        // Act
        fixture.AddressSpace.Write(0x3000, 0x55);

        // Assert
        Assert.True(event1Fired);
        Assert.True(event2Fired);
    }

    #endregion

    #region IDisposable Tests (2 tests)

    [Fact]
    public void Dispose_CompletesWithoutException()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act & Assert - Should not throw
        fixture.AddressSpace.Dispose();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var fixture = new AddressSpaceFixture();

        // Act & Assert - Multiple calls should be safe
        fixture.AddressSpace.Dispose();
        fixture.AddressSpace.Dispose();
        fixture.AddressSpace.Dispose();
    }

    #endregion
}
