# Task 5: GUI Disk Management — Active Development Guide

> **📌 Streamlined development guide** for remaining Task 5 work.
> Supersedes the original design document as of 2026-02-13.
> Original document archived for historical reference.

---

## Table of Contents
1. [Current Status](#current-status)
2. [Design Principles](#design-principles)
3. [Phase 3B-2: Command-Dialog Integration (Completed)](#phase-3b-2-command-dialog-integration)
4. [Phase 3C: Settings and Persistence Services](#phase-3c-settings-and-persistence-services)
5. [Phase 3D: Peripherals Menu and Polish](#phase-3d-peripherals-menu-and-polish)
6. [Files to Create](#files-to-create)
7. [Files to Modify](#files-to-modify)
8. [Testing Strategy](#testing-strategy)
9. [Key Architecture Decisions](#key-architecture-decisions)
10. [Coding Standards](#coding-standards)

---

## Current Status

### ✅ Completed (Reference Only)

| Phase | Completed | Summary |
|-------|-----------|---------|
| Phase 1 | 2026-01 | Card message infrastructure, 25 tests |
| Phase 2 | 2026-02-10 | Disk II messages, DiskFormatHelper, 54 tests |
| Phase 3A | 2026-02-11 | DiskCardPanel, context menus, commands, 7 tests |
| Phase 3B-1 | 2026-02-11 | DiskFileDialogService, drag-drop, dirty indicator, 15 tests |
| Phase 3B-2 | 2026-02-13 | IMessageBoxService, error handling, dirty eject confirmation, 3 tests |

**Total tests passing:** 2065 (175 UI + 1890 EmuCore)

### 📋 Remaining Phases

- **Phase 3C:** Settings and persistence services
- **Phase 3D:** Peripherals menu and polish

---

## Design Principles

1. **Generic card messages:** `IEmulatorCoreInterface.SendCardMessageAsync(SlotNumber?, ICardMessage)` routes to cards — no disk-specific APIs on core interface.
2. **Thread-safe:** All messages enqueued via `VA2M.Enqueue()`, executed at instruction boundaries.
3. **Card-centric UI:** `DiskCardPanel` groups drives by controller; discovers cards via `IdentifyCardMessage` broadcast.
4. **Never overwrite originals:** Imports create `InternalDiskImage` in memory; saves go to `DestinationFilePath` (derived with `_new` suffix).
5. **Error handling:** `CardMessageException` for failures; UI catches and shows via `IMessageBoxService`.
6. **Forgiving APIs:** No-op states (eject empty drive) are silent; only true failures throw.
7. **Third-party boundaries:** DiskArc, FileConv, CommonUtil are read-only dependencies.

---

## Phase 3B-2: Command-Dialog Integration

### IMessageBoxService

**Interface (`Pandowdy.UI\Interfaces\IMessageBoxService.cs`):**

```csharp
/// <summary>
/// Service for displaying message boxes and dialogs to the user.
/// </summary>
public interface IMessageBoxService
{
    /// <summary>
    /// Displays an error message dialog.
    /// </summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Displays a confirmation dialog with Yes/No buttons.
    /// </summary>
    /// <returns>True if user clicked Yes, false if No.</returns>
    Task<bool> ShowConfirmationAsync(string title, string message);
}
```

### Updated DiskStatusWidgetViewModel Constructor

```csharp
public DiskStatusWidgetViewModel(
    IEmulatorCoreInterface emulator,
    IDiskFileDialogService fileDialogService,
    IMessageBoxService messageBoxService,
    SlotNumber slot,
    int driveNumber)
```

### Command Patterns

**InsertDiskCommand:**
```csharp
InsertDiskCommand = ReactiveCommand.CreateFromTask(async () =>
{
    var filePath = await _fileDialogService.ShowOpenFileDialogAsync();
    if (!string.IsNullOrEmpty(filePath))
    {
        try
        {
            await _emulator.SendCardMessageAsync(_slot, new InsertDiskMessage(_driveNumber, filePath));
        }
        catch (CardMessageException ex)
        {
            await _messageBoxService.ShowErrorAsync("Insert Disk Failed", ex.Message);
        }
    }
});
```

**EjectDiskCommand (with dirty confirmation):**
```csharp
EjectDiskCommand = ReactiveCommand.CreateFromTask(async () =>
{
    if (IsDirty)
    {
        var confirmed = await _messageBoxService.ShowConfirmationAsync(
            "Unsaved Changes",
            "Disk has unsaved changes. Eject anyway?");
        if (!confirmed)
        {
            return;
        }
    }
    await _emulator.SendCardMessageAsync(_slot, new EjectDiskMessage(_driveNumber));
}, hasDiskObservable);
```

**SaveAsCommand:**
```csharp
SaveAsCommand = ReactiveCommand.CreateFromTask(async () =>
{
    var filePath = await _fileDialogService.ShowSaveFileDialogAsync(DiskImageFilename);
    if (!string.IsNullOrEmpty(filePath))
    {
        try
        {
            await _emulator.SendCardMessageAsync(_slot, new SaveDiskAsMessage(_driveNumber, filePath));
        }
        catch (CardMessageException ex)
        {
            await _messageBoxService.ShowErrorAsync("Save Disk Failed", ex.Message);
        }
    }
}, hasDiskObservable);
```

---

## Phase 3C: Settings and Persistence Services

### Settings Service

**Model (`Pandowdy.UI\Models\PandowdySettings.cs`):**
```csharp
public class PandowdySettings
{
    public int Version { get; set; } = 1;
    public string LastExportDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public double DiskPanelWidth { get; set; } = 150.0;
}
```

**Interface (`Pandowdy.UI\Interfaces\ISettingsService.cs`):**
```csharp
public interface ISettingsService
{
    string LastExportDirectory { get; set; }
    double DiskPanelWidth { get; set; }
    Task LoadAsync();
    Task SaveAsync();
}
```

**Storage:** `%AppData%/Pandowdy/pandowdy-settings.json`

### Drive State Service

**Model:**
```csharp
public class DriveStateEntry
{
    public string Slot { get; set; } = "";      // e.g., "Slot6"
    public int DriveNumber { get; set; }
    public string? DiskImagePath { get; set; }
}

public class DriveStateConfig
{
    public int Version { get; set; } = 1;
    public List<DriveStateEntry> Drives { get; set; } = new();
}
```

**Interface (`Pandowdy.UI\Interfaces\IDriveStateService.cs`):**
```csharp
public interface IDriveStateService
{
    Task LoadAndRestoreDriveStateAsync(IEmulatorCoreInterface emulator);
    Task SaveDriveStateAsync(SlotNumber slot, int driveNumber, string? diskImagePath);
}
```

**Storage:** `%AppData%/Pandowdy/drive-state.json`

**Program.cs change:** Replace hardcoded disk inserts with:
```csharp
var driveStateService = serviceProvider.GetRequiredService<IDriveStateService>();
await driveStateService.LoadAndRestoreDriveStateAsync(coreInterface);
```

---

## Phase 3D: Peripherals Menu and Polish

### Peripherals Menu Structure

```
Peripherals
├── Disks
│   ├── Slot 6 — Disk II Controller
│   │   ├── S6D1 - game.woz      → opens drive dialog
│   │   └── S6D2 - (empty)       → opens drive dialog
│   └── Slot 5 — Disk II Controller
│       └── ...
└── (future: Communication, Audio, etc.)
```

**Menu building:**
1. Subscribe to `ICardResponseProvider.Responses`
2. Broadcast `IdentifyCardMessage` to all slots
3. Filter out NullCard (CardId == 0)
4. Group disk controllers under "Disks"
5. Populate drive details from `IDiskStatusProvider.Current`
6. Rebuild on `IDiskStatusProvider.Stream` changes

### Other Phase 3D Items

- **Write-protect toggle:** Context menu checkbox → `SetWriteProtectMessage`
- **Disk label elision:** `TextTrimming="CharacterEllipsis"`, tooltip shows full path
- **Panel width:** Persisted via `ISettingsService.DiskPanelWidth`

---

## Files to Create

### Phase 3B-2
| File | Description |
|------|-------------|
| `Pandowdy.UI\Interfaces\IMessageBoxService.cs` | Error/confirmation dialog interface |
| `Pandowdy.UI\Services\MessageBoxService.cs` | Avalonia implementation |
| `Pandowdy.UI.Tests\Services\MessageBoxServiceTests.cs` | Tests |

### Phase 3C
| File | Description |
|------|-------------|
| `Pandowdy.UI\Interfaces\ISettingsService.cs` | Settings persistence interface |
| `Pandowdy.UI\Services\SettingsService.cs` | JSON settings service |
| `Pandowdy.UI\Models\PandowdySettings.cs` | Settings model |
| `Pandowdy.UI\Interfaces\IDriveStateService.cs` | Drive state interface |
| `Pandowdy.UI\Services\DriveStateService.cs` | Drive state service |
| `Pandowdy.UI.Tests\Services\SettingsServiceTests.cs` | Tests |
| `Pandowdy.UI.Tests\Services\DriveStateServiceTests.cs` | Tests |

### Phase 3D
| File | Description |
|------|-------------|
| `Pandowdy.UI\Controls\DriveDialog.axaml` | Drive management dialog |
| `Pandowdy.UI\Controls\DriveDialog.axaml.cs` | Code-behind |
| `Pandowdy.UI\ViewModels\DriveDialogViewModel.cs` | Dialog ViewModel |
| `Pandowdy.UI\ViewModels\PeripheralsMenuViewModel.cs` | Menu ViewModel |
| `Pandowdy.UI.Tests\ViewModels\DriveDialogViewModelTests.cs` | Tests |
| `Pandowdy.UI.Tests\ViewModels\PeripheralsMenuViewModelTests.cs` | Tests |

---

## Files to Modify

### Phase 3B-2
| File | Change |
|------|--------|
| `Pandowdy.UI\ViewModels\DiskStatusWidgetViewModel.cs` | Add `IDiskFileDialogService`, `IMessageBoxService` deps; wire commands to dialogs |
| `Pandowdy.UI\ViewModels\DiskCardPanelViewModel.cs` | Add `IMessageBoxService` for swap error handling |
| `Pandowdy\Program.cs` | Register `IMessageBoxService` |
| `Pandowdy.UI.Tests\ViewModels\DiskStatusWidgetViewModelTests.cs` | Update constructor, add integration tests |
| `Pandowdy.UI.Tests\ViewModels\DiskCardPanelViewModelTests.cs` | Update constructor |

### Phase 3C
| File | Change |
|------|--------|
| `Pandowdy\Program.cs` | Register `ISettingsService`, `IDriveStateService`; replace hardcoded disk inserts |
| `Pandowdy.EmuCore\DiskII\DiskIIControllerCard.cs` | `InsertBlankDisk` uses `ISettingsService.LastExportDirectory` |

### Phase 3D
| File | Change |
|------|--------|
| `Pandowdy.UI\MainWindow.axaml` | Add Peripherals menu |
| `Pandowdy.UI\ViewModels\MainWindowViewModel.cs` | Add menu building, exit confirmation |

---

## Testing Strategy

### Phase 3B-2 Tests
- `InsertDiskCommand` invokes dialog, sends message with path
- User cancels dialog → no message sent
- `CardMessageException` → `ShowErrorAsync()` called
- `SaveAsCommand` invokes save dialog, sends `SaveDiskAsMessage`
- Dirty eject shows confirmation dialog
- Confirmation rejected → no eject

### Phase 3C Tests
- Settings persist/load round-trip
- Default values when JSON missing
- Drive state persists on insert/eject/swap/save
- Missing files at startup → empty drive + warning logged

### Phase 3D Tests
- Peripherals menu built from card responses
- Empty slots excluded
- Menu rebuilds on disk status change
- Exit with dirty disk shows confirmation

---

## Key Architecture Decisions

| Decision | Resolution |
|----------|------------|
| Eject on empty drive | No-op (silent) |
| Motor state during swap | Unchanged |
| Head position during swap | Preserved |
| Read position during swap | Reset |
| Save policy | Never overwrite originals; `_new` suffix derivation |
| Blank disk format | Internal format; NIB destination path |
| Dirty indicator | ✏️ emoji adjacent to filename |
| Disk operations location | Peripherals menu, not File menu |
| Confirmation on dirty eject | Yes |
| Confirmation on exit with dirty | Yes |

---

## Coding Standards

### Copyright Header (Required on all C# files)
```csharp
// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

```

### Key Rules
- **Always use curly braces** for `if`, `for`, `foreach`, `while`, `using`, `lock`
- **Multi-line properties** when logic or private setters present
- **Primary constructors (C# 12)** for simple DI
- **XML documentation** on all public APIs
- **Test naming:** `MethodName_Scenario_ExpectedOutcome`
- **DI:** Constructor injection, depend on interfaces, register in `Program.cs`
- **Git:** Use `git mv` for file moves to preserve history

### Message Records
```csharp
// ✅ Immutable record types for messages
public record InsertDiskMessage(int DriveNumber, string DiskImagePath) : ICardMessage;
```

---

*Document Created: 2026-02-13*
*Supersedes: Task5-GUI-Disk-Management-Design.md*
