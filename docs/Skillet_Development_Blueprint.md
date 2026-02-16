# Pandowdy `.skillet` Project System — Development Blueprint

> **Authoritative Design Spec:** `docs/Skillet_Project_File_Development.md`  
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
10. [Debugger Subsystem Storage Model](#10-debugger-subsystem-storage-model)
11. [AppleSoft Subsystem Storage Model](#11-applesoft-subsystem-storage-model)
12. [Workspace / UI Layout Persistence Model](#12-workspace--ui-layout-persistence-model)
13. [Project-Type Flexibility](#13-project-type-flexibility)
14. [Legacy Save Logic Removal](#14-legacy-save-logic-removal)
15. [SQLite Access Patterns & Concurrency](#15-sqlite-access-patterns--concurrency)
16. [DI Registration & Service Wiring](#16-di-registration--service-wiring)
17. [Testing Strategy](#17-testing-strategy)
18. [Implementation Phases](#18-implementation-phases)

---

## 1. Architecture Overview

### Conceptual Layers

```
┌──────────────────────────────────────────────────────────┐
│  Pandowdy (Host)  — DI composition root, startup         │
├──────────────────────────────────────────────────────────┤
│  Pandowdy.UI      — Start Page, project dialogs, menus   │
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
│   ├── ISkilletProject.cs          — Read/write contract for open project
│   ├── ISkilletProjectManager.cs   — Open/create/close lifecycle
│   └── ISettingsResolver.cs        — JSON + skillet resolution
├── Models/
│   ├── ProjectMetadata.cs          — Project name, created, modified
│   ├── DiskImageRecord.cs          — Disk image metadata model
│   ├── MountConfiguration.cs       — Slot/drive mount assignments
│   ├── BreakpointRecord.cs         — Debugger breakpoint model
│   ├── WatchRecord.cs              — Debugger watch model
│   ├── SymbolRecord.cs             — Symbol table entry
│   ├── AppleSoftSourceRecord.cs    — AppleSoft source model
│   └── WorkspaceLayout.cs          — UI layout model
├── Services/
│   ├── SkilletProject.cs           — ISkilletProject implementation
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
    track_count         INTEGER NOT NULL DEFAULT 35,
    optimal_bit_timing  INTEGER NOT NULL DEFAULT 32,
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

### Table: `breakpoints`

Debugger breakpoints (Task 19/22 integration).

```sql
CREATE TABLE breakpoints (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    address         INTEGER NOT NULL,                -- PC address ($0000-$FFFF)
    is_enabled      INTEGER NOT NULL DEFAULT 1,
    hit_count       INTEGER NOT NULL DEFAULT 0,
    break_after     INTEGER,                         -- Break after N hits (null = every hit)
    condition       TEXT,                            -- Conditional expression (future)
    breakpoint_type TEXT NOT NULL DEFAULT 'address', -- 'address','data_read','data_write','io'
    label           TEXT,                            -- User label
    group_name      TEXT,                            -- Breakpoint group (future)
    created_utc     TEXT NOT NULL
);

CREATE INDEX idx_breakpoints_address ON breakpoints(address);
CREATE INDEX idx_breakpoints_type ON breakpoints(breakpoint_type);
```

### Table: `watches`

Debugger watch expressions.

```sql
CREATE TABLE watches (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    expression  TEXT NOT NULL,       -- Address or expression (e.g., "$0400", "A", "$0300+X")
    label       TEXT,                -- User label
    format      TEXT DEFAULT 'hex', -- 'hex', 'decimal', 'binary', 'ascii'
    byte_count  INTEGER DEFAULT 1,  -- Number of bytes to display
    sort_order  INTEGER NOT NULL DEFAULT 0
);
```

### Table: `symbols`

Symbol tables for disassembly/debugging.

```sql
CREATE TABLE symbols (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    address     INTEGER NOT NULL,
    name        TEXT NOT NULL,
    symbol_type TEXT NOT NULL DEFAULT 'label',  -- 'label','equate','entry_point','data'
    source      TEXT,                           -- Origin: 'user','import','auto'
    comment     TEXT,
    UNIQUE(address, name)
);

CREATE INDEX idx_symbols_address ON symbols(address);
CREATE INDEX idx_symbols_name ON symbols(name);
```

### Table: `disassembly_cache`

Cached disassembly results.

```sql
CREATE TABLE disassembly_cache (
    address         INTEGER PRIMARY KEY NOT NULL,  -- Start address
    opcode_bytes    BLOB NOT NULL,                 -- Raw bytes (1-3)
    mnemonic        TEXT NOT NULL,                 -- e.g., "LDA"
    operand_text    TEXT,                           -- e.g., "$0400,X"
    instruction_len INTEGER NOT NULL,              -- 1, 2, or 3
    is_data         INTEGER NOT NULL DEFAULT 0,    -- Marked as data, not code
    comment         TEXT                           -- User annotation
);
```

### Table: `applesoft_sources`

AppleSoft program storage (future subsystem).

```sql
CREATE TABLE applesoft_sources (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    name            TEXT NOT NULL,
    source_text     TEXT,            -- Detokenized source
    tokenized_blob  BLOB,           -- Tokenized Applesoft representation
    ast_json        TEXT,            -- AST as JSON
    semantic_json   TEXT,            -- Semantic analysis (variable types, flow)
    disk_image_id   INTEGER,         -- Optional link to source disk
    origin_address  INTEGER,         -- Memory address where program was found
    created_utc     TEXT NOT NULL,
    modified_utc    TEXT NOT NULL,
    FOREIGN KEY (disk_image_id) REFERENCES disk_images(id) ON DELETE SET NULL
);
```

### Table: `execution_history`

Optional execution trace buffer (Task 22).

```sql
CREATE TABLE execution_history (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id      TEXT NOT NULL,       -- Groups traces by debug session
    sequence        INTEGER NOT NULL,    -- Ordering within session
    address         INTEGER NOT NULL,    -- PC at execution
    opcode          INTEGER NOT NULL,    -- Opcode byte
    operand_lo      INTEGER,             -- Operand byte 1 (if applicable)
    operand_hi      INTEGER,             -- Operand byte 2 (if applicable)
    a_reg           INTEGER NOT NULL,
    x_reg           INTEGER NOT NULL,
    y_reg           INTEGER NOT NULL,
    sp_reg          INTEGER NOT NULL,
    status_reg      INTEGER NOT NULL,
    cycle_count     INTEGER NOT NULL
);

CREATE INDEX idx_exec_history_session ON execution_history(session_id, sequence);
```

### Table: `workspace_layout`

Per-project UI layout state.

```sql
CREATE TABLE workspace_layout (
    key     TEXT PRIMARY KEY NOT NULL,
    value   TEXT NOT NULL
);

-- Example rows:
-- ('panel_layout_json',    '{"panels":[...]}')
-- ('open_files_json',      '["file1.bas","file2.bas"]')
-- ('emulator_visible',     'true')
-- ('editor_visible',       'false')
```

### Table: `project_settings`

General-purpose key-value store for project-level settings not covered above.

```sql
CREATE TABLE project_settings (
    key     TEXT PRIMARY KEY NOT NULL,
    value   TEXT NOT NULL
);
```

### Table: `user_annotations`

Free-form annotations attached to addresses or disk images.

```sql
CREATE TABLE user_annotations (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    target_type     TEXT NOT NULL,      -- 'address', 'disk_image', 'applesoft_line'
    target_id       TEXT NOT NULL,      -- Address (hex), disk_image_id, line number
    annotation      TEXT NOT NULL,
    created_utc     TEXT NOT NULL,
    modified_utc    TEXT NOT NULL
);

CREATE INDEX idx_annotations_target ON user_annotations(target_type, target_id);
```

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
    public int TrackCount { get; init; } = 35;
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

### `MountConfiguration`

```csharp
public sealed record MountConfiguration(
    long Id,
    int Slot,
    int DriveNumber,
    long? DiskImageId,
    bool AutoMount);
```

### `BreakpointRecord`

```csharp
public sealed class BreakpointRecord
{
    public long Id { get; init; }
    public required int Address { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int HitCount { get; set; }
    public int? BreakAfter { get; set; }
    public string? Condition { get; set; }
    public string BreakpointType { get; set; } = "address";
    public string? Label { get; set; }
    public string? GroupName { get; set; }
    public DateTime CreatedUtc { get; init; }
}
```

### `WatchRecord`

```csharp
public sealed class WatchRecord
{
    public long Id { get; init; }
    public required string Expression { get; set; }
    public string? Label { get; set; }
    public string Format { get; set; } = "hex";
    public int ByteCount { get; set; } = 1;
    public int SortOrder { get; set; }
}
```

### `SymbolRecord`

```csharp
public sealed record SymbolRecord(
    long Id,
    int Address,
    string Name,
    string SymbolType,   // "label", "equate", "entry_point", "data"
    string? Source,       // "user", "import", "auto"
    string? Comment);
```

### `WorkspaceLayout`

```csharp
public sealed class WorkspaceLayout
{
    public string? PanelLayoutJson { get; set; }
    public List<string> OpenFiles { get; set; } = [];
    public bool EmulatorVisible { get; set; } = true;
    public bool EditorVisible { get; set; }
}
```

---

## 5. Project Load / Save Lifecycle

### 5.1 Creating a New Project

```
User clicks "Create New Skillet Project"
  → File dialog: choose location and name
  → SkilletProjectManager.CreateAsync(filePath, projectName)
    → Create new SQLite file at filePath
    → Set pragmas (application_id, user_version, journal_mode, foreign_keys)
    → Run SkilletSchemaManager.InitializeSchema() — creates all tables
    → Insert project_metadata rows (name, timestamps, version)
    → Insert default mount_configuration rows (slot 6, drives 1 & 2, empty)
    → Return ISkilletProject handle
  → Update pandowdy-settings.json: set active_project_path, add to recent list
  → UI transitions from Start Page to main workspace
```

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
  → Load workspace_layout → restore UI state
  → Load breakpoints, watches, symbols → feed into debugger subsystem
  → Update pandowdy-settings.json: set active_project_path, update recent list
  → UI transitions from Start Page to main workspace
```

### 5.3 Saving a Project (Explicit & Periodic)

```
User presses Ctrl+S or auto-save timer fires
  → ISkilletProject.SaveAsync()
    → BEGIN TRANSACTION
    → For each mounted disk with persist_working = true AND working_dirty = true:
      → Serialize InternalDiskImage to blob
      → UPDATE disk_images SET working_blob = ?, working_dirty = 0, modified_utc = ?
    → Persist current breakpoints, watches, symbols (upsert)
    → Persist workspace_layout
    → Update project_metadata modified_utc
    → COMMIT
```

### 5.4 Closing a Project

```
User closes project or opens another
  → Check for unsaved changes (working_dirty on any mounted disk)
  → If unsaved → prompt: Save / Discard / Cancel
  → If Save → ISkilletProject.SaveAsync()
  → ISkilletProject.Dispose()
    → Close SQLite connection
  → If opening another → proceed to Open flow
  → If not opening another → show Start Page
```

### 5.5 `ISkilletProject` Interface

```csharp
public interface ISkilletProject : IDisposable
{
    string FilePath { get; }
    ProjectMetadata Metadata { get; }
    bool HasUnsavedChanges { get; }

    // Disk image management
    Task<long> ImportDiskImageAsync(string filePath, string name);
    Task<DiskImageRecord> GetDiskImageAsync(long id);
    Task<IReadOnlyList<DiskImageRecord>> GetAllDiskImagesAsync();
    Task RemoveDiskImageAsync(long id);
    Task RegenerateWorkingCopyAsync(long id);
    Stream OpenOriginalBlobRead(long diskImageId);
    Stream OpenWorkingBlobRead(long diskImageId);
    Task WriteWorkingBlobAsync(long diskImageId, Stream data);

    // Mount configuration
    Task<IReadOnlyList<MountConfiguration>> GetMountConfigurationAsync();
    Task SetMountAsync(int slot, int driveNumber, long? diskImageId);

    // Debugger state
    Task<IReadOnlyList<BreakpointRecord>> GetBreakpointsAsync();
    Task UpsertBreakpointAsync(BreakpointRecord breakpoint);
    Task RemoveBreakpointAsync(long id);

    Task<IReadOnlyList<WatchRecord>> GetWatchesAsync();
    Task UpsertWatchAsync(WatchRecord watch);
    Task RemoveWatchAsync(long id);

    Task<IReadOnlyList<SymbolRecord>> GetSymbolsAsync();
    Task UpsertSymbolAsync(SymbolRecord symbol);
    Task ImportSymbolFileAsync(Stream symbolData, string format);

    // Settings
    Task<string?> GetSettingAsync(string table, string key);
    Task SetSettingAsync(string table, string key, string value);

    // Workspace layout
    Task<WorkspaceLayout> GetWorkspaceLayoutAsync();
    Task SaveWorkspaceLayoutAsync(WorkspaceLayout layout);

    // Lifecycle
    Task SaveAsync();
}
```

### 5.6 `ISkilletProjectManager` Interface

```csharp
public interface ISkilletProjectManager
{
    ISkilletProject? CurrentProject { get; }

    Task<ISkilletProject> CreateAsync(string filePath, string projectName);
    Task<ISkilletProject> OpenAsync(string filePath);
    Task CloseAsync();
}
```

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

The `InternalDiskImage` is serialized to blob using a compact binary format:

```
[Header]
  4 bytes: magic ("PIDI" — Pandowdy Internal Disk Image)
  2 bytes: format version (1)
  1 byte:  track count
  1 byte:  optimal bit timing
  1 byte:  write protected flag

[Per Track] × track_count
  4 bytes: bit count (little-endian int32)
  4 bytes: byte count (little-endian int32, = ceil(bit_count/8))
  N bytes: raw track data

[Footer]
  4 bytes: CRC-32 of all preceding bytes
```

This is implemented in `DiskBlobStore.Serialize()` / `DiskBlobStore.Deserialize()`.

The CRC-32 uses the existing `System.IO.Hashing` package already referenced by `Pandowdy.EmuCore`.

### 6.3 Internal Use

When a disk image is mounted into the emulator:

```
Mount: slot 6, drive 1, disk_image_id = 3
  → DiskBlobStore.DeserializeAsync(project, diskImageId: 3, useWorking: true)
    → SELECT working_blob FROM disk_images WHERE id = 3
    → If working_blob IS NULL → SELECT original_blob (regenerate)
    → Deserialize blob → InternalDiskImage
  → Feed InternalDiskImage into DiskIIControllerCard via existing InsertDiskMessage
  → Emulator reads/writes InternalDiskImage in-memory as normal
```

All modifications happen in-memory. On project save, the modified `InternalDiskImage`
is serialized back to `working_blob`.

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

---

## 7. Originals vs Working Copies

### Storage Model

| Aspect | Original | Working Copy |
|--------|----------|--------------|
| **Stored in** | `disk_images.original_blob` | `disk_images.working_blob` |
| **Mutability** | Immutable (never modified after import) | Mutable (updated on save) |
| **Null meaning** | N/A (required NOT NULL) | Regenerate from original |
| **Dirty tracking** | N/A | `working_dirty` flag |
| **Persistence policy** | Always persisted | Controlled by `persist_working` |

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
                 │  original_blob ──┼──── Immutable, always present
                 │  working_blob  ──┼──── Nullable, may be regenerated
                 └────────┬─────────┘
                          │ deserialize on mount
                          ▼
                 ┌──────────────────┐
                 │ InternalDiskImage│  ← In-memory, used by emulator
                 │  (mutable)       │
                 └────────┬─────────┘
                          │ serialize on save (if persist_working)
                          ▼
                 ┌──────────────────┐
                 │  .skillet file   │
                 │  working_blob    │  ← Updated
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
| Panel visibility | JSON (default), Skillet (override) | `workspace_layout` |
| Display: scanlines | JSON (default), Skillet (override) | `display_overrides` |
| Display: monochrome | JSON (default), Skillet (override) | `display_overrides` |
| Emulator: throttle | JSON (default), Skillet (override) | `emulator_overrides` |
| Emulator: caps lock | JSON (default), Skillet (override) | `emulator_overrides` |
| Recent projects | JSON | — |
| Active project path | JSON | — |
| Breakpoints | Skillet only | `breakpoints` |
| Watches | Skillet only | `watches` |
| Symbols | Skillet only | `symbols` |
| Disk images | Skillet only | `disk_images` |
| Mount config | Skillet only | `mount_configuration` |
| AppleSoft sources | Skillet only | `applesoft_sources` |
| User annotations | Skillet only | `user_annotations` |

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

On launch, Pandowdy shows a Start Page (new Avalonia UserControl):

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
│  ────────────────────────────────────────────────────────    │
│  Loose-disk mode is not available in this version.           │
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
    → Skip Start Page, go directly to workspace
  → Else:
    → Show Start Page
```

### 9.4 Project Switching

```
User: File → Open Project (while project is open)
  → Check for unsaved changes → prompt if needed
  → CloseAsync() current project
  → OpenAsync() new project
  → Rebuild UI from new project's workspace_layout + mount_configuration
```

### 9.5 Menu Changes

**File Menu (Updated):**
```
File
├── New Project...          Ctrl+Shift+N
├── Open Project...         Ctrl+Shift+O
├── ──────────────
├── Save Project            Ctrl+S
├── ──────────────
├── Import Disk Image...    Ctrl+I
├── Export Disk Image...    Ctrl+E
├── ──────────────
├── Close Project
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

---

## 10. Debugger Subsystem Storage Model

### Current State
- Task 19 (Basic Debugger Foundation) is in progress.
- `CpuStateSnapshot`, `CpuExecutionStatus`, `IMemoryInspector` exist.
- Debugger core stepping exists; breakpoints and UI panels are NOT STARTED.

### Storage Approach

Debugger state is stored in the `.skillet` and loaded when the project opens.
This enables **reproducible debugging sessions**.

### Breakpoints

Stored in `breakpoints` table. Loaded into the debugger's in-memory breakpoint set
on project open. Changes are persisted on project save.

```csharp
// On project open:
var breakpoints = await project.GetBreakpointsAsync();
foreach (var bp in breakpoints)
{
    debugger.AddBreakpoint(bp.Address, bp.IsEnabled, bp.Condition);
}

// On project save:
var activeBreakpoints = debugger.GetAllBreakpoints();
foreach (var bp in activeBreakpoints)
{
    await project.UpsertBreakpointAsync(bp.ToRecord());
}
```

### Watches

Stored in `watches` table. Loaded into debugger watch panel.
Sort order preserves user's preferred arrangement.

### Symbols

Stored in `symbols` table. Populated from:
- **User-defined:** Manually added labels/equates.
- **Imported:** Loaded from symbol files (future: various formats).
- **Auto-detected:** Common Apple II ROM entry points (future).

Symbols enhance disassembly output by replacing raw addresses with labels.

### Disassembly Cache

Stored in `disassembly_cache` table. Acts as a warm cache for the
`Pandowdy.Disassembler` output. Invalidated when memory changes are detected
at cached addresses.

### Execution History

Stored in `execution_history` table (Task 22). Sessions are grouped by `session_id`.
Bounded: old sessions are pruned automatically (configurable max rows, default 100,000).

---

## 11. AppleSoft Subsystem Storage Model

### Current State
- No AppleSoft subsystem exists yet.
- Storage model is defined here for future implementation.

### Storage Approach

Each AppleSoft program is a row in `applesoft_sources`:

| Column | Content |
|--------|---------|
| `source_text` | Human-readable detokenized BASIC source |
| `tokenized_blob` | Raw tokenized bytes as found in Apple II memory |
| `ast_json` | Parsed AST as JSON (line → statements → expressions tree) |
| `semantic_json` | Semantic analysis results (variable types, const detection, flow analysis) |
| `disk_image_id` | FK to the disk image the program was extracted from |
| `origin_address` | Memory address ($0801 typically) where program was found |

### Workflow

```
1. User loads a disk with BASIC programs
2. "Extract AppleSoft Programs" scans disk image for tokenized BASIC
3. For each program found:
   a. Store tokenized_blob (raw bytes)
   b. Detokenize → store source_text
   c. Parse → store ast_json
   d. Analyze → store semantic_json
4. User edits source_text in built-in editor (future)
5. Re-tokenize edited source → update tokenized_blob
6. Re-parse/analyze → update ast_json / semantic_json
```

### Editor State

Editor state (cursor position, fold regions, open tabs) is stored in `workspace_layout`
as part of the `open_files_json` structure:

```json
{
  "open_files": [
    {
      "type": "applesoft",
      "source_id": 3,
      "cursor_line": 42,
      "cursor_col": 10,
      "folded_regions": [100, 200],
      "scroll_position": 35
    }
  ]
}
```

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

### Skillet Layer (Project Overrides)

Stored in `workspace_layout` and `emulator_overrides` / `display_overrides` tables.

Example: A project that always uses monochrome display and shows the debugger panel:

```
workspace_layout:
  ('panel_layout_json', '{"showDebugger":true,"showCpuStatus":true}')
  ('emulator_visible', 'true')

display_overrides:
  ('force_monochrome', 'true')

emulator_overrides:
  ('throttle_enabled', 'false')
```

### Resolution at Runtime

```
User opens project "Karateka Debugging":
  → Window geometry: from JSON (not project-specific)
  → force_monochrome: project override = true (JSON default = false) → true
  → throttle_enabled: project override = false (JSON default = true) → false
  → showDebugger: project override = true (JSON default = absent) → true
  → showDiskStatus: no project override → JSON default = true → true
```

---

## 13. Project-Type Flexibility

The spec defines three project styles. The `.skillet` schema handles all without branching:

### Emulator-Centric (Full Workstation)

- Disk images mounted and actively used.
- Debugger breakpoints and watches active.
- CPU status panel visible.
- All tables populated.

### Editor-Centric (Little Emulator Usage)

- AppleSoft sources are primary focus.
- `workspace_layout`: `emulator_visible = false`, `editor_visible = true`.
- `mount_configuration`: may be empty or have reference disks.
- `breakpoints` / `watches`: may be empty.
- `applesoft_sources`: heavily used.

### Multi-Disk

- Multiple entries in `disk_images` table.
- Multiple `mount_configuration` entries across slots.
- User swaps disks during session via UI.
- Working copies tracked independently per disk.

### No Branching Logic Needed

The schema naturally supports all styles:
- Tables that aren't used are simply empty.
- `workspace_layout` controls which panels are visible.
- No `project_type` column or discriminator needed.

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

### Connection Management

- One `SqliteConnection` per `SkilletProject` instance.
- Connection opened on `OpenAsync()`, closed on `Dispose()`.
- WAL mode enables concurrent reads from UI thread while emulator thread writes.

### Thread Safety

```
UI Thread:       Reads (breakpoints, settings, layout)
Emulator Thread: Does NOT access SQLite directly
Save Operation:  Serializes in-memory state → writes to SQLite (UI thread or save-dedicated task)
```

The emulator thread never touches SQLite. Disk I/O during emulation happens against
the in-memory `InternalDiskImage`. Persistence is a separate operation triggered by
save events.

### Blob Access

Large blobs (disk images, ~230KB for 35-track disks) use streaming:

```csharp
// Reading a blob
using var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT working_blob FROM disk_images WHERE id = @id";
cmd.Parameters.AddWithValue("@id", diskImageId);

using var reader = await cmd.ExecuteReaderAsync();
if (reader.Read())
{
    using var blobStream = reader.GetStream(0);
    return DiskBlobSerializer.Deserialize(blobStream);
}
```

### Transaction Boundaries

- **Project save**: Single transaction wrapping all dirty disk writes + metadata updates.
- **Disk import**: Single transaction for INSERT.
- **Settings changes**: Individual UPSERTs (no transaction needed for single-row ops).
- **Breakpoint/watch changes**: Batched within project save transaction.

---

## 16. DI Registration & Service Wiring

### New Registrations in `Program.cs`

```csharp
// Pandowdy.Project services
services.AddSingleton<ISkilletProjectManager, SkilletProjectManager>();
services.AddSingleton<ISettingsResolver, SettingsResolver>();

// ISkilletProject is transient-like — managed by ISkilletProjectManager
// Not registered directly; accessed via ISkilletProjectManager.CurrentProject
```

### Dependency Graph (New)

```
Program.cs (composition root)
  ├── ISkilletProjectManager (singleton)
  │   └── Creates/manages ISkilletProject instances
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

### Impact on Existing Services

| Service | Impact |
|---------|--------|
| `GuiSettingsService` | Retains JSON persistence. Removes `DriveState` section. |
| `DriveStateService` | **Removed.** Replaced by `mount_configuration` table. |
| `MainWindowViewModel` | Gains project lifecycle commands. Save/SaveAs → Export. |
| `DiskIIControllerCard` | No change to card logic. Mount/unmount messages unchanged. |
| `InsertDiskMessage` | **Modified**: accepts `InternalDiskImage` directly (not file path). |

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

1. **Schema creation**: New `.skillet` file has all tables and correct pragmas.
2. **Blob round-trip**: Serialize `InternalDiskImage` → blob → deserialize → binary-identical.
3. **Working copy regeneration**: Set `working_blob = NULL`, re-read → gets original.
4. **Persist policy**: `persist_working = false` → working_blob not updated on save.
5. **Settings resolution**: JSON default overridden by skillet, hardcoded fallback works.
6. **Mount configuration**: Slot/drive mapping persists and restores correctly.
7. **Breakpoint persistence**: Add/remove/toggle breakpoints survive project close/reopen.
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

### Phase 1: Foundation (Core Infrastructure)

**Goal:** Create `Pandowdy.Project`, implement schema, basic CRUD, blob store.

1. Create `Pandowdy.Project` class library project.
2. Create `Pandowdy.Project.Tests` test project.
3. Implement `SkilletConstants` (application_id, schema version, table names).
4. Implement `SkilletSchemaManager` (create all tables, set pragmas).
5. Implement `DiskBlobStore` (serialize/deserialize `InternalDiskImage` ↔ blob).
6. Implement `ProjectSettingsStore` (generic key-value CRUD).
7. Implement `SkilletProject` (ISkilletProject — all table operations).
8. Implement `SkilletProjectManager` (create/open/close lifecycle).
9. Write comprehensive unit tests for all of the above.
10. Add project references from `Pandowdy` and `Pandowdy.UI` to `Pandowdy.Project`.

### Phase 2: Settings Resolution

**Goal:** Implement the four-layer settings resolution model.

1. Implement `ISettingsResolver` and `SettingsResolver`.
2. Define setting classification (which settings live where).
3. Modify `GuiSettings` / `GuiSettingsService` to add `recentProjects` and `activeProjectPath`.
4. Remove `DriveStateSettings` from `GuiSettings` (replaced by mount_configuration).
5. Wire `SettingsResolver` into DI.
6. Write tests for resolution logic.

### Phase 3: UI — Start Page & Project Dialogs

**Goal:** Implement the Start Page and project creation/opening UI.

1. Create `StartPage.axaml` / `StartPageViewModel`.
2. Create `NewProjectDialog.axaml` / `NewProjectDialogViewModel`.
3. Modify `MainWindow` to show Start Page when no project is open.
4. Implement recent projects list persistence.
5. Implement auto-open of last active project.
6. Update `App.axaml.cs` / `Program.cs` for new startup flow.

### Phase 4: Disk Lifecycle Migration

**Goal:** Replace legacy save/open with import/export through `.skillet`.

1. Implement disk import flow (file → blob → `disk_images` table).
2. Implement disk mount flow (blob → `InternalDiskImage` → emulator).
3. Implement disk export flow (`InternalDiskImage` → file via `IDiskImageExporter`).
4. Implement working copy regeneration.
5. Remove legacy `SaveDiskMessage`, `SaveDiskAsMessage`.
6. Remove `DestinationFilePath` / `DestinationFormat` from `InternalDiskImage`.
7. Remove `DriveStateService`, `DriveStateConfig`, `DriveStateEntry`.
8. Update `MainWindowViewModel` menus (Save → Export, Open → Import).
9. Update `InsertDiskMessage` to accept `InternalDiskImage` directly.

### Phase 5: Debugger Storage Integration

**Goal:** Wire debugger state persistence into `.skillet`.

1. Implement breakpoint CRUD in `SkilletProject`.
2. Implement watch CRUD in `SkilletProject`.
3. Implement symbol CRUD in `SkilletProject`.
4. On project open: load debugger state → feed into debugger subsystem.
5. On project save: persist debugger state from in-memory to `.skillet`.
6. Wire into Task 19 debugger implementation when breakpoints are built.

### Phase 6: Workspace Layout Persistence

**Goal:** Save/restore per-project UI layout.

1. Implement `WorkspaceLayout` load/save in `SkilletProject`.
2. On project open: restore panel visibility, docking state.
3. On project close: capture current UI state → save to `.skillet`.
4. Handle missing layout gracefully (fall back to JSON defaults).

### Phase 7: Polish & Integration Testing

**Goal:** End-to-end workflows, edge cases, cross-platform validation.

1. Full round-trip integration tests (create → import → mount → modify → save → reopen → verify).
2. Cross-platform testing (Windows, macOS, Linux).
3. Error handling for corrupt `.skillet` files.
4. Large disk image performance testing.
5. Update `Development-Roadmap.md` with Task 32 progress.

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
/// Message requesting a disk image be mounted from an in-memory InternalDiskImage.
/// Replaces the file-path-based InsertDiskMessage for project-based workflows.
/// </summary>
public record MountDiskMessage(int DriveNumber, InternalDiskImage DiskImage) : ICardMessage;

/// <summary>
/// Message requesting a disk image be exported to the filesystem.
/// Replaces SaveDiskAsMessage.
/// </summary>
public record ExportDiskMessage(int DriveNumber, string FilePath, DiskFormat Format) : ICardMessage;
```

### Retained Messages (Unchanged)

- `EjectDiskMessage` — still needed
- `SwapDrivesMessage` — still needed
- `SetWriteProtectMessage` — still needed
- `InsertBlankDiskMessage` — still needed (blank disk → immediately stored in project)
- `InsertDiskMessage` — retained for potential loose-disk mode (future)

### Removed Messages

- `SaveDiskMessage` — obsolete (internal persistence replaces filesystem save)
- `SaveDiskAsMessage` — replaced by `ExportDiskMessage`

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

*Document Created: 2026-07-15*
*Based on: docs/Skillet_Project_File_Development.md*
