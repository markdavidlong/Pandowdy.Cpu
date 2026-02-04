// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using System.Reactive.Subjects;
using Pandowdy.EmuCore.DataTypes;


namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Comprehensive tests for RenderContext class.
/// Tests context initialization, property access, invalidation mechanism, and error handling.
/// </summary>
public class RenderContextTests
{
    #region Test Helpers

    /// <summary>
    /// Test stub for IDirectMemoryPoolReader.
    /// </summary>
    private class TestMemoryReader : IDirectMemoryPoolReader
    {
        private readonly byte[] _mainMemory = new byte[0x10000];
        private readonly byte[] _auxMemory = new byte[0x10000];

        public TestMemoryReader()
        {
            // Fill with test pattern
            for (int i = 0; i < 0x10000; i++)
            {
                _mainMemory[i] = (byte)(i & 0xFF);
                _auxMemory[i] = (byte)((i + 0x80) & 0xFF);
            }
        }

        public byte ReadRawMain(int address) => _mainMemory[address & 0xFFFF];
        public byte ReadRawAux(int address) => _auxMemory[address & 0xFFFF];
    }

    /// <summary>
    /// Test stub for ISystemStatusProvider.
    /// </summary>
    private class TestStatusProvider : ISystemStatusProvider
    {
        private SystemStatusSnapshot _current;
        private readonly Subject<SystemStatusSnapshot> _subject = new();

        public TestStatusProvider()
        {
            _current = CreateDefaultSnapshot();
        }

        private static SystemStatusSnapshot CreateDefaultSnapshot()
        {
            return new SystemStatusSnapshot(
                State80Store: false,
                StateRamRd: false,
                StateRamWrt: false,
                StateIntCxRom: false,
                StateIntC8Rom: false,
                StateAltZp: false,
                StateSlotC3Rom: false,
                StatePb0: false,
                StatePb1: false,
                StatePb2: false,
                StateAnn0: false,
                StateAnn1: false,
                StateAnn2: false,
                StateAnn3_DGR: false,
                StatePage2: false,
                StateHiRes: false,
                StateMixed: false,
                StateTextMode: false,
                StateShow80Col: false,
                StateAltCharSet: false,
                StateFlashOn: false,
                StatePrewrite: false,
                StateUseBank1: false,
                StateHighRead: false,
                StateHighWrite: false,
                StateVBlank: false,
                StatePdl0: 0,
                StatePdl1: 0,
                StatePdl2: 0,
                StatePdl3: 0,
                StateIntC8RomSlot: 0,
                StateCurrentMhz: 1.023
            );
        }

        public bool StateTextMode
        {
            get => _current.StateTextMode;
            set => UpdateField(s => s with { StateTextMode = value });
        }

        public bool StateMixed
        {
            get => _current.StateMixed;
            set => UpdateField(s => s with { StateMixed = value });
        }

        public bool StateHiRes
        {
            get => _current.StateHiRes;
            set => UpdateField(s => s with { StateHiRes = value });
        }

        public bool StatePage2
        {
            get => _current.StatePage2;
            set => UpdateField(s => s with { StatePage2 = value });
        }

