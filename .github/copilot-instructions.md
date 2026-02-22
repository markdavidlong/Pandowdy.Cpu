# Pandowdy - Copilot Workspace Instructions

This file provides guidance for AI assistants working in the Pandowdy Apple IIe emulator codebase.

## Miscellaneous Utilities

The `misc_utils/` directory (at solution root level, not in any project) contains utility scripts:
- `convert-to-utf8.ps1` - Convert C# files to UTF-8 without BOM
- `convert-md-to-utf8.ps1` - Convert Markdown files to UTF-8 without BOM
- `convert-axaml-to-utf8.ps1` - Convert AXAML files to UTF-8 without BOM

These scripts are maintenance tools and not part of the build process.

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

### Property Formatting
- **Multi-line format for properties with non-default accessors** (getters/setters with logic or access modifiers)
- **Single-line format ONLY for simple auto-properties** with default `{ get; set; }`

### Examples
```csharp
// Correct: Multi-line for properties with logic or private setters
public bool ThrottleEnabled
{
    get => _throttleEnabled;
    set => this.RaiseAndSetIfChanged(ref _throttleEnabled, value);
}

public string Name
{
    get => _name;
    private set => _name = value;
}

// Correct: Single-line for simple auto-properties
public string Title { get; set; }
public int Count { get; init; }
public bool IsEnabled { get; set; } = true;

// Incorrect: Single-line for properties with logic (hard to read)
public bool ThrottleEnabled { get => _throttleEnabled; set => this.RaiseAndSetIfChanged(ref _throttleEnabled, value); }
```

