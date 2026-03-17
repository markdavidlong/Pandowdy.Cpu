// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Input;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Memory;
using Pandowdy.EmuCore.Video;
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

    #region Buffer Management Tests (7 tests)

    [Fact]
    public void RenderFrameFromSnapshot_WhenNoBufferAvailable_SkipsFrame()
    {
        // Arrange - Create a custom provider that returns null (all buffers busy)
        var busyProvider = new BusyFrameProvider();
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();
        var renderer = new TestRenderer();
        var frameGen = new FrameGenerator(busyProvider, memReader, statusProvider, renderer);

        // Act
        var snapshot = CreateTestSnapshot(statusProvider);
        frameGen.RenderFrameFromSnapshot(snapshot);

        // Assert - Renderer should NOT be called when no buffer available
        Assert.Equal(0, renderer.RenderCount);
        Assert.Equal(0, busyProvider.CommitCount);
    }

    [Fact]
    public void RenderFrameFromSnapshot_BufferContention_HandlesGracefully()
    {
        // Arrange - Simulate buffer contention scenario
        var fixture = new FrameGeneratorFixture();

        // Act - Render many frames rapidly (simulating 60 FPS)
        for (int i = 0; i < 100; i++)
        {
            var snapshot = CreateTestSnapshot(fixture.StatusProvider);
            fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);
        }

        // Assert - All frames should be processed (test provider never returns null)
        Assert.Equal(100, fixture.Renderer.RenderCount);
        Assert.Equal(100, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void RenderFrameFromSnapshot_CommitsBuffer_AfterRendering()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var snapshot = CreateTestSnapshot(fixture.StatusProvider);

        // Act
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

        // Assert - Buffer should be borrowed and committed
        Assert.Equal(1, fixture.FrameProvider.BorrowCount);
        Assert.Equal(1, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void RenderFrameFromSnapshot_RapidSequentialFrames_MaintainsCorrectCount()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act - Render 1000 frames rapidly (stress test)
        for (int i = 0; i < 1000; i++)
        {
            var snapshot = CreateTestSnapshot(fixture.StatusProvider);
            fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);
        }

        // Assert
        Assert.Equal(1000, fixture.FrameProvider.BorrowCount);
        Assert.Equal(1000, fixture.Renderer.RenderCount);
        Assert.Equal(1000, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void RenderFrameFromSnapshot_AlternatingDisplayModes_UpdatesMetadataCorrectly()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act & Assert - Alternate between text and graphics rapidly
        for (int i = 0; i < 50; i++)
        {
            fixture.StatusProvider.StateTextMode = (i % 2 == 0);
            fixture.StatusProvider.StateMixed = (i % 3 == 0);

            var snapshot = CreateTestSnapshot(fixture.StatusProvider);
            fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

            Assert.Equal(!fixture.StatusProvider.StateTextMode, fixture.FrameProvider.IsGraphics);
            Assert.Equal(fixture.StatusProvider.StateMixed, fixture.FrameProvider.IsMixed);
        }
    }

    [Fact]
    public void RenderFrameFromSnapshot_FrameProviderFiresEvent_OnCommit()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        int eventCount = 0;
        fixture.FrameProvider.FrameAvailable += (sender, args) => eventCount++;

        // Act - Render 5 frames
        for (int i = 0; i < 5; i++)
        {
            var snapshot = CreateTestSnapshot(fixture.StatusProvider);
            fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);
        }

        // Assert - Event should fire for each frame
        Assert.Equal(5, eventCount);
    }

    [Fact]
    public void RenderFrameFromSnapshot_BorrowsOneBufferPerFrame()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act - Render single frame
        var snapshot = CreateTestSnapshot(fixture.StatusProvider);
        fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

        // Assert - Should borrow exactly once
        Assert.Equal(1, fixture.FrameProvider.BorrowCount);
    }

    private class BusyFrameProvider : IFrameProvider
    {
        public int Width => 560;
        public int Height => 192;
        public bool IsGraphics { get; set; }
        public bool IsMixed { get; set; }
        public int CommitCount { get; private set; }
        public event EventHandler? FrameAvailable;

        public BitmapDataArray GetFrame() => new BitmapDataArray();
        public BitmapDataArray? BorrowWritable() => null; // Always busy
        public void CommitWritable(BitmapDataArray renderedBuffer)
        {
            CommitCount++;
            FrameAvailable?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion

    #region SnapshotMemoryReader Tests (8 tests)

    [Fact]
    public void SnapshotMemoryReader_ReadRawMain_WithinRange_ReturnsValue()
    {
        // Arrange
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = new SystemStatusSnapshot(
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, 0, 0, 0, 0, 0, 1.023)
        };
        snapshot.MainRam[0x0400] = 0x42;
        snapshot.MainRam[0x2000] = 0x43;
        snapshot.MainRam[0xBFFF] = 0x44;

        var reader = new SnapshotMemoryReader(snapshot);

        // Act & Assert
        Assert.Equal(0x42, reader.ReadRawMain(0x0400));
        Assert.Equal(0x43, reader.ReadRawMain(0x2000));
        Assert.Equal(0x44, reader.ReadRawMain(0xBFFF));
    }

    [Fact]
    public void SnapshotMemoryReader_ReadRawAux_WithinRange_ReturnsValue()
    {
        // Arrange
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = new SystemStatusSnapshot(
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, 0, 0, 0, 0, 0, 1.023)
        };
        snapshot.AuxRam[0x0400] = 0x52;
        snapshot.AuxRam[0x2000] = 0x53;
        snapshot.AuxRam[0xBFFF] = 0x54;

        var reader = new SnapshotMemoryReader(snapshot);

        // Act & Assert
        Assert.Equal(0x52, reader.ReadRawAux(0x0400));
        Assert.Equal(0x53, reader.ReadRawAux(0x2000));
        Assert.Equal(0x54, reader.ReadRawAux(0xBFFF));
    }

    [Fact]
    public void SnapshotMemoryReader_ReadRawMain_OutsideRange_ReturnsZero()
    {
        // Arrange
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = new SystemStatusSnapshot(
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, 0, 0, 0, 0, 0, 1.023)
        };

        var reader = new SnapshotMemoryReader(snapshot);

        // Act & Assert - Addresses outside 48KB range
        Assert.Equal(0x00, reader.ReadRawMain(-1));
        Assert.Equal(0x00, reader.ReadRawMain(0xC000));
        Assert.Equal(0x00, reader.ReadRawMain(0xC100));
        Assert.Equal(0x00, reader.ReadRawMain(0xFFFF));
    }

    [Fact]
    public void SnapshotMemoryReader_ReadRawAux_OutsideRange_ReturnsZero()
    {
        // Arrange
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = new SystemStatusSnapshot(
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, 0, 0, 0, 0, 0, 1.023)
        };

        var reader = new SnapshotMemoryReader(snapshot);

        // Act & Assert - Addresses outside 48KB range
        Assert.Equal(0x00, reader.ReadRawAux(-1));
        Assert.Equal(0x00, reader.ReadRawAux(0xC000));
        Assert.Equal(0x00, reader.ReadRawAux(0xC100));
        Assert.Equal(0x00, reader.ReadRawAux(0xFFFF));
    }

    [Fact]
    public void SnapshotMemoryReader_BoundaryAddresses_Main()
    {
        // Arrange
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = new SystemStatusSnapshot(
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, 0, 0, 0, 0, 0, 1.023)
        };
        snapshot.MainRam[0x0000] = 0xAA;
        snapshot.MainRam[0xBFFF] = 0xBB;

        var reader = new SnapshotMemoryReader(snapshot);

        // Act & Assert - Boundary addresses
        Assert.Equal(0xAA, reader.ReadRawMain(0x0000));
        Assert.Equal(0xBB, reader.ReadRawMain(0xBFFF));
        Assert.Equal(0x00, reader.ReadRawMain(0xC000)); // Just outside
    }

    [Fact]
    public void SnapshotMemoryReader_BoundaryAddresses_Aux()
    {
        // Arrange
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = new SystemStatusSnapshot(
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, 0, 0, 0, 0, 0, 1.023)
        };
        snapshot.AuxRam[0x0000] = 0xCC;
        snapshot.AuxRam[0xBFFF] = 0xDD;

        var reader = new SnapshotMemoryReader(snapshot);

        // Act & Assert - Boundary addresses
        Assert.Equal(0xCC, reader.ReadRawAux(0x0000));
        Assert.Equal(0xDD, reader.ReadRawAux(0xBFFF));
        Assert.Equal(0x00, reader.ReadRawAux(0xC000)); // Just outside
    }

    [Fact]
    public void SnapshotMemoryReader_TextPageAddresses_AccessCorrectly()
    {
        // Arrange
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = new SystemStatusSnapshot(
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, 0, 0, 0, 0, 0, 1.023)
        };
        // Text Page 1: $0400-$07FF
        snapshot.MainRam[0x0400] = 0x41; // 'A'
        snapshot.MainRam[0x07FF] = 0x5A; // 'Z'
        // Text Page 2: $0800-$0BFF
        snapshot.MainRam[0x0800] = 0x42; // 'B'
        snapshot.MainRam[0x0BFF] = 0x59; // 'Y'

        var reader = new SnapshotMemoryReader(snapshot);

        // Act & Assert - Text pages accessible
        Assert.Equal(0x41, reader.ReadRawMain(0x0400));
        Assert.Equal(0x5A, reader.ReadRawMain(0x07FF));
        Assert.Equal(0x42, reader.ReadRawMain(0x0800));
        Assert.Equal(0x59, reader.ReadRawMain(0x0BFF));
    }

    [Fact]
    public void SnapshotMemoryReader_HiResPageAddresses_AccessCorrectly()
    {
        // Arrange
        var snapshot = new VideoMemorySnapshot
        {
            SoftSwitches = new SystemStatusSnapshot(
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, false, false,
                false, false, false, false, false, 0, 0, 0, 0, 0, 1.023)
        };
        // Hi-Res Page 1: $2000-$3FFF
        snapshot.MainRam[0x2000] = 0xAA;
        snapshot.MainRam[0x3FFF] = 0xBB;
        // Hi-Res Page 2: $4000-$5FFF
        snapshot.MainRam[0x4000] = 0xCC;
        snapshot.MainRam[0x5FFF] = 0xDD;

        var reader = new SnapshotMemoryReader(snapshot);

        // Act & Assert - Hi-res pages accessible
        Assert.Equal(0xAA, reader.ReadRawMain(0x2000));
        Assert.Equal(0xBB, reader.ReadRawMain(0x3FFF));
        Assert.Equal(0xCC, reader.ReadRawMain(0x4000));
        Assert.Equal(0xDD, reader.ReadRawMain(0x5FFF));
    }

    #endregion

    #region SnapshotStatusProvider Tests (6 tests)

    [Fact]
    public void SnapshotStatusProvider_ReturnsSnapshotValues()
    {
        // Arrange
        var snapshot = new SystemStatusSnapshot(
            State80Store: true,
            StateRamRd: true,
            StateRamWrt: false,
            StateIntCxRom: true,
            StateIntC8Rom: false,
            StateAltZp: true,
            StateSlotC3Rom: false,
            StatePb0: true,
            StatePb1: false,
            StatePb2: true,
            StateAnn0: false,
            StateAnn1: true,
            StateAnn2: false,
            StateAnn3_DGR: true,
            StatePage2: false,
            StateHiRes: true,
            StateMixed: false,
            StateTextMode: true,
            StateShow80Col: false,
            StateAltCharSet: true,
            StateFlashOn: false,
            StatePrewrite: true,
            StateUseBank1: false,
            StateHighRead: true,
            StateHighWrite: false,
            StateVBlank: true,
            StatePdl0: 128,
            StatePdl1: 64,
            StatePdl2: 192,
            StatePdl3: 32,
            StateIntC8RomSlot: 7,
            StateCurrentMhz: 2.046
        );

        var provider = new SnapshotStatusProvider(snapshot);

        // Act & Assert - All properties return snapshot values
        Assert.True(provider.State80Store);
        Assert.True(provider.StateRamRd);
        Assert.False(provider.StateRamWrt);
        Assert.True(provider.StateIntCxRom);
        Assert.False(provider.StateIntC8Rom);
        Assert.True(provider.StateAltZp);
        Assert.False(provider.StateSlotC3Rom);
        Assert.True(provider.StatePb0);
        Assert.False(provider.StatePb1);
        Assert.True(provider.StatePb2);
        Assert.False(provider.StateAnn0);
        Assert.True(provider.StateAnn1);
        Assert.False(provider.StateAnn2);
        Assert.True(provider.StateAnn3_DGR);
        Assert.False(provider.StatePage2);
        Assert.True(provider.StateHiRes);
        Assert.False(provider.StateMixed);
        Assert.True(provider.StateTextMode);
        Assert.False(provider.StateShow80Col);
        Assert.True(provider.StateAltCharSet);
        Assert.False(provider.StateFlashOn);
        Assert.False(provider.StateUseBank1);
        Assert.True(provider.StateHighRead);
        Assert.False(provider.StateHighWrite);
        Assert.True(provider.StateVBlank);
        // Paddle values are not captured in snapshots (not needed for rendering)
        Assert.Equal(0, provider.Pdl0);
        Assert.Equal(0, provider.Pdl1);
        Assert.Equal(0, provider.Pdl2);
        Assert.Equal(0, provider.Pdl3);
        Assert.Equal(7, provider.StateIntC8RomSlot);
        Assert.Equal(2.046, provider.StateCurrentMhz);
    }

    [Fact]
    public void SnapshotStatusProvider_CurrentProperty_ReturnsSameSnapshot()
    {
        // Arrange
        var snapshot = new SystemStatusSnapshot(
            false, false, false, false, false, false, false,
            false, false, false, false, false, false, false,
            false, false, false, true, false, false, false,
            false, false, false, false, false, 0, 0, 0, 0, 0, 1.5
        );

        var provider = new SnapshotStatusProvider(snapshot);

        // Act & Assert
        Assert.Same(snapshot, provider.Current);
    }

    [Fact]
    public void SnapshotStatusProvider_NonRenderingProperties_ReturnDefaults()
    {
        // Arrange
        var snapshot = new SystemStatusSnapshot(
            false, false, false, false, false, false, false,
            false, false, false, false, false, false, false,
            false, false, false, false, false, false, false,
            false, false, false, false, false, 0, 0, 0, 0, 0, 1.023
        );

        var provider = new SnapshotStatusProvider(snapshot);

        // Act & Assert - Properties not in snapshot return defaults
        Assert.False(provider.StatePreWrite);
    }

    [Fact]
    public void SnapshotStatusProvider_StreamProperty_ThrowsNotSupportedException()
    {
        // Arrange
        var snapshot = new SystemStatusSnapshot(
            false, false, false, false, false, false, false,
            false, false, false, false, false, false, false,
            false, false, false, false, false, false, false,
            false, false, false, false, false, 0, 0, 0, 0, 0, 1.023
        );

        var provider = new SnapshotStatusProvider(snapshot);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _ = provider.Stream);
    }

    [Fact]
    public void SnapshotStatusProvider_Mutate_ThrowsNotSupportedException()
    {
        // Arrange
        var snapshot = new SystemStatusSnapshot(
            false, false, false, false, false, false, false,
            false, false, false, false, false, false, false,
            false, false, false, false, false, false, false,
            false, false, false, false, false, 0, 0, 0, 0, 0, 1.023
        );

        var provider = new SnapshotStatusProvider(snapshot);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            provider.Mutate(builder => builder.StateTextMode = true));
    }

    [Fact]
    public void SnapshotStatusProvider_Events_NoOp()
    {
        // Arrange
        var snapshot = new SystemStatusSnapshot(
            false, false, false, false, false, false, false,
            false, false, false, false, false, false, false,
            false, false, false, false, false, false, false,
            false, false, false, false, false, 0, 0, 0, 0, 0, 1.023
        );

        var provider = new SnapshotStatusProvider(snapshot);

        // Act & Assert - Adding/removing event handlers should not throw
        EventHandler<SystemStatusSnapshot>? handler = (sender, args) => { };
        provider.Changed += handler;
        provider.Changed -= handler;
        provider.MemoryMappingChanged += handler;
        provider.MemoryMappingChanged -= handler;
    }

    #endregion

    #region Legacy API Tests (2 tests)

    [Fact]
    public void AllocateRenderContext_ReturnsValidContext()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context.FrameBuffer);
        Assert.Equal(1, fixture.FrameProvider.BorrowCount);
    }

    [Fact]
    public void RenderFrame_ThrowsNotSupportedException()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act & Assert - Legacy method not supported
        Assert.Throws<NotSupportedException>(() =>
            fixture.FrameGenerator.RenderFrame(context));
    }

    #endregion

    #region Stress and Edge Case Tests (2 tests)

    [Fact]
    public void RenderFrameFromSnapshot_LargeSequence_MaintainsStability()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act - Render equivalent to 1 minute at 60 FPS (3600 frames)
        for (int i = 0; i < 3600; i++)
        {
            // Vary display modes throughout
            fixture.StatusProvider.StateTextMode = (i % 10 < 3);
            fixture.StatusProvider.StateMixed = (i % 15 < 5);
            fixture.StatusProvider.StateHiRes = (i % 20 < 10);

            var snapshot = CreateTestSnapshot(fixture.StatusProvider);
            fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);
        }

        // Assert
        Assert.Equal(3600, fixture.Renderer.RenderCount);
        Assert.Equal(3600, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void RenderFrameFromSnapshot_AllDisplayModeCombinations_HandlesCorrectly()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var combinations = new[]
        {
            (TextMode: true, Mixed: false, Expected: (IsGraphics: false, IsMixed: false)),
            (TextMode: false, Mixed: false, Expected: (IsGraphics: true, IsMixed: false)),
            (TextMode: true, Mixed: true, Expected: (IsGraphics: false, IsMixed: true)),
            (TextMode: false, Mixed: true, Expected: (IsGraphics: true, IsMixed: true))
        };

        // Act & Assert - Test all combinations
        foreach (var combo in combinations)
        {
            fixture.StatusProvider.StateTextMode = combo.TextMode;
            fixture.StatusProvider.StateMixed = combo.Mixed;

            var snapshot = CreateTestSnapshot(fixture.StatusProvider);
            fixture.FrameGenerator.RenderFrameFromSnapshot(snapshot);

            Assert.Equal(combo.Expected.IsGraphics, fixture.FrameProvider.IsGraphics);
            Assert.Equal(combo.Expected.IsMixed, fixture.FrameProvider.IsMixed);
        }
    }

    #endregion
}
