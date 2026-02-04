// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using System.Reactive.Subjects;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Comprehensive tests for FrameGenerator and RenderContext.
/// Tests the frame generation pipeline, context allocation,
/// and integration with frame providers, memory readers, and renderers.
/// </summary>
public class FrameGeneratorTests
{
    #region Test Helpers and Stubs

    /// <summary>
    /// Test stub for IFrameProvider that tracks operations.
    /// </summary>
    private class TestFrameProvider : IFrameProvider
    {
        private readonly BitmapDataArray _backBuffer = new();
        private readonly BitmapDataArray _frontBuffer = new();
        
        public int Width => 560;
        public int Height => 192;
        public bool IsGraphics { get; set; }
        public bool IsMixed { get; set; }
        public int BorrowCount { get; private set; }
        public int CommitCount { get; private set; }
        public event EventHandler? FrameAvailable;

        public BitmapDataArray GetFrame() => _frontBuffer;

        public BitmapDataArray? BorrowWritable()
        {
            BorrowCount++;
            return _backBuffer;
        }

        public void CommitWritable(BitmapDataArray renderedBuffer)
        {
            CommitCount++;
            FrameAvailable?.Invoke(this, EventArgs.Empty);
        }

        public void Reset()
        {
            BorrowCount = 0;
            CommitCount = 0;
            IsGraphics = false;
            IsMixed = false;
        }
    }

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

        public void SetMainMemory(int address, byte value)
        {
            _mainMemory[address & 0xFFFF] = value;
        }

        public void SetAuxMemory(int address, byte value)
        {
            _auxMemory[address & 0xFFFF] = value;
        }
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

        public bool State80Store
        {
            get => _current.State80Store;
            set => UpdateField(s => s with { State80Store = value });
        }

        public bool StateRamRd
        {
            get => _current.StateRamRd;
            set => UpdateField(s => s with { StateRamRd = value });
        }

        public bool StateRamWrt
        {
            get => _current.StateRamWrt;
            set => UpdateField(s => s with { StateRamWrt = value });
        }

        public bool StateAltZp
        {
            get => _current.StateAltZp;
            set => UpdateField(s => s with { StateAltZp = value });
        }

        public bool StateIntCxRom
        {
            get => _current.StateIntCxRom;
            set => UpdateField(s => s with { StateIntCxRom = value });
        }

        public bool StateSlotC3Rom
        {
            get => _current.StateSlotC3Rom;
            set => UpdateField(s => s with { StateSlotC3Rom = value });
        }

        public bool StatePb0
        {
            get => _current.StatePb0;
            set => UpdateField(s => s with { StatePb0 = value });
        }

        public bool StatePb1
        {
            get => _current.StatePb1;
            set => UpdateField(s => s with { StatePb1 = value });
        }

        public bool StatePb2
        {
            get => _current.StatePb2;
            set => UpdateField(s => s with { StatePb2 = value });
        }

        public bool StateAnn0
        {
            get => _current.StateAnn0;
            set => UpdateField(s => s with { StateAnn0 = value });
        }

        public bool StateAnn1
        {
            get => _current.StateAnn1;
            set => UpdateField(s => s with { StateAnn1 = value });
        }

        public bool StateAnn2
        {
            get => _current.StateAnn2;
            set => UpdateField(s => s with { StateAnn2 = value });
        }

        public bool StateAnn3_DGR
        {
            get => _current.StateAnn3_DGR;
            set => UpdateField(s => s with { StateAnn3_DGR = value });
        }

        public bool StateShow80Col
        {
            get => _current.StateShow80Col;
            set => UpdateField(s => s with { StateShow80Col = value });
        }

        public bool StateAltCharSet
        {
            get => _current.StateAltCharSet;
            set => UpdateField(s => s with { StateAltCharSet = value });
        }

        public bool StateFlashOn
        {
            get => _current.StateFlashOn;
            set => UpdateField(s => s with { StateFlashOn = value });
        }

        public bool StatePreWrite
        {
            get => _current.StatePrewrite;
            set => UpdateField(s => s with { StatePrewrite = value });
        }

        public bool StateUseBank1
        {
            get => _current.StateUseBank1;
            set => UpdateField(s => s with { StateUseBank1 = value });
        }

