# Pandowdy `.skillet` Project System — Development Blueprint

> **Authoritative Design Spec:** `docs/Skillet_Project_File_Development.md`  
> **Deferred Features Reference:** `docs/Skillet_Deferred_Features_Reference.md`  
> **This Document:** Implementation blueprint translating the conceptual design into engineering-ready structures.  
> **Status:** DRAFT — Ready for Task 32 execution phases.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [New Project: Pandowdy.Project](#2-new-project-pandowdyproject)
3. [SQLite Schema](#3-sqlite-schema)
4. [C# Data Models](#4-c-data-models)
5. [Project Load / Save Lifecycle](#5-project-load--save-lifecycle)
6. [Disk Import / Use / Export Lifecycle](#6-disk-import--use--export-lifecycle)
7. [Originals vs Working Copies](#7-originals-vs-working-copies)
8. [JSON Defaults vs `.skillet` Overrides — Resolution Model](#8-json-defaults-vs-skillet-overrides--resolution-model)
9. [UI Workflow: Startup, Creation, Switching](#9-ui-workflow-startup-creation-switching)
10. [Debugger Subsystem Storage Model](#10-debugger-subsystem-storage-model) *(stub — see deferred ref)*
11. [AppleSoft Subsystem Storage Model](#11-applesoft-subsystem-storage-model) *(stub — see deferred ref)*
12. [Workspace / UI Layout Persistence Model](#12-workspace--ui-layout-persistence-model)
13. [Project-Type Flexibility](#13-project-type-flexibility) *(summary — see deferred ref)*
14. [Legacy Save Logic Removal](#14-legacy-save-logic-removal)
15. [SQLite Access Patterns & Concurrency](#15-sqlite-access-patterns--concurrency)
16. [DI Registration & Service Wiring](#16-di-registration--service-wiring)
17. [Testing Strategy](#17-testing-strategy)
18. [Implementation Phases](#18-implementation-phases)
19. [Coding Standards & DI Architecture](#19-coding-standards--di-architecture)
20. [Appendix A: `GuiSettings` Changes](#appendix-a-guisettings-changes)
21. [Appendix B: Message Changes](#appendix-b-message-changes)
22. [Appendix C: Schema Version Strategy](#appendix-c-schema-version-strategy)
23. [Appendix D: Incorporating SQLite into Pandowdy](#appendix-d-incorporating-sqlite-into-pandowdy)
24. [Appendix E: Ad Hoc Project Design](#appendix-e-ad-hoc-project-design)

---

## 1. Architecture Overview

### Conceptual Layers

```
┌──────────────────────────────────────────────────────────┐
│  Pandowdy (Host)   — DI composition root, startup        │
├──────────────────────────────────────────────────────────┤
│  Pandowdy.UI       — Start Page, project dialogs, menus  │
├──────────────────────────────────────────────────────────┤
│  Pandowdy.Project  — NEW: .skillet read/write, models,   │
│                      resolution engine, migrations       │
├──────────────────────────────────────────────────────────┤
│  Pandowdy.EmuCore  — Emulator core (unchanged API)       │
│  Pandowdy.Disassembler — Disassembler (unchanged API)    │
│  Pandowdy.Cpu      — CPU emulation (unchanged)           │
└──────────────────────────────────────────────────────────┘
```

### Key Principle  
**The `.skillet` file (SQLite) is the single source of truth for all project state.**  
`pandowdy-settings.json` stores only global/workstation-level defaults.  
Runtime changes persist to the appropriate layer.

### File Extension  
`.skillet` — registered as a SQLite database with application_id and user_version pragmas.

### Disk Image Ownership Model

Disk images have three stakeholders with distinct roles:

```
┌─────────────────────────────────────────────────────────────────────┐
│                     Three-Party Ownership Model                     │
│                                                                     │
│  ┌──────────────┐   library mgmt    ┌───────────────────────┐       │
│  │   GUI        │ ──────────────►   │  Skillet (.skillet)   │       │
│  │  (Pandowdy   │   (import,        │  (Pandowdy.Project)   │       │
│  │   .UI)       │    catalog,       │                       │       │
│  │              │    remove)        │  Owns all disk image  │       │
│  │              │                   │  data (blobs).        │       │
│  │              │                   │  Implements           │       │
│  │              │                   │  IDiskImageStore.     │       │
│  └──────┬───────┘                   └───────────┬───────────┘       │
│         │ mount/eject                           │                   │
│         │ requests                              │ CheckOutAsync()   │
│         ▼                                       │ ReturnAsync()     │
│  ┌──────────────┐                               │                   │
│  │  EmuCore     │ ◄─────────────────────────────┘                   │
│  │              │                                                   │
│  │  Mediates    │  In-memory InternalDiskImage                      │
│  │  mount/eject │  lives here while mounted.                        │
│  │  via card    │  EmuCore reads/writes it at                       │
│  │  messages.   │  full speed — no SQLite                           │
│  │              │  during emulation.                                │
│  └──────────────┘                                                   │
└─────────────────────────────────────────────────────────────────────┘
```

**Roles:**

| Party | Responsibilities |
|-------|------------------|
| **GUI** (Pandowdy.UI) | User intent: requests mount/eject via card messages to EmuCore. Talks to Skillet directly only for library management (import, catalog, remove). |
| **EmuCore** | Mediates all mount/eject operations. On mount, calls `IDiskImageStore.CheckOutAsync()` to borrow an `InternalDiskImage`. On eject, calls `IDiskImageStore.ReturnAsync()` to return (and serialize) the image. Owns the in-memory `InternalDiskImage` while it is mounted. |
| **Skillet** (Pandowdy.Project) | Owns all disk image data. Implements `IDiskImageStore` (interface defined in EmuCore). Lends `InternalDiskImage` to EmuCore on checkout; receives it back on return. Serializes returned images to `working_blob`. |

**`IDiskImageStore` Interface (defined in Pandowdy.EmuCore):**

```csharp
/// <summary>
/// Abstraction for a persistent store that can lend and accept disk images.
/// Defined in EmuCore so the controller card can depend on it without
/// referencing Pandowdy.Project.
/// </summary>
public interface IDiskImageStore
{
    /// <summary>
    /// Checks out a disk image for use by the emulator. The returned
    /// InternalDiskImage is owned by the caller until ReturnAsync is called.
    /// </summary>
    Task<InternalDiskImage> CheckOutAsync(long diskImageId);

    /// <summary>
    /// Returns a disk image to the store after ejection. The store serializes
    /// the image to working_blob if it has been modified.
    /// </summary>
    Task ReturnAsync(long diskImageId, InternalDiskImage image);
}
```

**Layer rule:** EmuCore defines `IDiskImageStore` but never references
`Pandowdy.Project`. `SkilletProject` implements the interface. The host app's
DI composition root wires the implementation to the interface and injects it
into the card factory / controller card.

---

## 2. New Project: Pandowdy.Project

A new class library project is introduced to own all `.skillet`-related logic.  
This keeps the project file system concerns isolated from emulator core and UI.

### Project File: `Pandowdy.Project/Pandowdy.Project.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>True</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Pandowdy.Project.Tests" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.7" />
    <PackageReference Include="System.Reactive" Version="6.1.0" />
  </ItemGroup>
</Project>
```

### Why `Microsoft.Data.Sqlite`
- Official Microsoft ADO.NET provider for SQLite.
- No ORM — direct SQL for full control over schema, blobs, and transactions.
- Lightweight; no Entity Framework dependency.
- Cross-platform (Windows, macOS, Linux).

### Directory Structure

```
Pandowdy.Project/
├── Interfaces/
│   ├── ISkilletProject.cs          — Read/write contract for open project (extends IDiskImageStore)
│   ├── ISkilletProjectManager.cs   — Open/create/close lifecycle
│   └── ISettingsResolver.cs        — JSON + skillet resolution
├── Models/
│   ├── ProjectMetadata.cs          — Project name, created, modified
│   ├── DiskImageRecord.cs          — Disk image metadata model
│   └── MountConfiguration.cs       — Slot/drive mount assignments
├── Services/
│   ├── SkilletProject.cs           — ISkilletProject + IDiskImageStore implementation
│   ├── SkilletProjectManager.cs    — ISkilletProjectManager implementation
│   ├── SkilletSchemaManager.cs     — Schema creation & migration
│   ├── SettingsResolver.cs         — Hardcoded → JSON → skillet resolution
│   ├── DiskBlobStore.cs            — Blob read/write for disk images
│   └── ProjectSettingsStore.cs     — Key-value settings within .skillet
├── Constants/
│   └── SkilletConstants.cs         — Schema version, application_id, table names
└── Migrations/
    └── V1_InitialSchema.cs         — Initial schema migration
```

Models for deferred features (`BreakpointRecord`, `WatchRecord`, `SymbolRecord`,
`AppleSoftSourceRecord`, `WorkspaceLayout`) will be added when their respective
phases are implemented.

### Corresponding Test Project: `Pandowdy.Project.Tests/`

Mirrors `Pandowdy.Project/` structure.

---

## 3. SQLite Schema

### Pragmas (set on every connection open)

```sql
PRAGMA application_id = 0x534B494C;   -- "SKIL" in hex
PRAGMA user_version = 1;              -- Schema version for migrations
PRAGMA journal_mode = WAL;            -- Write-Ahead Logging for concurrency
PRAGMA foreign_keys = ON;
```

### V1 vs Deferred Tables

The V1 schema creates only the 6 tables required by Phases 1–3:

| V1 Table | Purpose |
|----------|---------|  
| `project_metadata` | Singleton project-level key-value pairs |
| `disk_images` | Disk image metadata and blobs |
| `mount_configuration` | Slot/drive mount assignments |
| `emulator_overrides` | Per-project emulator setting overrides |
| `display_overrides` | Per-project display setting overrides |
| `project_settings` | General-purpose key-value store |

All other tables (`breakpoints`, `watches`, `symbols`, `disassembly_cache`,
`applesoft_sources`, `execution_history`, `workspace_layout`, `user_annotations`)
are **deferred** — they will be created by future schema migrations when their
respective features are designed and implemented. The migration infrastructure
(`ISchemaMigration`, `SkilletSchemaManager`) makes adding tables later zero-cost.
This avoids speculative schema design for features whose technical requirements
are not yet finalized.

---

### V1 Tables

### Table: `project_metadata`

Stores singleton project-level information.

```sql
CREATE TABLE project_metadata (
    key         TEXT PRIMARY KEY NOT NULL,
    value       TEXT NOT NULL
);

-- Seeded rows:
-- ('name',            'My Project')
-- ('created_utc',     '2026-07-15T12:00:00Z')
-- ('modified_utc',    '2026-07-15T12:00:00Z')
-- ('schema_version',  '1')
-- ('pandowdy_version','0.1.0')
```

### Table: `disk_images`

Stores both original and working copy blobs for imported disk images.

```sql
CREATE TABLE disk_images (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    name                TEXT NOT NULL,                -- User-facing name (e.g., "DOS 3.3 Master")
    original_filename   TEXT,                         -- Original filename at import time
    original_format     TEXT NOT NULL,                -- DiskFormat enum name ('Woz','Nib','Dsk','Do','Po')
    import_source_path  TEXT,                         -- Full path at import time (informational only)
    imported_utc        TEXT NOT NULL,                -- ISO 8601 timestamp
    whole_track_count    INTEGER NOT NULL DEFAULT 35, -- Whole tracks (quarter-track count derived: (N-1)*4+1)
    optimal_bit_timing  INTEGER NOT NULL DEFAULT 32,  -- Track-based images only
    is_write_protected  INTEGER NOT NULL DEFAULT 0,   -- SQLite boolean
    persist_working     INTEGER NOT NULL DEFAULT 1,   -- Whether to persist working copy on save
    notes               TEXT,                         -- User annotations
    original_blob       BLOB NOT NULL,                -- Immutable pristine copy (Internal format)
    working_blob        BLOB,                         -- Mutable working copy (null = regenerate from original)
    working_dirty       INTEGER NOT NULL DEFAULT 0,   -- Tracks if working copy has unsaved modifications
    created_utc         TEXT NOT NULL,
    modified_utc        TEXT NOT NULL
);

CREATE INDEX idx_disk_images_name ON disk_images(name);
```

### Table: `mount_configuration`

Maps disk images to emulator slots/drives.

```sql
CREATE TABLE mount_configuration (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    slot            INTEGER NOT NULL,            -- Expansion slot (1-7)
    drive_number    INTEGER NOT NULL,            -- Drive (1 or 2)
    disk_image_id   INTEGER,                     -- FK to disk_images, null = empty drive
    auto_mount      INTEGER NOT NULL DEFAULT 1,  -- Mount on project open
    FOREIGN KEY (disk_image_id) REFERENCES disk_images(id) ON DELETE SET NULL,
    UNIQUE(slot, drive_number)
);
```

### Table: `emulator_overrides`

Per-project emulator configuration overrides.

```sql
CREATE TABLE emulator_overrides (
    key     TEXT PRIMARY KEY NOT NULL,
    value   TEXT NOT NULL
);

-- Example rows:
-- ('throttle_enabled', 'true')
-- ('caps_lock_enabled', 'false')
-- ('target_mhz', '1.023')
```

### Table: `display_overrides`

Per-project display configuration overrides.

```sql
CREATE TABLE display_overrides (
    key     TEXT PRIMARY KEY NOT NULL,
    value   TEXT NOT NULL
);

-- Example rows:
-- ('show_scanlines', 'true')
-- ('force_monochrome', 'false')
```

### Table: `project_settings`

General-purpose key-value store for project-level settings not covered above.

```sql
CREATE TABLE project_settings (
    key     TEXT PRIMARY KEY NOT NULL,
    value   TEXT NOT NULL
);
```

---

### Deferred Tables

Additional tables for debugger state, AppleSoft sources, workspace layout, and
user annotations will be created by future schema migrations when their features
are implemented. See `docs/Skillet_Deferred_Features_Reference.md` §1 for
reference designs.

---

## 4. C# Data Models

All models live in `Pandowdy.Project/Models/`. They are plain C# records or classes — no ORM attributes.
Manual mapping from `SqliteDataReader` to model.

### `ProjectMetadata`

```csharp
public sealed record ProjectMetadata(
    string Name,
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    int SchemaVersion,
    string PandowdyVersion);
```

### `DiskImageRecord`

```csharp
public sealed class DiskImageRecord
{
    public long Id { get; init; }
    public required string Name { get; set; }
    public string? OriginalFilename { get; init; }
    public required string OriginalFormat { get; init; }  // DiskFormat enum name
    public string? ImportSourcePath { get; init; }
    public DateTime ImportedUtc { get; init; }
    public int WholeTrackCount { get; init; } = 35;
    public byte OptimalBitTiming { get; init; } = 32;
    public bool IsWriteProtected { get; set; }
    public bool PersistWorking { get; set; } = true;
    public string? Notes { get; set; }
    public bool WorkingDirty { get; set; }
    public DateTime CreatedUtc { get; init; }
    public DateTime ModifiedUtc { get; set; }
}
```

> **Note:** Blob data (`original_blob`, `working_blob`) is accessed via `DiskBlobStore` using
> streaming APIs, never loaded into `DiskImageRecord` to avoid large heap allocations.

> **Note:** `DiskImageRecord` is designed for track-based disk images (`InternalDiskImage`).
> `WholeTrackCount` and `OptimalBitTiming` are not meaningful for block-based devices. If
> `InternalBlockDeviceImage` is introduced for hard drive images (see §6.2 Block Device
> Support), a corresponding model with block-specific properties (`BlockCount`, `BlockSize`)
> will be added alongside the necessary schema migration.

> **Note:** `WholeTrackCount` in the `.skillet` domain maps to `InternalDiskImage.PhysicalTrackCount`
> in EmuCore. "Whole" was chosen for the project layer to avoid ambiguity — "physical" could
> be misread as the count of physical head positions (which is actually `QuarterTrackCount`).
> If EmuCore is renamed later, the mapping is in `DiskBlobStore.Serialize()` / `Deserialize()`.

### `MountConfiguration`

```csharp
public sealed record MountConfiguration(
    long Id,
    int Slot,
    int DriveNumber,
    long? DiskImageId,
    bool AutoMount);
```

Models for deferred features (`BreakpointRecord`, `WatchRecord`, `SymbolRecord`,
`WorkspaceLayout`) will be added when their respective phases are implemented.
See `docs/Skillet_Deferred_Features_Reference.md` §2 for reference designs.

---

## 5. Project Load / Save Lifecycle

### 5.1 Creating a New Project

**File-based project (user-initiated):**

```
User clicks "Create New Skillet Project"
  → File dialog: choose location and name
  → SkilletProjectManager.CreateAsync(filePath, projectName)
    → Create new SQLite file at filePath
    → Set pragmas (application_id, user_version, journal_mode, foreign_keys)
    → Run SkilletSchemaManager.InitializeSchema() — creates V1 tables
    → Insert project_metadata rows (name, timestamps, version)
    → Insert default mount_configuration rows (slot 6, drives 1 & 2, empty)
    → Return ISkilletProject handle
  → Update pandowdy-settings.json: set active_project_path, add to recent list
  → UI transitions to main workspace
```

**Ad hoc project (automatic on startup / after close):**

```
SkilletProjectManager.CreateAdHocAsync()
  → Open in-memory SQLite connection (Data Source=:memory:)
  → Set pragmas (application_id, user_version, journal_mode not applicable, foreign_keys)
  → Run SkilletSchemaManager.InitializeSchema() — creates V1 tables
  → Insert project_metadata rows (name = "untitled", timestamps, version)
  → Insert default mount_configuration rows (slot 6, drives 1 & 2, empty)
  → Return ISkilletProject handle (FilePath = null, IsAdHoc = true)
```

The ad hoc project is treated identically to any file-based project once created.
See [Appendix E](#appendix-e-ad-hoc-project-design) for the full design.

### 5.2 Opening an Existing Project

```
User selects project from recent list or file dialog
  → SkilletProjectManager.OpenAsync(filePath)
    → Validate file exists
    → Open SQLite connection
    → Verify application_id pragma == 0x534B494C
    → Read user_version pragma
    → If schema_version < current → run SkilletSchemaManager.Migrate()
    → Load ProjectMetadata
    → Return ISkilletProject handle
  → SettingsResolver merges JSON defaults with skillet overrides
  → Load mount_configuration → auto-mount disk images into emulator
  → Update pandowdy-settings.json: set active_project_path, update recent list
  → UI transitions from Start Page to main workspace
```

### 5.3 Saving a Project (Explicit & Periodic)

**Save Project (Ctrl+Alt+S):** Available only when all of the following are true:
(1) the project is file-based (`IsAdHoc` is false), and (2) the project is not
pristine (it has imported disk images or unsaved changes). Disabled in the UI
for ad hoc projects (no file path) and for pristine projects (nothing to save).

```
User presses Ctrl+Alt+S or auto-save timer fires
  → Guard: if ISkilletProject.IsAdHoc OR project is pristine → no-op (Save is disabled)
  → ISkilletProject.SaveAsync()
    → For each checked-out disk with IsDirty:
      → DiskBlobStore.SerializeSnapshot(diskImage):
        → Acquire InternalDiskImage.SerializationLock
        → Copy raw quarter-track byte arrays (~1ms memcpy)
        → Release lock
        → Compress snapshot via Deflate (no lock held, ~50ms)
    → Enqueue IO request:
      → BEGIN TRANSACTION
      → For each serialized disk with persist_working = true:
        → UPDATE disk_images SET working_blob = ?, working_dirty = 0, modified_utc = ?
      → Update project_metadata modified_utc
      → COMMIT
```

**Save Project As... (Ctrl+Shift+S):** Available when the project is not pristine
(has imported disk images or unsaved changes). Disabled for pristine projects
that have no content worth persisting. Persists the current project (including
ad hoc) to a new file on disk.

```
User: File → Save Project As...
  → File dialog: choose location and filename
  → ISkilletProjectManager.SaveAsAsync(filePath)
    → Serialize all checked-out dirty disks (same as SaveAsync)
    → VACUUM INTO 'filePath' — copies entire in-memory DB to new file
    → Close current in-memory connection
    → Open file-based connection to the new file
    → Update ISkilletProject: FilePath = filePath, IsAdHoc = false
  → Update pandowdy-settings.json: set active_project_path, add to recent list
  → Update title bar: "Pandowdy — {ProjectName}"
```

**`VACUUM INTO` rationale:** SQLite 3.27.0+ (available in the bundled e_sqlite3
native library) supports `VACUUM INTO 'filepath'` which atomically copies the
entire database to a new file. This is the cleanest way to persist an in-memory
database: no manual table copying, no schema recreation, and the output file is
optimally compacted. After the vacuum, the `SkilletProject` instance is preserved
— only the internal `SqliteConnection` swaps. All external references
(`IDiskImageStore`, checked-out images, dirty tracking) remain valid.

**⚠️ Known race condition — Save As during active disk writes:**
`SaveAsAsync()` calls `SaveAsync()` (which snapshot-serializes mounted disks under
`SerializationLock`) and then enqueues a `VACUUM INTO` to the IO thread. Between
the snapshot and the vacuum, the emulator may write additional bits to a mounted
`InternalDiskImage`. Those writes exist in the live in-memory object but are **not**
captured in the new file until the next explicit save or eject. This is the same
staleness window that exists during any regular `SaveAsync()` — the in-memory
`InternalDiskImage` is always the authoritative state, and the file is a
point-in-time snapshot. No data is lost; the writes are captured by the next save
or eject auto-flush. However, if the application crashes or is force-killed
immediately after Save As (before the next save/eject), those in-flight writes
would not be in the file. This edge case will be evaluated further in Phase 6
(Integration Testing) and may warrant a "pause emulator during Save As" option
or a post-swap re-snapshot for safety.

**Serialization safety:** The emulator thread mutates `InternalDiskImage` at cycle
speed. `SaveAsync()` uses a **snapshot-under-lock** strategy to serialize mounted
disks without pausing the emulator:

1. **Acquire lock** — `InternalDiskImage.SerializationLock` is acquired briefly.
   The emulator's write path (`WriteBit()` in `UnifiedDiskImageProvider`) also
   acquires this lock, so writes are blocked only during the snapshot window.
2. **Snapshot** — Raw quarter-track byte arrays are copied via `ReadOctet()`
   (~1ms for a 35-track disk, ~230KB memcpy). Read operations are not locked.
3. **Release lock** — The emulator write path resumes immediately.
4. **Compress** — The snapshot is Deflate-compressed on the calling thread
   (~50ms). No lock is held during compression.

The lock overhead on the emulator hot path is ~20ns per `WriteBit()` call
(uncontended lock acquisition). At the Apple IIe's 1.023 MHz clock, disk write
operations occur at ~4µs per bit (32 × 125ns), so the lock overhead is <0.5%
of one bit time — negligible. The lock only contends during the brief snapshot
window (~1ms per save), which occurs at explicit lifecycle boundaries
(Ctrl+Alt+S, auto-save timer).

`SkilletProject` tracks checked-out images internally: `CheckOutAsync()` adds
the image to a `ConcurrentDictionary`, `ReturnAsync()` removes it. `SaveAsync()`
iterates the dictionary to find dirty images — no external caller needs to
provide the list of mounted disks.

### 5.4 Closing a Project

**Close Project enablement:**

- **File-based project:** Close Project is **always enabled**. The project has a
  file on disk and is always closable.
- **Ad hoc project with data** (imported disks, overrides, or other modifications):
  Close Project is **enabled**. The user is prompted to save before discarding.
- **Pristine ad hoc project** (no imported disks, no overrides — the project is
  in its initial creation state and has never been saved to disk): Close Project
  is **disabled**. There is nothing to close.

```
User closes project or opens another
  → Guard: if ad hoc AND pristine → Close is disabled (unreachable from UI)
  → Check for unsaved changes (working_dirty on any mounted disk, or ad hoc with data)
  → If unsaved:
    → If ad hoc with disk images → prompt: Save As / Discard / Cancel
    → If file-based with dirty data → prompt: Save / Discard / Cancel
  → If Save → ISkilletProject.SaveAsync() (snapshot-serialize checked-out disks)
  → If Save As → ISkilletProjectManager.SaveAsAsync(filePath) (file dialog first)
  → GUI sends EjectAllDisksMessage to EmuCore (async, waits for confirmation)
    → EmuCore ejects each mounted drive:
      → For each drive with a mounted disk:
        → DiskIIControllerCard calls IDiskImageStore.ReturnAsync(id, image)
        → Skillet serializes the returned image to working_blob
        → Drive is cleared
    → EmuCore confirms all ejections complete
  → ISkilletProject.Dispose()
    → Drain IO thread queue
    → Close SQLite connection (in-memory data is lost if ad hoc)
  → If opening another → proceed to Open flow
  → If not opening another → SkilletProjectManager.CreateAdHocAsync()
```

**Key points:**

- The GUI never closes the Skillet while disks are mounted. The eject-all step
  ensures every in-memory `InternalDiskImage` is returned to the store before the
  SQLite connection is torn down. This prevents data loss.
- After closing, the system **always** creates a new ad hoc project — there is
  never a state with no active project. This may result in an "untitled" project
  being replaced by another "untitled" project — that is expected behavior.
- Closing an ad hoc project with disk images imported prompts "Save As" (not
  "Save") since there is no file path to save to. Discarding loses all imported
  disk images and settings.
- A pristine ad hoc project cannot be closed — the menu item is disabled. The
  user must import a disk, change a setting, or use "Save Project As..." to give
  the project content before Close Project becomes available.

### 5.5 `ISkilletProject` Interface

```csharp
public interface ISkilletProject : IDiskImageStore, IDisposable
{
    string? FilePath { get; }           // null for ad hoc in-memory projects
    bool IsAdHoc { get; }               // true when FilePath is null
    ProjectMetadata Metadata { get; }
    bool HasUnsavedChanges { get; }     // true if any mutation since create/open/save
    bool HasDiskImages { get; }         // true if disk_images table has any rows

    // Disk image library management (GUI → Skillet directly)
    Task<long> ImportDiskImageAsync(string filePath, string name);
    Task<DiskImageRecord> GetDiskImageAsync(long id);
    Task<IReadOnlyList<DiskImageRecord>> GetAllDiskImagesAsync();
    Task RemoveDiskImageAsync(long id);
    Task RegenerateWorkingCopyAsync(long id);

    // IDiskImageStore (EmuCore → Skillet via interface)
    // Task<InternalDiskImage> CheckOutAsync(long diskImageId);  — inherited
    // Task ReturnAsync(long diskImageId, InternalDiskImage image);  — inherited

    // Mount configuration
    Task<IReadOnlyList<MountConfiguration>> GetMountConfigurationAsync();
    Task SetMountAsync(int slot, int driveNumber, long? diskImageId);

    // Settings
    Task<string?> GetSettingAsync(string table, string key);
    Task SetSettingAsync(string table, string key, string value);

    // Lifecycle
    Task SaveAsync();
}
```

**Note:** `FilePath` is nullable — ad hoc in-memory projects have no file on
disk. `IsAdHoc` is a convenience property equivalent to `FilePath is null`.
The "Save Project" command is disabled when `IsAdHoc` is true; the user must
use "Save Project As..." to persist an ad hoc project to a file.

**Note:** `HasUnsavedChanges` is backed by a single volatile `_projectDirty` flag,
set exclusively via the centralized `MarkDirty()` method and cleared by
`MarkClean()` (called at the end of `SaveAsync()`). Every mutation method
(import, remove, settings change, mount configuration, eject auto-flush, working
copy regeneration) calls `MarkDirty()`. The property simply returns `_projectDirty`
— no SQL queries are involved, making it safe to call from any thread without
blocking on the IO thread.

**Note:** `MarkDirty()` is distinct from the per-disk-image `working_dirty` SQL
column. The SQL column tracks whether a specific disk image's `working_blob` has
been modified via eject auto-flush — it is an internal persistence detail used by
`SaveAsync()` to manage which blobs to flush and which `persist_working` policies
to enforce. `MarkDirty()` tracks the project-level dirty state used by
`HasUnsavedChanges` for UI enablement (Save, Save As, Close). As new mutation
methods are added to the project, they should call `MarkDirty()` to ensure
consistent dirty tracking.

**Note:** `HasDiskImages` is a lightweight check against the `disk_images` table.
Used for Export Disk Image enablement — the command is disabled when the library
is empty.

**Note:** `ISkilletProject` extends `IDiskImageStore` (defined in EmuCore).
The `CheckOutAsync()` and `ReturnAsync()` methods are called by
`DiskIIControllerCard` during mount and eject operations. The remaining
methods (library management, settings, lifecycle) are called by the GUI
or other Pandowdy.Project consumers directly.

### 5.6 `ISkilletProjectManager` Interface

```csharp
public interface ISkilletProjectManager
{
    ISkilletProject CurrentProject { get; }   // Never null — ad hoc if no file

    Task<ISkilletProject> CreateAdHocAsync();  // In-memory project (Data Source=:memory:)
    Task<ISkilletProject> CreateAsync(string filePath, string projectName);
    Task<ISkilletProject> OpenAsync(string filePath);
    Task SaveAsAsync(string filePath);         // Persist ad hoc/current to file
    Task CloseAsync();                         // Closes current; creates new ad hoc
}
```

**Key design change:** `CurrentProject` is **non-nullable**. On startup, the
manager calls `CreateAdHocAsync()` to create an in-memory project. `CloseAsync()`
disposes the current project and immediately creates a new ad hoc project — the
result is never "no project open." See [Appendix E](#appendix-e-ad-hoc-project-design)
for full lifecycle details.

---

## 6. Disk Import / Use / Export Lifecycle

### 6.1 Import Flow

```
User: "Import Disk Image" → file dialog → selects game.woz
  → ISkilletProject.ImportDiskImageAsync("C:\disks\game.woz", "Lode Runner")
    → Detect format from extension via DiskFormatHelper
    → Use existing IDiskImageImporter to load → InternalDiskImage
    → Serialize InternalDiskImage to byte[] (the "Internal" format blob)
    → BEGIN TRANSACTION
    → INSERT into disk_images:
      - name = "Lode Runner"
      - original_filename = "game.woz"
      - original_format = "Woz"
      - import_source_path = "C:\disks\game.woz"
      - original_blob = serialized bytes
      - working_blob = copy of serialized bytes (initial working = original)
      - working_dirty = 0
    → COMMIT
    → Return new disk_image_id
```

### 6.2 Blob Serialization Strategy

The `InternalDiskImage` is serialized to blob using a compact binary format.
All blobs stored in `.skillet` are compressed — originals, working copies, and
future snapshots alike. Every blob in storage is cold by definition: the hot
data is always the in-memory `InternalDiskImage` that the emulator reads and
writes directly. Blobs are only deserialized at mount time and serialized at
save time — infrequent lifecycle boundaries where compression cost is negligible.
Disk images often contain large runs of zeros or repeated byte patterns, so
compression significantly reduces `.skillet` file size.

```
[Header] (uncompressed, 12 bytes)
  4 bytes: magic ("PIDI" — Pandowdy Internal Disk Image)
  2 bytes: format version (1)
  1 byte:  compression method (0 = none, 1 = Deflate)
  1 byte:  whole track count (typically 35; quarter-track count derived: (N-1)*4+1)
  1 byte:  optimal bit timing
  1 byte:  write protected flag
  2 bytes: quarter-track count (little-endian uint16, = (whole_track_count - 1) * 4 + 1)

[Presence Bitmap] (uncompressed, ceil(quarter_track_count / 8) bytes — 18 bytes for 137 QTs)
  1 bit per quarter-track position, LSB-first within each byte.
  Bit = 1: quarter-track has data (non-null CircularBitBuffer).
  Bit = 0: quarter-track is unwritten (null — returns MC3470 random noise when read).

[Payload] (compressed via method specified in header)
  [Per non-null quarter-track] — only entries with bit = 1 in presence bitmap, in index order
    4 bytes: bit count (little-endian int32)
    4 bytes: byte count (little-endian int32, = ceil(bit_count/8))
    N bytes: raw quarter-track data

[Footer] (uncompressed)
  4 bytes: CRC-32 of header + presence bitmap + compressed payload
```

The presence bitmap efficiently encodes the sparse nature of quarter-track data.
A standard 35-track disk imported from NIB/DSK format has only 35 of 137 quarter-track
positions populated (indices 0, 4, 8, ..., 136). WOZ images may populate additional
fractional quarter-track positions. The bitmap costs only `ceil(137/8) = 18 bytes`
and avoids storing empty entries in the payload.

This is implemented in `DiskBlobStore.Serialize()` / `DiskBlobStore.Deserialize()`.

The CRC-32 uses the existing `System.IO.Hashing` package already referenced by
`Pandowdy.EmuCore`. Compression uses `System.IO.Compression.DeflateStream`
(built into the .NET runtime — no additional package required).

### Compression Details

- **Algorithm:** Deflate via `DeflateStream` (`System.IO.Compression`).
- **Level:** `CompressionLevel.Optimal` for storage. Speed is not critical —
  serialize/deserialize happens at lifecycle boundaries (mount, save), never
  per-cycle.
- **Scope:** The per-quarter-track payload is compressed as a single Deflate stream.
  The 12-byte header, presence bitmap, and 4-byte CRC footer remain uncompressed so
  the header can be read and the CRC validated without decompressing.
- **method byte = 0 (none):** Reserved for diagnostic/debugging use. The
  deserializer must handle uncompressed payloads, but `Serialize()` always
  writes method = 1 (Deflate) in production.
- **Expected ratio:** Typical Apple II disk images compress to ~40–60% of
  original size. The primary size range spans 140KB 5.25" floppies (most
  common, ~230KB in PIDI quarter-track form with 35 populated positions),
  880KB 3.5" floppies (common; half-track resolution only — the Sony 3.5"
  drives do not support quarter-track positioning), and 32MB hard drive images
  (upper bound — may use a block-based blob format instead of PIDI; see Block
  Device Support below). A 140KB floppy (~230KB PIDI) typically stores as
  ~100–140KB compressed. WOZ images with additional fractional quarter-track
  data (5.25" only) may be slightly larger before compression. At the upper
  bound, a hard drive image (~32MB uncompressed) is still fast to
  compress/decompress at lifecycle boundaries regardless of blob format —
  these are infrequent bulk operations, not per-cycle work, and the data
  sizes involved are modest by modern standards.
- **No new dependencies:** `System.IO.Compression` is part of the .NET runtime.
  `DeflateStream` is available in all target frameworks (net8.0+).

### Block Device Support (Future)

The PIDI format is designed for quarter-track-based `InternalDiskImage` — floppy disk
images where data is organized as nibblized quarter-tracks with per-quarter-track bit
counts and a presence bitmap indicating which positions have data.
Hard drive images (up to 65,536 × 512-byte blocks, ~32MB) may use a
block-based internal representation (`InternalBlockDeviceImage`) rather than
the quarter-track model. If so, a separate blob format (e.g., "PIBI" — Pandowdy
Internal Block Image) would be defined with block-oriented payload structure
instead of per-quarter-track layout. The `DiskBlobStore` would gain a second
serializer, and the blob magic bytes would distinguish PIDI from PIBI at
deserialization time. The same compression strategy (Deflate) and CRC-32
footer apply regardless of internal format.

This is not part of the V1 implementation. The schema, model, and blob format
changes for block devices will be designed when `InternalBlockDeviceImage` is
created. The migration infrastructure makes adding the necessary schema
columns and tables zero-cost at that point.

### 6.3 Internal Use (Mount via Checkout)

When a disk image is mounted into the emulator:

```
GUI: User selects "Mount" for disk_image_id = 3 into slot 6, drive 1
  → GUI sends MountDiskMessage(DriveNumber: 1, DiskImageId: 3) to EmuCore
  → DiskIIControllerCard.HandleMessage(MountDiskMessage):
    → Controller calls IDiskImageStore.CheckOutAsync(diskImageId: 3)
      → Skillet (on IO thread):
        → SELECT COALESCE(working_blob, original_blob) FROM disk_images WHERE id = 3
        → Validate CRC-32 on compressed blob
        → Read presence bitmap → decompress payload (Deflate)
        → Reconstruct InternalDiskImage with quarter-tracks (null for absent positions)
        → Return InternalDiskImage to caller
    → Controller receives InternalDiskImage, assigns to drive
    → Emulator reads/writes InternalDiskImage in-memory as normal
```

**Note:** The `HandleMessage()` method is synchronous. The `CheckOutAsync()` call
blocks the emulator thread briefly (~50ms for decompression of a 35-track disk).
This is acceptable — mount operations occur at lifecycle boundaries (user action),
not during cycle-accurate emulation. CPU throttling easily accommodates this pause.

All modifications happen in-memory (uncompressed). On project save, dirty
`InternalDiskImage` objects are snapshot-serialized under lock and written back to
`working_blob` (see §5.3). On eject, the image is returned to the store via `ReturnAsync()` (see §6.6).

### 6.4 Export Flow

```
User: "Export Disk" → context menu on mounted disk
  → Choose export format and destination path
  → Read InternalDiskImage from emulator (current in-memory state)
    — OR —
    Read from project: original_blob or working_blob
  → Use existing IDiskImageExporter to export to chosen format
  → Write to filesystem
  → No project state changes (export is non-destructive, non-persistent)
```

### 6.5 Regeneration

```
User: "Revert to Original" → context menu on disk
  → ISkilletProject.RegenerateWorkingCopyAsync(diskImageId)
    → UPDATE disk_images SET working_blob = NULL, working_dirty = 0 WHERE id = ?
    → Next mount will deserialize from original_blob
  → If disk is currently mounted → re-mount from original
```

### 6.6 Eject Auto-Flush

When a disk is ejected from a drive, the controller automatically returns the
`InternalDiskImage` to the store. This ensures that within-session modifications
are preserved across mount/unmount cycles — the user can eject a disk, mount
another, then re-mount the first and see their changes.

```
User: "Eject Disk" → GUI sends EjectDiskMessage(DriveNumber: 1) to EmuCore
  → DiskIIControllerCard.HandleMessage(EjectDiskMessage):
    → Retrieve InternalDiskImage from the drive being ejected
    → Retrieve the associated diskImageId (tracked by the controller)
    → Call IDiskImageStore.ReturnAsync(diskImageId, image)
      → Skillet (on IO thread):
        → Serialize InternalDiskImage via DiskBlobStore.Serialize()
        → UPDATE disk_images SET working_blob = ?, working_dirty = 1, modified_utc = ?
    → Clear the drive (no disk mounted)
```

**Key behaviors:**

- **Every eject serializes.** The working copy is updated on every eject, not just
  on project save. This means `working_blob` always reflects the latest state of
  any disk that has been ejected.
- **`working_dirty` flag.** Set to `1` on eject. This flag controls cross-session
  persistence: on project save, only disks with `persist_working = 1` AND
  `working_dirty = 1` write their `working_blob` to durable storage. Disks with
  `persist_working = 0` still have `working_blob` updated on eject (for within-session
  remounting) but the blob is discarded when the project is closed.
- **Synchronous blocking.** `ReturnAsync()` blocks the emulator thread briefly
  (~50ms for serialization + compression). Acceptable for the same reason as mount:
  eject is a lifecycle operation, not a per-cycle event.
- **Physical media metaphor.** Think of eject as putting the floppy back in its
  box — the disk's current state is captured before it leaves the drive.

---

## 7. Originals vs Working Copies

### Storage Model

| Aspect | Original | Working Copy |
|--------|----------|--------------|
| **Stored in** | `disk_images.original_blob` | `disk_images.working_blob` |
| **Mutability** | Immutable (never modified after import) | Mutable (updated on eject and save) |
| **Null meaning** | N/A (required NOT NULL) | Regenerate from original |
| **Dirty tracking** | N/A | `working_dirty` flag |
| **Persistence policy** | Always persisted | Controlled by `persist_working` |
| **Compression** | Deflate (always) | Deflate (always) |

### `persist_working` Flag

Per-disk-image setting controlling whether the working copy is persisted:

- `persist_working = 1` (default): Working copy is saved to `.skillet` on project save.
  Changes survive across sessions.
- `persist_working = 0`: Working copy is **not** saved. On next project open,
  the working copy is regenerated from the original. Session changes are throwaway.

This implements the spec's requirement: *"the project configuration will determine
on a disk image-specific basis whether the working copy is persisted or recreated
periodically."*

### In-Memory vs On-Disk

```
                 ┌──────────────────┐
                 │  .skillet file   │
                 │                  │
                 │  original_blob ──┼──── Immutable, compressed (Deflate)
                 │  working_blob  ──┼──── Nullable, compressed (Deflate)
                 └────────┬─────────┘
                          │ CheckOutAsync: decompress + deserialize on mount
                          ▼
                 ┌──────────────────┐
                 │ InternalDiskImage│  ← In-memory, uncompressed, used by emulator
                 │  (mutable)       │
                 └────────┬─────────┘
                          │ ReturnAsync: serialize + compress on eject (§6.6)
                          │ SaveAsync: serialize + compress on save (§5.3, if persist_working)
                          ▼
                 ┌──────────────────┐
                 │  .skillet file   │
                 │  working_blob    │  ← Updated, compressed
                 └──────────────────┘
```

---

## 8. JSON Defaults vs `.skillet` Overrides — Resolution Model

### Resolution Order (from spec §5.3)

```
1. Hard-coded defaults (failsafe)
2. pandowdy-settings.json (global)
3. .skillet project overrides
4. Runtime user changes (persisted to appropriate layer)
```

### `ISettingsResolver` Interface

```csharp
public interface ISettingsResolver
{
    /// <summary>
    /// Resolves a setting value using the four-layer resolution order.
    /// </summary>
    T Resolve<T>(string key, T hardcodedDefault);

    /// <summary>
    /// Determines which layer a setting should persist to.
    /// </summary>
    SettingsLayer GetPersistLayer(string key);
}

public enum SettingsLayer
{
    Hardcoded,   // Never persisted
    Json,        // Global workstation preference
    Skillet      // Project-specific override
}
```

### `SettingsResolver` Implementation

```csharp
public class SettingsResolver(
    GuiSettings globalSettings,
    ISkilletProject? currentProject) : ISettingsResolver
{
    public T Resolve<T>(string key, T hardcodedDefault)
    {
        // Layer 3: Check .skillet project override
        if (currentProject is not null)
        {
            var projectValue = currentProject.GetSettingAsync(DetermineTable(key), key)
                .GetAwaiter().GetResult();  // Sync wrapper for resolution
            if (projectValue is not null)
            {
                return Parse<T>(projectValue);
            }
        }

        // Layer 2: Check JSON global setting
        var jsonValue = GetFromGuiSettings<T>(globalSettings, key);
        if (jsonValue is not null)
        {
            return jsonValue;
        }

        // Layer 1: Hard-coded default
        return hardcodedDefault;
    }
}
```

### Setting Classification

| Setting | Layer | Table |
|---------|-------|-------|
| Window geometry | JSON | — |
| Display: scanlines | JSON (default), Skillet (override) | `display_overrides` |
| Display: monochrome | JSON (default), Skillet (override) | `display_overrides` |
| Emulator: throttle | JSON (default), Skillet (override) | `emulator_overrides` |
| Emulator: caps lock | JSON (default), Skillet (override) | `emulator_overrides` |
| Recent projects | JSON | — |
| Active project path | JSON | — |
| Disk images | Skillet only | `disk_images` |
| Mount config | Skillet only | `mount_configuration` |

Settings for deferred features (breakpoints, watches, symbols, AppleSoft sources,
workspace layout, user annotations) are documented in
`docs/Skillet_Deferred_Features_Reference.md` §8.

### Runtime Change Persistence

When the user changes a setting at runtime:

- **Global settings** (window geometry, recent list): persist to JSON immediately.
- **Ambiguous settings** with project open (throttle, scanlines): persist to `.skillet` overrides.
  The project captures the user's intent for that specific project.
- **Ambiguous settings** with no project open: persist to JSON global defaults.
- **Project-only settings** (breakpoints, disks): always persist to `.skillet`.

---

## 9. UI Workflow: Startup, Creation, Switching

### 9.1 Start Page

On launch, Pandowdy can show a Start Page (new Avalonia UserControl) as an
optional project management panel. The workspace is always accessible — an ad
hoc project exists on startup (see §9.6).

```
┌──────────────────────────────────────────────────────────────┐
│                        Pandowdy                              │
│                                                              │
│  ┌─────────────────────┐   ┌──────────────────────────────┐  │
│  │ Create New Project  │   │       Recent Projects        │  │
│  │                     │   │                              │  │
│  │  [New Project...]   │   │  • Lode Runner Analysis      │  │
│  │                     │   │    C:\projects\loderunner    │  │
│  │                     │   │  • DOS 3.3 Development       │  │
│  │                     │   │    C:\projects\dos33dev      │  │
│  │                     │   │  • Karateka Debugging        │  │
│  │                     │   │    C:\projects\karateka      │  │
│  │                     │   │                              │  │
│  └─────────────────────┘   │  [Open Project...]           │  │
│                            └──────────────────────────────┘  │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### 9.2 New Files

| File | Purpose |
|------|---------|
| `Pandowdy.UI/Views/StartPage.axaml` | Start Page layout |
| `Pandowdy.UI/Views/StartPage.axaml.cs` | Code-behind |
| `Pandowdy.UI/ViewModels/StartPageViewModel.cs` | Recent projects, commands |
| `Pandowdy.UI/Views/NewProjectDialog.axaml` | New project dialog |
| `Pandowdy.UI/Views/NewProjectDialog.axaml.cs` | Code-behind |
| `Pandowdy.UI/ViewModels/NewProjectDialogViewModel.cs` | Name, path, options |

### 9.3 Startup Flow

```
Program.Main()
  → Load pandowdy-settings.json via GuiSettingsService
  → Read active_project_path from settings
  → If active_project_path exists AND file exists:
    → Auto-open project (SkilletProjectManager.OpenAsync)
    → Go directly to workspace
  → Else:
    → SkilletProjectManager.CreateAdHocAsync()
    → Go directly to workspace (with ad hoc project)
```

**Note:** There is no gated Start Page that blocks the workspace. The emulator
is always usable — even with an ad hoc project, users can import and mount disk
images. The Start Page (Phase 4) becomes an **optional** project management
panel, not a required gate.

### 9.4 Project Switching

```
User: File → Open Project (while project is open)
  → Check for unsaved changes → prompt if needed (Save As if ad hoc)
  → CloseAsync() current project
  → OpenAsync() new project
  → Rebuild UI from new project's workspace_layout + mount_configuration
```

### 9.5 Menu Changes

**Keyboard Shortcut Constraint:** The EmuCore captures `Ctrl+A` through `Ctrl+Z`
(ASCII 1–26) for Apple IIe keyboard emulation. GUI keyboard shortcuts must avoid
bare `Ctrl+letter` combinations. Preferred patterns: `Ctrl+Shift+letter`,
`Ctrl+Alt+letter`, `Alt+letter`, or multi-keystroke sequences. This constraint
applies whenever the emulator display has focus; future editor windows may reclaim
simple `Ctrl+letter` shortcuts for their own use.

**File Menu (Updated):**
```
File
├── New Project...          Ctrl+Shift+N
├── Open Project...         Ctrl+Shift+O
├── ──────────────
├── Save Project            Ctrl+Alt+S     (disabled when ad hoc or pristine)
├── Save Project As...      Ctrl+Shift+S   (disabled when pristine)
├── ──────────────
├── Import Disk Image...    Ctrl+Shift+I
├── Export Disk Image...    Ctrl+Shift+E   (disabled when no disk images in library)
├── ──────────────
├── Close Project                          (disabled when ad hoc is pristine)
├── ──────────────
├── Recent Projects  →
│   ├── Lode Runner Analysis
│   ├── DOS 3.3 Development
│   └── Clear Recent
├── ──────────────
└── Exit
```

**Removed from File Menu:**
- ~~Open Disk Image~~ (replaced by Import)
- ~~Save~~ (disk images are no longer saved to filesystem)
- ~~Save As~~ (replaced by Export)

### 9.6 Ad Hoc Project Behavior

Pandowdy **always** has an active project. When no named project is specified
(fresh launch with no `active_project_path`, or after closing a project without
opening another), the system automatically creates an **ad hoc project** — an
in-memory SQLite database that behaves identically to any file-based project.

See [Appendix E: Ad Hoc Project Design](#appendix-e-ad-hoc-project-design) for
full design details including lifecycle, persistence, and transition mechanics.

**Key invariant:** `ISkilletProjectManager.CurrentProject` is **never null**.
This eliminates null guards throughout the codebase and simplifies DI wiring —
`IDiskImageStore` injection is always non-nullable.

---

## 10. Debugger Subsystem Storage Model

*Deferred.* See `docs/Skillet_Deferred_Features_Reference.md` §4 for the full
debugger storage design (breakpoints, watches, symbols, disassembly cache,
execution history).

---

## 11. AppleSoft Subsystem Storage Model

*Deferred.* See `docs/Skillet_Deferred_Features_Reference.md` §5 for the full
AppleSoft storage design (source text, tokenized blobs, AST, semantic analysis,
editor state).

---

## 12. Workspace / UI Layout Persistence Model

### JSON Layer (Global Defaults)

Stored in `pandowdy-settings.json`:

```json
{
  "window": {
    "left": 100, "top": 100, "width": 1024, "height": 768,
    "isMaximized": false
  },
  "display": {
    "showScanLines": false,
    "forceMonochrome": false
  },
  "panels": {
    "showDiskStatus": true,
    "showCpuStatus": true,
    "showSoftSwitches": false
  },
  "emulator": {
    "throttleEnabled": true,
    "capsLockEnabled": false
  },
  "recentProjects": [
    { "name": "Lode Runner", "path": "C:\\projects\\loderunner.skillet" }
  ],
  "activeProjectPath": "C:\\projects\\loderunner.skillet"
}
```

Per-project overrides (via `emulator_overrides`, `display_overrides`, and future
`workspace_layout` table) are described in
`docs/Skillet_Deferred_Features_Reference.md` §6.

---

## 13. Project-Type Flexibility

The `.skillet` schema supports multiple project styles (emulator-centric, editor-centric,
multi-disk) without branching logic or a `project_type` discriminator. Tables that
aren't used for a given project style are simply empty.

See `docs/Skillet_Deferred_Features_Reference.md` §7 for detailed project-style
examples.

---

## 14. Legacy Save Logic Removal

### What Gets Removed

Per the spec §6.6: *"The old 'Save As…' filename logic (_new, _new2, etc.) is obsolete."*

| File | Change |
|------|--------|
| `InternalDiskImage.DestinationFilePath` | Remove property |
| `InternalDiskImage.DestinationFormat` | Remove property |
| `SaveDiskMessage` | Remove message class |
| `SaveDiskAsMessage` | Replace with `ExportDiskMessage` |
| `DiskIIControllerCard.HandleSave*` | Remove handlers |
| `MainWindowViewModel` Save commands | Replace with Export commands |
| `DriveStateService` | Remove (state now lives in `.skillet`) |
| `DriveStateConfig` / `DriveStateEntry` | Remove (replaced by `mount_configuration`) |

### What Replaces It

- **`ExportDiskMessage`**: Exports disk to filesystem (user-initiated, explicit).
- **`ISkilletProject.SaveAsync()`**: Persists working copies to `.skillet`.
- **`MountConfiguration`**: Replaces `DriveStateConfig` for slot/drive mapping.

### Migration Path

Since this is a clean break (pre-release), no migration of old settings is needed.
The old `drive-state.json` and save-related menu items are simply removed.

---

## 15. SQLite Access Patterns & Concurrency

### 15.1 Core Principle: Dedicated IO Thread

SQLite is accessed by **exactly one dedicated IO thread**. No other thread —
UI, emulator, background task, or otherwise — ever touches `SqliteConnection`,
`SqliteCommand`, or `SqliteDataReader` directly. This eliminates all SQLite
threading concerns at the architectural level.

```
┌───────────────────────────────────────────────────────────────┐
│                    Request Queue (FIFO)                       │
│                                                               │
│ UI Thread ──────────► ┌──────────┐                            │
│ Emulator Thread ────► │  Queue   │ ────► IO Thread ──► SQLite │
│ Background Tasks ───► └──────────┘        (single)            │
│                             │                                 │
│                    TaskCompletionSource<T>                    │
│                    returned to caller                         │
└───────────────────────────────────────────────────────────────┘
```

### 15.2 Why a Dedicated IO Thread

1. **`Microsoft.Data.Sqlite` async methods are not truly async.** The underlying
   SQLite C library is synchronous. The `*Async()` methods are thin wrappers that
   complete synchronously on the calling thread. Running them on the UI thread
   blocks rendering; running them on the emulator thread introduces jitter.
   A dedicated thread absorbs the blocking cost without affecting either.

2. **Single-thread access eliminates locking.** SQLite's threading modes
   (serialized, multi-thread) add overhead and complexity. With exactly one
   thread touching the connection, no mutexes, reader-writer locks, or
   `PRAGMA busy_timeout` tuning are needed.

3. **Deterministic ordering.** A FIFO queue guarantees that requests are
   processed in the order they are submitted. This simplifies reasoning about
   state transitions — e.g., an import that completes before a mount is
   guaranteed to have its data visible to the mount.

4. **Frontend-agnostic persistence.** The persistence layer is project-centric.
   It knows nothing about Avalonia, WinUI, MAUI, or any other UI framework.
   The IO thread does not marshal results to a dispatcher, raise property-changed
   notifications, or depend on any UI threading model. Callers are responsible
   for dispatching results to their own context (e.g., `Dispatcher.UIThread`).

### 15.3 Connection Management

- One `SqliteConnection` per `SkilletProject` instance.
- Opened on the IO thread during `OpenAsync()` or `CreateAdHocAsync()`.
- Closed on the IO thread during `Dispose()`.
- The connection object is **never** exposed outside the IO thread.
- **Ad hoc projects** use `Data Source=:memory:` — the database exists only in
  process memory and is lost when the connection is closed.
- **File-based projects** use `Data Source={filePath}` — standard persistent connection.

```csharp
// Connection lifecycle — all calls execute on the IO thread
internal sealed class ProjectIOThread : IDisposable
{
    private readonly Thread _thread;
    private readonly BlockingCollection<IORequest> _queue = new();
    private SqliteConnection? _connection;

    public ProjectIOThread(string? filePath)
    {
        var connectionString = filePath is not null
            ? $"Data Source={filePath}"
            : "Data Source=:memory:";

        _thread = new Thread(() => RunLoop(connectionString))
        {
            Name = "Pandowdy.Project.IO",
            IsBackground = true
        };
        _thread.Start();
    }

    private void RunLoop(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        SetPragmas(_connection);

        foreach (var request in _queue.GetConsumingEnumerable())
        {
            request.Execute(_connection);
        }

        _connection.Dispose();
    }

    // ...
}
```

**Note:** `PRAGMA journal_mode = WAL` is not applicable for in-memory databases
(SQLite silently ignores it). All other pragmas (`foreign_keys`, `application_id`,
`user_version`) work normally with in-memory connections.

### 15.4 Request Queue & Async Façade

All threads interact with SQLite through a thread-safe request queue. Each
request carries a `TaskCompletionSource<T>` that the caller `await`s.

#### Request Structure

```csharp
internal abstract class IORequest
{
    public abstract void Execute(SqliteConnection connection);
}

internal sealed class IORequest<T>(
    Func<SqliteConnection, T> operation,
    TaskCompletionSource<T> tcs) : IORequest
{
    public override void Execute(SqliteConnection connection)
    {
        try
        {
            var result = operation(connection);
            tcs.SetResult(result);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
    }
}
```

#### Enqueue Pattern

```csharp
// Inside SkilletProject — available to any thread
internal Task<T> EnqueueAsync<T>(Func<SqliteConnection, T> operation)
{
    var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    _ioThread.Enqueue(new IORequest<T>(operation, tcs));
    return tcs.Task;
}

internal Task EnqueueAsync(Action<SqliteConnection> operation)
{
    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    _ioThread.Enqueue(new IORequest<bool>(conn =>
    {
        operation(conn);
        return true;
    }, tcs));
    return tcs.Task;
}
```

#### Caller Usage (Any Thread)

```csharp
// UI thread — reading settings
var throttle = await project.GetSettingAsync("emulator_overrides", "throttle_enabled");

// Emulator thread — requesting a disk snapshot load
var diskImage = await project.LoadWorkingCopyAsync(diskImageId);

// Background task — saving project state
await project.SaveAsync();
```

All three calls enqueue a request and return a `Task`. The IO thread processes
each request in FIFO order. The caller resumes on whatever context the
`TaskCompletionSource` continuation runs on (typically the thread pool, unless
the caller uses `ConfigureAwait` or a `SynchronizationContext`).

### 15.5 Emulator Thread Access

The emulator thread runs at ~1 MHz cycle accuracy. It **never** calls SQLite
directly. However, it **can and does** request project-state operations through
the queue. These requests are non-blocking from the emulator's perspective —
the emulator enqueues and continues; results arrive asynchronously.

#### Operations the Emulator Thread May Request

| Operation | Direction | Example |
|-----------|-----------|---------|
| Check out disk image | Read | `IDiskImageStore.CheckOutAsync()` during mount |
| Return disk image | Write | `IDiskImageStore.ReturnAsync()` during eject |
| Read symbols | Read | Symbol lookup for disassembly overlay |
| Read breakpoints | Read | Load breakpoint set on debug session start |
| Read watchpoints | Read | Load watch expressions for debugger panel |
| Read annotations | Read | Fetch address annotations for disassembly view |
| Query project state | Read | Check `persist_working` flag, project metadata |

#### Emulator Timing Considerations

During cycle-accurate emulation, the emulator thread operates on in-memory data
structures (`InternalDiskImage`, `CircularBitBuffer[]`, breakpoint sets, etc.).
IO requests are for **bulk operations** at lifecycle boundaries — not per-cycle
lookups:

- **Disk mount:** `MountDiskMessage` → controller calls `IDiskImageStore.CheckOutAsync()`
  → IO thread deserializes blob → emulator receives `InternalDiskImage`. Blocks
  the emulator thread briefly (~50ms) for decompression — acceptable at lifecycle
  boundaries.
- **Disk eject:** `EjectDiskMessage` → controller calls `IDiskImageStore.ReturnAsync()`
  → IO thread serializes `InternalDiskImage` to `working_blob`. Blocks briefly for
  compression.
- **Disk save:** `SaveAsync()` snapshot-serializes all checked-out dirty disks
  using `InternalDiskImage.SerializationLock` — acquires the lock briefly (~1ms)
  to copy raw quarter-track data, then compresses outside the lock. The emulator
  continues running throughout; only the write path is blocked during the snapshot.
- **Debug session start:** Enqueue breakpoint/symbol reads → IO thread fetches
  from SQLite → debugger receives collections via `Task` completion.

The emulator does **not** make per-instruction SQLite queries. Real-time disk I/O
during emulation operates against in-memory `InternalDiskImage` objects — SQLite
is only involved when loading or persisting those objects.

### 15.6 Ordering Guarantees

The request queue provides **strict FIFO ordering**. Requests are processed in
the order they are enqueued. This means:

- An import request that completes before a mount request is guaranteed to have
  written its blob to SQLite before the mount reads it.
- A save request enqueued after a settings change will see the updated settings.
- Multiple save requests from different sources (auto-save timer, explicit Ctrl+Alt+S,
  emulator snapshot) are serialized — no concurrent writes, no transaction conflicts.

If priority scheduling is needed in the future (e.g., expediting a breakpoint
read during a debug pause), the queue can be extended with priority levels.
For Phase 1, FIFO is sufficient.

### 15.7 UI Framework Isolation

The persistence layer is **frontend-agnostic**. Key rules:

1. **No UI dispatcher references** in `Pandowdy.Project`. The IO thread does not
   post results to `Dispatcher.UIThread`, `SynchronizationContext.Current`, or
   any framework-specific marshaling mechanism.

2. **Callers marshal their own results.** A ViewModel that `await`s a project
   query is responsible for dispatching the result to the UI thread if needed:

   ```csharp
   // In a ViewModel (Avalonia/ReactiveUI context)
   var metadata = await _projectManager.CurrentProject!.GetMetadataAsync();
   // If already on UI thread (e.g., command handler), no marshaling needed.
   // If on a background thread, use Dispatcher:
   await Dispatcher.UIThread.InvokeAsync(() => ProjectName = metadata.Name);
   ```

3. **`TaskCompletionSource` uses `RunContinuationsAsynchronously`.** This prevents
   the IO thread from accidentally running caller continuations on itself, which
   would block the queue. Continuations always run on the thread pool.

4. **No Avalonia, ReactiveUI, or WinUI references** in `Pandowdy.Project.csproj`.
   The project depends only on `Microsoft.Data.Sqlite` and `System.Reactive`.

### 15.8 Blob Access

Large blobs (disk images, ~230KB for a 35-whole-track disk with 137 quarter-track
positions) are read on the IO thread
using `SqliteDataReader.GetStream()` for streaming access:

```csharp
// Executed ON the IO thread via the request queue
internal InternalDiskImage ReadWorkingBlob(SqliteConnection connection, long diskImageId)
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = """
        SELECT COALESCE(working_blob, original_blob)
        FROM disk_images WHERE id = @id
        """;
    cmd.Parameters.AddWithValue("@id", diskImageId);

    using var reader = cmd.ExecuteReader();
    if (reader.Read())
    {
        using var blobStream = reader.GetStream(0);
        return DiskBlobSerializer.Deserialize(blobStream);
    }

    throw new InvalidOperationException($"Disk image {diskImageId} not found.");
}
```

Note: synchronous `ExecuteReader()` is used intentionally — this code runs on the
dedicated IO thread where blocking is expected and acceptable. The caller
(`CheckOutAsync`) sees an async `Task<InternalDiskImage>` via the queue façade.

### 15.9 Transaction Boundaries

All transactions execute on the IO thread. A request may span multiple SQL
statements within a single transaction:

- **Project save**: Single transaction wrapping all dirty disk writes + metadata updates.
- **Disk import**: Single transaction for INSERT (blob + metadata).
- **Disk eject**: Single UPDATE for `working_blob` + `working_dirty` via `ReturnAsync`.
- **Settings changes**: Individual UPSERTs (no transaction needed for single-row ops).
- **Breakpoint/watch changes**: Batched within project save transaction.
- **Bulk symbol import**: Single transaction for batch INSERT.

Because the IO thread processes requests sequentially, transactions are inherently
serialized. There is no risk of concurrent transactions or deadlocks.

### 15.10 WAL Mode

WAL (Write-Ahead Logging) journal mode **may** be enabled as a performance
optimization but is **not** central to the concurrency model. The single-thread
access pattern works correctly with any SQLite journal mode (DELETE, WAL,
TRUNCATE, etc.).

WAL benefits in this architecture:

- Faster writes for small transactions (settings changes, single-row UPSERTs).
- Reduced fsync overhead on project save.
- No benefit for concurrent reader/writer scenarios — there is only one thread.

WAL is set as a default but can be overridden per-project if needed:

```sql
PRAGMA journal_mode = WAL;  -- Default; performance optimization only
```

### 15.11 Shutdown & Drain

On project close, the queue is marked as complete (`BlockingCollection.CompleteAdding()`).
The IO thread processes all remaining enqueued requests, then closes the
`SqliteConnection` and exits. Any requests enqueued after `CompleteAdding()` are
rejected with an `ObjectDisposedException` set on their `TaskCompletionSource`.

```csharp
public async Task CloseAsync()
{
    _queue.CompleteAdding();
    await Task.Run(() => _thread.Join());  // Wait for IO thread to drain and exit
}
```

This ensures:
- All pending writes complete before the connection closes.
- No data loss on shutdown.
- Callers that `await` pending requests receive either results or exceptions.

### 15.12 Summary

```
┌─────────────────────────────────────────────────────────────────┐
│  Principle                │  Design Choice                      │
├───────────────────────────┼─────────────────────────────────────┤
│  SQLite thread ownership  │  Single dedicated IO thread         │
│  Cross-thread access      │  FIFO request queue + TCS<T>        │
│  Emulator access          │  Enqueue requests; never direct SQL │
│  UI framework coupling    │  None — callers marshal own results │
│  Ordering                 │  Deterministic FIFO                 │
│  Async model              │  Task-based façade over sync SQLite │
│  WAL mode                 │  Optional perf tuning, not required │
│  Shutdown                 │  Drain queue, complete all pending  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 16. DI Registration & Service Wiring

Pandowdy leans heavily on **constructor-based dependency injection** throughout the
codebase. Every service, view model, and subsystem receives its dependencies via
constructor parameters, registered in the composition root (`Program.cs`). The
`.skillet` project system follows this pattern strictly — no service locators, no
internal `new` instantiation, no chain-of-command parameter passing.

### DI Principles (Binding for All New Code)

These rules are established project-wide and apply to every class created for
`Pandowdy.Project`, its tests, and any UI integration:

1. **Constructor Injection for required dependencies.** All dependencies arrive
   via constructor parameters. Never resolve services from a container at runtime.
2. **Depend on interfaces, not concrete implementations.** `ISkilletProjectManager`,
   not `SkilletProjectManager`. `ISkilletProject`, not `SkilletProject`.
3. **Register services in the DI container** (`Program.cs`) with appropriate lifetimes.
4. **No Chain of Command.** Classes do not receive a dependency merely to pass it
   through to another class they construct. Each class requests only what it uses.
5. **No internal `new` instantiation** of dependencies. This makes classes hard to
   test and tightly coupled. (Plain data objects and records are fine.)
6. **No Service Locator pattern.** Hidden dependencies via `ServiceLocator.Get<T>()`
   or `IServiceProvider.GetService<T>()` are an anti-pattern in this codebase.

### New Registrations in `Program.cs`

```csharp
// Pandowdy.Project services
services.AddSingleton<ISkilletProjectManager, SkilletProjectManager>();
services.AddSingleton<ISettingsResolver, SettingsResolver>();

// ISkilletProject is transient-like — managed by ISkilletProjectManager
// Not registered directly; accessed via ISkilletProjectManager.CurrentProject
```

### Lifetime Guidelines

| Lifetime | When to Use | Examples |
|----------|------------|----------|
| **Singleton** | Stateless services, long-lived managers, global state providers | `ISkilletProjectManager`, `ISettingsResolver`, `ISystemStatusProvider` |
| **Scoped** | Per-request or per-window services | Avalonia windows (future) |
| **Transient** | Lightweight, stateless services created per use | `ICard` implementations |
| **Managed** | Lifecycle owned by another singleton (not registered directly) | `ISkilletProject` (owned by `ISkilletProjectManager`) |

### Dependency Graph (New)

```
Program.cs (composition root)
  ├── ISkilletProjectManager (singleton)
  │   └── Creates/manages ISkilletProject instances
  │       └── ISkilletProject also implements IDiskImageStore
  ├── IDiskImageStore (managed — same instance as ISkilletProject)
  │   └── Injected into DiskIIControllerCard via ICardFactory
  │       (interface defined in EmuCore; implemented by SkilletProject)
  ├── ISettingsResolver (singleton)
  │   ├── Reads: GuiSettings (JSON)
  │   └── Reads: ISkilletProjectManager.CurrentProject (skillet)
  ├── GuiSettingsService (existing, unchanged)
  ├── MainWindowViewModel
  │   ├── ISkilletProjectManager (new dependency)
  │   └── ISettingsResolver (new dependency)
  └── StartPageViewModel (new)
      └── ISkilletProjectManager
```

**`IDiskImageStore` wiring:** The interface is defined in `Pandowdy.EmuCore` so
that `DiskIIControllerCard` can depend on it without referencing `Pandowdy.Project`.
`SkilletProject` implements the interface. Because an ad hoc project always exists
on startup, the `IDiskImageStore` reference is **always non-null** — the controller
card can depend on it without nullable guards. When the active project changes
(open, close, Save As), the card factory receives the new project's store reference.

### Constructor Parameter Guidelines

For classes with many dependencies (>5 parameters), consider:
- Grouping related dependencies into facade interfaces.
- Using primary constructors for cleaner syntax (see §19).
- Reviewing whether the class has too many responsibilities (SRP violation).
- Documenting constructor parameters when dependency purpose isn't obvious.

All injected dependencies should be stored in `readonly` fields (or captured by
primary constructor assignment).

### Impact on Existing Services

| Service | Impact |
|---------|--------|
| `GuiSettingsService` | Retains JSON persistence. Removes `DriveState` section. |
| `DriveStateService` | **Removed.** Replaced by `mount_configuration` table. |
| `MainWindowViewModel` | Gains project lifecycle commands. Save/SaveAs → Export. |
| `DiskIIControllerCard` | Gains `IDiskImageStore` dependency. Calls `CheckOutAsync()` on mount, `ReturnAsync()` on eject. Handles new `MountDiskMessage(DriveNumber, DiskImageId)`. |
| `InsertDiskMessage` | **Removed in Phase 2a.** Filesystem-based disk loading is eliminated entirely. All disk loading flows through `IDiskImageStore.CheckOutAsync()` via `MountDiskMessage`. |

---

## 17. Testing Strategy

### Test Project: `Pandowdy.Project.Tests`

```
Pandowdy.Project.Tests/
├── Services/
│   ├── SkilletProjectTests.cs          — CRUD operations on all tables
│   ├── SkilletProjectManagerTests.cs   — Create/open/close lifecycle
│   ├── SkilletSchemaManagerTests.cs    — Schema creation, migration
│   ├── SettingsResolverTests.cs        — Four-layer resolution
│   ├── DiskBlobStoreTests.cs           — Serialize/deserialize round-trip
│   └── ProjectSettingsStoreTests.cs    — Key-value settings
├── Models/
│   └── ModelValidationTests.cs         — Record/class construction
└── Integration/
    └── DiskImportExportRoundTripTests.cs — Full import → mount → modify → save → reopen cycle
```

### Key Test Scenarios

1. **Schema creation**: New `.skillet` file has all V1 tables (6) and correct pragmas.
2. **Blob round-trip**: Serialize `InternalDiskImage` → blob → deserialize → binary-identical.
3. **Working copy regeneration**: Set `working_blob = NULL`, re-read → gets original.
4. **Persist policy**: `persist_working = false` → working_blob not updated on save.
5. **Settings resolution**: JSON default overridden by skillet, hardcoded fallback works.
6. **Mount configuration**: Slot/drive mapping persists and restores correctly.
7. **Schema migration**: Future schema version adds tables correctly via migration.
8. **Project validation**: Opening a non-skillet SQLite file fails gracefully.
9. **Schema migration**: Opening a v1 skillet with v2 code runs migration.
10. **Concurrent access**: WAL mode allows reads during write transaction.

### Testing Infrastructure

- Tests use temporary files (`Path.GetTempFileName()` with `.skillet` extension).
- No cleanup (per project testing guidelines — randomize paths, don't clean up).
- Use xUnit (matches existing test projects).
- Log file locations for debugging.

---

## 18. Implementation Phases

### Priority Rationale

The phases below are ordered to achieve a **working .skillet-based workflow as
quickly as possible**, prioritizing the transition from loose files to
project-based disk management. Features that depend on subsystems not yet
implemented (debugger breakpoints/watches, AppleSoft processing, advanced
workspace layout) are deferred to later phases; their schema tables will be
created by future migrations when those features are designed and implemented.

**Priority order:**
1. Core infrastructure (project + schema + blob store) — everything depends on this.
2. Disk lifecycle migration — the primary user-facing change; replaces filesystem save/load.
2a. Minimal UI — exercises Phase 2 infrastructure with project commands and mount picker.
3. Settings & project overrides — enables project-specific emulator/display configuration.
4. UI polish — Start Page & project dialogs — completes the user-facing project experience.
5. Legacy cleanup — removes dead code paths after new paths are proven.
6. Integration testing — end-to-end validation of the full workflow.
7. Deferred features — debugger storage, AppleSoft, workspace layout (when those subsystems exist).

---

### Phase 1: Foundation — Project & Schema Infrastructure

**Goal:** Stand up `Pandowdy.Project`, implement the SQLite schema, the blob
serializer, and the project lifecycle manager. At the end of this phase, code
can create, open, and close `.skillet` files with correct pragmas and V1 tables,
and round-trip `InternalDiskImage` through the PIDI blob format.

**Branch:** `skillet`

#### Steps

1. **Create `Pandowdy.Project` class library project.**
   - `dotnet new classlib -n Pandowdy.Project --framework net8.0` (Completed)
   - Add to solution: `dotnet sln add Pandowdy.Project/Pandowdy.Project.csproj` (Completed)
   - Configure `.csproj` per §2 (nullable, doc generation, `InternalsVisibleTo`). (Completed)
   - Add `PackageReference`: `Microsoft.Data.Sqlite` 9.0.7, `System.Reactive` 6.1.0. (Completed)
   - Add `ProjectReference` to `Pandowdy.EmuCore` (for `InternalDiskImage`, `DiskFormat`). (Completed)

2. **Create `Pandowdy.Project.Tests` xUnit test project.**
   - `dotnet new xunit -n Pandowdy.Project.Tests --framework net8.0` (Completed)
   - Add to solution: `dotnet sln add Pandowdy.Project.Tests/Pandowdy.Project.Tests.csproj` (Completed)
   - Match existing test project packages (xUnit 2.9.3, `Microsoft.NET.Test.Sdk` 18.0.1, 
     `xunit.runner.visualstudio` 3.1.5, `coverlet.collector` 6.0.4). (Completed)
   - Add `ProjectReference` to `Pandowdy.Project`. (Completed)

3. **Implement `SkilletConstants`.** (Completed)
   - `Pandowdy.Project/Constants/SkilletConstants.cs` (Completed)
   - `ApplicationId = 0x534B494C` ("SKIL") (Completed)
   - `SchemaVersion = 1` (Completed)
   - table name constants. (Completed)

4. **Implement `ISchemaMigration` interface and `V1_InitialSchema`.** (Completed)
   - `Pandowdy.Project/Migrations/ISchemaMigration.cs` — `FromVersion`, `ToVersion`, `Apply()`. (Completed)
   - `Pandowdy.Project/Migrations/V1_InitialSchema.cs` — DDL for the 6 V1 tables: (Completed)
     `project_metadata`, `disk_images`, `mount_configuration`, `emulator_overrides`,
     `display_overrides`, `project_settings`.
   - Deferred tables (breakpoints, watches, symbols, disassembly_cache, applesoft_sources,
     execution_history, workspace_layout, user_annotations) are **not** created in V1.
     They will be added by future migrations when their features are implemented.

5. **Implement `SkilletSchemaManager`.** (Completed)
   - `Pandowdy.Project/Services/SkilletSchemaManager.cs` (Completed)
   - `InitializeSchema(SqliteConnection)` — sets pragmas, runs V1 migration. (Completed)
   - `Migrate(SqliteConnection, int currentVersion)` — sequential migration runner. (Completed)
   - Seeds `project_metadata` rows (name, timestamps, schema version, Pandowdy version). (Completed)
   - Seeds default `mount_configuration` (slot 6, drives 1 & 2, empty). (Completed)

6. **Implement PIDI blob serializer (`DiskBlobStore`).** (Partial — Serialize blocked on Phase 2)
   - `Pandowdy.Project/Services/DiskBlobStore.cs` (Completed)
   - `Serialize(InternalDiskImage) → byte[]` — PIDI header (12 bytes, uncompressed) +
     presence bitmap (ceil(quarter_track_count/8) bytes) +
     Deflate-compressed per-quarter-track payload (non-null entries only) + CRC-32 footer.
     (Partial — structure implemented but throws `NotImplementedException`;
     `CircularBitBuffer.mBuffer` is private, blocking byte array extraction.
     Will be completed in Phase 2 when disk import is implemented.)
   - `Deserialize(byte[]) → InternalDiskImage` — validates magic and CRC-32,
     reads presence bitmap, decompresses Deflate payload, reconstructs quarter-tracks
     (null for positions with bit = 0 in bitmap). (Completed)
   - `Deserialize(Stream) → InternalDiskImage` — streaming variant for SQLite blob reads. (Completed)
   - Compression: `DeflateStream` with `CompressionLevel.Optimal` (`System.IO.Compression`,
     built into .NET runtime — no new package). (Completed)
   - Uses `System.IO.Hashing.Crc32` (already in `Pandowdy.EmuCore`'s dependency graph). (Completed)

7. **Implement `ProjectSettingsStore`.** (Completed)
   - `Pandowdy.Project/Services/ProjectSettingsStore.cs` (Completed)
   - Generic key-value CRUD against any `(key TEXT, value TEXT)` table. (Completed)
   - `Get(connection, tableName, key) → string?` (Completed)
   - `Set(connection, tableName, key, value)` — UPSERT via `INSERT OR REPLACE`. (Completed)
   - `GetAll(connection, tableName) → Dictionary<string, string>` (Completed)
   - `Remove(connection, tableName, key)` (Completed)

8. **Implement `ISkilletProject` / `SkilletProject`.** (Completed)
   - `Pandowdy.Project/Interfaces/ISkilletProject.cs` — interface per §5.5. (Completed)
   - `Pandowdy.Project/Services/SkilletProject.cs` — implementation. (Completed)
   - Phase 1 scope: metadata queries, settings CRUD, disk image CRUD (metadata rows +
     blob read/write), mount configuration CRUD. (Completed)
   - `ImportDiskImageAsync()` — full implementation completed in Phase 2 backfill.
   - Includes dedicated `ProjectIOThread` with FIFO `BlockingCollection` queue,
     `IORequest<T>` with `TaskCompletionSource`, and async façade per §15. (Completed)
   - Debugger, AppleSoft, and workspace layout methods are not included in the Phase 1
     interface. They will be added to `ISkilletProject` when their features and schema
     tables are implemented in deferred phases.

9. **Implement `ISkilletProjectManager` / `SkilletProjectManager`.** (Completed)
   - `Pandowdy.Project/Interfaces/ISkilletProjectManager.cs` — interface per §5.6. (Completed)
   - `Pandowdy.Project/Services/SkilletProjectManager.cs` — implementation. (Completed)
   - `CreateAsync(filePath, projectName)` — creates file, runs schema init, returns handle. (Completed)
   - `OpenAsync(filePath)` — validates application_id, runs migrations if needed, returns handle. (Completed)
   - `CloseAsync()` — disposes current project. (Completed)

10. **Add project references from host and UI projects.** (Completed)
    - `Pandowdy.csproj` → `<ProjectReference>` to `Pandowdy.Project`. (Completed)
    - `Pandowdy.UI.csproj` → `<ProjectReference>` to `Pandowdy.Project`. (Completed)

11. **Write unit tests for all Phase 1 deliverables.** (Partial — 2 of 5 test files not yet created)
    - `SkilletSchemaManagerTests.cs` — schema creation, pragma verification, table existence. (Completed — 8 tests)
    - `DiskBlobStoreTests.cs` — serialize/deserialize round-trip (including sparse quarter-track
      images with presence bitmap), CRC validation, corrupt data. (Not started — blocked on `Serialize()` completing in Phase 2)
    - `ProjectSettingsStoreTests.cs` — get/set/remove/upsert across key-value tables. (Completed — 7 tests)
    - `SkilletProjectTests.cs` — disk image CRUD (metadata), mount config CRUD. (Not started)
    - `SkilletProjectManagerTests.cs` — create/open/close lifecycle, invalid file rejection. (Completed — 8 tests)
    - Use in-memory SQLite (`Data Source=:memory:`) for unit tests. (Completed)
    - Use temp files for lifecycle tests (create → close → reopen → verify). (Completed)

#### Deliverables

| Artifact | State |
|----------|-------|
| `Pandowdy.Project/` class library | New project, compiles, in solution |
| `Pandowdy.Project.Tests/` test project | New project, compiles, all tests green |
| Schema creation | 6 V1 tables created, pragmas verified |
| PIDI blob round-trip | `InternalDiskImage` (with quarter-tracks) → blob → `InternalDiskImage`, binary-identical |
| Project lifecycle | Create → open → close → reopen cycle works |

#### Test Gate

All unit tests pass. Solution builds cleanly. No regressions in existing tests.

---

### Phase 2: Disk Lifecycle Migration

**Goal:** Replace the legacy filesystem-based disk open/save workflow with the
`.skillet`-based import/mount/export lifecycle. At the end of this phase, disk
images are ingested into the `.skillet` file, mounted from blobs, and exported
on demand — the emulator never reads from or writes to external disk files during
normal operation.

**Depends on:** Phase 1 (project infrastructure, blob store).

#### Phase 1 Backfill Items

The following items were deferred from Phase 1 because they are blocked on
work that Phase 2 enables. They must be completed as part of Phase 2 before
or alongside the Phase 2 steps below.

- **Complete `DiskBlobStore.Serialize()`** (Phase 1 Step 6 backfill)
  ✅ **COMPLETED** — Serialize() extracts byte arrays using only `CircularBitBuffer`'s
  public API (`ReadOctet()` method). No modifications to third-party libraries.
  Saves current position, reads all bytes, then restores position. Full serialization
  logic implemented with Deflate compression and CRC-32 validation.

- **Create `DiskBlobStoreTests.cs`** (Phase 1 Step 11 backfill)
  ✅ **COMPLETED** — Comprehensive test suite created with 8 tests covering:
  serialize/deserialize round-trip, sparse quarter-tracks, write protection,
  non-standard timing, corrupt magic bytes, corrupt CRC, 40-track disks.

- **Complete `ImportDiskImageAsync()` in `SkilletProject`** (Phase 1 Step 8 backfill)
  ✅ **COMPLETED** — Full implementation at line 37-92 of SkilletProject.cs.
  Uses DiskFormatHelper for format detection, DiskImageFactory for importer
  selection, DiskBlobStore for serialization, and inserts both original_blob
  and working_blob into disk_images table.

- **Create `SkilletProjectTests.cs`** (Phase 1 Step 11 backfill — not blocked on Phase 2)
  ✅ **COMPLETED** — Renamed to SkilletProjectDiskTests.cs. 12 tests covering:
  ImportDiskImageAsync (with skip for files), CheckOutAsync/ReturnAsync,
  GetDiskImageAsync/GetAllDiskImagesAsync, RegenerateWorkingCopyAsync.

#### Steps

1. **Define `IDiskImageStore` interface in EmuCore.**
   ✅ **COMPLETED** — Created at `Pandowdy.EmuCore/DiskII/IDiskImageStore.cs`.
   Interface defines `CheckOutAsync(long)` and `ReturnAsync(long, InternalDiskImage)`.
   Comprehensive XML documentation added per §1 Disk Image Ownership Model.

2. **Implement `IDiskImageStore` in `SkilletProject`.**
   ✅ **COMPLETED** — `ISkilletProject` extends `IDiskImageStore` (line 24 of ISkilletProject.cs).
   `SkilletProject` implements both methods at lines 280-295. `CheckOutAsync()` delegates
   to `LoadDiskImageAsync()`. `ReturnAsync()` delegates to `SaveDiskImageAsync()` which
   serializes and sets `working_dirty = 1`.

3. **Implement disk import flow in `SkilletProject`.**
   ✅ **COMPLETED** — See backfill item above. `ImportDiskImageAsync()` fully implemented.
   Also created `DiskImageFactory` service (Pandowdy.Project/Services/DiskImageFactory.cs)
   with `GetImporter()` and `GetExporter()` methods.

4. **Implement disk mount flow (checkout model).**
   🟡 **PARTIAL** — `MountDiskMessage` created at `Pandowdy.EmuCore/DiskII/Messages/MountDiskMessage.cs`.
   Handler implementation in `DiskIIControllerCard` deferred to Phase 2a (requires
   `IDiskImageStore` injection and DI wiring).

5. **Implement eject auto-flush (return model).**
   🟡 **PARTIAL** — Architecture designed. `ReturnAsync()` implementation exists in
   `SkilletProject`. `EjectDiskMessage` handler update in `DiskIIControllerCard`
   deferred to Phase 2a (requires `IDiskImageStore` injection).

6. **Implement disk export flow.**
   🟡 **PARTIAL** — `ExportDiskMessage` created at `Pandowdy.EmuCore/DiskII/Messages/ExportDiskMessage.cs`.
   Handler implementation in `DiskIIControllerCard` deferred to Phase 2a.
   `DiskImageFactory.GetExporter()` exists for format selection.

7. **Implement working copy persistence in `SkilletProject.SaveAsync()`.**
   ✅ **COMPLETED** — `SaveAsync()` snapshot-serializes all checked-out dirty disks
   using `DiskBlobStore.SerializeSnapshot()` (lock-based snapshot, then Deflate outside
   the lock). Clears `working_dirty` flags for disks with `persist_working = 1`.
   `SkilletProject` tracks checked-out images via `ConcurrentDictionary` — populated
   by `CheckOutAsync()`, cleared by `ReturnAsync()`.

8. **Implement working copy regeneration.**
   ✅ **COMPLETED** — `RegenerateWorkingCopyAsync()` implemented in Phase 1 at
   lines 107-121 of SkilletProject.cs. Sets `working_blob = NULL`, `working_dirty = 0`.

9. **Wire `IDiskImageStore` into DI.**
   ⏸️ **DEFERRED TO PHASE 2a** — DI wiring deferred until UI integration is complete.
   The interface and implementation are ready. Wiring will happen in Phase 2a
   when DiskIIControllerCard receives IDiskImageStore via constructor.

10. **Write tests for disk lifecycle.**
    ✅ **COMPLETED** — Two comprehensive test suites created:
    - `DiskBlobStoreTests.cs` — 8 tests for PIDI serialization
    - `SkilletProjectDiskTests.cs` — 12 tests for import, CheckOut/Return, CRUD
    Tests for mount/eject/export handlers in DiskIIControllerCard will be added
    in Phase 2a when card integration is complete.

#### Deliverables

| Artifact | State |
|----------|-------|
| `IDiskImageStore` | ✅ Interface defined in EmuCore — CheckOutAsync/ReturnAsync |
| `MountDiskMessage` | ✅ Message type defined — carries `DiskImageId`, not `InternalDiskImage` |
| `ExportDiskMessage` | ✅ Message type defined |
| `EjectAllDisksMessage` | ✅ Message type defined |
| Disk import into `.skillet` | ✅ External file → blob, metadata recorded (library management) |
| `CheckOutAsync` implementation | ✅ `SkilletProject` reads blob, deserializes to `InternalDiskImage` |
| `ReturnAsync` implementation | ✅ `SkilletProject` serializes `InternalDiskImage`, updates `working_blob` |
| Working copy dirty flag | ✅ `SaveAsync()` clears flags for `persist_working = 1` disks |
| Working copy regeneration | ✅ `RegenerateWorkingCopyAsync()` sets `working_blob = NULL` |
| DiskImageFactory | ✅ GetImporter/GetExporter factory for format selection |
| DiskBlobStore | ✅ Full PIDI serialize/deserialize with compression and CRC |
| CircularBitBuffer.GetBuffer() | ✅ Public accessor added for blob serialization |
| Test suite | ✅ DiskBlobStoreTests (8 tests), SkilletProjectDiskTests (12 tests) |
| **Integration work (Phase 2a)** | |
| Controller message handlers | ⏸️ Deferred — mount/eject/export/eject-all handlers in Phase 2a |
| DI wiring | ⏸️ Deferred — `IDiskImageStore` injection in Phase 2a |
| Mounted disk serialization | ✅ Complete — snapshot-under-lock via `SerializeSnapshot()` |

#### Test Gate

✅ **Phase 2 Infrastructure Complete:**
- DiskBlobStore round-trip is binary-identical for standard, sparse, and 40-track disks.
- SkilletProject CheckOut/Return workflow verified with in-memory projects.
- Import (with format detection), CRUD, and regeneration tested.
- All messages defined, all interfaces implemented.
- 2303 tests passing, zero regressions.

**Phase 2a Integration:** Controller handlers (mount/eject/export), DI wiring, and UI commands will complete the end-to-end disk lifecycle. These are not blocked—Phase 2 infrastructure is ready for integration.

---

### Phase 2a: Minimal UI for Project & Disk Workflow

**Goal:** Provide the minimum UI necessary to exercise the Phase 2 infrastructure:
create/open/save/close projects, import disk images into the library, mount from
the library into drives, and verify eject auto-flush. Without this phase, the
Phase 2 work has no user-facing entry point.

**Depends on:** Phase 2 (disk lifecycle, IDiskImageStore, mount/eject).

**Rationale:** Originally, all UI work was planned for Phase 4. However, Phases 2
and 3 produce infrastructure that cannot be tested end-to-end without at least
minimal UI wiring. Phase 2a pulls forward the essential UI items needed to
validate the disk lifecycle; Phase 4 retains the Start Page, new project dialog,
and other polish work.

**All Phase 2 infrastructure is complete.** Phase 2a integration work (controller handlers, DI wiring, UI commands) can proceed.

#### ⚠️ Breaking Change: Filesystem-Based Disk Loading Removed

Phase 2a is the point of a **fundamental paradigm shift** in how Pandowdy loads
disk images. The legacy workflow — where the emulator core directly opens and
reads disk image files from the host filesystem via `InsertDiskMessage` — is
**removed entirely** in this phase. There is no fallback, no compatibility
shim, and no "loose-disk mode" retained for continuity.

**Before Phase 2a:**
```
User → file dialog → filesystem path → InsertDiskMessage → EmuCore loads file directly
```

**After Phase 2a:**
```
User → Import → .skillet library → Mount from Library → MountDiskMessage
  → EmuCore calls IDiskImageStore.CheckOutAsync() → receives InternalDiskImage
```

All disk images must be imported into a `.skillet` project before they can be
used. The emulator core no longer has any code path that opens disk image files
from the filesystem. `InsertDiskMessage` and its handler are removed in this
phase (not deferred to Phase 5). This is a clean break — the old system is
replaced, not wrapped or deprecated.

**Rationale:** Maintaining two parallel loading paths (filesystem and library)
adds complexity without benefit. The `.skillet` project is the single source of
truth for all disk image data. Removing the filesystem path in Phase 2a ensures
that all subsequent phases build exclusively on the new architecture.

#### Phase 2 Integration Work (Deferred to Phase 2a)

The following items were deferred from Phase 2 because they are **controller and
UI integration work** — the wiring layer between Phase 2 infrastructure and the
user-facing application. They are not prerequisites; they **are** Phase 2a work
and will be implemented alongside the UI commands and dialogs below.

1. **Implement `MountDiskMessage` handler in `DiskIIControllerCard`** (Phase 2 Step 4)
   - `HandleMessage(MountDiskMessage)` calls `IDiskImageStore.CheckOutAsync(diskImageId)`
   - Assigns returned `InternalDiskImage` to the specified drive
   - **Status:** Message type exists; handler implementation is Phase 2a work

2. **Update `EjectDiskMessage` handler in `DiskIIControllerCard`** (Phase 2 Step 5)
   - Existing handler extended to call `IDiskImageStore.ReturnAsync(diskImageId, image)`
   - Serializes `InternalDiskImage` to `working_blob` before clearing drive (eject auto-flush)
   - **Status:** Return model exists; handler integration is Phase 2a work

3. **Implement `ExportDiskMessage` handler in `DiskIIControllerCard`** (Phase 2 Step 6)
   - `HandleMessage(ExportDiskMessage)` retrieves `InternalDiskImage` from drive
   - Uses `DiskImageFactory.GetExporter()` to export to chosen format
   - **Status:** Message type exists; handler implementation is Phase 2a work

4. **Implement `EjectAllDisksMessage` handler in `DiskIIControllerCard`** (new)
   - Ejects all mounted drives via normal eject flow (triggers `ReturnAsync` for each)
   - Used during project close to return all images to store before connection teardown
   - **Status:** Message type exists; handler implementation is Phase 2a work

5. **Wire `IDiskImageStore` into DI** (Phase 2 Step 9)
   - Add `IDiskImageStore` dependency to `DiskIIControllerCard` constructor (non-nullable — ad hoc project always exists)
   - Update `ICardFactory` to accept `IDiskImageStore` parameter
   - Register in `Program.cs`: pass `ISkilletProjectManager.CurrentProject` to card factory
   - **Status:** Interface ready; DI wiring is Phase 2a work

6. **Implement snapshot-serialize for mounted disks in `SaveAsync()`** (Phase 2 Step 7)
   ✅ **COMPLETED** — `SaveAsync()` uses `DiskBlobStore.SerializeSnapshot()` to
   snapshot-serialize checked-out dirty disks under `InternalDiskImage.SerializationLock`.
   Lock held briefly (~1ms) for memcpy, Deflate compression runs outside the lock.
   Emulator continues running — only the write path is blocked during snapshot.
   `SkilletProject` tracks checked-out images via `ConcurrentDictionary`.
   `UnifiedDiskImageProvider.WriteBit()` acquires the same lock to prevent mid-write
   corruption during the snapshot window.
   - **Status:** ✅ Complete

#### Completed Steps

1. **Implement controller handlers for disk lifecycle messages** (integration work items 1–4).
   ✅ **COMPLETED** — All 4 handlers implemented in `DiskIIControllerCard.cs`:
   - `MountDiskFromStore()` (lines 1778-1814): CheckOut → assign to drive, tracks `_mountedDiskImageIds`.
   - `EjectDisk()` update (lines 1825-1853): Return → clear drive (eject auto-flush via `ReturnAsync`).
   - `ExportDisk()` (lines 1864-1900): retrieve image → export via factory.
   - `EjectAllDisks()` (lines 1909-1922): ejects all drives, used during project close.

2. **Wire `IDiskImageStore` into DI and card factory** (integration work item 5).
   ✅ **COMPLETED** — DI infrastructure in place:
   - `DiskIIControllerCard` constructor receives `IDiskImageStore diskImageStore` parameter (line 172, non-nullable).
   - `CreateWithStore(IDiskImageStore)` abstract method (line 1934) for card factory integration.
   - `_mountedDiskImageIds` dictionary (line 180) tracks disk-to-drive mapping for Return operations.

3. **Create "Mount from Library" picker dialog.**
   ✅ **COMPLETED** — Full implementation exists:
   - `MountFromLibraryDialog.axaml` and `.axaml.cs` (Pandowdy.UI/Controls/)
   - `MountFromLibraryDialogViewModel.cs` (Pandowdy.UI/ViewModels/) — loads disk images via `GetAllDiskImagesAsync()`.
   - `SelectDiskCommand` and `CancelCommand` implemented.
   - `MountFromLibraryDialogViewModelTests.cs` (Pandowdy.UI.Tests/ViewModels/) — tests exist.

4. **Remove `InsertDiskMessage` and filesystem loading path.**
   ✅ **COMPLETED** — Filesystem loading eliminated:
   - `InsertDiskMessage.cs` deleted — `file_search` returns no results.
   - `DiskIIControllerCard.HandleMessage()` (lines 1314-1383) has no `InsertDiskMessage` case.
   - All disk loading flows through `MountDiskMessage` → `MountDiskFromStore()` → `CheckOutAsync()`.
   - **Verified:** Clean break from filesystem-based loading paradigm.

5. **Show project name in title bar.**
   ✅ **COMPLETED** — `MainWindowViewModel.WindowTitle` property added with:
   - Format: "Pandowdy — {ProjectName}" for file-based projects
   - Format: "Pandowdy — untitled" for ad hoc projects (null project)
   - `UpdateWindowTitle()` helper method updates title when project changes
   - Called in constructor (when project initially set) and `CloseProjectInternalAsync()`
   - Bound to `MainWindow.axaml` Title property
   - Tests added to `MainWindowViewModelTests.cs`:
     - `WindowTitle_DefaultValue_IsUntitled` — verifies default "untitled" state
     - `WindowTitle_WithProject_ShowsProjectName` — verifies project name display
     - `WindowTitle_PropertyChangedRaised_WhenProjectChanges` — documents expected behavior

#### Phase 2a Immediate Backlog

The following items are straightforward UI/DI wiring work with no blocking dependencies.
They should be completed before moving to Phase 3.

**Backlog Item 1: Complete DI registration in Program.cs**  
✅ **COMPLETED** — All DI registration exists in `Program.cs` lines 86-102:
- Line 86: `ISkilletProjectManager` singleton registered
- Lines 88-94: `IDiskImageStore` singleton resolves from `projectManager.CurrentProject`
- Lines 96-102: `ICardFactory` singleton receives `IDiskImageStore` parameter
- CardFactory.cs line 40: Constructor accepts `IDiskImageStore diskImageStore`
- CardFactory.cs lines 105-112: `CreateCardInstance` injects store via `CreateWithStore`
- DiskIIControllerCard16Sector.cs lines 81-84: `CreateWithStore` implementation complete

**Backlog Item 2: Add File menu commands**  
✅ **COMPLETED** — All File menu commands implemented and validated:

**Dialog Service:**
- `IProjectFileDialogService` interface created (30 lines) with `ShowOpenProjectDialogAsync()` and `ShowSaveProjectDialogAsync()`
- `ProjectFileDialogService` implementation (95 lines) using Avalonia `StorageProvider` API with .skillet file filters
- Registered in `Program.cs` line 194 as singleton

**Command Implementations (MainWindowViewModel):**
- `NewProjectAsync()` — file dialog → `CreateAsync()` → `RefreshProjectStateProperties()`
- `OpenProjectAsync()` — file dialog → `OpenAsync()` → `RefreshProjectStateProperties()`
- `SaveProjectAsync()` — calls `SaveAsync()` (disabled when ad hoc or pristine via command guard)
- `SaveProjectAsAsync()` — file dialog → `SaveAsAsync()` → `RefreshProjectStateProperties()` (disabled when pristine)
- `CloseProjectAsync()` — handles ad hoc vs file-based with appropriate prompts
- `ExportDiskImageAsync()` — command handler exists, disabled when no disk images in library
- `ImportDiskImageAsync()` — calls `RefreshProjectStateProperties()` after import, enabling Save/Export

**Interface Additions (per blueprint Appendix E):**
- `ISkilletProject.IsAdHoc` property — convenience property for distinguishing in-memory from file-based projects
- `ISkilletProjectManager.SaveAsAsync(string filePath)` method — for ad hoc → file transition or copying file-based projects

**Implementations:**
- `SkilletProject.IsAdHoc` (line 32) — `public bool IsAdHoc => string.IsNullOrEmpty(_filePath) || _filePath == ":memory:";`
- `SkilletProjectManager.SaveAsAsync()` (lines 106-193, ~85 lines):
  - Ad hoc path: `VACUUM INTO` to persist in-memory DB to file, close old connection, open file-based
  - File-based path: `SaveAsync()`, `File.Copy()`, close old, open new
  - Handles connection swap on IO thread, updates `FilePath` and `IsAdHoc`

**Menu XAML (MainWindow.axaml lines 25-37):**
- New Project (Ctrl+Shift+N)
- Open Project (Ctrl+Shift+O)
- Save Project (Ctrl+Alt+S) — disabled when ad hoc or pristine
- Save Project As (Ctrl+Shift+S) — disabled when pristine
- Import Disk Image (Ctrl+Shift+I) — existing
- Export Disk Image (Ctrl+Shift+E) — disabled when no disk images in library
- Close Project (no shortcut) — disabled when pristine ad hoc
- Keyboard shortcut conflict resolved: Scan Lines changed to Ctrl+Alt+L

**Test Updates:**
- `MainWindowViewModelTests.cs` — 5 constructor calls updated with `MockProjectFileDialogService`
- `MainWindowViewModelImportTests.cs` — 1 constructor call updated

**Adaptations:**
- All prompts use `ShowConfirmationAsync(bool)` (two-choice Yes/No dialogs) — `ShowYesNoCancelAsync` doesn't exist
- `RefreshDriveLibraryStateAsync()` calls commented out (method doesn't exist yet)

**Build Validation:** ✅ SUCCESS — All 2303+ tests passing, zero compilation errors, zero regressions

**Backlog Item 3: Wire DiskStatusWidget commands**
✅ **COMPLETED** — All disk widget commands updated to align with project-based workflow.

**Changes Made:**

**DiskStatusWidgetViewModel.cs (Pandowdy.UI/ViewModels/):**
- **InsertDiskCommand** (lines 85-87): Already using `ShowMountFromLibraryDialogAsync()` — no changes needed. Verified working.
- **SaveCommand and SaveAsCommand**: Removed entirely — replaced with single `ExportDiskCommand`.
- **ExportDiskCommand** (new, line 109): Uses `ExportDiskMessage` with format detection via `DiskFormatHelper.GetFormatFromPath()`.
- **ExportDiskAsync()** method (lines 176-195): Replaces `SaveAsAsync()`. Shows file dialog, detects format from extension, sends `ExportDiskMessage` to controller.
- Added `using Pandowdy.EmuCore.DiskII;` for `DiskFormatHelper` access (line 11).
- Removed `canSaveObservable` (obsolete — checked `HasDestinationPath` and `IsDirty`, both irrelevant in project workflow).

**DiskStatusWidget.axaml (Pandowdy.UI/Controls/):**
- Context menu (lines 23-35): Replaced "Save" and "Save As..." menu items with single "Export Disk..." item.
- Command binding: `{Binding ExportDiskCommand}` (line 30).

**DiskStatusWidgetViewModelTests.cs (Pandowdy.UI.Tests/ViewModels/):**
- `Constructor_InitializesCommands` test (lines 188-203): Updated assertion — checks for `ExportDiskCommand` instead of `SaveCommand` and `SaveAsCommand`.

**Paradigm Shift:**
In the project-based workflow:
- Disks are automatically persisted to `.skillet` on eject (via `ReturnAsync()` — eject auto-flush).
- Project save persists all working copies of mounted dirty disks.
- "Export" is an explicit user action to write a disk image to an external file for sharing/backup.
- No individual per-disk "Save" — that concept no longer exists.

**Build Validation:** ✅ SUCCESS — All 2323 tests passing (2322 succeeded + 1 skipped), zero compilation errors, zero regressions. NuGet packages restored successfully for Test Explorer discovery.

**Backlog Item 4: Write tests for Phase 2a work**
- **What:** Create test files for controller handlers (mount/eject/export/eject-all) and UI commands (project lifecycle, disk widget).
- **Where:** `Pandowdy.EmuCore.Tests/Cards/`, `Pandowdy.UI.Tests/ViewModels/`
- **Acceptance:** 3 new test files with comprehensive coverage of Steps 1-3.
- **Effort:** 4-6 hours — 3 test files mirroring production structure.
- **Status:** Not started (Step 9); `MountFromLibraryDialogViewModelTests.cs` already exists.

**Execution order:** Items 2-3 in sequence (no blocking dependencies between them), then Item 4 (tests) can run in parallel with Phase 3 work.

#### Deliverables

| Artifact | Phase 2a Status | Backlog Item |
|----------|----------------|---------------|
| Controller handlers | ✅ Complete | — |
| DI wiring (infrastructure) | ✅ Complete | — |
| DI registration (Program.cs) | ✅ Complete | — |
| Mount from Library picker | ✅ Complete | — |
| `InsertDiskMessage` removed | ✅ Complete | — |
| Title bar | ✅ Complete | — |
| File menu commands | ✅ **Complete** | **Backlog Item 2** |
| Disk widget commands | ✅ **Complete** | **Backlog Item 3** |
| Test coverage | ⏸️ **Backlog Item 4** | 4-6 hours |
| Mounted disk serialization | ✅ **Complete** | **Phase 2b** |

#### Test Gate (Phase 2a Complete)

**Phase 2a is functionally complete** — all core infrastructure exists and the breaking change (filesystem loading removal) is implemented. The immediate backlog items (File menu, disk widget, tests) are straightforward wiring work with no architectural decisions remaining.

**Phase 2a Backlog Gate:** Before starting Phase 3, complete Backlog Items 2-3 (File menu commands, disk widget commands). Backlog Item 4 (tests) can run in parallel with Phase 3.

**Phase 2a end-to-end validation (after backlog complete):**
- Create project → import disk → mount via picker → emulator uses disk → write to disk → eject (auto-flush) → verify blob updated → save project → close → reopen → remount → verify changes persisted.
- Export workflow: mount → modify → export via menu → verify exported file matches in-memory state.
- EjectAll: mount multiple disks → close project → verify all returned to store.

**Existing tests:** All 2303+ tests still pass, no regressions.

---

### Phase 2b: Lock-Based Mounted Disk Serialization

**Goal:** Serialize mounted disk images during project save without ejecting them,
using a snapshot-under-lock strategy that allows the emulator to continue running.

**Depends on:** Phase 2a backlog complete.

#### Design

The emulator thread writes to `InternalDiskImage` at cycle speed (~250,000 bits/sec).
Serializing a disk image while the emulator writes to it would produce corrupted
blobs. Rather than pausing the entire emulator, the design uses a fine-grained
lock on each `InternalDiskImage` that protects only the write path during the
brief snapshot window.

**Lock protocol:**

```
InternalDiskImage.SerializationLock (object)
  ├── Acquired by: UnifiedDiskImageProvider.WriteBit()  — emulator write path
  └── Acquired by: DiskBlobStore.SerializeSnapshot()    — save path (snapshot only)
```

**Snapshot-serialize flow (in `SaveAsync()`):**

```
For each checked-out dirty disk image:
  1. Acquire InternalDiskImage.SerializationLock
  2. Copy raw quarter-track byte arrays (~1ms memcpy, ~230KB for 35-track disk)
  3. Release lock
  4. Compress snapshot via Deflate (~50ms, no lock held)
  5. Enqueue compressed blob write to IO thread
```

**Checked-out image tracking:**

`SkilletProject` maintains a `ConcurrentDictionary<long, InternalDiskImage>`
that tracks which images are currently lent to the emulator:
- `CheckOutAsync()` adds the image after deserialization.
- `ReturnAsync()` removes the image before serialization.
- `SaveAsync()` iterates the dictionary to find dirty images.

This eliminates the need for callers to pass mounted images as a parameter —
the project knows what it has lent out.

#### Completed Steps

1. **Add `SerializationLock` to `InternalDiskImage`.**
   ✅ **COMPLETED** — `public object SerializationLock { get; } = new object();`
   Acquired by both the emulator write path and the serializer.

2. **Wrap `WriteBit()` in `UnifiedDiskImageProvider` with lock.**
   ✅ **COMPLETED** — `lock (_diskImage.SerializationLock)` around the write
   operation including new-track creation. Read path (`ReadBit`) is not locked.

3. **Add `DiskBlobStore.SerializeSnapshot()`.**
   ✅ **COMPLETED** — Acquires lock briefly to copy raw byte arrays via
   `ReadOctet()`, releases lock, then compresses the snapshot with Deflate.
   Produces identical PIDI blobs to `Serialize()` but is safe for concurrent use.

4. **Add checked-out image tracking to `SkilletProject`.**
   ✅ **COMPLETED** — `ConcurrentDictionary<long, InternalDiskImage> _checkedOutImages`.
   `CheckOutAsync()` populates, `ReturnAsync()` removes.

5. **Update `SkilletProject.SaveAsync()` to use snapshot serialization.**
   ✅ **COMPLETED** — Iterates `_checkedOutImages`, calls `SerializeSnapshot()`
   for each dirty image, enqueues blob writes + dirty flag clears in a single
   transaction. Disks remain mounted throughout — no eject/remount needed.

6. **Write tests for snapshot-serialize flow.**
   ⏸️ **Pending** — Will be added as part of Phase 2a Backlog Item 4 (test coverage).

#### Deliverables

| Artifact | State |
|----------|-------|
| `InternalDiskImage.SerializationLock` | ✅ Complete |
| `UnifiedDiskImageProvider.WriteBit()` lock | ✅ Complete |
| `DiskBlobStore.SerializeSnapshot()` | ✅ Complete |
| Checked-out image tracking | ✅ Complete |
| `SaveAsync()` snapshot integration | ✅ Complete |
| Tests | ⏸️ Pending (Phase 2a Backlog Item 4) |

#### Test Gate

All 2,323 existing tests pass. No regressions. Snapshot-serialize specific tests
will be added in Phase 2a Backlog Item 4.

---

### Phase 3: Settings Resolution & Project Overrides

**Goal:** Implement the four-layer settings resolution model so that emulator
and display settings can be overridden per-project. Add `RecentProjects` and
`ActiveProjectPath` to `GuiSettings`.

**Depends on:** Phase 1 (project settings store).

#### Steps

1. **Implement `ISettingsResolver` / `SettingsResolver`.**
   - `Pandowdy.Project/Interfaces/ISettingsResolver.cs` — interface per §8.
     (Scaffolded in Phase 1 — complete interface with `Resolve<T>()`, `GetPersistLayer()`,
     and `SettingsLayer` enum. No changes expected.)
   - `Pandowdy.Project/Services/SettingsResolver.cs` — implementation.
     (Scaffolded in Phase 1 as explicit stub — `Resolve<T>()` returns `hardcodedDefault`,
     `GetPersistLayer()` returns `SettingsLayer.Json`. Must be replaced with full
     four-layer resolution logic.)
   - Resolution order: hardcoded → JSON (`GuiSettings`) → `.skillet` (`emulator_overrides`
     / `display_overrides`) → runtime.

2. **Add `RecentProjects` and `ActiveProjectPath` to `GuiSettings`.**
   - Add `RecentProject` model class to `Pandowdy.UI/Models/`.
   - Add `List<RecentProject>? RecentProjects` and `string? ActiveProjectPath` to `GuiSettings`.
   - Update `GuiSettingsService` serialization to handle new properties.

3. **Wire `SettingsResolver` into DI.**
   - Register `ISettingsResolver` as singleton in `Program.cs`.
   - Inject into `MainWindowViewModel` (replaces direct `GuiSettings` reads for
     overridable settings).

4. **Write tests for resolution logic.**
   - `SettingsResolverTests.cs` — hardcoded fallback, JSON override, skillet override,
     correct precedence order.

#### Deliverables

| Artifact | State |
|----------|-------|
| `ISettingsResolver` / `SettingsResolver` | New service, DI-registered |
| `GuiSettings` additions | `RecentProjects`, `ActiveProjectPath` |
| Resolution tests | All 4 layers verified |

#### Test Gate

All resolution tests pass. Existing `GuiSettingsService` tests still pass.

---

### Phase 4: UI Polish — Start Page, Dialogs & Startup Flow

**Goal:** Add the Start Page as an optional project management panel, the new
project dialog, and recent projects list. These are the UI polish items that were
not essential for Phase 2a's minimal workflow validation but complete the
user-facing project experience.

**Depends on:** Phases 2a and 3 (minimal UI wiring, settings, recent projects).

**Note:** File menu commands, disk widget commands, and the "Mount from Library"
picker were pulled forward to Phase 2a. This phase adds the remaining UI pieces.

**Note:** The Start Page is **not** a gate — the emulator workspace is always
accessible because an ad hoc project exists on startup. The Start Page is an
optional panel for project management (creating, opening, or switching projects).

#### Steps

1. **Create `StartPageViewModel`.**
   - `Pandowdy.UI/ViewModels/StartPageViewModel.cs`
   - Exposes `RecentProjects` collection, `CreateProjectCommand`, `OpenProjectCommand`.
   - Receives `ISkilletProjectManager` and `GuiSettingsService` via DI.

2. **Create `StartPage.axaml`.**
   - `Pandowdy.UI/Views/StartPage.axaml` + code-behind.
   - Layout per §9.1: "Create New Project" panel + "Recent Projects" list.
   - Shown as an optional panel or welcome overlay, not as a blocking gate.

3. **Create `NewProjectDialogViewModel` and dialog.**
   - `Pandowdy.UI/ViewModels/NewProjectDialogViewModel.cs`
   - `Pandowdy.UI/Views/NewProjectDialog.axaml` + code-behind.
   - Fields: project name, folder location, file name.
   - Also used for "Save Project As..." when an ad hoc project is being persisted.

4. **Integrate Start Page into `MainWindow`.**
   - Add a `ContentControl` that can show the Start Page as an overlay or panel.
   - The workspace is always accessible regardless of Start Page visibility.
   - Start Page is shown on launch when running with an ad hoc project (no
     `active_project_path`) and can be dismissed.

5. **Add Recent Projects submenu to File menu.**
   - Populated from `GuiSettings.RecentProjects`.
   - Clicking an entry opens that project (closes current, including ad hoc).
   - "Clear Recent" option.

6. **Write tests for Start Page and project dialogs.**
   - `StartPageViewModelTests.cs` — recent projects list, create/open commands.
   - `NewProjectDialogViewModelTests.cs` — validation, path generation.

#### Deliverables

| Artifact | State |
|----------|-------|
| Start Page | New view + view model, optional project management panel |
| New Project Dialog | New dialog for creating `.skillet` files and Save As |
| Recent Projects submenu | Populated from GuiSettings |
| Startup flow | Ad hoc project on startup; auto-open if `ActiveProjectPath` exists |

#### Test Gate

Start Page renders. Project creation flow works end-to-end via dialog. Recent
projects list populates correctly. Startup auto-open works. Ad hoc-to-file
transition via Save As works. Existing UI tests still pass.

---

### Phase 5: Legacy Cleanup

**Goal:** Remove dead code paths that are fully superseded by the `.skillet`
workflow. This phase runs after Phases 2a–4 are proven working.

**Depends on:** Phases 2a and 4 (disk lifecycle UI and project dialogs are fully wired).

#### Steps

1. **Remove `SaveDiskMessage`.**
   - Delete `Pandowdy.EmuCore/DiskII/Messages/SaveDiskMessage.cs`.
   - Remove handler in `DiskIIControllerCard` (both variants).
   - Remove all references.

2. **Remove `SaveDiskAsMessage`.**
   - Delete `Pandowdy.EmuCore/DiskII/Messages/SaveDiskAsMessage.cs`.
   - Remove handler in `DiskIIControllerCard` (both variants).
   - Remove all references (replaced by `ExportDiskMessage`).

3. **Remove `DestinationFilePath` and `DestinationFormat` from `InternalDiskImage`.**
   - Remove properties (lines 100–121 of current `InternalDiskImage.cs`).
   - Fix all compilation errors from removed properties.

4. **Remove `DriveStateService` and related types.**
   - Delete `Pandowdy.UI/Services/DriveStateService.cs`.
   - Delete `Pandowdy.UI/Interfaces/IDriveStateService.cs`.
   - Delete `Pandowdy.UI/Models/DriveStateConfig.cs`.
   - Delete `Pandowdy.UI.Tests/Services/DriveStateServiceTests.cs`.
   - Remove `DriveStateSettings`, `DiskControllerEntry`, `DriveEntry` from `GuiSettings.cs`.
   - Remove `DriveState` property from `GuiSettings`.
   - Remove DI registration for `IDriveStateService` from `Program.cs`.

5. **~~Remove legacy `InsertDiskMessage`~~ — handled in Phase 2a.**
   - `InsertDiskMessage` and its filesystem loading handler were removed in Phase 2a
     Step 7 as part of the paradigm shift to library-based disk loading. No evaluation
     needed — the decision to remove (not deprecate) was made in Phase 2a.

6. **Clean up `MainWindowViewModel` exit flow.**
   - `HandleExitAsync()` currently checks `dirtyDisks` via `DiskStatusWidgetViewModel`.
   - Migrate to check `ISkilletProject.HasUnsavedChanges` instead.
   - Prompt: "Save project before exiting?" → calls `SaveAsync()`.

7. **Build and fix all compilation errors.**
   - `dotnet build` the full solution.
   - Fix all remaining references to removed types.

#### Deliverables

| Artifact | State |
|----------|-------|
| `SaveDiskMessage` | Deleted |
| `SaveDiskAsMessage` | Deleted |
| `InternalDiskImage.DestinationFilePath` | Removed |
| `InternalDiskImage.DestinationFormat` | Removed |
| `DriveStateService` + interfaces + models | Deleted |
| `DriveStateSettings` in `GuiSettings` | Removed |

#### Test Gate

Solution builds cleanly with zero references to removed types. All existing tests
pass (tests for deleted types are also deleted). No regressions.

---

### Phase 6: Integration Testing & Polish

**Goal:** End-to-end validation of the full `.skillet` workflow. Verify the
complete user journey from project creation through disk management to project
reopen.

**Depends on:** Phases 1–5 (all core functionality in place).

#### Steps

1. **Write full round-trip integration tests.**
   - Create project → import disk → mount → modify (write to disk in emulator) →
     save project → close → reopen → verify working copy has modifications →
     export to filesystem → verify exported file.

2. **Write project validation tests.**
   - Opening a non-SQLite file → graceful error.
   - Opening a SQLite file without correct `application_id` → graceful error.
   - Opening a corrupted `.skillet` → graceful error.
   - Opening a future-version `.skillet` (higher `user_version`) → informative error.

3. **Write multi-disk project tests.**
   - Import multiple disk images.
   - Mount to different slots/drives.
   - Verify mount configuration persists across sessions.
   - Swap disks between drives, verify state.

4. **Write settings override integration tests.**
   - Create project with throttle override → close → reopen → verify override applied.
   - Delete override → verify falls back to JSON default.

5. **Performance baseline for blob operations.**
   - Time serialize/deserialize for a 35-whole-track disk (137 quarter-track positions, ~230KB).
   - Time full project save with 2 dirty disks.
   - Log results; flag if >500ms for either operation.

6. **Update `Development-Roadmap.md`.**
   - Mark Task 32 phases complete with dates and notes.

#### Deliverables

| Artifact | State |
|----------|-------|
| Integration test suite | All scenarios pass |
| Error handling | Corrupt/invalid files handled gracefully |
| Performance baseline | Documented, within acceptable range |

#### Test Gate

All integration tests pass. No regressions in any existing test project.

---

### Deferred Phases

Deferred phases (Debugger Storage, AppleSoft Storage, Workspace Layout) are
documented in `docs/Skillet_Deferred_Features_Reference.md` §9.

---

### Phase Summary

| Phase | Priority | Depends On | Core Deliverable | Status |
|-------|----------|------------|------------------|--------|
| 1. Foundation | **P0** | — | Project + schema + blob store + lifecycle | ✅ Complete |
| 2. Disk Lifecycle | **P0** | Phase 1 | IDiskImageStore + import / mount / export / eject auto-flush | ✅ Complete |
| 2a. Minimal UI | **P0** | Phase 2 | Controller handlers + mount picker + title bar | ⏸️ Complete with Backlog (3 items) |
| 2b. Mounted Disk Serialization | **P0** | Phase 2a | Snapshot-under-lock for SaveAsync | ✅ Complete |
| 3. Settings | **P1** | Phase 1 | Four-layer resolution + recent projects | ⏸️ Pending |
| 4. UI Polish | **P1** | Phases 2a, 3 | Start Page + new project dialog + startup flow | ⏸️ Pending |
| 5. Legacy Cleanup | **P2** | Phases 2a, 4 | Remove dead code (SaveDisk*, DriveState*) | ⏸️ Pending |
| 6. Integration Testing | **P2** | Phases 1–5 | End-to-end validation | ⏸️ Pending |

**Phase 2a Immediate Backlog (complete before Phase 3):**
1. ~~Program.cs DI registration~~ ✅ Complete (lines 86-102 already exist)
2. File menu commands (2-3 hours)
3. DiskStatusWidget commands (1-2 hours)
4. Test coverage for Phase 2a (4-6 hours, can parallelize with Phase 3)

---

## 19. Coding Standards & DI Architecture

This section consolidates the project-wide coding standards that apply to all
`Pandowdy.Project` implementation work. These rules are drawn from
`.github/copilot-instructions.md` and `docs/Development-Roadmap.md` §Code Style
Guidelines, and are **binding for all code produced during Task 32**.

### 19.1 Braces — Always Required

**Every** control statement must use curly braces, even for single-line bodies.
This applies to: `if`, `else`, `for`, `foreach`, `while`, `do-while`, `using`, `lock`.

```csharp
// ✅ Correct
if (connection is null)
{
    throw new InvalidOperationException("No open project.");
}

foreach (var mount in mounts)
{
    await MountDiskAsync(mount);
}

// ❌ Incorrect — will trigger IDE0011 warning
if (connection is null)
    throw new InvalidOperationException("No open project.");

if (connection is null) throw new InvalidOperationException("No open project.");
```

### 19.2 Property Formatting

- **Multi-line** for properties with non-default accessors (logic, access modifiers, ReactiveUI).
- **Single-line** only for simple auto-properties with default `{ get; set; }`.

```csharp
// ✅ Multi-line: logic or access modifiers
public bool HasUnsavedChanges
{
    get => _hasUnsavedChanges;
    private set => _hasUnsavedChanges = value;
}

// ✅ Single-line: simple auto-properties
public long Id { get; init; }
public required string Name { get; set; }
public bool PersistWorking { get; set; } = true;
```

### 19.3 Primary Constructors (C# 12)

**Prefer primary constructors** when class initialization is straightforward —
i.e., no complex logic beyond field assignments. This is the standard pattern
for service classes and view models in Pandowdy, and is **especially important
for unit test classes** where this pattern is often forgotten.

```csharp
// ✅ Correct: Primary constructor for service with DI
public class SkilletProjectManager(
    GuiSettingsService settingsService) : ISkilletProjectManager
{
    private readonly GuiSettingsService _settingsService = settingsService;
    private ISkilletProject? _currentProject;

    public ISkilletProject? CurrentProject => _currentProject;
    // ...
}

// ✅ Correct: Primary constructor in test class
public class SkilletProjectManagerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task CreateAsync_CreatesValidSkilletFile()
    {
        // ...
    }
}

// ❌ Incorrect: Traditional constructor for simple DI — use primary constructor instead
public class SkilletProjectManager : ISkilletProjectManager
{
    private readonly GuiSettingsService _settingsService;

    public SkilletProjectManager(GuiSettingsService settingsService)
    {
        _settingsService = settingsService;
    }
}
```

**Exception:** When construction involves significant logic (opening connections,
validating state, subscribing to observables), a traditional constructor body is
appropriate.

### 19.4 Dependency Injection — The Core Architecture Pattern

Pandowdy leans heavily on DI. The `.skillet` project system follows this strictly.
See §16 for the full DI rules, which are summarized here:

| ✅ Do | ❌ Don't |
|-------|----------|
| Constructor injection for required dependencies | Service Locator (`ServiceLocator.Get<T>()`) |
| Depend on interfaces (`ISkilletProject`) | Depend on concrete types (`SkilletProject`) |
| Register in DI container with correct lifetime | Internal `new` of dependencies |
| Each class requests only what it directly uses | Chain of Command (passing deps through layers) |

```csharp
// ✅ Correct: Dependencies injected, interface-typed
public class StartPageViewModel(
    ISkilletProjectManager projectManager,
    GuiSettingsService settingsService) : ReactiveObject
{
    private readonly ISkilletProjectManager _projectManager = projectManager;
    private readonly GuiSettingsService _settingsService = settingsService;
    // ...
}

// ❌ Incorrect: Internal instantiation — untestable, tightly coupled
public class StartPageViewModel : ReactiveObject
{
    private readonly SkilletProjectManager _projectManager = new SkilletProjectManager(...);
}

// ❌ Incorrect: Chain of Command — passing a dep just to forward it
public class SkilletProjectManager(GuiSettingsService settings, ICardFactory cardFactory)
{
    public ISkilletProject CreateProject()
    {
        return new SkilletProject(settings, cardFactory); // cardFactory passed through!
    }
}
```

### 19.5 Naming Conventions

| Element | Convention | Example |
|---------|------------|--------|
| Public members | PascalCase | `HasUnsavedChanges`, `ImportDiskImageAsync` |
| Private fields | `_camelCase` (underscore prefix) | `_connection`, `_currentProject` |
| Local variables | camelCase | `var diskRecord = ...` |
| Constants | PascalCase | `SchemaVersion`, `ApplicationId` |
| Interfaces | `I` prefix + PascalCase | `ISkilletProject`, `ISettingsResolver` |
| Async methods | `Async` suffix | `SaveAsync()`, `OpenAsync()` |
| Test methods | `MethodName_Scenario_ExpectedOutcome` | `CreateAsync_ValidPath_ReturnsProject` |

### 19.6 Other Style Rules

- **`var`** for local variables when type is obvious from the right-hand side.
- **Expression-bodied members** for simple one-liners.
- **Nullable reference types** enabled (`string?`, `object?`) — all projects use `<Nullable>enable</Nullable>`.
- **4-space indentation** (no tabs).
- **Field default initializers** for small, read-only arrays and collections;
  use `new[] { ... }` for literal content and `Array.Empty<T>()` for empty arrays.
- **No comments** unless they match the style of existing comments in the file
  or are necessary to explain complex logic.

### 19.7 Testing Standards

- **Mirror production code structure** in test projects.
  `Pandowdy.Project/Services/SkilletProject.cs` → `Pandowdy.Project.Tests/Services/SkilletProjectTests.cs`
- **xUnit** as the test framework (matches all existing test projects).
- **`[Fact]`** for standard tests; **`[Theory]`** for parameterized tests.
- **`[Fact(Skip = "reason")]`** for tests blocked by technical limitations —
  keep them as design specs.
- **Group tests** with `#region` blocks by logical category.
- **Test naming:** `MethodName_Scenario_ExpectedOutcome`

```csharp
#region ImportDiskImageAsync

[Fact]
public async Task ImportDiskImageAsync_ValidWozFile_StoresOriginalBlob()
{
    // Arrange
    // Act
    // Assert
}

[Fact]
public async Task ImportDiskImageAsync_NonexistentFile_ThrowsFileNotFoundException()
{
    // Arrange
    // Act & Assert
}

#endregion
```

### 19.8 Git Operations

- **Always use `git mv`** when moving or renaming files — preserves history.
- **Never use create/delete cycles** for file moves.
- Current branch: `skillet` (dedicated feature branch for Task 32).

---

## Appendix A: `GuiSettings` Changes

### Added Properties

```csharp
public sealed class GuiSettings
{
    // ... existing properties ...

    /// <summary>
    /// Gets or sets the list of recently opened .skillet projects.
    /// </summary>
    public List<RecentProject>? RecentProjects { get; set; }

    /// <summary>
    /// Gets or sets the path to the currently active .skillet project.
    /// </summary>
    public string? ActiveProjectPath { get; set; }
}

public sealed class RecentProject
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public DateTime LastOpenedUtc { get; set; }
}
```

### Removed Properties

```csharp
// DriveStateSettings removed from GuiSettings — replaced by mount_configuration table
// DriveState property removed from GuiSettings
```

---

## Appendix B: Message Changes

### New Messages

```csharp
/// <summary>
/// Message requesting a disk image be mounted from the project's disk image store.
/// The controller calls IDiskImageStore.CheckOutAsync(DiskImageId) to obtain the
/// InternalDiskImage. This replaces InsertDiskMessage entirely — all disk loading
/// now flows through the .skillet library.
/// </summary>
public record MountDiskMessage(int DriveNumber, long DiskImageId) : ICardMessage;

/// <summary>
/// Message requesting a disk image be exported to the filesystem.
/// Replaces SaveDiskAsMessage.
/// </summary>
public record ExportDiskMessage(int DriveNumber, string FilePath, DiskFormat Format) : ICardMessage;

/// <summary>
/// Message requesting all drives eject their disks. Used during project close
/// to ensure all InternalDiskImages are returned to the store before the
/// SQLite connection is torn down.
/// </summary>
public record EjectAllDisksMessage() : ICardMessage;
```

### New Interface (in EmuCore)

```csharp
/// <summary>
/// Abstraction for a persistent store that can lend and accept disk images.
/// Defined in EmuCore so the controller card can depend on it without
/// referencing Pandowdy.Project. Implemented by SkilletProject.
/// </summary>
public interface IDiskImageStore
{
    Task<InternalDiskImage> CheckOutAsync(long diskImageId);
    Task ReturnAsync(long diskImageId, InternalDiskImage image);
}
```

### Retained Messages (Unchanged)

- `SwapDrivesMessage` — still needed
- `SetWriteProtectMessage` — still needed
- `InsertBlankDiskMessage` — still needed (blank disk → immediately stored in project)

### Modified Messages

- `EjectDiskMessage` — still needed, but handler now calls `IDiskImageStore.ReturnAsync()`
  before clearing the drive (eject auto-flush, see §6.6).

### Removed Messages

- `SaveDiskMessage` — obsolete (internal persistence replaces filesystem save)
- `SaveDiskAsMessage` — replaced by `ExportDiskMessage`
- `InsertDiskMessage` — removed in Phase 2a; filesystem-based disk loading eliminated entirely, replaced by `MountDiskMessage` + `IDiskImageStore.CheckOutAsync()`

---

## Appendix C: Schema Version Strategy

### Version Tracking

- `PRAGMA user_version` stores the schema version as an integer.
- `SkilletSchemaManager.Migrate()` checks current version and applies sequential migrations.
- Each migration is a self-contained class implementing a common interface.

```csharp
internal interface ISchemaMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    void Apply(SqliteConnection connection);
}
```

### Migration Example (Future)

```csharp
internal class V2_AddProfilingTables : ISchemaMigration
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public void Apply(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE profiling_data (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id  TEXT NOT NULL,
                address     INTEGER NOT NULL,
                hit_count   INTEGER NOT NULL DEFAULT 0,
                total_cycles INTEGER NOT NULL DEFAULT 0
            );
            PRAGMA user_version = 2;
            """;
        cmd.ExecuteNonQuery();
    }
}
```

---

## Appendix D: Incorporating SQLite into Pandowdy

### D.1 Package Selection

SQLite access in .NET involves two distinct layers:

| Layer | Purpose | Package |
|-------|---------|---------|
| **ADO.NET Provider** | C# API — connections, commands, readers, parameters | `Microsoft.Data.Sqlite` |
| **Native Engine** | Platform-specific SQLite C library (`e_sqlite3.dll`, `libe_sqlite3.so`, `libe_sqlite3.dylib`) | `SQLitePCLRaw.bundle_e_sqlite3` |

`Microsoft.Data.Sqlite` declares a transitive dependency on `SQLitePCLRaw.bundle_e_sqlite3`,
which in turn depends on `SQLitePCLRaw.core` and the platform-specific `SQLitePCLRaw.lib.e_sqlite3`
packages. The entire chain is pulled in automatically by referencing the single
`Microsoft.Data.Sqlite` package.

### D.2 Package Dependency Graph

```
Microsoft.Data.Sqlite (9.0.x)
  └── SQLitePCLRaw.bundle_e_sqlite3 (2.1.x)
        ├── SQLitePCLRaw.core (2.1.x)
        └── SQLitePCLRaw.lib.e_sqlite3 (2.1.x)
              ├── runtimes/win-x64/native/e_sqlite3.dll
              ├── runtimes/win-arm64/native/e_sqlite3.dll
              ├── runtimes/linux-x64/native/libe_sqlite3.so
              ├── runtimes/linux-arm64/native/libe_sqlite3.so
              ├── runtimes/osx-x64/native/libe_sqlite3.dylib
              └── runtimes/osx-arm64/native/libe_sqlite3.dylib
```

There is no need to install SQLite separately on any platform.
The native library is bundled inside the NuGet package and copied to the output directory at build time.

### D.3 Pandowdy.Project Owns the Dependency

The `Microsoft.Data.Sqlite` package is referenced **only** in `Pandowdy.Project.csproj`.
No other project in the solution references it directly.

```
Pandowdy (host app)
  ├── Pandowdy.UI         (no SQLite reference)
  ├── Pandowdy.Project    ← Microsoft.Data.Sqlite here
  ├── Pandowdy.EmuCore    (no SQLite reference)
  └── Pandowdy.Cpu        (no SQLite reference)
```

The host app `Pandowdy` gains the native SQLite binary transitively through its
`<ProjectReference>` to `Pandowdy.Project`. The native `e_sqlite3` library is
automatically copied to the publish/output directory as part of the build.

### D.4 Version Alignment

The existing solution uses `Microsoft.Extensions.Hosting` 10.0.1 and
`Microsoft.Extensions.Logging` 10.0.1 in the host project. `Microsoft.Data.Sqlite`
is versioned independently from the `Microsoft.Extensions.*` packages — it follows
the Entity Framework Core release cadence, not the runtime/extensions cadence.
Version 9.0.x is the latest stable release compatible with .NET 8.

| Package | Version | Rationale |
|---------|---------|-----------|
| `Microsoft.Data.Sqlite` | 9.0.7 | Latest stable; targets `netstandard2.0` so compatible with net8.0 |
| `SQLitePCLRaw.bundle_e_sqlite3` | (transitive) | Pulled in automatically |

**No version conflicts** exist between `Microsoft.Data.Sqlite` 9.0.x and the
existing package set. The package has no dependency on `Microsoft.Extensions.*`,
`Avalonia`, or `System.Reactive`.

### D.5 What the Package Adds to the Output Directory

After building `Pandowdy`, the following new files appear in the output directory:

```
bin/Debug/net8.0/
  ├── Microsoft.Data.Sqlite.dll          (~120 KB managed assembly)
  ├── SQLitePCLRaw.core.dll              (~50 KB managed assembly)
  ├── SQLitePCLRaw.batteries_v2.dll      (~10 KB managed assembly)
  └── runtimes/
      ├── win-x64/native/e_sqlite3.dll   (~1.5 MB native)
      ├── linux-x64/native/libe_sqlite3.so
      └── osx-x64/native/libe_sqlite3.dylib
```

The `runtimes/` folder structure is created automatically by the NuGet restore process.
On publish with a specific RID (e.g., `dotnet publish -r win-x64`), only the relevant
native binary is included.

**Total size impact:** ~1.7 MB (managed + one platform native binary).

### D.6 Initialization

`SQLitePCLRaw.bundle_e_sqlite3` includes a module initializer that calls
`SQLitePCL.Batteries_V2.Init()` automatically at assembly load time. No explicit
initialization code is required in `Program.cs` or elsewhere.

If explicit initialization is ever needed (e.g., for a specific provider configuration),
it can be done once in the composition root:

```csharp
// Only if automatic initialization fails (rare)
SQLitePCL.Batteries_V2.Init();
```

### D.7 Connection Strings

`Microsoft.Data.Sqlite` uses simple connection strings:

```csharp
// Open or create a database file
var connectionString = $"Data Source={filePath}";

// Read-only mode (for inspecting .skillet files)
var connectionString = $"Data Source={filePath};Mode=ReadOnly";

// In-memory database (for unit tests or future loose-disk mode)
var connectionString = "Data Source=:memory:";
```

WAL mode and other pragmas are set via SQL commands after opening:

```csharp
using var connection = new SqliteConnection(connectionString);
await connection.OpenAsync();

using var pragmaCmd = connection.CreateCommand();
pragmaCmd.CommandText = """
    PRAGMA journal_mode = WAL;
    PRAGMA foreign_keys = ON;
    PRAGMA application_id = 0x534B494C;
    """;
await pragmaCmd.ExecuteNonQueryAsync();
```

### D.8 Testing Considerations

**In-memory databases for tests:**

Unit tests use in-memory SQLite databases rather than temp files when testing
schema creation and CRUD logic. This avoids filesystem I/O and is faster:

```csharp
// In-memory database — lives as long as the connection is open
using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
```

**Named in-memory databases for shared connections** (if concurrent access testing is needed):

```csharp
// Shared in-memory database across multiple connections in the same process
var connectionString = "Data Source=TestDb;Mode=Memory;Cache=Shared";
```

**File-based tests for round-trip integration:**

Integration tests (e.g., create project → close → reopen → verify) use temporary
files with randomized names, following the project's testing convention:

```csharp
var testPath = Path.Combine(Path.GetTempPath(), $"pandowdy_test_{Guid.NewGuid():N}.skillet");
// No cleanup — per testing guidelines
```

**Test project reference:**

`Pandowdy.Project.Tests` references `Microsoft.Data.Sqlite` transitively through
its `<ProjectReference>` to `Pandowdy.Project`. No additional package reference
is needed in the test project for SQLite access.

### D.9 Cross-Platform Deployment Notes

| Platform | Native Binary | Notes |
|----------|---------------|-------|
| Windows x64 | `e_sqlite3.dll` | Works out of the box |
| Windows ARM64 | `e_sqlite3.dll` | ARM64 binary included in package |
| Linux x64 | `libe_sqlite3.so` | No system SQLite dependency; bundled |
| Linux ARM64 | `libe_sqlite3.so` | ARM64 binary included in package |
| macOS x64 | `libe_sqlite3.dylib` | Bundled; does not use system SQLite |
| macOS ARM64 | `libe_sqlite3.dylib` | Apple Silicon native binary included |

The bundled `e_sqlite3` library is a custom build maintained by the SQLitePCLRaw
project. It includes common SQLite compile-time options (FTS5, JSON1, etc.)
but is distinct from any system-installed SQLite. This avoids version-mismatch
issues across platforms.

### D.10 Why Not Other Options

| Alternative | Why Not |
|-------------|---------|
| `System.Data.SQLite` | Older, heavier, Windows-focused heritage. `Microsoft.Data.Sqlite` is the modern recommended choice. |
| `Entity Framework Core` + `Microsoft.EntityFrameworkCore.Sqlite` | ORM overhead not justified. Direct SQL gives full control over schema, pragmas, blob streaming, and transaction boundaries. EF Core would add ~3 MB of assemblies and hide critical SQLite-specific behaviors. |
| `Dapper` | Micro-ORM convenience not needed. The query surface is small and well-defined. Direct `SqliteCommand` + `SqliteDataReader` is sufficient and avoids the dependency. |
| `LiteDB` | Document database, not relational. Doesn't support SQL, foreign keys, or the structured schema model the `.skillet` design requires. |
| Raw `SQLitePCLRaw` without `Microsoft.Data.Sqlite` | Too low-level. `Microsoft.Data.Sqlite` provides the standard ADO.NET abstraction (`DbConnection`, `DbCommand`, `DbDataReader`) with proper async support and parameterized queries. |

### D.11 Additions to Solution File

The solution file must be updated to include the new project:

```
dotnet sln add Pandowdy.Project/Pandowdy.Project.csproj
dotnet sln add Pandowdy.Project.Tests/Pandowdy.Project.Tests.csproj
```

Project references to add:

```xml
<!-- Pandowdy.csproj (host app) — add project reference -->
<ProjectReference Include="..\Pandowdy.Project\Pandowdy.Project.csproj" />

<!-- Pandowdy.UI.csproj — add project reference (for ISkilletProjectManager, ISettingsResolver) -->
<ProjectReference Include="..\Pandowdy.Project\Pandowdy.Project.csproj" />

<!-- Pandowdy.Project.csproj — add project reference to EmuCore (for InternalDiskImage, DiskFormat) -->
<ProjectReference Include="..\Pandowdy.EmuCore\Pandowdy.EmuCore.csproj" />

<!-- Pandowdy.Project.Tests.csproj — add project reference -->
<ProjectReference Include="..\Pandowdy.Project\Pandowdy.Project.csproj" />
```

---

## Appendix E: Ad Hoc Project Design

### E.1 Concept

Pandowdy **always** has an active project. When no named project file is
specified — on first launch, after closing a project, or when the last
`active_project_path` no longer exists — the system automatically creates an
**ad hoc project**: an in-memory SQLite database (`Data Source=:memory:`) that
behaves identically to any file-based `.skillet` project.

**Key invariant:** `ISkilletProjectManager.CurrentProject` is **never null**.

This eliminates an entire class of null guards throughout the codebase:
`IDiskImageStore` injection is always non-nullable, "Insert Disk" is always
enabled, settings resolution always has a project layer to query, and view
models never need to check for a "no project" state.

### E.2 Ad Hoc Project Properties

| Property | Value |
|----------|-------|
| `FilePath` | `null` |
| `IsAdHoc` | `true` |
| `Metadata.Name` | `"untitled"` |
| SQLite connection | `Data Source=:memory:` |
| Schema | Full V1 schema (6 tables, all pragmas except WAL) |
| `mount_configuration` | Default: slot 6, drives 1 & 2, empty |
| `HasUnsavedChanges` | `true` if any mutation since creation (import, settings, mount config, eject) |

### E.3 Lifecycle

```
┌───────────────────────────────────────────────────────────────────┐
│                    Ad Hoc Project Lifecycle                       │
│                                                                   │
│  1. CREATION (automatic)                                          │
│     Startup with no active_project_path                           │
│     CloseAsync() without opening another project                  │
│     → CreateAdHocAsync() → in-memory SQLite → full V1 schema      │
│                                                                   │
│  2. USE (identical to file-based)                                 │
│     Import disk images → mount → emulate → eject                  │
│     All data lives in the in-memory SQLite database               │
│                                                                   │
│  3. TRANSITION (user-initiated)                                   │
│     "Save Project As..." → VACUUM INTO 'filepath'                 │
│     → Close in-memory connection → reopen as file-based           │
│     → IsAdHoc becomes false, FilePath becomes non-null            │
│     → Project is now a normal file-based .skillet project         │
│                                                                   │
│  4. DISCARD (on close without saving)                             │
│     Close project → prompt if data exists → Discard               │
│     → Dispose() closes in-memory connection → all data lost       │
│     → New ad hoc created immediately                              │
└───────────────────────────────────────────────────────────────────┘
```

### E.4 Save Behavior

| Command | Ad Hoc Project | File-Based Project |
|---------|---------------|--------------------|
| **Save Project** (Ctrl+Alt+S) | Disabled — no file path | Enabled when not pristine; disabled for pristine |
| **Save Project As...** (Ctrl+Shift+S) | Enabled when not pristine (has data) | Enabled when not pristine |
| **Auto-save timer** | Skipped — nothing to auto-save to | Fires normally |
| **Eject auto-flush** | Works normally — `ReturnAsync()` writes to in-memory `working_blob` | Works normally |
| **Close Project** | Disabled when pristine; enabled when project has data | Always enabled |

### E.5 Save As Mechanics

The transition from ad hoc (in-memory) to file-based uses SQLite's `VACUUM INTO`
command, available in SQLite 3.27.0+ (bundled in `SQLitePCLRaw.lib.e_sqlite3`):

```sql
VACUUM INTO 'C:\projects\myproject.skillet';
```

This atomically copies the entire in-memory database to a new file on disk. The
output file is optimally compacted (no wasted pages). After the vacuum:

1. Close the in-memory `SqliteConnection`.
2. Open a new `SqliteConnection` with `Data Source=C:\projects\myproject.skillet`.
3. Set pragmas (including `PRAGMA journal_mode = WAL` — now applicable).
4. Update `ISkilletProject.FilePath` and `ISkilletProject.IsAdHoc`.
5. Update `pandowdy-settings.json`: set `active_project_path`, add to recent list.
6. Update title bar: "Pandowdy — {ProjectName}".

The `ProjectIOThread` manages this transition internally — the connection swap
happens on the IO thread, so callers never see an inconsistent state.

**Transparent to EmuCore:** The connection swap does not disrupt any
`InternalDiskImage` objects currently checked out by the emulator. Checked-out
disk images are fully deserialized, in-memory .NET objects with no reference to
the underlying `SqliteConnection` — they were produced by `DiskBlobStore.Deserialize()`
at checkout time and exist independently of the database thereafter. The emulator
continues reading and writing them at full speed throughout the transition.

The swap is transparent to all `ISkilletProject` and `IDiskImageStore` consumers:

- **Pending IO requests** (already enqueued before the swap) execute against the
  old connection. They complete normally because the old connection is not closed
  until the IO thread finishes the swap sequence.
- **Future IO requests** (enqueued after the swap) execute against the new
  file-based connection. The data is identical — `VACUUM INTO` made a complete copy.
- **`ReturnAsync()` calls** from future eject operations write `working_blob` to
  the new file-based database. The `disk_images.id` values are unchanged across
  the vacuum, so the controller's stored `diskImageId` remains valid.
- **`CheckOutAsync()` calls** for subsequent mounts read from the new file-based
  database. Blob data is byte-identical to the in-memory version.

The `SkilletProject` instance itself is preserved — `ISkilletProjectManager`
does **not** dispose and recreate it. Only the internal `SqliteConnection` and
the `FilePath` / `IsAdHoc` properties change. References held by other services
(view models, card factory, settings resolver) remain valid without re-injection
or event-driven updates.

### E.6 Close / Discard Behavior

**File-based projects** always have Close Project enabled:

- **Clean file-based** (no dirty disks, no unsaved overrides): closes immediately
  — no prompt. A new ad hoc project is created.
- **Dirty file-based** (unsaved changes): prompt: "Save / Discard / Cancel".
  Save writes to the existing file, then closes. A new ad hoc project is created.

**Ad hoc projects** have Close Project enabled only when the project has content:

- **Pristine ad hoc** (no imported disks, no overrides — initial creation state):
  Close Project is **disabled** in the menu. The user cannot initiate a close.
  This state is the starting point on launch or after closing another project.
- **Ad hoc with data** (imported disks, settings overrides): prompt the user:
  "You have unsaved work in the current project. Save before closing?"
  - **Save As** → file dialog → `SaveAsAsync()` → then close and create new ad hoc.
  - **Discard** → `Dispose()` → all in-memory data lost → new ad hoc created.
  - **Cancel** → abort close operation.

### E.7 Title Bar

| State | Title |
|-------|-------|
| Ad hoc project | Pandowdy — untitled |
| File-based project | Pandowdy — {ProjectName} |
| File-based project (dirty) | Pandowdy — {ProjectName} * |

### E.8 Impact on Start Page (Phase 4)

The Start Page is **not** a gate that blocks the workspace. Because an ad hoc
project always exists on startup, the emulator workspace is always accessible.
The Start Page becomes an optional project management panel:

- Shown as an overlay or sidebar on first launch (with ad hoc project).
- Can be dismissed — the user can immediately start importing and using disks.
- Provides "Create New Project", "Open Project", and "Recent Projects" actions.
- Can be reopened via File menu or a toolbar button.

### E.9 Impact on DI Wiring

Because `CurrentProject` is never null:

- `IDiskImageStore` is injected as **non-nullable** into `DiskIIControllerCard`.
- `ICardFactory` accepts `IDiskImageStore` (not `IDiskImageStore?`).
- `ISettingsResolver` always has a project layer to query.
- View models do not need "no project" checks for disk operations.

When the active project changes (open, close, Save As), the card factory and
other consumers receive the new project's `IDiskImageStore` reference. The
exact mechanism (re-injection, event, or re-creation) is an implementation
detail resolved in Phase 2a.

### E.10 Testing Considerations

- **Unit tests** can use `CreateAdHocAsync()` to get a fully functional project
  without filesystem involvement — simpler and faster than temp file creation.
- **Round-trip tests** for Save As: create ad hoc → import disk → `SaveAsAsync()`
  → open saved file → verify data integrity.
- **Lifecycle tests**: create ad hoc → verify `IsAdHoc` → `SaveAsAsync()` →
  verify `IsAdHoc` is false and `FilePath` is set.
- **Close behavior tests**: ad hoc with data → close → verify prompt → discard →
  verify new ad hoc created.

---

*Document Created: 2026-07-15*
*Based on: docs/Skillet_Project_File_Development.md*
