# Pandowdy.UI.Tests

Test suite for Pandowdy's Avalonia-based UI layer.

## Test Structure

### Services
- **MainWindowFactoryTests** - Factory pattern tests for window creation
- **AvaloniaRefreshTickerTests** - 60Hz refresh timing tests

### ViewModels
- **EmulatorStateViewModelTests** - Emulator state binding tests (14 passing)
- **MainWindowViewModelTests** - Main window orchestration tests (25 passing)
- **SystemStatusViewModelTests** - System status binding tests (14 passing)

## Testing Strategy

### Avalonia Headless Testing

This project uses **Avalonia.Headless.XUnit 11.3.10** to enable UI testing without a window system.

**What Works with Headless:**
- ? Control creation and initialization
- ? Property bindings and ReactiveUI observables
- ? DispatcherTimer-based operations
- ? ViewModels and their lifecycle
- ? Basic Avalonia controls

**What Doesn't Work:**
- ? `ReactiveWindow<T>` activation lifecycle (`WhenActivated()`)
- ? Full window rendering and visual tree operations
- ? Some platform-specific features

### Test Categories

#### Fully Working (69 passing tests)
Tests that run successfully in headless mode:
- All ViewModel tests with reactive properties
- AvaloniaRefreshTicker with dispatcher timing
- MainWindowFactory dependency validation
- Interface compliance tests
- **MainWindowViewModel** - all 25 tests including commands and property bindings
- **SystemStatusViewModel** - all 14 tests for Apple II system status tracking

#### Skipped with Documentation (4 tests)
Tests skipped due to technical limitations, kept for:
- **Documentation** - Shows expected API behavior
- **Design validation** - Proves the design makes sense
- **Future work** - Easy to enable when infrastructure improves
- **Manual testing** - Serves as checklist for integration testing

Skip reason:
1. `ReactiveWindow` requires full Application context (4 MainWindowFactory tests that call `MainWindow.Create()`)

## Future Improvements

- ? ~~Implement MainWindowViewModel tests~~ **DONE! 25 tests passing**
- ? ~~Implement SystemStatusViewModel tests~~ **DONE! 14 tests passing**
- Add Apple2Display control tests
- Investigate ReactiveUI headless support improvements for ReactiveWindow
- Add integration tests for full window lifecycle
- Consider UI automation tests for end-to-end scenarios

**All ViewModel testing is complete!** ??