        public bool StateHighRead
        {
            get => _current.StateHighRead;
            set => UpdateField(s => s with { StateHighRead = value });
        }

        public bool StateHighWrite
        {
            get => _current.StateHighWrite;
            set => UpdateField(s => s with { StateHighWrite = value });
        }

        public bool StateVBlank
        {
            get => _current.StateVBlank;
            set => UpdateField(s => s with { StateVBlank = value });
        }
        
        // CurrentKey removed - keyboard state is managed entirely by SingularKeyHandler subsystem
        public byte Pdl0 => _current.StatePdl0;
        public byte Pdl1 => _current.StatePdl1;
        public byte Pdl2 => _current.StatePdl2;
        public byte Pdl3 => _current.StatePdl3;
        public bool StateIntC8Rom => _current.StateIntC8Rom;
        public byte StateIntC8RomSlot => _current.StateIntC8RomSlot;
        public double StateCurrentMhz => _current.StateCurrentMhz;
        

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

    /// <summary>
    /// Test stub for IDisplayBitmapRenderer that tracks render calls.
    /// </summary>
    private class TestRenderer : IDisplayBitmapRenderer
    {
        public int RenderCount { get; private set; }
        public RenderContext? LastContext { get; private set; }
        public bool ShouldDrawPattern { get; set; }

        public void Render(RenderContext context)
        {
            RenderCount++;
            LastContext = context;

            if (ShouldDrawPattern)
            {
                // Draw a simple test pattern
                for (int y = 0; y < 192; y++)
                {
                    for (int x = 0; x < 560; x++)
                    {
                        if ((x + y) % 2 == 0)
                        {
                            context.FrameBuffer.SetPixel(x, y, 0);
                        }
                    }
                }
            }
        }

        public void Reset()
        {
            RenderCount = 0;
            LastContext = null;
        }
    }

    /// <summary>
    /// Helper to create a configured FrameGenerator with test doubles.
    /// </summary>
    private class FrameGeneratorFixture
    {
        public TestFrameProvider FrameProvider { get; }
        public TestMemoryReader MemoryReader { get; }
        public TestStatusProvider StatusProvider { get; }
        public TestRenderer Renderer { get; }
        public FrameGenerator FrameGenerator { get; }

        public FrameGeneratorFixture()
        {
            FrameProvider = new TestFrameProvider();
            MemoryReader = new TestMemoryReader();
            StatusProvider = new TestStatusProvider();
            Renderer = new TestRenderer();
            FrameGenerator = new FrameGenerator(
                FrameProvider,
                MemoryReader,
                StatusProvider,
                Renderer);
        }

        public void Reset()
        {
            FrameProvider.Reset();
            Renderer.Reset();
        }
    }

    #endregion

    #region Constructor Tests (5 tests)

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var frameProvider = new TestFrameProvider();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var renderer = new TestRenderer();

        // Act
        var frameGenerator = new FrameGenerator(
            frameProvider,
            memReader,
            statusProvider,
            renderer);