        public bool State80Store { get => _current.State80Store; set => UpdateField(s => s with { State80Store = value }); }
        public bool StateRamRd { get => _current.StateRamRd; set => UpdateField(s => s with { StateRamRd = value }); }
        public bool StateRamWrt { get => _current.StateRamWrt; set => UpdateField(s => s with { StateRamWrt = value }); }
        public bool StateAltZp { get => _current.StateAltZp; set => UpdateField(s => s with { StateAltZp = value }); }
        public bool StateIntCxRom { get => _current.StateIntCxRom; set => UpdateField(s => s with { StateIntCxRom = value }); }
        public bool StateSlotC3Rom { get => _current.StateSlotC3Rom; set => UpdateField(s => s with { StateSlotC3Rom = value }); }
        public bool StatePb0 { get => _current.StatePb0; set => UpdateField(s => s with { StatePb0 = value }); }
        public bool StatePb1 { get => _current.StatePb1; set => UpdateField(s => s with { StatePb1 = value }); }
        public bool StatePb2 { get => _current.StatePb2; set => UpdateField(s => s with { StatePb2 = value }); }
        public bool StateAnn0 { get => _current.StateAnn0; set => UpdateField(s => s with { StateAnn0 = value }); }
        public bool StateAnn1 { get => _current.StateAnn1; set => UpdateField(s => s with { StateAnn1 = value }); }
        public bool StateAnn2 { get => _current.StateAnn2; set => UpdateField(s => s with { StateAnn2 = value }); }
        public bool StateAnn3_DGR { get => _current.StateAnn3_DGR; set => UpdateField(s => s with { StateAnn3_DGR = value }); }
        public bool StateShow80Col { get => _current.StateShow80Col; set => UpdateField(s => s with { StateShow80Col = value }); }
        public bool StateAltCharSet { get => _current.StateAltCharSet; set => UpdateField(s => s with { StateAltCharSet = value }); }
        public bool StateFlashOn { get => _current.StateFlashOn; set => UpdateField(s => s with { StateFlashOn = value }); }
        public bool StatePreWrite { get => _current.StatePrewrite; set => UpdateField(s => s with { StatePrewrite = value }); }
        public bool StateUseBank1 { get => _current.StateUseBank1; set => UpdateField(s => s with { StateUseBank1 = value }); }
        public bool StateHighRead { get => _current.StateHighRead; set => UpdateField(s => s with { StateHighRead = value }); }
        public bool StateHighWrite { get => _current.StateHighWrite; set => UpdateField(s => s with { StateHighWrite = value }); }
        public bool StateVBlank { get => _current.StateVBlank; set => UpdateField(s => s with { StateVBlank = value }); }
        public bool StateIntC8Rom { get => _current.StateIntC8Rom; set => UpdateField(s => s with { StateIntC8Rom = value }); }
        public byte StateIntC8RomSlot => _current.StateIntC8RomSlot;
        public double StateCurrentMhz => _current.StateCurrentMhz;
        public byte Pdl0 => _current.StatePdl0;
        public byte Pdl1 => _current.StatePdl1;
        public byte Pdl2 => _current.StatePdl2;
        public byte Pdl3 => _current.StatePdl3;

        private void UpdateField(Func<SystemStatusSnapshot, SystemStatusSnapshot> updater)
        {
            _current = updater(_current);
        }

        public SystemStatusSnapshot Current => _current;
        public event EventHandler<SystemStatusSnapshot>? Changed;
#pragma warning disable CS0067 // Event is never used - test stub doesn't need to raise the event
        public event EventHandler<SystemStatusSnapshot>? MemoryMappingChanged;
#pragma warning restore CS0067
        public IObservable<SystemStatusSnapshot> Stream => _subject;

        public void Mutate(Action<SystemStatusSnapshotBuilder> mutator)
        {
            var builder = new SystemStatusSnapshotBuilder(_current);
            mutator(builder);
            _current = builder.Build();
            Changed?.Invoke(this, _current);
            _subject.OnNext(_current);
        }

        public void UpdateSnapshot(SystemStatusSnapshot snapshot)
        {
            _current = snapshot;
            Changed?.Invoke(this, snapshot);
            _subject.OnNext(snapshot);
        }
    }

    #endregion

    #region Constructor Tests (4 tests)

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();

        // Act
        var context = new RenderContext(frameBuffer, memReader, statusProvider);

