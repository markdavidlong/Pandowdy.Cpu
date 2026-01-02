using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Pandowdy.UI.ViewModels;
using System.Reactive.Subjects;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for EmulatorStateViewModel - binds emulator state to UI.
/// Tests reactive property updates, observable subscriptions, and threading.
/// </summary>
public class EmulatorStateViewModelTests
{
    #region Test Helpers

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

        public void Update(StateSnapshot snapshot)
        {
            _subject.OnNext(snapshot);
        }

        public void RequestPause() { }
        public void RequestContinue() { }
        public void RequestStep() { }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidState_CreatesViewModel()
    {
        // Arrange
        var state = new TestEmulatorState();

        // Act
        var viewModel = new EmulatorStateViewModel(state);

        // Assert
        Assert.NotNull(viewModel);
        Assert.NotNull(viewModel.Activator);
    }

    [Fact]
    public void Constructor_InitializesPropertiesWithDefaultValues()
    {
        // Arrange
        var state = new TestEmulatorState();

        // Act
        var viewModel = new EmulatorStateViewModel(state);

        // Assert - Before activation, properties should be at defaults
        Assert.Equal(0, viewModel.PC);
        Assert.Equal(0UL, viewModel.Cycles);
        Assert.Null(viewModel.LineNumber);
    }

    #endregion

    #region Property Update Tests

    [Fact]
    public void PC_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var state = new TestEmulatorState();
        var viewModel = new EmulatorStateViewModel(state);
        bool propertyChanged = false;
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EmulatorStateViewModel.PC))
                propertyChanged = true;
        };

        // Activate to start subscriptions
        using (viewModel.Activator.Activate())
        {
            // Act
            state.Update(new StateSnapshot(PC: 0x1234, SP: 0, Cycles: 100, LineNumber: null, IsRunning: true, IsPaused: false));
            
            // Give time for observable to propagate
            System.Threading.Thread.Sleep(100);

            // Assert
            Assert.True(propertyChanged, "PropertyChanged should be raised for PC");
        }
    }

    [Fact]
    public void Cycles_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var state = new TestEmulatorState();
        var viewModel = new EmulatorStateViewModel(state);
        bool propertyChanged = false;
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EmulatorStateViewModel.Cycles))
                propertyChanged = true;
        };

        // Activate to start subscriptions
        using (viewModel.Activator.Activate())
        {
            // Act
            state.Update(new StateSnapshot(PC: 0, SP: 0, Cycles: 1000000, LineNumber: null, IsRunning: true, IsPaused: false));
            
            System.Threading.Thread.Sleep(100);

            // Assert
            Assert.True(propertyChanged);
        }
    }

    [Fact]
    public void LineNumber_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var state = new TestEmulatorState();
        var viewModel = new EmulatorStateViewModel(state);
        bool propertyChanged = false;
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EmulatorStateViewModel.LineNumber))
                propertyChanged = true;
        };

        // Activate to start subscriptions
        using (viewModel.Activator.Activate())
        {
            // Act
            state.Update(new StateSnapshot(PC: 0, SP: 0, Cycles: 0, LineNumber: 100, IsRunning: true, IsPaused: false));
            
            System.Threading.Thread.Sleep(100);

            // Assert
            Assert.True(propertyChanged);
        }
    }

    #endregion

    #region Activation Tests

    [Fact]
    public void Activation_StartsSubscriptions()
    {
        // Arrange
        var state = new TestEmulatorState();
        var viewModel = new EmulatorStateViewModel(state);

        // Act - Activate the ViewModel
        using (viewModel.Activator.Activate())
        {
            state.Update(new StateSnapshot(PC: 0xABCD, SP: 0xFF, Cycles: 5000, LineNumber: 42, IsRunning: true, IsPaused: false));
            
            System.Threading.Thread.Sleep(100);

            // Assert - Values should update after activation
            Assert.Equal(0xABCD, viewModel.PC);
            Assert.Equal(5000UL, viewModel.Cycles);
            Assert.Equal(42, viewModel.LineNumber);
        }
    }

    [Fact]
    public void Deactivation_StopsSubscriptions()
    {
        // Arrange
        var state = new TestEmulatorState();
        var viewModel = new EmulatorStateViewModel(state);

        // Act - Activate and then deactivate
        var activationHandle = viewModel.Activator.Activate();
        state.Update(new StateSnapshot(PC: 0x1000, SP: 0, Cycles: 100, LineNumber: null, IsRunning: true, IsPaused: false));
        System.Threading.Thread.Sleep(100);
        
        var pcBeforeDispose = viewModel.PC;
        activationHandle.Dispose();

        // Update state after deactivation
        state.Update(new StateSnapshot(PC: 0x2000, SP: 0, Cycles: 200, LineNumber: null, IsRunning: true, IsPaused: false));
        System.Threading.Thread.Sleep(100);

        // Assert - PC should not change after deactivation
        Assert.Equal(0x1000, viewModel.PC);
        Assert.NotEqual(0x2000, viewModel.PC);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void Scenario_TypicalEmulationRun_UpdatesPropertiesSequentially()
    {
        // Arrange
        var state = new TestEmulatorState();
        var viewModel = new EmulatorStateViewModel(state);

        using (viewModel.Activator.Activate())
        {
            // Act - Simulate emulation progress
            state.Update(new StateSnapshot(PC: 0x0800, SP: 0xFF, Cycles: 0, LineNumber: null, IsRunning: true, IsPaused: false));
            System.Threading.Thread.Sleep(100);
            
            state.Update(new StateSnapshot(PC: 0x0803, SP: 0xFE, Cycles: 100, LineNumber: null, IsRunning: true, IsPaused: false));
            System.Threading.Thread.Sleep(100);
            
            state.Update(new StateSnapshot(PC: 0x0810, SP: 0xFD, Cycles: 1000, LineNumber: 10, IsRunning: true, IsPaused: false));
            System.Threading.Thread.Sleep(100);

            // Assert
            Assert.Equal(0x0810, viewModel.PC);
            Assert.Equal(1000UL, viewModel.Cycles);
            Assert.Equal(10, viewModel.LineNumber);
        }
    }

    #endregion
}