        // Assert
        Assert.NotNull(frameGenerator);
    }

    [Fact]
    public void Constructor_NullFrameProvider_ThrowsException()
    {
        // Arrange
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var renderer = new TestRenderer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FrameGenerator(null!, memReader, statusProvider, renderer));
    }

    [Fact]
    public void Constructor_NullMemoryReader_ThrowsException()
    {
        // Arrange
        var frameProvider = new TestFrameProvider();
        var statusProvider = new TestStatusProvider();
        var renderer = new TestRenderer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FrameGenerator(frameProvider, null!, statusProvider, renderer));
    }

    [Fact]
    public void Constructor_NullStatusProvider_ThrowsException()
    {
        // Arrange
        var frameProvider = new TestFrameProvider();
        var memReader = new TestMemoryReader();
        var renderer = new TestRenderer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FrameGenerator(frameProvider, memReader, null!, renderer));
    }

    [Fact]
    public void Constructor_NullRenderer_ThrowsException()
    {
        // Arrange
        var frameProvider = new TestFrameProvider();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FrameGenerator(frameProvider, memReader, statusProvider, null!));
    }

    #endregion




    #region RenderFrameFromSnapshot Tests (NEW API)

    [Fact]
    public void RenderFrameFromSnapshot_CallsRenderer()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var snapshot = CreateTestSnapshot(fixture.StatusProvider);

        // Act
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

        // Assert - Renderer should have been called
        Assert.Equal(1, fixture.Renderer.RenderCount);
    }

    [Fact]
    public void RenderFrameFromSnapshot_SetsIsGraphics_WhenNotTextMode()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateTextMode = false;
        var snapshot = CreateTestSnapshot(fixture.StatusProvider);

        // Act
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

        // Assert
        Assert.True(fixture.FrameProvider.IsGraphics);
    }

    [Fact]
    public void RenderFrameFromSnapshot_ClearsIsGraphics_WhenTextMode()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateTextMode = true;
        var snapshot = CreateTestSnapshot(fixture.StatusProvider);

        // Act
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

        // Assert
        Assert.False(fixture.FrameProvider.IsGraphics);
    }

    [Fact]
    public void RenderFrameFromSnapshot_SetsIsMixed_WhenMixedMode()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateMixed = true;
        var snapshot = CreateTestSnapshot(fixture.StatusProvider);

        // Act
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

        // Assert
        Assert.True(fixture.FrameProvider.IsMixed);
    }

    [Fact]
    public void RenderFrameFromSnapshot_ClearsIsMixed_WhenNotMixedMode()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateMixed = false;
        var snapshot = CreateTestSnapshot(fixture.StatusProvider);

        // Act
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

        // Assert
        Assert.False(fixture.FrameProvider.IsMixed);
    }

    [Fact]
    public void RenderFrameFromSnapshot_MultipleFrames_CallsRendererEachTime()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act - Render 3 frames
        for (int i = 0; i < 3; i++)
        {
            var snapshot = CreateTestSnapshot(fixture.StatusProvider);
            fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);
        }

        // Assert
        Assert.Equal(3, fixture.Renderer.RenderCount);
        Assert.Equal(3, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void RenderFrameFromSnapshot_WithDifferentDisplayModes()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act & Assert - Text mode
        fixture.StatusProvider.StateTextMode = true;
        fixture.StatusProvider.StateMixed = false;
        var snapshot1 = CreateTestSnapshot(fixture.StatusProvider);
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot1);
        Assert.False(fixture.FrameProvider.IsGraphics);
        Assert.False(fixture.FrameProvider.IsMixed);

        // Act & Assert - Graphics mode
        fixture.StatusProvider.StateTextMode = false;
        fixture.StatusProvider.StateMixed = false;
        var snapshot2 = CreateTestSnapshot(fixture.StatusProvider);
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot2);
        Assert.True(fixture.FrameProvider.IsGraphics);
        Assert.False(fixture.FrameProvider.IsMixed);

        // Act & Assert - Mixed mode
        fixture.StatusProvider.StateTextMode = false;
        fixture.StatusProvider.StateMixed = true;
        var snapshot3 = CreateTestSnapshot(fixture.StatusProvider);
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot3);
        Assert.True(fixture.FrameProvider.IsGraphics);
        Assert.True(fixture.FrameProvider.IsMixed);
    }

    [Fact]
    public void RenderFrameFromSnapshot_WithNullSnapshot_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            fixture.FrameGenerator.RenderFrameFromSnapshot(null!));
    }

    [Fact]
    public void RenderFrameFromSnapshot_WithNullSoftSwitches_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = null, // Null soft switches
            FrameNumber = 1
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot));
    }

    /// <summary>
    /// Helper to create a test snapshot with current system status.
    /// </summary>
    private static VideoMemorySnapshot CreateTestSnapshot(TestStatusProvider statusProvider)
    {
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = new SystemStatusSnapshot(
                State80Store: statusProvider.State80Store,
                StateRamRd: statusProvider.StateRamRd,
                StateRamWrt: statusProvider.StateRamWrt,
                StateIntCxRom: statusProvider.StateIntCxRom,
                StateIntC8Rom: statusProvider.StateIntC8Rom,
                StateAltZp: statusProvider.StateAltZp,
                StateSlotC3Rom: statusProvider.StateSlotC3Rom,
                StatePb0: statusProvider.StatePb0,
                StatePb1: statusProvider.StatePb1,
                StatePb2: statusProvider.StatePb2,
                StateAnn0: statusProvider.StateAnn0,
                StateAnn1: statusProvider.StateAnn1,
                StateAnn2: statusProvider.StateAnn2,
                StateAnn3_DGR: statusProvider.StateAnn3_DGR,
                StatePage2: statusProvider.StatePage2,
                StateHiRes: statusProvider.StateHiRes,
                StateMixed: statusProvider.StateMixed,
                StateTextMode: statusProvider.StateTextMode,
                StateShow80Col: statusProvider.StateShow80Col,
                StateAltCharSet: statusProvider.StateAltCharSet,
                StateFlashOn: statusProvider.StateFlashOn,
                StatePrewrite: statusProvider.StatePreWrite, // Property name is StatePreWrite
                StateUseBank1: statusProvider.StateUseBank1,
                StateHighRead: statusProvider.StateHighRead,
                StateHighWrite: statusProvider.StateHighWrite,
                StateVBlank: statusProvider.StateVBlank,
                StatePdl0: statusProvider.Pdl0, // Property name is Pdl0
                StatePdl1: statusProvider.Pdl1, // Property name is Pdl1
                StatePdl2: statusProvider.Pdl2, // Property name is Pdl2
                StatePdl3: statusProvider.Pdl3, // Property name is Pdl3
                StateIntC8RomSlot: statusProvider.StateIntC8RomSlot,
                StateCurrentMhz: statusProvider.StateCurrentMhz
            ),
            FrameNumber = 1
        };
        
        return snapshot;
    }

    #endregion

    #region Integration Tests - Updated for Snapshot API

    [Fact]
    public void Integration_CompleteRenderCycle()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateTextMode = false;
        fixture.StatusProvider.StateMixed = true;
        fixture.StatusProvider.StateHiRes = true;
        fixture.StatusProvider.StatePage2 = false;

        // Act - Complete render cycle with snapshot
        var snapshot = CreateTestSnapshot(fixture.StatusProvider);
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

        // Assert
        Assert.Equal(1, fixture.FrameProvider.BorrowCount);
        Assert.Equal(1, fixture.Renderer.RenderCount);
        Assert.Equal(1, fixture.FrameProvider.CommitCount);
        Assert.True(fixture.FrameProvider.IsGraphics);
        Assert.True(fixture.FrameProvider.IsMixed);
    }

    [Fact]
    public void Integration_MultipleFrameRendering()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act - Render 5 frames
        for (int i = 0; i < 5; i++)
        {
            var snapshot = CreateTestSnapshot(fixture.StatusProvider);
            fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);
        }

        // Assert
        Assert.Equal(5, fixture.Renderer.RenderCount);
        Assert.Equal(5, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void Integration_MemoryAccessThroughSnapshot()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.MemoryReader.SetMainMemory(0x0400, 0x41); // 'A'
        fixture.MemoryReader.SetMainMemory(0x0401, 0x42); // 'B'
        fixture.MemoryReader.SetAuxMemory(0x0400, 0x61); // 'a'

        // Act - Create snapshot (would normally copy memory)
        var snapshot = CreateTestSnapshot(fixture.StatusProvider);
        
        // Note: In real usage, snapshot would contain copied memory data
        // Here we're testing the API flow, not the memory copy itself
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

        // Assert - Rendering succeeded
        Assert.Equal(1, fixture.Renderer.RenderCount);
    }

    [Fact]
    public void Integration_DisplayModeChanges()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act & Assert - Text mode
        fixture.StatusProvider.StateTextMode = true;
        var snapshot1 = CreateTestSnapshot(fixture.StatusProvider);
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot1);
        Assert.False(fixture.FrameProvider.IsGraphics);

        // Act & Assert - Graphics mode
        fixture.StatusProvider.StateTextMode = false;
        var snapshot2 = CreateTestSnapshot(fixture.StatusProvider);
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot2);
        Assert.True(fixture.FrameProvider.IsGraphics);

        // Act & Assert - Mixed mode
        fixture.StatusProvider.StateMixed = true;
        var snapshot3 = CreateTestSnapshot(fixture.StatusProvider);
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot3);
        Assert.True(fixture.FrameProvider.IsMixed);
    }

    #endregion
}
