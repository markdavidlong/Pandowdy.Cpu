using Avalonia;
using Avalonia.Headless;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Pandowdy.UI.ViewModels;
using Pandowdy.UI.Interfaces;
using Emulator;
using System.Reactive.Subjects;

namespace Pandowdy.UI.Tests.Services;

/// <summary>
/// Tests for MainWindowFactory - creates and initializes MainWindow instances.
/// Tests factory pattern implementation and dependency injection.
/// 
/// Note: Some tests are skipped because MainWindow inherits from ReactiveWindow
/// which requires full Avalonia Application context for WhenActivated lifecycle.
/// Avalonia.Headless.XUnit doesn't fully support ReactiveUI activation in headless mode.
/// These tests are valuable as documentation of expected behavior and can be run
/// as manual integration tests or when better headless support becomes available.
/// </summary>
public class MainWindowFactoryTests
{
    #region Test Helpers and Mocks

    /// <summary>
    /// Test stub for IRefreshTicker that provides a controllable 60Hz stream.
    /// </summary>
    private class TestRefreshTicker : IRefreshTicker
    {
        private readonly Subject<DateTime> _subject = new();

        public IObservable<DateTime> Stream => _subject;

        public void Start() { }
        public void Stop() { }

        public void Tick(DateTime timestamp) => _subject.OnNext(timestamp);
    }

    /// <summary>
    /// Test stub for IEmulatorState that allows manual state updates.
    /// </summary>
    private class TestEmulatorState : IEmulatorState
    {
        private readonly BehaviorSubject<StateSnapshot> _subject;

        public TestEmulatorState()
        {
            _subject = new BehaviorSubject<StateSnapshot>(
                new StateSnapshot(PC: 0, SP: 0, Cycles: 0, LineNumber: null, IsRunning: false, IsPaused: false));
        }

        public IObservable<StateSnapshot> Stream => _subject;
        public StateSnapshot GetCurrent() => _subject.Value;
        public void Update(StateSnapshot snapshot) => _subject.OnNext(snapshot);
        public void RequestPause() { }
        public void RequestContinue() { }
        public void RequestStep() { }
    }

    /// <summary>
    /// Helper fixture to create all dependencies for MainWindowFactory.
    /// </summary>
    private class MainWindowFactoryFixture
    {
        public MainWindowViewModel ViewModel { get; }
        public VA2M Machine { get; }
        public IFrameProvider FrameProvider { get; }
        public IRefreshTicker RefreshTicker { get; }
        public MainWindowFactory Factory { get; }

        public MainWindowFactoryFixture()
        {
            // Create core dependencies
            var statusProvider = new SystemStatusProvider();
            var memoryPool = new MemoryPool(statusProvider, new TestLanguageCard());
            var stateProvider = new EmulatorStateProvider();
            var cpu = new CPUAdapter(new Emulator.CPU());
            var frameProvider = new FrameProvider();
            
            // Create renderer and frame generator dependencies
            var charRomProvider = new CharacterRomProvider();
            var bitmapRenderer = new LegacyBitmapRenderer(charRomProvider);
            var frameGenerator = new FrameGenerator(frameProvider, memoryPool, statusProvider, bitmapRenderer);
            
            var bus = new VA2MBus(memoryPool, statusProvider, cpu);
            
            Machine = new VA2M(stateProvider, frameProvider, statusProvider, bus, memoryPool, frameGenerator);
            FrameProvider = frameProvider;
            RefreshTicker = new TestRefreshTicker();

            // Create ViewModels
            var emulatorStateViewModel = new EmulatorStateViewModel(stateProvider);
            var systemStatusViewModel = new SystemStatusViewModel(statusProvider);
            ViewModel = new MainWindowViewModel(emulatorStateViewModel, stateProvider, systemStatusViewModel);

            // Create factory
            Factory = new MainWindowFactory(ViewModel, Machine, FrameProvider, RefreshTicker);
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesFactory()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act
        var factory = new MainWindowFactory(
            fixture.ViewModel,
            fixture.Machine,
            fixture.FrameProvider,
            fixture.RefreshTicker);

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Constructor_NullViewModel_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowFactory(null!, fixture.Machine, fixture.FrameProvider, fixture.RefreshTicker));
    }

