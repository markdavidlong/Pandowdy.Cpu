# Pandowdy - Copilot Workspace Instructions

This file provides guidance for AI assistants working in the Pandowdy Apple IIe emulator codebase.

## Git Best Practices

### File Operations
- **ALWAYS use `git mv`** when moving or renaming files to preserve Git history
- **Never use create/delete cycles** for file moves - this loses history
- Check for `.git` directory before file operations
- Prefer Git-aware commands in version-controlled workspaces

### Examples
```bash
# Correct: Preserves history
git mv old/path/File.cs new/path/File.cs

# Incorrect: Loses history
# create_file(new/path/File.cs)
# remove_file(old/path/File.cs)
```

## C# Coding Style

### Braces and Control Structures
- **ALWAYS use curly braces** for control statements, even single-line statements
- This applies to: `if`, `else`, `for`, `foreach`, `while`, `do-while`, `using`, `lock`
- Never use single-line statements without braces

### Examples
```csharp
// Correct: Always use braces
if (condition)
{
    DoSomething();
}

foreach (var item in collection)
{
    ProcessItem(item);
}

// Incorrect: Missing braces (will cause IDE0011 warning)
if (condition)
    DoSomething();

if (condition) DoSomething(); // Also incorrect
```

### Other Style Guidelines
- Use `var` for local variables when type is obvious
- Prefer expression-bodied members for simple one-liners
- Use nullable reference types (`string?`, `object?`)
- Follow naming conventions: PascalCase for public members, camelCase for private fields with `_` prefix

## Project Structure

### Pandowdy.EmuCore (Emulator Core)
```
Pandowdy.EmuCore/
??? Root (Core Domain)
?   ??? VA2M.cs                    - Main emulator orchestrator
?   ??? VA2MBus.cs                 - Central bus coordinator (~570 lines, well-tested)
?   ??? MemoryPool.cs              - 128KB Apple IIe memory model
?   ??? CPUAdapter.cs              - Adapter for 6502.NET CPU
?   ??? SoftSwitch.cs              - Apple II soft switch model
?   ??? BitField16.cs              - Utility data structure
?   ??? BitmapDataArray.cs         - Core bitmap data type
?   ??? RenderContext.cs           - Rendering data structure
?
??? Services/ (Cross-cutting Services)
    ??? EmulatorStateServices.cs   - State management (EmulatorStateProvider)
    ??? SystemStatusServices.cs    - Status provider (SystemStatusProvider + ISoftSwitchResponder)
    ??? FrameProvider.cs           - Double-buffered frame management
    ??? FrameGenerator.cs          - Frame generation coordinator
    ??? CharacterRomProvider.cs    - Apple IIe character ROM provider
    ??? LegacyBitmapRenderer.cs    - Bitmap rendering service
```

### Pandowdy.EmuCore.Tests (Test Mirror)
```
Pandowdy.EmuCore.Tests/
??? Root (Core Domain Tests)
?   ??? VA2MTests.cs
?   ??? VA2MBusTests.cs            - 80+ comprehensive tests
?   ??? MemoryPoolTests.cs
?   ??? CPUAdapterTests.cs
?   ??? SoftSwitchTests.cs
?   ??? BitField16Tests.cs
?   ??? BitmapDataArrayTests.cs
?   ??? RenderingIntegrationTests.cs
?
??? Services/ (Service Tests - mirrors EmuCore/Services)
    ??? EmulatorStateProviderTests.cs
    ??? SystemStatusProviderTests.cs
    ??? FrameProviderTests.cs
    ??? FrameGeneratorTests.cs
    ??? CharacterRomProviderTests.cs
    ??? LegacyBitmapRendererTests.cs
```

### Pandowdy.UI (Avalonia GUI)
```
Pandowdy.UI/
??? ViewModels/
?   ??? MainWindowViewModel.cs    - Main UI orchestrator (ReactiveUI)
?   ??? EmulatorStateViewModel.cs - Emulator state binding
?   ??? SystemStatusViewModel.cs  - System status binding
?
??? Services/
?   ??? AvaloniaRefreshTicker.cs  - 60Hz refresh timer
?   ??? MainWindowFactory.cs      - Window creation factory
?
??? Controls/
    ??? Apple2Display.cs          - Custom Apple II display control
    ??? SoftSwitchStatusPanel.axaml - Soft switch status panel
```

### Pandowdy.UI.Tests (UI Test Project)
```
Pandowdy.UI.Tests/
??? ViewModels/
?   ??? EmulatorStateViewModelTests.cs  - 14 passing tests (? Complete)
?   ??? MainWindowViewModelTests.cs     - 25 passing tests (? Complete)
?   ??? SystemStatusViewModelTests.cs   - 14 passing tests (? Complete)
?
??? Services/
    ??? AvaloniaRefreshTickerTests.cs   - 7 passing tests (? Complete with headless)
    ??? MainWindowFactoryTests.cs       - 13 tests (9 passing, 4 skipped due to ReactiveWindow)
```

## Testing Guidelines

### Avalonia Headless Testing
- Use `[Fact]` for standard tests that don't need UI thread
- Use `[AvaloniaFact]` for tests requiring Avalonia dispatcher/threading
- Use `[Fact(Skip = "reason")]` for tests blocked by technical limitations
- Keep skipped tests with good documentation - they serve as design specs

### Test Organization
- Mirror production code structure in test projects
- Use test fixtures for complex setup
- Group tests with `#region` blocks
- Name tests: `MethodName_Scenario_ExpectedOutcome`

### What Works in Headless Mode
- ? ViewModels (ReactiveObject)
- ? ReactiveUI properties and commands
- ? DispatcherTimer operations
- ? Observable streams
- ? ReactiveWindow activation lifecycle
- ? Full window rendering
