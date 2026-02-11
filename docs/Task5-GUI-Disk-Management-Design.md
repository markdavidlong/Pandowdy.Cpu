# Task 5: GUI Disk Management — Design Document

> **📌 Single Source of Truth** for the GUI Disk Management feature implementation.
> Referenced from `docs/Development-Roadmap.md`, Task 5.

---

## Table of Contents
0. [Tasks](#tasks)
1. [Overview](#overview)
2. [Design Principles](#design-principles)
3. [Phase 1: Card Message Infrastructure](#phase-1-card-message-infrastructure)
   - [ICardMessage and Message Types](#icardmessage-and-message-types)
   - [Slot Inventory and RefreshStatusMessage](#slot-inventory-and-refreshstatusmessage)
   - [IEmulatorCoreInterface.SendCardMessageAsync and GetSlotInventoryAsync](#iemulatorcoreinterfacesendcardmessageasync-and-getslotinventoryasync)
   - [ICard.HandleMessage](#icardhandlemessage)
   - [Thread Safety and Queueing](#thread-safety-and-queueing)
   - [Error Handling](#error-handling)
4. [Phase 2: Disk II Message Implementations](#phase-2-disk-ii-message-implementations)
   - [InsertDiskMessage](#insertdiskmessage)
   - [InsertBlankDiskMessage](#insertblankdiskmessage)
   - [EjectDiskMessage](#ejectdiskmessage)
   - [SwapDrivesMessage](#swapdrivesmessage)
   - [SaveDiskMessage and SaveDiskAsMessage](#savediskmessage-and-savediskasmessage)
   - [SetWriteProtectMessage](#setwriteprotectmessage)
   - [DiskIIControllerCard.HandleMessage](#diskiicontrollercardhandlemessage)
5. [Phase 3: UI Integration](#phase-3-ui-integration)
   - [DiskCardPanel — Card-Level Grouping](#diskcardpanel--card-level-grouping)
   - [Context Menus on DiskStatusWidget](#context-menus-on-diskstatuswidget)
   - [Peripherals Menu — Dynamic Card Discovery](#peripherals-menu--dynamic-card-discovery)
   - [File Dialog and Format Filtering](#file-dialog-and-format-filtering)
   - [Drag and Drop to Individual Drives](#drag-and-drop-to-individual-drives)
   - [Write-Protect Toggle](#write-protect-toggle)
   - [Disk Image Label Elision](#disk-image-label-elision)
   - [Disk Panel Width](#disk-panel-width)
   - [Dirty Indicator](#dirty-indicator)
6. [Phase 4: Recent Disk Images](#phase-4-recent-disk-images)
   - [Persistence Format](#persistence-format)
   - [UI: Peripherals → Recent Disks Submenu](#ui-peripherals--recent-disks-submenu)
7. [Architecture Decisions](#architecture-decisions)
   - [Why Generic Card Messages, Not Disk-Specific Methods](#why-generic-card-messages-not-disk-specific-methods)
   - [Why a Peripherals Menu, Not File Menu Disk Items](#why-a-peripherals-menu-not-file-menu-disk-items)
   - [Why Slot Inventory + Status Push, Not Card Self-Identification Messages](#why-slot-inventory--status-push-not-card-self-identification-messages)
   - [Why No Keyboard Shortcuts for Disk Operations](#why-no-keyboard-shortcuts-for-disk-operations)
   - [Why Async / Enqueued Execution](#why-async--enqueued-execution)
   - [Swap Mechanics](#swap-mechanics)
8. [Files to Create](#files-to-create)
9. [Files to Modify](#files-to-modify)
10. [Testing Strategy](#testing-strategy)
11. [Resolved Decisions](#resolved-decisions)
12. [Open Questions](#open-questions)

---

## Tasks

### Current task in progress

- None

### Next proposed task

- Phase 1: Card Management Infrastructure

---

## Overview

**Goal:** Add user-facing disk management capabilities to the GUI, including inserting/ejecting disk images, inserting blank disks, swapping drives, exporting disk images (Save As), toggling write-protect, drag-and-drop, and a recent disk images menu.

**Key Architectural Decision:** Disk operations are performed through a **generic card message system** on `IEmulatorCoreInterface`, not through disk-specific methods. This keeps the core interface extensible for future card types (printers, serial cards, Mockingboard, etc.) without polluting `IEmulatorCoreInterface` with card-specific APIs.

---

## Design Principles

1. **Generic over specific:** `IEmulatorCoreInterface` routes messages to cards by slot number. It has no knowledge of disk drives, printers, or any other card type.
2. **Thread-safe by design:** All card messages are enqueued on VA2M's existing `ConcurrentQueue<Action>` command queue and executed at instruction boundaries on the emulator thread, just like `Reset()`, `EnqueueKey()`, and `SetPushButton()`.
3. **Card-centric UI:** The UI is organized by **controller card** (`DiskCardPanel`), with each card containing one or two `DiskStatusWidget` children representing individual drives. The UI discovers cards dynamically via `GetSlotInventoryAsync()` (for slot/card names) and `IDiskStatusProvider` (for real-time disk drive status updates), not from hardcoded slot assumptions.
4. **No keyboard shortcuts** for disk insert/eject/swap — the number of cards and drives is arbitrary (e.g., Disk II in slots 5 and 6), making fixed shortcuts impractical. All disk operations go through context menus and the Peripherals menu.
5. **Error feedback via exceptions:** Card message handling throws a custom `CardMessageException` if the operation fails (wrong card type, bad drive number, unsupported format, etc.). The UI catches this and displays an error dialog.
6. **Forgiving APIs:** Operations that target a no-op state (e.g., eject on empty drive) are silently ignored. The GUI should prevent such operations from being offered, but they should be benign if they occur.
7. **Never overwrite originals:** Disk images loaded from files are converted to the internal `InternalDiskImage` format in memory. Writes go to the in-memory copy only (`Flush()` is a no-op on `UnifiedDiskImageProvider`). The original source file is never modified. Each imported disk gets an attached **destination path** (derived from the source with a `_new` suffix) for save operations. "Save" writes to the attached destination; "Save As" lets the user choose a new path. This protects source disk images from accidental corruption and matches the read-import / write-export architecture already in place.
8. **Tests track code:** Every new file of production code gets a corresponding test file. Test cases are added in the same PR as the code they cover. The design document is updated as decisions are made or implementations diverge from the plan.

---

## Phase 1: Card Message Infrastructure

### ICardMessage and Message Types

A card message is a small, immutable data object that describes an operation to perform on a card. Messages are not aware of threading — they're pure data. All messages are immutable `record` types.

```csharp
/// <summary>
/// Marker interface for messages that can be sent to expansion cards via
/// <see cref="IEmulatorCoreInterface.SendCardMessageAsync"/>.
/// </summary>
/// <remarks>
/// Each card type defines its own concrete message types. The card's
/// <see cref="ICard.HandleMessage"/> method is responsible for recognizing
/// and executing messages it supports, and rejecting those it does not.
/// </remarks>
public interface ICardMessage { }
```

Message types are defined alongside the card they target. For Disk II, they live in `Pandowdy.EmuCore.DiskII.Messages`. Generic messages (like `QueryCardInfoMessage`) live in `Pandowdy.EmuCore.Messages`:

```csharp
/// <summary>
/// Message requesting a disk image be inserted into a specific drive.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="DiskImagePath">Full path to the disk image file.</param>
public record InsertDiskMessage(int DriveNumber, string DiskImagePath) : ICardMessage;

/// <summary>
/// Message requesting a blank (empty, formatted) disk be inserted into a specific drive.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="FilePath">Optional file path to associate with the blank disk (for later Save).
/// Empty string means in-memory only.</param>
public record InsertBlankDiskMessage(int DriveNumber, string FilePath = "") : ICardMessage;

/// <summary>
/// Message requesting a disk be ejected from a specific drive.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
public record EjectDiskMessage(int DriveNumber) : ICardMessage;

/// <summary>
/// Message requesting the disk images in Drive 1 and Drive 2 be swapped.
/// </summary>
public record SwapDrivesMessage() : ICardMessage;

/// <summary>
/// Message requesting the internal disk image be saved to its attached destination path.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
public record SaveDiskMessage(int DriveNumber) : ICardMessage;

/// <summary>
/// Message requesting the internal disk image be exported (saved) to a user-chosen file.
/// Also updates the attached destination path for future Save operations.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="FilePath">Destination path for the exported disk image.</param>
public record SaveDiskAsMessage(int DriveNumber, string FilePath) : ICardMessage;

/// <summary>
/// Message requesting the write-protect state of a drive be changed.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="WriteProtected">True to enable write protection, false to disable.</param>
public record SetWriteProtectMessage(int DriveNumber, bool WriteProtected) : ICardMessage;
```

### Slot Inventory and RefreshStatusMessage

The GUI needs two things to build its Peripherals menu and status panels:

1. **What card is in each slot** — a simple slot inventory (card names/IDs).
2. **A way to force cards to push their current status** — so the GUI's existing status streams are up to date.

These are handled by two separate mechanisms:

**Slot inventory** via a new method on `IEmulatorCoreInterface`:

```csharp
/// <summary>
/// Identifies a card installed in a specific slot.
/// </summary>
/// <param name="Slot">The slot number.</param>
/// <param name="CardId">The card's unique numeric ID (0 = empty/NullCard).</param>
/// <param name="CardName">The card's human-readable name (e.g., "Disk II Controller").</param>
public record SlotInfo(SlotNumber Slot, int CardId, string CardName);
```

The GUI calls `GetSlotInventoryAsync()` to get a `IReadOnlyList<SlotInfo>` for all 7 slots. This is a lightweight read of each slot's `ICard.Name` and `ICard.Id` — no card message involved, no complex response types. The GUI uses the card names and IDs to build the Peripherals menu structure (which slots have disk controllers, which are empty, etc.).

**Status refresh** via a simple card message:

```csharp
/// <summary>
/// Message requesting a card to push a fresh status update through its existing
/// status channel (e.g., <see cref="IDiskStatusMutator"/> for disk controllers).
/// </summary>
/// <remarks>
/// <para>
/// This message carries no response payload. The card handles it by re-publishing
/// its current state through whatever push mechanism it uses. For example, a Disk II
/// controller calls <see cref="IDiskStatusMutator.MutateDrive"/> for each drive,
/// causing <see cref="IDiskStatusProvider.Stream"/> to emit a new snapshot.
/// </para>
/// <para>
/// Cards that have no status push mechanism can silently ignore this message.
/// </para>
/// </remarks>
public record RefreshStatusMessage() : ICardMessage;
```

**How it works together:**

1. At startup, the GUI calls `GetSlotInventoryAsync()` to learn which cards are in which slots.
2. The GUI builds the Peripherals menu from the slot inventory (card names, slot numbers).
3. For disk controllers, the GUI already subscribes to `IDiskStatusProvider.Stream` — drive state (filenames, track positions, motor state, dirty flags) flows automatically via the existing push mechanism.
4. When the GUI needs a forced refresh (e.g., after opening the Peripherals menu), it sends `RefreshStatusMessage` to the relevant slots. Each card pushes its current state through its own channel. No response to read — the GUI receives updates via its existing subscriptions.
5. Cards that don't handle `RefreshStatusMessage` throw `CardMessageException` via the default `HandleMessage` — the GUI catches this and ignores it.

**Why not a complex response type?** Each peripheral type already has its own status push infrastructure (disk controllers use `IDiskStatusProvider`, future serial cards would use their own provider). Duplicating that state into a generic response record would create two parallel representations of the same data. Instead, the query message simply triggers the push, and the GUI reads status from the streams it already subscribes to.

### IEmulatorCoreInterface.SendCardMessageAsync and GetSlotInventoryAsync

Two new methods on the core interface:

```csharp
public interface IEmulatorCoreInterface : IKeyboardSetter
{
    // ... existing members ...

    /// <summary>
    /// Sends a message to the card installed in the specified slot.
    /// </summary>
    /// <param name="slot">Target slot (Slot1–Slot7).</param>
    /// <param name="message">The card message to deliver.</param>
    /// <returns>A task that completes when the message has been processed on the emulator thread.</returns>
    /// <exception cref="CardMessageException">
    /// Thrown (on the returned Task) if the card rejects or cannot process the message.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe. The message is enqueued on the emulator
    /// thread's command queue and executed at the next instruction boundary, identical to
    /// Reset() and EnqueueKey(). The returned Task allows the caller to await completion
    /// and observe any errors.
    /// </para>
    /// <para>
    /// <strong>Generic Design:</strong> This method is intentionally card-type-agnostic.
    /// IEmulatorCoreInterface has no knowledge of disk drives, printers, or any specific
    /// card type. It simply routes the message to the card in the requested slot.
    /// </para>
    /// </remarks>
    Task SendCardMessageAsync(SlotNumber slot, ICardMessage message);

    /// <summary>
    /// Gets the identity of the card installed in each slot.
    /// </summary>
    /// <returns>A list of 7 <see cref="SlotInfo"/> records (Slot1–Slot7), including empty slots (NullCard).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe. Enqueued on the emulator thread like
    /// other commands. Reads <see cref="ICard.Name"/> and <see cref="ICard.Id"/> for each slot.
    /// </para>
    /// <para>
    /// <strong>Lightweight:</strong> Returns only static identity (name, ID, slot number).
    /// For live device state, subscribe to the appropriate status provider
    /// (e.g., <see cref="Services.IDiskStatusProvider"/> for disk drives).
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<SlotInfo>> GetSlotInventoryAsync();
}
```

### ICard.HandleMessage

Each card can opt into message handling:

```csharp
public interface ICard : IConfigurable
{
    // ... existing members ...

    /// <summary>
    /// Handles a message sent to this card.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <exception cref="CardMessageException">
    /// Thrown if the message is not recognized or cannot be processed.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Thread Context:</strong> Always called on the emulator thread at an
    /// instruction boundary. Implementations do not need to worry about thread safety
    /// relative to other emulator operations.
    /// </para>
    /// <para>
    /// Default implementation throws <see cref="CardMessageException"/> indicating
    /// the card does not support messages. Cards that accept messages override this.
    /// </para>
    /// </remarks>
    void HandleMessage(ICardMessage message)
    {
        throw new CardMessageException(
            $"Card '{Name}' in slot {Slot} does not support messages.");
    }
}
```

The default interface method means existing cards (NullCard, etc.) require no changes.

### Thread Safety and Queueing

VA2M implements `SendCardMessageAsync` using the existing `Enqueue()` pattern with a `TaskCompletionSource<bool>` to bridge the async gap:

```csharp
public Task SendCardMessageAsync(SlotNumber slot, ICardMessage message)
{
    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    Enqueue(() =>
    {
        try
        {
            var card = Bus.Slots.GetCardIn(slot);
            card.HandleMessage(message);
            tcs.SetResult(true);
        }
        catch (CardMessageException ex)
        {
            tcs.SetException(ex);
        }
        catch (Exception ex)
        {
            tcs.SetException(new CardMessageException(
                $"Unexpected error sending message to slot {slot}: {ex.Message}", ex));
        }
    });

    return tcs.Task;
}
```

VA2M implements `GetSlotInventoryAsync` using the same pattern:

```csharp
public Task<IReadOnlyList<SlotInfo>> GetSlotInventoryAsync()
{
    var tcs = new TaskCompletionSource<IReadOnlyList<SlotInfo>>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    Enqueue(() =>
    {
        var slots = new List<SlotInfo>();
        foreach (SlotNumber slot in Enum.GetValues<SlotNumber>())
        {
            if (slot == SlotNumber.Unslotted)
            {
                continue;
            }
            var card = Bus.Slots.GetCardIn(slot);
            slots.Add(new SlotInfo(slot, card.Id, card.Name));
        }
        tcs.SetResult(slots.AsReadOnly());
    });

    return tcs.Task;
}
```

**Key points:**
- Message is enqueued, not executed immediately — respects instruction boundary atomicity.
- `TaskCompletionSource` with `RunContinuationsAsynchronously` ensures continuations don't run on the emulator thread.
- The UI can `await` the result and catch `CardMessageException` for error display.
- No disk operation occurs mid-instruction or mid-I/O sequence.

### Error Handling

```csharp
/// <summary>
/// Exception thrown when a card message cannot be processed.
/// </summary>
public class CardMessageException : Exception
{
    public CardMessageException(string message) : base(message) { }
    public CardMessageException(string message, Exception innerException) 
        : base(message, innerException) { }
}
```

---

## Phase 2: Disk II Message Implementations

### InsertDiskMessage

```csharp
public record InsertDiskMessage(int DriveNumber, string DiskImagePath) : ICardMessage;
```

- `DriveNumber`: 1-based (1 or 2), matching the `DiskDriveStatusSnapshot.DriveNumber` convention.
- `DiskImagePath`: Full path. The drive's `InsertDisk()` method handles format detection.
- If a disk is already inserted, the existing disk is ejected first (matching current `InsertDisk` behavior).

### InsertBlankDiskMessage

```csharp
public record InsertBlankDiskMessage(int DriveNumber, string FilePath = "") : ICardMessage;
```

- Creates a blank formatted disk image in the internal format and inserts it into the drive.
- `FilePath`: Optional path to associate with the blank disk for later save operations. Empty string means the disk is in-memory only (no backing file until "Save As").
- If a disk is already inserted, it is ejected first.
- The blank disk is writable by default (write-protect off).

### EjectDiskMessage

```csharp
public record EjectDiskMessage(int DriveNumber) : ICardMessage;
```

- Ejects the disk from the specified drive.
- **No-op if no disk is inserted.** The GUI should prevent offering this option when the drive is empty, but the operation is benign if it occurs.

### SwapDrivesMessage

```csharp
public record SwapDrivesMessage() : ICardMessage;
```

- Swaps the `IDiskImageProvider` instances between Drive 1 and Drive 2.
- **Only valid for controllers with 2 drives.** Returns `CardMessageException` (invalid operation) if the controller has fewer than 2 drives.
- **Head positions are preserved** — the disk media moves between drives, not the heads. Each drive keeps its current head position.
- **Read positions are reset** — after swap, each drive's `IDiskImageProvider` has its read position/cycle remainder reset (via `NotifyMotorStateChanged` or equivalent) to avoid corrupt data streams from stale positional state.
- **Motor state does not change** — the motor continues running or stays off. The motor powers whichever drive is currently selected, and that selection is not affected by a media swap. The drive rotation position is reset.
- Handles the case where one or both drives are empty (empty provider swaps to the other drive).

### SaveDiskMessage and SaveDiskAsMessage

```csharp
/// <summary>
/// Message requesting the internal disk image be saved to its attached destination path.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
public record SaveDiskMessage(int DriveNumber) : ICardMessage;

/// <summary>
/// Message requesting the internal disk image be exported (saved) to a user-chosen file.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="FilePath">Destination path for the exported disk image.</param>
public record SaveDiskAsMessage(int DriveNumber, string FilePath) : ICardMessage;
```

**Attached Destination Path:**

Every `InternalDiskImage` carries an optional **destination path** (`DestinationFilePath`) and **destination format** (`DestinationFormat`). This is the path the image will be written to when the user invokes "Save" (as opposed to "Save As").

- When a disk is imported from a file, the destination path is derived from the source path using a `_new` suffix on the basename. For example, importing `E:\disks\game.woz` sets `DestinationFilePath` to `E:\disks\game_new.woz` and `DestinationFormat` to `Woz`. If `game_new.woz` already exists on disk, the path is auto-incremented: `game_new_2.woz`, `game_new_3.woz`, etc., until a non-existing path is found. The collision check is performed at import time when the destination path is first derived.
- When a blank disk is created, the destination path is empty (no file association until "Save As").
- The destination format can be inferred from the file extension, or set explicitly.
- The **original source file is never overwritten.** The `SourceFilePath` is read-only (`init`); the `DestinationFilePath` is always a separate path.

**Two save operations:**

| Operation | Context Menu | Behavior |
|-----------|-------------|----------|
| **Save** | `SaveDiskMessage` | Writes to the attached `DestinationFilePath` using `DestinationFormat`. Throws `CardMessageException` if no destination path is attached — the UI prevents this by disabling Save when `HasDestinationPath` is false. |
| **Save As...** | `SaveDiskAsMessage` | Opens a save dialog, writes to the user-chosen path, and updates `DestinationFilePath`/`DestinationFormat` on the `InternalDiskImage` so future "Save" operations use the new path. |

- Both use the existing `IDiskImageExporter` infrastructure (Task 30).
- After a successful save, `InternalDiskImage.ClearDirty()` is called to reset the dirty flag.
- Throws `CardMessageException` if no disk is inserted, or if the export format is unsupported/lossy and the conversion fails.

**New properties on `InternalDiskImage`:**

```csharp
/// <summary>
/// Destination file path for save operations (null until set by import derivation or Save As).
/// </summary>
/// <remarks>
/// <para>
/// When a disk is imported, this is derived from <see cref="SourceFilePath"/> with a "_new"
/// suffix (e.g., "game.woz" → "game_new.woz"). If the derived path already exists on disk,
/// the suffix is auto-incremented: "game_new_2.woz", "game_new_3.woz", etc.
/// When created blank, this is null until the user performs a "Save As".
/// </para>
/// <para>
/// This is always a separate path from <see cref="SourceFilePath"/> — the original
/// source file is never overwritten.
/// </para>
/// </remarks>
public string? DestinationFilePath { get; set; }

/// <summary>
/// Format to use when saving to <see cref="DestinationFilePath"/>.
/// Inferred from the file extension, or set explicitly.
/// </summary>
public DiskFormat DestinationFormat { get; set; } = DiskFormat.Unknown;
```

> **Architecture note:** The internal `UnifiedDiskImageProvider.Flush()` is a no-op — the in-memory `InternalDiskImage` is the live working copy and is never automatically persisted. This is intentional. All persistence goes through `IDiskImageExporter` via `SaveDiskMessage` / `SaveDiskAsMessage`.

### SetWriteProtectMessage

```csharp
public record SetWriteProtectMessage(int DriveNumber, bool WriteProtected) : ICardMessage;
```

- Sets or clears the write-protect state on the specified drive's disk image.
- Delegates to `IDiskImageProvider.IsWriteProtected` setter (already exists on the interface).
- Throws `CardMessageException` if no disk is inserted.
- Updates the `DiskDriveStatusSnapshot.IsReadOnly` state via `IDiskStatusMutator` after changing.

### DiskIIControllerCard.HandleMessage

```csharp
public void HandleMessage(ICardMessage message)
{
    switch (message)
    {
        case InsertDiskMessage insert:
            ValidateDriveNumber(insert.DriveNumber);
            _drives[insert.DriveNumber - 1].InsertDisk(insert.DiskImagePath);
            break;

        case InsertBlankDiskMessage blank:
            ValidateDriveNumber(blank.DriveNumber);
            InsertBlankDisk(blank.DriveNumber, blank.FilePath);
            break;

        case EjectDiskMessage eject:
            ValidateDriveNumber(eject.DriveNumber);
            _drives[eject.DriveNumber - 1].EjectDisk(); // no-op if empty
            break;

        case SwapDrivesMessage:
            if (_drives.Length < 2)
            {
                throw new CardMessageException(
                    "Cannot swap drives: controller has fewer than 2 drives.");
            }
            SwapDriveMedia();
            break;

        case SaveDiskMessage save:
            ValidateDriveNumber(save.DriveNumber);
            SaveDriveImage(save.DriveNumber); // uses attached DestinationFilePath
            break;

        case SaveDiskAsMessage saveAs:
            ValidateDriveNumber(saveAs.DriveNumber);
            ExportDriveImage(saveAs.DriveNumber, saveAs.FilePath);
            break;

        case SetWriteProtectMessage wp:
            ValidateDriveNumber(wp.DriveNumber);
            SetDriveWriteProtect(wp.DriveNumber, wp.WriteProtected);
            break;

        case RefreshStatusMessage:
            // Push current status for all drives via IDiskStatusMutator
            RefreshAllDriveStatus();
            break;

        default:
            throw new CardMessageException(
                $"Disk II controller does not recognize message type '{message.GetType().Name}'.");
    }
}

private void ValidateDriveNumber(int driveNumber)
{
    if (driveNumber < 1 || driveNumber > _drives.Length)
    {
        throw new CardMessageException(
            $"Invalid drive number {driveNumber}. Valid range: 1-{_drives.Length}.");
    }
}
```

**SwapDriveMedia implementation:**

The `DiskIIControllerCard` already has direct access to `_drives[]` (its own field). The swap implementation should:

1. Extract the `IDiskImageProvider` from each drive. Since `DiskIIControllerCard` owns the drives and the drives are created through its factory, it can access the concrete `DiskIIDrive` type (or an internal accessor) to get/set the provider.
2. Swap the providers between the two drives.
3. Call `SetQuarterTrack()` on each provider with the new drive's current head position (so the provider knows which track data to serve).
4. Reset the cycle remainder / read position on each provider (e.g., via `NotifyMotorStateChanged(true, currentCycle)` if motor is running, or just resetting internal tracking).
5. Update the `DiskIIStatusDecorator` state via `IDiskStatusMutator` to reflect the new disk image path/filename in each drive.

**Implementation approach:** Add an internal `IDiskImageProvider? ImageProvider { get; set; }` property to `DiskIIDrive` (not on `IDiskIIDrive` interface). The controller can access this via cast since it creates the drives. The `DiskIIStatusDecorator` will need to propagate through to the inner drive for the swap, or the swap operates at the controller level beneath the decorator layer.

---

## Phase 3: UI Integration

### DiskCardPanel — Card-Level Grouping

The current `DiskStatusPanel` displays a flat list of `DiskStatusWidget` instances. This needs to be restructured into a **two-level hierarchy**:

```
DiskStatusPanel
├── DiskCardPanel (Slot 5 — "Disk II Controller")
│   ├── DiskStatusWidget (S5D1)
│   └── DiskStatusWidget (S5D2)
├── DiskCardPanel (Slot 6 — "Disk II Controller")
│   ├── DiskStatusWidget (S6D1)
│   └── DiskStatusWidget (S6D2)
└── (additional controllers...)
```

**`DiskCardPanel`** is a new `UserControl` that:
- Groups drives belonging to the same controller card.
- Shows a header with the card name and slot number (e.g., "Disk II — Slot 6").
- Contains 1 or 2 `DiskStatusWidget` children depending on the card's drive count.
- The "Swap Drives" context menu item lives at this level (applies to the whole card).
- Visually delineates card boundaries with a subtle border or background.

**`DiskCardPanelViewModel`** wraps the per-card state:
- Slot number and card name.
- Collection of `DiskStatusWidgetViewModel` instances (1 or 2).
- `SwapDrivesCommand` — enabled only when the card has 2 drives and at least one has a disk.
- Reference to `IEmulatorCoreInterface` for sending messages.

**`DiskStatusPanelViewModel`** is updated to produce `DiskCardPanelViewModel` instances grouped by slot, rather than a flat list of `DiskStatusWidgetViewModel`.

### Context Menus on DiskStatusWidget

Each `DiskStatusWidget` (representing one drive) gets a context menu:

| Menu Item | Condition | Action |
|-----------|-----------|--------|
| **Insert Disk...** | Always | Opens file dialog, sends `InsertDiskMessage` |
| **Insert Blank Disk** | Always | Sends `InsertBlankDiskMessage` |
| **Eject Disk** | Disk inserted | Sends `EjectDiskMessage` |
| *(separator)* | | |
| **Save** | Disk inserted, `HasDestinationPath` is true, disk is dirty | Sends `SaveDiskMessage` (writes to attached path) |
| **Save As...** | Disk inserted | Opens save dialog, sends `SaveDiskAsMessage` |
| *(separator)* | | |
| **Write Protect** | Disk inserted | Toggleable checkbox, sends `SetWriteProtectMessage` |
| *(separator)* | | |
| **Recent Disks →** | Recent list non-empty | Submenu of recent disk images |

**Context menu on DiskCardPanel header (card level):**

| Menu Item | Condition | Action |
|-----------|-----------|--------|
| **Swap Drives** | Card has 2 drives, at least one has a disk | Sends `SwapDrivesMessage` |

**Implementation:** `DiskStatusWidgetViewModel` needs:
- Its slot number and drive number (already available from `DiskDriveStatusSnapshot`)
- Whether a disk is inserted (already available: `HasDisk` / `DiskImageFilename`)
- The current write-protect state (already available: `IsReadOnly` from snapshot)
- A reference to `IEmulatorCoreInterface` for sending messages
- `InsertDiskCommand`, `InsertBlankDiskCommand`, `EjectDiskCommand`, `SaveCommand`, `SaveAsCommand`, `ToggleWriteProtectCommand` as `ReactiveCommand` instances
- Commands are enabled/disabled based on drive state (e.g., `EjectDiskCommand` disabled when no disk, `SaveCommand` disabled when no destination path or not dirty)

### Peripherals Menu — Dynamic Card Discovery

Disk operations live in a **Peripherals** top-level menu, not in the File menu. The File menu remains clean (Quit only). The Peripherals menu is built dynamically by querying installed cards.

**Menu structure:**

```
Peripherals
├── Disks
│   ├── Slot 6 — Disk II Controller
│   │   ├── S6D1 - game.woz                → opens drive dialog for S6D1
│   │   └── S6D2 - (empty)                 → opens drive dialog for S6D2
│   └── Slot 5 — Disk II Controller
│       ├── S5D1 - utilities.do             → opens drive dialog for S5D1
│       └── S5D2 - (empty)                 → opens drive dialog for S5D2
├── ─────────────
├── Recent Disks →
│   ├── E:\test.woz
│   ├── E:\test.do
│   └── (more...)
├── ─────────────
└── (future: Communication, Audio, etc.)
```

**How the menu is built:**

1. At startup (and after any card configuration change), the GUI calls `GetSlotInventoryAsync()` to get the card name and ID for each slot.
2. The GUI filters out empty slots (`CardId == 0` / NullCard) and groups cards by their `ICard.Id` to determine card type:
   - Known disk controller IDs → under the **Disks** submenu header
   - Future: known serial card IDs → under **Communication**, etc.
3. For disk controllers, the GUI reads `IDiskStatusProvider.Current` to get the drive-level details (filenames, drive count) that populate the submenu entries.
4. Each card becomes a submenu item with its drives listed underneath.
5. Clicking a drive menu item opens a **drive dialog** (not inline menu operations) — see below.

**Drive dialog:**

Clicking a drive entry in the Peripherals menu opens a modal or flyout dialog for that specific drive. The dialog provides all drive operations in one place:

| Action | Description |
|--------|-------------|
| **Insert Disk...** | File picker → `InsertDiskMessage` |
| **Insert Blank Disk** | `InsertBlankDiskMessage` |
| **Eject** | `EjectDiskMessage` (disabled if empty) |
| **Save** | `SaveDiskMessage` (disabled if clean or no destination) |
| **Save As...** | File picker → `SaveDiskAsMessage` |
| **Write Protect** | Toggle → `SetWriteProtectMessage` |
| **Recent Disks** | List of recent images, click to insert directly |

The drive dialog shows current drive state (filename, write-protect status, dirty indicator) and enables/disables actions based on that state.

**Menu rebuilding:**

- The menu structure is rebuilt whenever `IDiskStatusProvider.Stream` emits a new snapshot (disk inserted/ejected, filename changed).
- `GetSlotInventoryAsync()` is called at startup and after card configuration changes. `RefreshStatusMessage` is sent to relevant slots when the Peripherals menu is opened (lightweight — each card just re-pushes its current state through its existing channel).
- Cards that don't handle `RefreshStatusMessage` are silently skipped (their `HandleMessage` throws, caught and ignored).

**Swap Drives** remains on the `DiskCardPanel` context menu (card-level operation, not per-drive), not in the Peripherals menu.

### File Dialog and Format Filtering

**Insert Disk (open) dialog:**

```
Supported Disk Images (*.dsk;*.do;*.po;*.nib;*.woz;*.2mg)|*.dsk;*.do;*.po;*.nib;*.woz;*.2mg
DSK Images (*.dsk)|*.dsk
DOS Order (*.do)|*.do
ProDOS Order (*.po)|*.po
NIB Images (*.nib)|*.nib
WOZ Images (*.woz)|*.woz
2IMG Images (*.2mg)|*.2mg
All Files (*.*)|*.*
```

**Save As (export) dialog:**

```
NIB Images (*.nib)|*.nib
WOZ Images (*.woz)|*.woz
DSK Images (*.dsk)|*.dsk
DOS Order (*.do)|*.do
ProDOS Order (*.po)|*.po
All Files (*.*)|*.*
```

Use Avalonia's `StorageProvider.OpenFilePickerAsync()` / `SaveFilePickerAsync()` for cross-platform file dialogs.

### Drag and Drop to Individual Drives

- Each `DiskStatusWidget` sets `DragDrop.AllowDrop="True"`.
- On `DragOver`: Validate the dragged file has a supported extension. Set `DragDropEffects.Copy` if valid, `DragDropEffects.None` otherwise. Provide visual feedback (highlight border, change background color) to indicate the target drive.
- On `Drop`: Extract file path, send `InsertDiskMessage` to the appropriate slot/drive.
- Drop on the **main window** area (not on a drive widget) should **not** default to any particular drive — the user needs to target a specific drive.

**Visual Feedback:**
- When a valid disk image is dragged over a drive widget, the widget border changes to indicate it can receive the file (e.g., green glow or highlighted border).
- When the file is invalid or the cursor is not over a drive widget, show `DragDropEffects.None`.

### Write-Protect Toggle

The `DiskStatusWidget` displays the current write-protect state and allows toggling:

- The context menu includes a **"Write Protect"** checkbox item that toggles the state.
- Toggling sends a `SetWriteProtectMessage` through the card message system.
- The `DiskDriveStatusSnapshot.IsReadOnly` field is updated by `DiskIIStatusDecorator` after the provider's `IsWriteProtected` property is changed, flowing through the reactive stream to update the UI automatically.
- **Visual feedback:** The existing filename color (red = read-only, white = writable) via `FilenameForeground` in `DiskStatusWidgetViewModel` is the sole visual indicator. No additional lock icon is needed — the color coding is already clear and consistent with the existing UI language.

### Disk Image Label Elision

The disk image filename label on `DiskStatusWidget` must handle long filenames gracefully:

- Use `TextTrimming="CharacterEllipsis"` on the filename `TextBlock` (already present in current AXAML).
- The filename `MaxWidth` tracks the panel's current width minus padding, so the label adapts when the panel is resized.
- The full path remains available via `ToolTip.Tip` (already present).
- Consider `TextTrimming="WordEllipsis"` if character-level ellipsis looks awkward for filenames.

### Disk Panel Width

The `DiskStatusPanel` (containing `DiskCardPanel` groups) has a configurable width:

- **Default minimum width:** 150 pixels.
- Width can be adjusted by the user (drag handle, or future layout system).
- **Persisted in JSON** alongside other UI settings (e.g., in `pandowdy-ui-settings.json`):
  ```json
  {
      "diskPanelWidth": 180
  }
  ```
- The panel will eventually move to a more flexible docking/layout system, so the current implementation should be simple (a stored double + min-width constraint) and not over-engineered.

### Dirty Indicator

When `InternalDiskImage.IsDirty` is true (the in-memory disk has been modified since load or last save), the `DiskStatusWidget` displays a dirty indicator:

- A single emoji (✏️) is displayed adjacent to the filename, indicating unsaved changes.
- The emoji is hidden when the disk is clean (`IsDirty == false`) or when no disk is inserted.
- `IsDirty` is exposed to the UI through `DiskDriveStatusSnapshot` (new fields: `bool IsDirty`, `bool HasDestinationPath`) and flows through the reactive stream like other status fields.
- After a successful save (`SaveDiskMessage` or `SaveDiskAsMessage`), the dirty flag is cleared and the emoji disappears.
- This indicator helps users know they have unsaved work before ejecting or exiting.

**Accessor chain:** `InternalDiskImage.IsDirty` → `UnifiedDiskImageProvider` (holds reference to `InternalDiskImage`) → `DiskIIDrive` (new internal accessor for `InternalDiskImage`) → `DiskIIStatusDecorator` (reads dirty/destination state and propagates to `IDiskStatusMutator`) → `DiskDriveStatusSnapshot.IsDirty` / `HasDestinationPath` → UI binding. The decorator reads these values during `SyncStatus()` and whenever disk state changes (insert, eject, save).

---

## Phase 4: Recent Disk Images

### Persistence Format

Recent disk images are stored as a JSON file in the user's application data directory:

```json
{
    "version": 1,
    "maxItems": 15,
    "recentDisks": [
        {
            "path": "E:\\test.woz",
            "lastUsed": "2026-02-10T14:30:00Z"
        },
        {
            "path": "E:\\test.do",
            "lastUsed": "2026-02-10T14:25:00Z"
        }
    ]
}
```

**Storage location:** `Environment.GetFolderPath(SpecialFolder.ApplicationData)/Pandowdy/recent-disks.json`

**Behavior:**
- When a disk image is inserted (via any method), its path is added/moved to the top of the recent list.
- Maximum 15 entries (configurable).
- Entries are ordered by `lastUsed` (most recent first).
- Entries whose files no longer exist are shown grayed out or removed on load.

### UI: Peripherals → Recent Disks Submenu

Recent disks appear in two places:

1. **Peripherals → Recent Disks** (top-level submenu): Each entry shows the filename and full path in a tooltip. Clicking a recent disk entry opens a flyout or submenu to choose which drive to insert it into:

```
Recent Disks →
├── test.woz → [Insert into...] → S5D1 | S5D2 | S6D1 | S6D2
├── test.do  → [Insert into...] → S5D1 | S5D2 | S6D1 | S6D2
└── blank.nib → [Insert into...] → S5D1 | S5D2 | S6D1 | S6D2
```

2. **Drive dialog → Recent Disks**: When accessed from a specific drive's dialog, the recent disk is inserted directly into that drive (no submenu needed).

3. **DiskStatusWidget context menu → Recent Disks**: Same direct-insert behavior as the drive dialog.

---

## Architecture Decisions

### Why Generic Card Messages, Not Disk-Specific Methods

Adding `InsertDisk()`, `EjectDisk()`, `SwapDrives()` directly to `IEmulatorCoreInterface` would:
- Couple the core interface to Disk II specifics
- Require interface changes for every new card type (printer, serial, audio)
- Violate Interface Segregation Principle — the interface already has 11 members

The generic `SendCardMessageAsync(SlotNumber, ICardMessage)` pattern:
- Supports any card type with zero core interface changes
- Cards define their own message vocabulary
- Future printer card adds `PrintMessage`, `SetBaudRateMessage`, etc. without touching core
- Matches the extensible slot/card architecture already in place

### Why a Peripherals Menu, Not File Menu Disk Items

Disk operations don't belong in the File menu because:
- The File menu is for application-level operations (open project, save session, quit).
- Disk insert/eject are **peripheral operations** — they control hardware, not application state.
- As more card types are added (serial, audio), each would need File menu entries, polluting it.
- A dedicated Peripherals menu provides a natural home for all hardware management.
- The menu structure mirrors the physical Apple IIe: slots → cards → devices.

The Peripherals menu also scales naturally:
- One disk controller → one card with two drives.
- Two disk controllers → two cards, each with two drives.
- Future serial card → appears under a "Communication" section.
- No menu restructuring needed as card types are added.

### Why Slot Inventory + Status Push, Not Card Self-Identification Messages

An earlier design used a `QueryCardInfoMessage` with a complex `CardInfoResponse` carrying sub-device state (media paths, dirty flags, etc.). This was replaced with the simpler slot inventory + status push pattern because:

- **Each peripheral already has its own status push infrastructure.** Disk controllers publish via `IDiskStatusProvider`; future card types will have their own providers. Duplicating that state into a generic response record creates two parallel representations of the same data.
- **Slot identity is static.** What's in each slot (card name, card ID) doesn't change at runtime (until card configuration changes). This is a simple read of `ICard.Name` and `ICard.Id` — no card message needed, no response buffering.
- **`RefreshStatusMessage` is a trigger, not a query.** It tells the card "push your current state now" through its existing channel. The GUI reads the result from the stream it already subscribes to (`IDiskStatusProvider.Stream`, etc.).
- **Fewer types to maintain.** No `CardInfoResponse`, `CardCategory`, `CardSubDeviceInfo` — just a simple `SlotInfo` record and a parameterless `RefreshStatusMessage`.
- **Cards opt in naturally.** Cards that have no status to push simply ignore `RefreshStatusMessage` (the default `HandleMessage` throws, which the GUI catches and ignores). No special handling required.

### Why No Keyboard Shortcuts for Disk Operations

- The emulator supports arbitrary numbers of disk controllers in any slots (currently slots 5 and 6).
- Each controller has 1-2 drives.
- Fixed shortcuts like `Ctrl+1` would need to map to specific slot/drive combos, which vary per configuration.
- Context menus and the Peripherals menu provide unambiguous targeting.

### Why Async / Enqueued Execution

Disk operations (insert, eject, swap) must not occur while the emulator thread is mid-operation:
- Inserting a disk while the controller is reading bits would corrupt the data stream.
- Ejecting during a write could lose data.
- The existing `Enqueue()` → `ProcessAnyPendingActions()` pattern already solves this for Reset/Keys/Buttons.
- `SendCardMessageAsync` returns a `Task` so the UI can:
  1. `await` completion before updating UI state
  2. Catch `CardMessageException` for error dialogs
  3. Show a brief "working" indicator if needed

### Swap Mechanics

**What the controller knows:** The `DiskIIControllerCard` owns the `_drives[]` array and creates drives through its factory. It has full visibility into which drive has which `IDiskImageProvider`.

**What happens during a swap:**
1. The `IDiskImageProvider` from each drive is extracted.
2. Providers are transplanted to the opposite drive.
3. Each provider's quarter-track is set to the new drive's current head position (`SetQuarterTrack()`).
4. The drive rotation position / cycle remainder is reset on each provider (prevents stale data stream corruption).
5. The `DiskIIStatusDecorator` layer updates `IDiskStatusMutator` with the new filename/path/write-protect state for each drive.
6. Motor state does not change — it continues powering whichever drive is currently selected.

**Why reset read position but keep head position:**
- Head position is a physical property of the drive mechanism — it doesn't move when you swap floppies.
- Read position (bit offset within the track) should be reset because the new disk is starting fresh in that drive — the rotational phase of the new disk is unrelated to where the old disk was.
- This matches real hardware: inserting a new floppy doesn't guarantee any particular rotational alignment.

---

## Files to Create

### Core Infrastructure

| File | Description |
|------|-------------|
| `Pandowdy.EmuCore\Interfaces\ICardMessage.cs` | `ICardMessage` marker interface |
| `Pandowdy.EmuCore\Messages\RefreshStatusMessage.cs` | Trigger message for cards to re-push current status |
| `Pandowdy.EmuCore\Messages\SlotInfo.cs` | `SlotInfo` record for slot inventory |
| `Pandowdy.EmuCore\Exceptions\CardMessageException.cs` | Custom exception for card message failures |
| `Pandowdy.EmuCore\DiskII\Messages\InsertDiskMessage.cs` | Insert disk message record |
| `Pandowdy.EmuCore\DiskII\Messages\InsertBlankDiskMessage.cs` | Insert blank disk message record |
| `Pandowdy.EmuCore\DiskII\Messages\EjectDiskMessage.cs` | Eject disk message record |
| `Pandowdy.EmuCore\DiskII\Messages\SwapDrivesMessage.cs` | Swap drives message record |
| `Pandowdy.EmuCore\DiskII\Messages\SaveDiskMessage.cs` | Save to attached destination path message record |
| `Pandowdy.EmuCore\DiskII\Messages\SaveDiskAsMessage.cs` | Save As / export to user-chosen path message record |
| `Pandowdy.EmuCore\DiskII\Messages\SetWriteProtectMessage.cs` | Write-protect toggle message record |

### UI

| File | Description |
|------|-------------|
| `Pandowdy.UI\Controls\DiskCardPanel.axaml` | Card-level grouping control (header + 1-2 DiskStatusWidgets) |
| `Pandowdy.UI\Controls\DiskCardPanel.axaml.cs` | Code-behind for DiskCardPanel |
| `Pandowdy.UI\ViewModels\DiskCardPanelViewModel.cs` | ViewModel for card-level grouping (swap command, drive children) |
| `Pandowdy.UI\Controls\DriveDialog.axaml` | Drive management dialog (insert, eject, save, write-protect, recent) |
| `Pandowdy.UI\Controls\DriveDialog.axaml.cs` | Code-behind for DriveDialog |
| `Pandowdy.UI\ViewModels\DriveDialogViewModel.cs` | ViewModel for drive dialog (commands, state, recent list) |
| `Pandowdy.UI\ViewModels\PeripheralsMenuViewModel.cs` | ViewModel for building Peripherals menu from slot inventory + status streams |
| `Pandowdy.UI\Services\RecentDiskService.cs` | Recent disk image tracking and persistence |
| `Pandowdy.UI\Services\DiskFileDialogService.cs` | File dialog helpers for disk image selection and save |

### Tests

| File | Description |
|------|-------------|
| `Pandowdy.EmuCore.Tests\Cards\CardMessageTests.cs` | Tests for message routing through VA2M |
| `Pandowdy.EmuCore.Tests\Cards\SlotInventoryAndRefreshTests.cs` | Tests for slot inventory and RefreshStatusMessage (Disk II, NullCard) |
| `Pandowdy.EmuCore.Tests\Cards\DiskIIMessageHandlerTests.cs` | Tests for DiskII message handling (all 8 message types) |
| `Pandowdy.UI.Tests\Services\RecentDiskServiceTests.cs` | Tests for recent disk persistence |
| `Pandowdy.UI.Tests\ViewModels\DriveDialogViewModelTests.cs` | Tests for drive dialog command enablement and state |
| `Pandowdy.UI.Tests\ViewModels\PeripheralsMenuViewModelTests.cs` | Tests for Peripherals menu building from slot inventory + status streams |
| `Pandowdy.UI.Tests\ViewModels\DiskCardPanelViewModelTests.cs` | Tests for card panel grouping and swap command |

---

## Files to Modify

| File | Change |
|------|--------|
| `Pandowdy.EmuCore\Interfaces\IEmulatorCoreInterface.cs` | Add `SendCardMessageAsync` and `GetSlotInventoryAsync` methods |
| `Pandowdy.EmuCore\Interfaces\ICard.cs` | Add `HandleMessage` default interface method |
| `Pandowdy.EmuCore\VA2M.cs` | Implement `SendCardMessageAsync` and `GetSlotInventoryAsync` using `Enqueue()` + `TaskCompletionSource` |
| `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs` | Implement `HandleMessage` for all 8 message types (7 disk + RefreshStatusMessage) |
| `Pandowdy.EmuCore\DiskII\DiskIIDrive.cs` | Add internal `IDiskImageProvider? ImageProvider { get; set; }` for swap support; add internal `InternalDiskImage?` accessor for dirty/destination state |
| `Pandowdy.EmuCore\DiskII\InternalDiskImage.cs` | Add `DestinationFilePath` and `DestinationFormat` properties |
| `Pandowdy.EmuCore\DiskII\DiskIIStatusDecorator.cs` | Propagate `IsDirty` and `HasDestinationPath` from inner drive's `InternalDiskImage` to status snapshot |
| `Pandowdy.EmuCore\Services\DiskStatusServices.cs` | Add `IsDirty` and `HasDestinationPath` fields to `DiskDriveStatusSnapshot`; add corresponding fields to `DiskDriveStatusBuilder` |
| `Pandowdy.UI\Controls\DiskStatusWidget.axaml` | Add context menu, drag/drop support, label elision improvements |
| `Pandowdy.UI\ViewModels\DiskStatusWidgetViewModel.cs` | Add commands (Insert, InsertBlank, Eject, Save, SaveAs, ToggleWriteProtect), drag/drop handlers |
| `Pandowdy.UI\Controls\DiskStatusPanel.axaml` | Restructure to use `DiskCardPanel` grouping instead of flat widget list |
| `Pandowdy.UI\ViewModels\DiskStatusPanelViewModel.cs` | Produce `DiskCardPanelViewModel` instances grouped by slot; inject `IEmulatorCoreInterface` |
| `Pandowdy.UI\MainWindow.axaml` | Add Peripherals top-level menu (dynamically built from slot inventory + status streams) |
| `Pandowdy.UI\ViewModels\MainWindowViewModel.cs` | Add Peripherals menu building, drive dialog commands, slot inventory dispatch |
| `Pandowdy\Program.cs` | Register `RecentDiskService` in DI; hardcoded disk inserts remain for development |

---

## Testing Strategy

### Unit Tests (EmuCore)

1. **Card message routing:**
   - `SendCardMessageAsync` delivers message to correct slot
   - `SendCardMessageAsync` to empty slot (NullCard) throws `CardMessageException`
   - `SendCardMessageAsync` with unknown message type throws `CardMessageException`

2. **DiskII message handling:**
   - `InsertDiskMessage` with valid path inserts disk
   - `InsertDiskMessage` with invalid drive number throws
   - `InsertDiskMessage` to drive with existing disk ejects first, then inserts
   - `InsertBlankDiskMessage` creates blank disk and inserts it
   - `InsertBlankDiskMessage` with empty path creates in-memory blank disk
   - `InsertBlankDiskMessage` to drive with existing disk ejects first
   - `EjectDiskMessage` ejects disk from drive
   - `EjectDiskMessage` on empty drive is no-op (does not throw)
   - `SwapDrivesMessage` swaps disk providers between drives
   - `SwapDrivesMessage` with one empty drive works correctly (empty swaps to other side)
   - `SwapDrivesMessage` with both empty drives is no-op
   - `SwapDrivesMessage` with fewer than 2 drives throws `CardMessageException`
   - `SwapDrivesMessage` preserves head positions after swap
   - `SwapDrivesMessage` resets read position / cycle remainder after swap
   - `SwapDrivesMessage` does not change motor state
   - `SaveDiskMessage` saves to attached destination path
   - `SaveDiskMessage` with no destination path throws `CardMessageException`
   - `SaveDiskAsMessage` exports disk to specified path
   - `SaveDiskAsMessage` updates `DestinationFilePath` and `DestinationFormat` on the `InternalDiskImage`
   - `SaveDiskAsMessage` on empty drive throws `CardMessageException`
   - Save (both messages) clears `IsDirty` flag after successful export
   - Save (both messages) does not modify the original source file
   - Destination path derived from import path uses `_new` suffix
   - Imported disk has `HasDestinationPath == true` in snapshot
   - Blank disk has `HasDestinationPath == false` in snapshot until Save As
   - `SetWriteProtectMessage` toggles write-protect state
   - `SetWriteProtectMessage` on empty drive throws `CardMessageException`
   - `SetWriteProtectMessage` updates `DiskDriveStatusSnapshot.IsReadOnly`

3. **Slot inventory and status refresh:**
   - `GetSlotInventoryAsync` returns 7 `SlotInfo` entries (one per slot)
   - Empty slots have `CardId == 0` and `CardName == "Empty Slot"`
   - Installed cards return correct `CardId` and `CardName`
   - `RefreshStatusMessage` to Disk II controller triggers `IDiskStatusMutator` update for all drives
   - `RefreshStatusMessage` to NullCard throws `CardMessageException` (default HandleMessage behavior)
   - Status push after refresh reflects current drive state (path, filename, write-protect, dirty)

4. **Thread safety:**
   - Message is not executed immediately (verify via mock)
   - Message executes after `ProcessAnyPendingActions()` at instruction boundary
   - Task completes after execution
   - Exception propagates through Task

### UI Tests

1. **Peripherals menu building:**
   - Menu is built from slot inventory + `IDiskStatusProvider` at startup
   - Disk controllers grouped under "Disks" section based on card ID
   - Each card shows slot number and card name (from `SlotInfo`)
   - Each drive shows S*D* label and current filename (from `DiskDriveStatusSnapshot`)
   - Empty slots (CardId == 0) are excluded from menu
   - Menu rebuilds when disk status stream emits new snapshot

2. **Drive dialog:**
   - Opens with correct drive state (filename, write-protect, dirty)
   - Insert Disk command opens file dialog (mock)
   - Eject command disabled when no disk inserted
   - Save command disabled when not dirty or no destination path
   - Save As command opens save dialog (mock)
   - Write-protect toggle sends correct message
   - Recent disks submenu inserts directly into this drive

3. **Recent disk service:**
   - Add disk path updates list and moves to top
   - Maximum items enforced
   - Persistence round-trip (save/load)
   - Missing files handled gracefully

4. **DiskStatusWidget context menu commands:**
   - Insert command opens file dialog (mock)
   - Insert blank command sends correct message
   - Eject command sends correct message
   - Eject command is disabled when no disk inserted
   - Swap command sends correct message
   - Swap command is disabled when fewer than 2 drives
   - Save command sends correct message when destination path is attached
   - Save command is disabled when no disk inserted or no destination path
   - Save command is disabled when disk is not dirty
   - Save As command opens save dialog (mock) and sends correct message
   - Save As command updates destination path on the `InternalDiskImage`
   - Save As command is disabled when no disk inserted
   - Write-protect toggle sends correct message with toggled state
   - Write-protect toggle is disabled when no disk inserted
   - Context menu items enabled/disabled based on drive state

5. **DiskCardPanel grouping:**
   - Drives from same slot grouped into single DiskCardPanel
   - Drives from different slots in separate DiskCardPanels
   - DiskCardPanel swap command targets correct slot

---

## Resolved Decisions

These items were originally open questions that have been resolved:

1. **Eject on empty drive:** ✅ **No-op.** The GUI should prevent offering this option, but the operation is benign if it occurs. No exception thrown.

2. **Swap implementation:** ✅ **Controller manages swap directly.** The `DiskIIControllerCard` owns the `_drives[]` array and has visibility into the `IDiskImageProvider` on each drive. Add an internal `IDiskImageProvider? ImageProvider { get; set; }` property to `DiskIIDrive` (concrete class, not interface). The controller extracts providers, swaps them, resets read positions, and updates status.

3. **Motor state during swap:** ✅ **Motor state does not change.** The motor continues running or stays off. The drive rotation position is reset. This matches real hardware — users could swap floppies with the motor running.

4. **Head position during swap:** ✅ **Head positions are preserved.** The disk media moves between drives, not the heads. Each provider is told the new drive's head position via `SetQuarterTrack()`.

5. **Read position during swap:** ✅ **Read position is reset.** The cycle remainder and bit position within the track are reset after swap to prevent corrupt data streams from stale positional state.

6. **Swap with fewer than 2 drives:** ✅ **Returns error.** Throws `CardMessageException` indicating invalid operation.

7. **DiskStatusWidget grouping:** ✅ **Yes — `DiskCardPanel`.** A new card-level container groups 1-2 `DiskStatusWidget` instances per controller. The Swap Drives option lives at this card level.

8. **Recent disks scope:** ✅ **Global.** All drives share one recent list — simpler, and users typically work with a pool of disk images.

9. **Save policy — never overwrite originals:** ✅ **Dual save model with attached destination path.** The original source file is never modified. `UnifiedDiskImageProvider.Flush()` is intentionally a no-op — the in-memory `InternalDiskImage` is the live working copy. Each disk carries an attached `DestinationFilePath` (derived from the source path with a `_new` suffix) and `DestinationFormat`. **Save** writes to the attached destination; **Save As** writes to a user-chosen path and updates the attached destination. There is no overwrite-in-place of the source. This protects source images and matches the import/export architecture.

10. **Write-protect visual indicator:** ✅ **Red/white filename color (existing) is sufficient.** The current `FilenameForeground` binding already displays red for write-protected disks and white for writable disks. The context menu provides the toggle. No additional lock icon is needed — the color coding is clear and consistent with the existing UI language.

11. **Blank disk format:** ✅ **Configurable via Options menu.** The default blank disk format is set through a GUI options/preferences menu (e.g., Options → Blank Disk Format → Unformatted / DOS 3.3 / ProDOS). The default is **Unformatted** (all zeros in internal format) — software running in the emulator can format it as needed. The setting is persisted in the UI settings JSON alongside other preferences.

12. **Save format auto-detection:** ✅ **Extension-based.** Both `SaveDiskMessage` and `SaveDiskAsMessage` determine the export format from the file extension of the destination path. The `DestinationFormat` property on `InternalDiskImage` caches this so it's computed once and reused. No explicit `DiskFormat` parameter on the message — the path is the single source of truth for format.

13. **Disk panel width:** ✅ **Min 150, persisted in JSON.** The `DiskStatusPanel` has a default minimum width of 150 pixels. The current width is persisted in `pandowdy-ui-settings.json` and restored on startup. The implementation is intentionally simple (a stored double + `MinWidth` constraint) because the panel will move to a more flexible docking/layout system later.

14. **Dirty indicator:** ✅ **✏️ emoji adjacent to filename.** When `IsDirty` is true, a ✏️ emoji is shown next to the filename in `DiskStatusWidget`. Hidden when clean or no disk inserted. Both `IsDirty` and `HasDestinationPath` are exposed through `DiskDriveStatusSnapshot` (new fields) and flow through the reactive stream. The accessor chain is: `InternalDiskImage` → `UnifiedDiskImageProvider` → `DiskIIDrive` (internal accessor) → `DiskIIStatusDecorator` → snapshot.

15. **Disk operations in Peripherals menu, not File menu:** ✅ **Dedicated Peripherals top-level menu.** Disk insert/eject/save are peripheral hardware operations, not application file operations. The Peripherals menu is organized by card type (grouped by `ICard.Id` from slot inventory) and populated with live state from each card type's status provider (e.g., `IDiskStatusProvider` for disk controllers). Each drive entry opens a drive dialog with all operations. The File menu remains clean (Quit only). This architecture scales naturally as more card types are added.

16. **Card discovery via slot inventory + status push:** ✅ **Two-part design.** `GetSlotInventoryAsync()` returns a simple `IReadOnlyList<SlotInfo>` with the card name and ID for each slot — no card message involved, just a lightweight read of `ICard.Name`/`ICard.Id`. Live device state comes from each card type's existing push infrastructure (`IDiskStatusProvider` for disk controllers, future providers for other card types). `RefreshStatusMessage` is a simple trigger that tells a card to re-push its current state — no response payload, no complex types. This avoids duplicating status data into a parallel response record and keeps the card message system clean (messages are actions, not queries).

17. **Confirmation on eject with unsaved changes:** ✅ **Yes — confirm before ejecting dirty disks.** If `IsDirty` is true when the user requests eject, the GUI shows a confirmation dialog ("Disk has unsaved changes. Eject anyway?"). The infrastructure (`IsDirty` in `DiskDriveStatusSnapshot`) already exists. Implementation: the UI checks dirty state from the snapshot before sending `EjectDiskMessage`, and shows a dialog if dirty. The `EjectDiskMessage` itself remains unconditional — the confirmation is purely a UI-layer concern.

18. **Destination path collision handling:** ✅ **Auto-increment with `_new_2`, `_new_3`, etc.** When deriving the destination path from the source path, the `_new` suffix is applied first (`game.woz` → `game_new.woz`). If `game_new.woz` already exists on disk, the path is incremented: `game_new_2.woz`, `game_new_3.woz`, etc., until a non-existing path is found. This avoids silently overwriting existing files and requires no user prompt during import. The collision check is performed at import time when `DestinationFilePath` is first derived.

19. **Confirmation on application exit with dirty disks:** ✅ **Yes — confirm before exiting with unsaved changes.** If any drive has `IsDirty == true` when the user closes the application, the app prompts "You have unsaved disk changes. Exit anyway?" This follows standard application behavior for unsaved work. Implementation: the window closing handler checks all drives' dirty state via `IDiskStatusProvider.Current` and shows a confirmation dialog if any are dirty.

---

## Open Questions

*No open questions at this time. All design decisions have been resolved.*

---

## Document Maintenance

This document is the **single source of truth** for Task 5 design decisions. It must be updated:

- **Before implementation:** When a design decision is made or an open question is resolved.
- **During implementation:** When the actual code diverges from the plan (new edge cases, API adjustments, etc.).
- **After implementation:** When a phase is complete — move relevant details to "Resolved Decisions" and note completion date.

Every new production code file must have a corresponding test file created in the same work session. Test cases should be enumerated in the [Testing Strategy](#testing-strategy) section before implementation begins.

---

*Document Created: 2026-02-10*
*Last Updated: 2026-02-10 — Resolved all open questions: eject/exit confirmation for dirty disks (yes), destination path collision handling (_new_2/_new_3 auto-increment)*