    [Fact]
    public void Constructor_NullMachine_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowFactory(fixture.ViewModel, null!, fixture.FrameProvider, fixture.RefreshTicker));
    }

    [Fact]
    public void Constructor_NullFrameProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowFactory(fixture.ViewModel, fixture.Machine, null!, fixture.RefreshTicker));
    }

    [Fact]
    public void Constructor_NullRefreshTicker_ThrowsArgumentNullException()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowFactory(fixture.ViewModel, fixture.Machine, fixture.FrameProvider, null!));
    }

    #endregion

    #region Interface Compliance Tests

    [Fact]
    public void ImplementsIMainWindowFactory()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act
        var factory = fixture.Factory;

        // Assert
        Assert.IsAssignableFrom<IMainWindowFactory>(factory);
    }

    #endregion

    #region Factory Method Tests

    // Suppress xUnit1004 for intentionally skipped tests that document behavior
    // but cannot run in headless test environment (require full Avalonia Application context)
#pragma warning disable xUnit1004 // Test methods should not be skipped

    [Fact(Skip = "ReactiveWindow requires full Application context for WhenActivated - not supported in headless mode")]
    public void Create_ReturnsMainWindowInstance()
    {
        // This test documents expected behavior but can't run in unit test context
        // because MainWindow : ReactiveWindow<TViewModel> calls WhenActivated in constructor
        // which requires IActivationForViewFetcher implementation not available in headless mode.
        //
        // To verify manually:
        // 1. Run the actual application
        // 2. Verify MainWindow creates successfully
        // 3. Check that ViewModel is properly injected
        
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act
        var window = fixture.Factory.Create();

        // Assert
        Assert.NotNull(window);
        Assert.IsType<MainWindow>(window);
    }

    [Fact(Skip = "ReactiveWindow requires full Application context - see Create_ReturnsMainWindowInstance")]
    public void Create_ConfiguresWindow_WithViewModel()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act
        var window = fixture.Factory.Create();

        // Assert
        Assert.NotNull(window.ViewModel);
        Assert.Same(fixture.ViewModel, window.ViewModel);
        Assert.Same(fixture.ViewModel, window.DataContext);
    }

    [Fact(Skip = "ReactiveWindow requires full Application context - see Create_ReturnsMainWindowInstance")]
    public void Create_MultipleCalls_ReturnsDifferentInstances()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act
        var window1 = fixture.Factory.Create();
        var window2 = fixture.Factory.Create();

        // Assert
        Assert.NotSame(window1, window2);
    }

    [Fact(Skip = "ReactiveWindow requires full Application context - see Create_ReturnsMainWindowInstance")]
    public void Create_InitializesDependencies_Atomically()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act
        var window = fixture.Factory.Create();

        // Assert - Window should be fully initialized and ready to use
        Assert.NotNull(window.ViewModel);
        Assert.NotNull(window.DataContext);
    }

#pragma warning restore xUnit1004 // Test methods should not be skipped

    #endregion

    #region Integration Scenarios

    [Fact]
    public void Scenario_FactoryCanBeCreated_WithTypicalDependencies()
    {
        // Arrange & Act
        var fixture = new MainWindowFactoryFixture();

        // Assert - All dependencies should wire up correctly
        Assert.NotNull(fixture.Factory);
        Assert.NotNull(fixture.ViewModel);
        Assert.NotNull(fixture.Machine);
        Assert.NotNull(fixture.FrameProvider);
        Assert.NotNull(fixture.RefreshTicker);
    }

    [Fact]
    public void Scenario_FactoryPattern_EncapsulatesWindowCreation()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();
        IMainWindowFactory factory = fixture.Factory;

        // Act & Assert - Factory interface hides implementation details
        Assert.NotNull(factory);
        Assert.IsAssignableFrom<IMainWindowFactory>(factory);
    }

    [Fact]
    public void Scenario_FactoryCreation_ValidatesAllDependencies()
    {
        // Arrange
        var fixture = new MainWindowFactoryFixture();

        // Act - Factory constructor validates all dependencies
        var exception = Record.Exception(() => 
            new MainWindowFactory(fixture.ViewModel, fixture.Machine, fixture.FrameProvider, fixture.RefreshTicker));

        // Assert - No exceptions thrown when all dependencies valid
        Assert.Null(exception);
    }

    #endregion

    /// <summary>
    /// Mock Language Card for UI tests that don't need full Language Card functionality.
    /// </summary>
    private class TestLanguageCard : ILanguageCard
    {
        public int Size => 0x3000;
        public byte Read(ushort address) => 0xFF;
        public void Write(ushort address, byte value) { }
        public byte this[ushort address]
        {
            get => 0xFF;
            set { }
        }
    }
}