### Other Style Guidelines
- Use `var` for local variables when type is obvious
- Prefer expression-bodied members for simple one-liners
- Use nullable reference types (`string?`, `object?`)
- Follow naming conventions: PascalCase for public members, camelCase for private fields with `_` prefix
- **Prefer using Primary Constructors (C# 12)** when class initialization is straightforward (i.e., no complicated tasks at construction aside from field assignments). This is especially important when generating Unit Tests, where this pattern is often forgotten.
- **Use interface-based constructors** for decorator patterns; consider concrete-type constructors a workaround that should be documented as technical debt.

## Project Structure

### Pandowdy.EmuCore (Emulator Core)
```
Pandowdy.EmuCore/
├── Machine/ (Emulator orchestration)
│   ├── VA2M.cs                    - Main emulator orchestrator
│   ├── VA2MBus.cs                 - Central bus coordinator
│   ├── IEmulatorCoreInterface.cs  - Top-level emulator interface
│   ├── IEmulatorState.cs          - Emulator state contract
│   ├── EmulatorStateServices.cs   - State management (EmulatorStateProvider)
│   ├── IAppleIIBus.cs             - Bus interface
│   ├── CpuClockingCounters.cs     - CPU cycle counting and timing
│   ├── CpuStateSnapshot.cs        - Immutable CPU state for UI/debug
│   ├── IRestartable.cs            - Cold-boot restart contract
│   ├── RestartCollection.cs       - Priority-ordered batch restart
│   └── CapabilityAttribute.cs     - DI auto-discovery attribute
│
├── Memory/ (RAM, ROM, address space)
│   ├── AddressSpaceController.cs  - Address space routing
│   ├── SystemRamSelector.cs       - Main/aux 48K RAM routing
│   ├── LanguageCard.cs            - 16K Language Card banking
│   ├── MemoryBlock.cs             - Raw memory storage
│   ├── SystemRomProvider.cs       - ROM loading service
│   ├── MemoryInspector.cs         - Memory inspection for debugger
│   └── (interfaces: ISystemRam, ISystemRamSelector, ILanguageCard, etc.)
│
├── IO/ (Soft switches and I/O handling)
│   ├── SoftSwitches.cs            - Apple II soft switch model
│   ├── SystemIoHandler.cs         - I/O address space handler
│   ├── NoSlotClockIoHandler.cs    - Dallas DS1216 No-Slot Clock
│   ├── SystemStatusServices.cs    - Status provider (SystemStatusProvider)
│   └── (interfaces: ISystemIoHandler, ISystemStatusProvider, etc.)
│
├── Input/ (Keyboard and game controller)
│   ├── QueuedKeyHandler.cs        - Keyboard with key queue
│   ├── SingularKeyHandler.cs      - Simple single-key handler
│   ├── SimpleGameController.cs    - Game controller buttons
│   └── (interfaces: IKeyboardSetter, IKeyboardReader, IGameControllerStatus)
│
├── Slots/ (Expansion card system)
│   ├── Slots.cs                   - Slot manager (7 slots)
│   ├── NullCard.cs                - Empty slot placeholder
│   ├── CardFactory.cs             - Card creation and cloning
│   ├── CardResponseChannel.cs     - Card message response bus
│   └── (interfaces: ICard, ISlots, ICardMessage, ICardFactory, etc.)
│
├── DiskII/ (Disk II controller subsystem)
│   ├── DiskIIControllerCard.cs    - Base controller card
│   ├── DiskIIControllerCard13Sector.cs - 13-sector variant
│   ├── DiskIIControllerCard16Sector.cs - 16-sector variant
│   ├── DiskIIDrive.cs             - Physical drive emulation
│   ├── DiskIIFactory.cs           - Drive creation factory
│   ├── DiskStatusServices.cs      - Drive status provider/mutator
│   ├── InternalDiskImage.cs       - In-memory disk image
│   ├── Messages/                  - Disk-specific card messages
│   ├── Providers/                 - Disk image format providers
│   ├── Importers/                 - Disk image importers
│   └── Exporters/                 - Disk image exporters
│
├── Video/ (Rendering pipeline)
│   ├── RenderingService.cs        - Main rendering coordinator
│   ├── FrameGenerator.cs          - Frame generation coordinator
│   ├── FrameProvider.cs           - Double-buffered frame management
│   ├── CharacterRomProvider.cs    - Apple IIe character ROM
│   ├── LegacyBitmapRenderer.cs    - Bitmap rendering service
│   ├── VideoMemorySnapshotPool.cs - Video memory snapshot pooling
│   └── (interfaces: IFrameGenerator, IFrameProvider, etc.)
│
└── DataTypes/
    └── BitField16.cs              - 16-bit field utility
```

### Pandowdy.EmuCore.Tests (Test Mirror)
```
Pandowdy.EmuCore.Tests/
├── Machine/                       - VA2M, VA2MBus, CpuClockingCounters tests
├── Memory/                        - AddressSpaceController, RAM, LanguageCard tests
├── IO/                            - SoftSwitches, SystemIoHandler tests
├── Input/                         - Keyboard, game controller tests
├── Slots/                         - Slots, card messaging tests
├── Cards/                         - DiskII controller card tests
├── DiskII/                        - DiskII integration/specification tests
├── DataTypes/                     - BitField16, RestartCollection tests
├── Video/                         - Rendering tests
├── Services/                      - Cross-cutting service tests
├── Messages/                      - Card message tests
├── Helpers/                       - Test builders and utilities
└── IntegrationTests/              - Cross-subsystem integration tests
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
- ✅ ViewModels (ReactiveObject)
- ✅ ReactiveUI properties and commands
- ✅ DispatcherTimer operations
- ✅ Observable streams
- ✅ ReactiveWindow activation lifecycle
- ✅ Full window rendering

## Hardware Emulation Architecture

### Disk II Controller Motor State (Since 2026-01)
The Disk II controller emulation reflects hardware-accurate motor control:

**Architecture:**
- **Controller-Level Motor State:** The `DiskIIControllerCard` owns the motor state (Off/On/ScheduledOff)
- **Single Motor Line:** One motor line powers only the currently selected drive at a time
- **Passive Drives:** `IDiskIIDrive` implementations are passive mechanical devices (head position, disk media)
- **Motor Property Removed:** Drives no longer have a `MotorOn` property (removed 2026-01)

**Key Design Points:**
- Motor state is checked via `DiskIIControllerCard.IsMotorRunning` (internal, exposed to tests via InternalsVisibleTo)
- When switching drives with motor on, the motor stays ON - it just powers the newly selected drive
- Motor-off delay (~1 second) is managed by the controller, not individual drives
- `DiskIIStatusDecorator` publishes drive mechanical state (track, disk insertion) but not motor state
- Controller publishes motor state via `IDiskStatusMutator`

**For Code Changes:**
- Never add motor state to drive implementations
- Motor checks should be at controller level only
- Drive operations (GetBit, SetBit, head stepping) assume controller has already verified motor is running
- Test assertions should check `controller.IsMotorRunning`, not per-drive motor state

**Reference:** See `docs/DiskII-Motor-Refactoring-Plan.md` for complete refactoring history (8 phases, completed 2026-01)

## Disk Image Formats
- When discussing disk image formats, use "nibble format" instead of ".nib" or ".nib extension" to work around rendering/display issues in VS.

## Keyboard Shortcuts

### Apple IIe Keyboard Emulation
- EmuCore captures Ctrl-@ and Ctrl+A through Ctrl+/ (ASCII 0-31) for Apple IIe keyboard emulation.
- GUI keyboard shortcuts must avoid bare Ctrl+letter combinations.
- Preferred patterns: 
  - Ctrl+Shift+letter
  - Ctrl+Alt+letter
  - Alt+letter
  - Multi-keystroke sequences
- Simple Ctrl+letter is reserved for the emulator (unless focused in a future editor window).
