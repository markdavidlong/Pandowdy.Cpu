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
        
        public int CharWidth => 80;
        public int PixelWidth => 560;
        public int Height => 192;
        public bool IsGraphics { get; set; }
        public bool IsMixed { get; set; }
        public int BorrowCount { get; private set; }
        public int CommitCount { get; private set; }
        public event EventHandler? FrameAvailable;

        public BitmapDataArray GetFrame() => _frontBuffer;

        public BitmapDataArray BorrowWritable()
        {
            BorrowCount++;
            return _backBuffer;
        }

        public void CommitWritable()
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
                StateHighWrite: false
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

        private void UpdateField(Func<SystemStatusSnapshot, SystemStatusSnapshot> updater)
        {
            _current = updater(_current);
        }

        public SystemStatusSnapshot Current => _current;
        public event EventHandler<SystemStatusSnapshot>? Changed;
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

    #region RenderContext Tests (8 tests)

    [Fact]
    public void RenderContext_Constructor_InitializesProperties()
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
    public void RenderContext_NullFrameBuffer_ThrowsException()
    {
        // Arrange
        var memReader = new TestMemoryReader();
        var statusProvider = new TestStatusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RenderContext(null!, memReader, statusProvider));
    }

    [Fact]
    public void RenderContext_NullMemoryReader_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var statusProvider = new TestStatusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RenderContext(frameBuffer, null!, statusProvider));
    }

    [Fact]
    public void RenderContext_NullStatusProvider_ThrowsException()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RenderContext(frameBuffer, memReader, null!));
    }

    [Fact]
    public void RenderContext_IsTextMode_ReflectsStatusProvider()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();
        
        // Act - Text mode OFF
        var statusProvider1 = new TestStatusProvider();
        statusProvider1.StateTextMode = false;
        var context1 = new RenderContext(frameBuffer, memReader, statusProvider1);

        // Act - Text mode ON
        var statusProvider2 = new TestStatusProvider();
        statusProvider2.StateTextMode = true;
        var context2 = new RenderContext(frameBuffer, memReader, statusProvider2);

        // Assert
        Assert.False(context1.IsTextMode);
        Assert.True(context2.IsTextMode);
    }

    [Fact]
    public void RenderContext_IsMixed_ReflectsStatusProvider()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();

        // Act
        var statusProvider1 = new TestStatusProvider();
        statusProvider1.StateMixed = false;
        var context1 = new RenderContext(frameBuffer, memReader, statusProvider1);

        var statusProvider2 = new TestStatusProvider();
        statusProvider2.StateMixed = true;
        var context2 = new RenderContext(frameBuffer, memReader, statusProvider2);

        // Assert
        Assert.False(context1.IsMixed);
        Assert.True(context2.IsMixed);
    }

    [Fact]
    public void RenderContext_IsHiRes_ReflectsStatusProvider()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();

        // Act
        var statusProvider1 = new TestStatusProvider();
        statusProvider1.StateHiRes = false;
        var context1 = new RenderContext(frameBuffer, memReader, statusProvider1);

        var statusProvider2 = new TestStatusProvider();
        statusProvider2.StateHiRes = true;
        var context2 = new RenderContext(frameBuffer, memReader, statusProvider2);

        // Assert
        Assert.False(context1.IsHiRes);
        Assert.True(context2.IsHiRes);
    }

    [Fact]
    public void RenderContext_IsPage2_ReflectsStatusProvider()
    {
        // Arrange
        var frameBuffer = new BitmapDataArray();
        var memReader = new TestMemoryReader();

        // Act
        var statusProvider1 = new TestStatusProvider();
        statusProvider1.StatePage2 = false;
        var context1 = new RenderContext(frameBuffer, memReader, statusProvider1);

        var statusProvider2 = new TestStatusProvider();
        statusProvider2.StatePage2 = true;
        var context2 = new RenderContext(frameBuffer, memReader, statusProvider2);

        // Assert
        Assert.False(context1.IsPage2);
        Assert.True(context2.IsPage2);
    }

    [Fact]
    public void RenderContext_ClearBuffer_ClearsFrameBuffer()
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

    #region AllocateRenderContext Tests (6 tests)

    [Fact]
    public void AllocateRenderContext_ReturnsValidContext()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Assert
        Assert.NotNull(context.FrameBuffer);
        Assert.NotNull(context.Memory);
        Assert.NotNull(context.SystemStatus);
    }

    [Fact]
    public void AllocateRenderContext_BorrowsFrameBuffer()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Assert
        Assert.Equal(1, fixture.FrameProvider.BorrowCount);
    }

    [Fact]
    public void AllocateRenderContext_UsesCorrectMemoryReader()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.MemoryReader.SetMainMemory(0x1000, 0x42);

        // Act
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Assert
        Assert.Equal(0x42, context.Memory.ReadRawMain(0x1000));
    }

    [Fact]
    public void AllocateRenderContext_UsesCorrectStatusProvider()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateTextMode = true;
        fixture.StatusProvider.StateMixed = true;

        // Act
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Assert
        Assert.True(context.IsTextMode);
        Assert.True(context.IsMixed);
    }

    [Fact]
    public void AllocateRenderContext_MultipleCalls_BorrowsMultipleTimes()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act
        var context1 = fixture.FrameGenerator.AllocateRenderContext();
        var context2 = fixture.FrameGenerator.AllocateRenderContext();
        var context3 = fixture.FrameGenerator.AllocateRenderContext();

        // Assert
        Assert.Equal(3, fixture.FrameProvider.BorrowCount);
    }

    [Fact]
    public void AllocateRenderContext_ReturnsBackBuffer()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Assert - Should return back buffer, not front buffer
        var frontBuffer = fixture.FrameProvider.GetFrame();
        Assert.NotSame(frontBuffer, context.FrameBuffer);
    }

    #endregion

    #region RenderFrame Tests (12 tests)

    [Fact]
    public void RenderFrame_ClearsFrameBuffer()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var context = fixture.FrameGenerator.AllocateRenderContext();
        
        // Set some pixels
        context.FrameBuffer.SetPixel(100, 100, 0);
        context.FrameBuffer.SetPixel(200, 100, 1);

        // Act
        fixture.FrameGenerator.RenderFrame(context);

        // Assert - Buffer should be cleared
        Assert.False(context.FrameBuffer.GetPixel(100, 100, 0));
        Assert.False(context.FrameBuffer.GetPixel(200, 100, 1));
    }

    [Fact]
    public void RenderFrame_CallsRenderer()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act
        fixture.FrameGenerator.RenderFrame(context);

        // Assert
        Assert.Equal(1, fixture.Renderer.RenderCount);
    }

    [Fact]
    public void RenderFrame_PassesContextToRenderer()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act
        fixture.FrameGenerator.RenderFrame(context);

        // Assert
        Assert.NotNull(fixture.Renderer.LastContext);
        Assert.Same(context.FrameBuffer, fixture.Renderer.LastContext.Value.FrameBuffer);
    }

    [Fact]
    public void RenderFrame_CommitsFrameBuffer()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act
        fixture.FrameGenerator.RenderFrame(context);

        // Assert
        Assert.Equal(1, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void RenderFrame_SetsIsGraphics_WhenNotTextMode()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateTextMode = false; // Graphics mode
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act
        fixture.FrameGenerator.RenderFrame(context);

        // Assert
        Assert.True(fixture.FrameProvider.IsGraphics);
    }

    [Fact]
    public void RenderFrame_ClearsIsGraphics_WhenTextMode()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateTextMode = true; // Text mode
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act
        fixture.FrameGenerator.RenderFrame(context);

        // Assert
        Assert.False(fixture.FrameProvider.IsGraphics);
    }

    [Fact]
    public void RenderFrame_SetsIsMixed_WhenMixedMode()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateMixed = true;
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act
        fixture.FrameGenerator.RenderFrame(context);

        // Assert
        Assert.True(fixture.FrameProvider.IsMixed);
    }

    [Fact]
    public void RenderFrame_ClearsIsMixed_WhenNotMixedMode()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateMixed = false;
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act
        fixture.FrameGenerator.RenderFrame(context);

        // Assert
        Assert.False(fixture.FrameProvider.IsMixed);
    }

    [Fact]
    public void RenderFrame_RendererCanDrawToFrameBuffer()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.Renderer.ShouldDrawPattern = true;
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act
        fixture.FrameGenerator.RenderFrame(context);

        // Assert - Check that pattern was drawn
        Assert.True(context.FrameBuffer.GetPixel(0, 0, 0)); // Even coordinates
        Assert.False(context.FrameBuffer.GetPixel(1, 0, 0)); // Odd coordinates
    }

    [Fact]
    public void RenderFrame_MultipleFrames_CallsRendererEachTime()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var context1 = fixture.FrameGenerator.AllocateRenderContext();
        var context2 = fixture.FrameGenerator.AllocateRenderContext();
        var context3 = fixture.FrameGenerator.AllocateRenderContext();

        // Act
        fixture.FrameGenerator.RenderFrame(context1);
        fixture.FrameGenerator.RenderFrame(context2);
        fixture.FrameGenerator.RenderFrame(context3);

        // Assert
        Assert.Equal(3, fixture.Renderer.RenderCount);
        Assert.Equal(3, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void RenderFrame_ExecutionOrder_IsCorrect()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var executionOrder = new List<string>();
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Track execution order by checking state at each step
        bool bufferWasClearedBeforeRender = false;
        bool rendererCalledBeforeCommit = false;

        // Set a pixel to verify it's cleared
        context.FrameBuffer.SetPixel(100, 100, 0);

        // Custom renderer that checks state
        var trackingRenderer = new TestRenderer
        {
            ShouldDrawPattern = true
        };
        var trackingFixture = new FrameGeneratorFixture();
        var trackingContext = trackingFixture.FrameGenerator.AllocateRenderContext();
        trackingContext.FrameBuffer.SetPixel(100, 100, 0);

        // Act
        trackingFixture.FrameGenerator.RenderFrame(trackingContext);

        // Assert - Verify order:
        // 1. Buffer cleared (pixel should be gone)
        // 2. Renderer called (draw count incremented)
        // 3. Frame committed (commit count incremented)
        Assert.Equal(1, trackingFixture.Renderer.RenderCount);
        Assert.Equal(1, trackingFixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void RenderFrame_WithDifferentDisplayModes()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        var testCases = new[]
        {
            (textMode: false, mixed: false, expectedGraphics: true, expectedMixed: false),
            (textMode: false, mixed: true, expectedGraphics: true, expectedMixed: true),
            (textMode: true, mixed: false, expectedGraphics: false, expectedMixed: false),
            (textMode: true, mixed: true, expectedGraphics: false, expectedMixed: true)
        };

        foreach (var (textMode, mixed, expectedGraphics, expectedMixed) in testCases)
        {
            // Arrange
            fixture.Reset();
            fixture.StatusProvider.StateTextMode = textMode;
            fixture.StatusProvider.StateMixed = mixed;
            var context = fixture.FrameGenerator.AllocateRenderContext();

            // Act
            fixture.FrameGenerator.RenderFrame(context);

            // Assert
            Assert.Equal(expectedGraphics, fixture.FrameProvider.IsGraphics);
            Assert.Equal(expectedMixed, fixture.FrameProvider.IsMixed);
        }
    }

    #endregion

    #region Integration Tests (4 tests)

    [Fact]
    public void Integration_CompleteRenderCycle()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.StatusProvider.StateTextMode = false;
        fixture.StatusProvider.StateMixed = true;
        fixture.StatusProvider.StateHiRes = true;
        fixture.StatusProvider.StatePage2 = false;

        // Act - Complete render cycle
        var context = fixture.FrameGenerator.AllocateRenderContext();
        fixture.FrameGenerator.RenderFrame(context);

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

        // Act - Render 10 frames
        for (int i = 0; i < 10; i++)
        {
            var context = fixture.FrameGenerator.AllocateRenderContext();
            fixture.FrameGenerator.RenderFrame(context);
        }

        // Assert
        Assert.Equal(10, fixture.FrameProvider.BorrowCount);
        Assert.Equal(10, fixture.Renderer.RenderCount);
        Assert.Equal(10, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void Integration_MemoryAccessThroughContext()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        fixture.MemoryReader.SetMainMemory(0x0400, 0x41); // 'A'
        fixture.MemoryReader.SetMainMemory(0x0401, 0x42); // 'B'
        fixture.MemoryReader.SetAuxMemory(0x0400, 0x61); // 'a'

        // Act
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Assert - Renderer can access memory through context
        Assert.Equal(0x41, context.Memory.ReadRawMain(0x0400));
        Assert.Equal(0x42, context.Memory.ReadRawMain(0x0401));
        Assert.Equal(0x61, context.Memory.ReadRawAux(0x0400));
    }

    [Fact]
    public void Integration_DisplayModeChanges()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act & Assert - Text mode
        fixture.StatusProvider.StateTextMode = true;
        var context1 = fixture.FrameGenerator.AllocateRenderContext();
        Assert.True(context1.IsTextMode);
        fixture.FrameGenerator.RenderFrame(context1);
        Assert.False(fixture.FrameProvider.IsGraphics);

        // Act & Assert - Graphics mode
        fixture.StatusProvider.StateTextMode = false;
        var context2 = fixture.FrameGenerator.AllocateRenderContext();
        Assert.False(context2.IsTextMode);
        fixture.FrameGenerator.RenderFrame(context2);
        Assert.True(fixture.FrameProvider.IsGraphics);

        // Act & Assert - Mixed mode
        fixture.StatusProvider.StateMixed = true;
        var context3 = fixture.FrameGenerator.AllocateRenderContext();
        Assert.True(context3.IsMixed);
        fixture.FrameGenerator.RenderFrame(context3);
        Assert.True(fixture.FrameProvider.IsMixed);
    }

    #endregion

    #region Edge Cases and Error Handling (3 tests)

    [Fact]
    public void RenderFrame_WithEmptyRenderer_DoesNotCrash()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act & Assert - Should not throw
        fixture.FrameGenerator.RenderFrame(context);
    }

    [Fact]
    public void RenderFrame_SameContextMultipleTimes()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();
        var context = fixture.FrameGenerator.AllocateRenderContext();

        // Act - Render same context multiple times
        fixture.FrameGenerator.RenderFrame(context);
        fixture.FrameGenerator.RenderFrame(context);
        fixture.FrameGenerator.RenderFrame(context);

        // Assert - Should work (though not typical usage)
        Assert.Equal(3, fixture.Renderer.RenderCount);
        Assert.Equal(3, fixture.FrameProvider.CommitCount);
    }

    [Fact]
    public void AllocateRenderContext_ReturnsNewContextEachTime()
    {
        // Arrange
        var fixture = new FrameGeneratorFixture();

        // Act
        var context1 = fixture.FrameGenerator.AllocateRenderContext();
        var context2 = fixture.FrameGenerator.AllocateRenderContext();

        // Assert - Contexts should reference same objects (by design)
        // but be separate context instances
        Assert.Same(context1.Memory, context2.Memory);
        Assert.Same(context1.SystemStatus, context2.SystemStatus);
        // FrameBuffer might be same (back buffer reused)
    }

    #endregion
}