        // Assert
        Assert.Same(frameBuffer, context.FrameBuffer);
        Assert.Same(memReader, context.Memory);
        Assert.Same(statusProvider, context.SystemStatus);
    }

    [Fact]
    public void Constructor_NullFrameBuffer_ThrowsException()
    {
        // Arrange
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RenderContext(null!, memReader, statusProvider));
    }

    [Fact]
    public void Constructor_NullMemoryReader_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var statusProvider = new TestStatusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RenderContext(frameBuffer, null!, statusProvider));
    }

    [Fact]
    public void Constructor_NullStatusProvider_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RenderContext(frameBuffer, memReader, null!));
    }

    #endregion

    #region Property Access Tests (5 tests)

    [Fact]
    public void IsTextMode_ReflectsStatusProvider()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();

        // Act - Text mode OFF
        var statusProvider1 = new TestStatusProvider
        {
            StateTextMode = false
        };
        var context1 = new RenderContext(frameBuffer, memReader, statusProvider1);

        // Act - Text mode ON
        var statusProvider2 = new TestStatusProvider
        {
            StateTextMode = true
        };
        var context2 = new RenderContext(frameBuffer, memReader, statusProvider2);

        // Assert
        Assert.False(context1.IsTextMode);
        Assert.True(context2.IsTextMode);
    }

    [Fact]
    public void IsMixed_ReflectsStatusProvider()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();

        // Act
        var statusProvider1 = new TestStatusProvider
        {
            StateMixed = false
        };
        var context1 = new RenderContext(frameBuffer, memReader, statusProvider1);

        var statusProvider2 = new TestStatusProvider
        {
            StateMixed = true
        };
        var context2 = new RenderContext(frameBuffer, memReader, statusProvider2);

        // Assert
        Assert.False(context1.IsMixed);
        Assert.True(context2.IsMixed);
    }

    [Fact]
    public void IsHiRes_ReflectsStatusProvider()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();

        // Act
        var statusProvider1 = new TestStatusProvider
        {
            StateHiRes = false
        };
        var context1 = new RenderContext(frameBuffer, memReader, statusProvider1);

        var statusProvider2 = new TestStatusProvider
        {
            StateHiRes = true
        };
        var context2 = new RenderContext(frameBuffer, memReader, statusProvider2);

        // Assert
        Assert.False(context1.IsHiRes);
        Assert.True(context2.IsHiRes);
    }

    [Fact]
    public void IsPage2_ReflectsStatusProvider()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();

        // Act
        var statusProvider1 = new TestStatusProvider
        {
            StatePage2 = false
        };
        var context1 = new RenderContext(frameBuffer, memReader, statusProvider1);

        var statusProvider2 = new TestStatusProvider
        {
            StatePage2 = true
        };
        var context2 = new RenderContext(frameBuffer, memReader, statusProvider2);

        // Assert
        Assert.False(context1.IsPage2);
        Assert.True(context2.IsPage2);
    }

    [Fact]
    public void ClearBuffer_ClearsFrameBuffer()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);

        // Set some pixels
        frameBuffer.SetPixel(100, 100, 0);
        frameBuffer.SetPixel(200, 100, 1);
        Assert.True(frameBuffer.GetPixel(100, 100, 0));
        Assert.True(frameBuffer.GetPixel(200, 100, 1));

        // Act
        context.ClearBuffer();

        // Assert
        Assert.False(frameBuffer.GetPixel(100, 100, 0));
        Assert.False(frameBuffer.GetPixel(200, 100, 1));
    }

    #endregion

    #region Invalidation Mechanism Tests (11 tests)

    [Fact]
    public void IsInvalidated_InitiallyFalse()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();

        // Act
        var context = new RenderContext(frameBuffer, memReader, statusProvider);

        // Assert
        Assert.False(context.IsInvalidated);
    }

    [Fact]
    public void Invalidate_SetsIsInvalidatedToTrue()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);

        // Act
        context.Invalidate();

        // Assert
        Assert.True(context.IsInvalidated);
    }

    [Fact]
    public void Invalidate_CalledTwice_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);
        context.Invalidate();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => context.Invalidate());
        Assert.Contains("already been invalidated", ex.Message);
        Assert.Contains("programming error", ex.Message);
    }

    [Fact]
    public void FrameBuffer_AfterInvalidation_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);
        context.Invalidate();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = context.FrameBuffer);
        Assert.Contains("has been invalidated after commit", ex.Message);
        Assert.Contains("cannot be reused", ex.Message);
    }

    [Fact]
    public void Memory_AfterInvalidation_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);
        context.Invalidate();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = context.Memory);
        Assert.Contains("has been invalidated after commit", ex.Message);
        Assert.Contains("cannot be reused", ex.Message);
    }

    [Fact]
    public void SystemStatus_AfterInvalidation_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);
        context.Invalidate();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = context.SystemStatus);
        Assert.Contains("has been invalidated after commit", ex.Message);
        Assert.Contains("cannot be reused", ex.Message);
    }

    [Fact]
    public void IsTextMode_AfterInvalidation_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);
        context.Invalidate();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = context.IsTextMode);
        Assert.Contains("has been invalidated after commit", ex.Message);
        Assert.Contains("cannot be reused", ex.Message);
    }

    [Fact]
    public void IsMixed_AfterInvalidation_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);
        context.Invalidate();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = context.IsMixed);
        Assert.Contains("has been invalidated after commit", ex.Message);
        Assert.Contains("cannot be reused", ex.Message);
    }

    [Fact]
    public void IsHiRes_AfterInvalidation_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);
        context.Invalidate();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = context.IsHiRes);
        Assert.Contains("has been invalidated after commit", ex.Message);
        Assert.Contains("cannot be reused", ex.Message);
    }

    [Fact]
    public void IsPage2_AfterInvalidation_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);
        context.Invalidate();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = context.IsPage2);
        Assert.Contains("has been invalidated after commit", ex.Message);
        Assert.Contains("cannot be reused", ex.Message);
    }

    [Fact]
    public void ClearBuffer_AfterInvalidation_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);
        context.Invalidate();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => context.ClearBuffer());
        Assert.Contains("has been invalidated after commit", ex.Message);
        Assert.Contains("cannot be reused", ex.Message);
    }

    #endregion

    #region Valid Usage Tests (4 tests)

    [Fact]
    public void BeforeInvalidation_AllPropertiesAccessible()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider
        {
            StateTextMode = true,
            StateMixed = true,
            StateHiRes = true,
            StatePage2 = true
        };

        var context = new RenderContext(frameBuffer, memReader, statusProvider);

        // Act & Assert - All properties should be accessible
        Assert.Same(frameBuffer, context.FrameBuffer);
        Assert.Same(memReader, context.Memory);
        Assert.Same(statusProvider, context.SystemStatus);
        Assert.True(context.IsTextMode);
        Assert.True(context.IsMixed);
        Assert.True(context.IsHiRes);
        Assert.True(context.IsPage2);
        Assert.False(context.IsInvalidated);
    }

    [Fact]
    public void BeforeInvalidation_ClearBufferWorks()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);

        // Set some pixels on bitplane 0 (composite)
        frameBuffer.SetPixel(10, 10, 0);
        frameBuffer.SetPixel(20, 20, 0);

        // Act
        context.ClearBuffer();

        // Assert - Buffer should be cleared (all zeros)
        Assert.False(frameBuffer.GetPixel(10, 10, 0));
        Assert.False(frameBuffer.GetPixel(20, 20, 0));
    }

    [Fact]
    public void BeforeInvalidation_MemoryAccessWorks()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var context = new RenderContext(frameBuffer, memReader, statusProvider);

        // Act - Read memory through context
        byte mainByte = context.Memory.ReadRawMain(0x400);
        byte auxByte = context.Memory.ReadRawAux(0x800);

        // Assert - Memory reads should work
        Assert.Equal(0x00, mainByte);  // Test pattern: i & 0xFF at 0x400 = 0x00
        Assert.Equal(0x80, auxByte);   // Test pattern: (i + 0x80) & 0xFF at 0x800 = 0x80
    }

    [Fact]
    public void BeforeInvalidation_StatusPropertiesReflectActualState()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider
        {
            // Set specific states
            StateTextMode = false,
            StateMixed = true,
            StateHiRes = false,
            StatePage2 = true
        };

        var context = new RenderContext(frameBuffer, memReader, statusProvider);

        // Act & Assert - Context should reflect system status
        Assert.False(context.IsTextMode, "Expected IsTextMode to be false");
        Assert.True(context.IsMixed, "Expected IsMixed to be true");
        Assert.False(context.IsHiRes, "Expected IsHiRes to be false");
        Assert.True(context.IsPage2, "Expected IsPage2 to be true");
    }

    #endregion
}
