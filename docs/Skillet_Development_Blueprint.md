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
│   └── MountConfiguration.cs       — Slot/drive mount assignments
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
    track_count         INTEGER NOT NULL DEFAULT 35,  -- Track-based images only (see §6.2 Block Device Support)
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

> **Note:** `DiskImageRecord` is designed for track-based disk images (`InternalDiskImage`).
> `TrackCount` and `OptimalBitTiming` are not meaningful for block-based devices. If
> `InternalBlockDeviceImage` is introduced for hard drive images (see §6.2 Block Device
> Support), a corresponding model with block-specific properties (`BlockCount`, `BlockSize`)
> will be added alongside the necessary schema migration.

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

    // Settings
    Task<string?> GetSettingAsync(string table, string key);
    Task SetSettingAsync(string table, string key, string value);

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

The `InternalDiskImage` is serialized to blob using a compact binary format.
All blobs stored in `.skillet` are compressed — originals, working copies, and
future snapshots alike. Every blob in storage is cold by definition: the hot
data is always the in-memory `InternalDiskImage` that the emulator reads and
writes directly. Blobs are only deserialized at mount time and serialized at
save time — infrequent lifecycle boundaries where compression cost is negligible.
Disk images often contain large runs of zeros or repeated byte patterns, so
compression significantly reduces `.skillet` file size.

```
[Header] (uncompressed, 10 bytes)
  4 bytes: magic ("PIDI" — Pandowdy Internal Disk Image)
  2 bytes: format version (1)
  1 byte:  compression method (0 = none, 1 = Deflate)
  1 byte:  track count
  1 byte:  optimal bit timing
  1 byte:  write protected flag

[Payload] (compressed via method specified in header)
  [Per Track] × track_count
    4 bytes: bit count (little-endian int32)
    4 bytes: byte count (little-endian int32, = ceil(bit_count/8))
    N bytes: raw track data

[Footer] (uncompressed)
  4 bytes: CRC-32 of header + compressed payload
```

This is implemented in `DiskBlobStore.Serialize()` / `DiskBlobStore.Deserialize()`.

The CRC-32 uses the existing `System.IO.Hashing` package already referenced by
`Pandowdy.EmuCore`. Compression uses `System.IO.Compression.DeflateStream`
(built into the .NET runtime — no additional package required).

### Compression Details

- **Algorithm:** Deflate via `DeflateStream` (`System.IO.Compression`).
- **Level:** `CompressionLevel.Optimal` for storage. Speed is not critical —
  serialize/deserialize happens at lifecycle boundaries (mount, save), never
  per-cycle.
- **Scope:** The per-track payload is compressed as a single Deflate stream.
  The 10-byte header and 4-byte CRC footer remain uncompressed so the header
  can be read and the CRC validated without decompressing.
- **method byte = 0 (none):** Reserved for diagnostic/debugging use. The
  deserializer must handle uncompressed payloads, but `Serialize()` always
  writes method = 1 (Deflate) in production.
- **Expected ratio:** Typical Apple II disk images compress to ~40–60% of
  original size. The primary size range spans 140KB 5.25" floppies (most
  common, ~230KB in PIDI nibblized form), 880KB 3.5" floppies (common), and
  32MB hard drive images (upper bound — may use a block-based blob format
  instead of PIDI; see Block Device Support below). A 140KB floppy (~230KB
  PIDI) typically stores as ~100–140KB compressed. At the upper bound, a hard
  drive image (~32MB uncompressed) is still fast to compress/decompress at
  lifecycle boundaries regardless of blob format — these are infrequent bulk
  operations, not per-cycle work, and the data sizes involved are modest by
  modern standards.
- **No new dependencies:** `System.IO.Compression` is part of the .NET runtime.
  `DeflateStream` is available in all target frameworks (net8.0+).

### Block Device Support (Future)

The PIDI format is designed for track-based `InternalDiskImage` — floppy disk
images where data is organized as nibblized tracks with per-track bit counts.
Hard drive images (up to 65,536 × 512-byte blocks, ~32MB) may use a
block-based internal representation (`InternalBlockDeviceImage`) rather than
the track-based model. If so, a separate blob format (e.g., "PIBI" — Pandowdy
Internal Block Image) would be defined with block-oriented payload structure
instead of per-track layout. The `DiskBlobStore` would gain a second
serializer, and the blob magic bytes would distinguish PIDI from PIBI at
deserialization time. The same compression strategy (Deflate) and CRC-32
footer apply regardless of internal format.

This is not part of the V1 implementation. The schema, model, and blob format
changes for block devices will be designed when `InternalBlockDeviceImage` is
created. The migration infrastructure makes adding the necessary schema
columns and tables zero-cost at that point.

### 6.3 Internal Use

When a disk image is mounted into the emulator:

```
Mount: slot 6, drive 1, disk_image_id = 3
  → DiskBlobStore.DeserializeAsync(project, diskImageId: 3, useWorking: true)
    → SELECT working_blob FROM disk_images WHERE id = 3
    → If working_blob IS NULL → SELECT original_blob (regenerate)
    → Validate CRC-32 on compressed blob
    → Decompress payload (Deflate) → reconstruct InternalDiskImage
  → Feed InternalDiskImage into DiskIIControllerCard via existing InsertDiskMessage
  → Emulator reads/writes InternalDiskImage in-memory as normal
```

All modifications happen in-memory (uncompressed). On project save, the modified
`InternalDiskImage` is serialized and compressed back to `working_blob`.

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
                          │ decompress + deserialize on mount
                          ▼
                 ┌──────────────────┐
                 │ InternalDiskImage│  ← In-memory, uncompressed, used by emulator
                 │  (mutable)       │
                 └────────┬─────────┘
                          │ serialize + compress on save (if persist_working)
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
- Opened on the IO thread during `OpenAsync()`.
- Closed on the IO thread during `Dispose()`.
- The connection object is **never** exposed outside the IO thread.

```csharp
// Connection lifecycle — all calls execute on the IO thread
internal sealed class ProjectIOThread : IDisposable
{
    private readonly Thread _thread;
    private readonly BlockingCollection<IORequest> _queue = new();
    private SqliteConnection? _connection;

    public ProjectIOThread(string filePath)
    {
        _thread = new Thread(() => RunLoop(filePath))
        {
            Name = "Pandowdy.Project.IO",
            IsBackground = true
        };
        _thread.Start();
    }

    private void RunLoop(string filePath)
    {
        _connection = new SqliteConnection($"Data Source={filePath}");
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
| Load disk snapshot | Read | Mount a disk image from blob on project open |
| Save disk snapshot | Write | Persist dirty `InternalDiskImage` working copy |
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

- **Disk mount:** Enqueue blob read → IO thread deserializes → emulator receives
  `InternalDiskImage` via `Task` completion.
- **Disk save:** Emulator serializes in-memory disk to `byte[]` (on its own thread),
  then enqueues the blob write to the IO thread.
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
- Multiple save requests from different sources (auto-save timer, explicit Ctrl+S,
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

Large blobs (disk images, ~230KB for 35-track disks) are read on the IO thread
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
dedicated IO thread where blocking is expected and acceptable. The caller sees
an async `Task<InternalDiskImage>` via the queue façade.

### 15.9 Transaction Boundaries

All transactions execute on the IO thread. A request may span multiple SQL
statements within a single transaction:

- **Project save**: Single transaction wrapping all dirty disk writes + metadata updates.
- **Disk import**: Single transaction for INSERT (blob + metadata).
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
3. Settings & project overrides — enables project-specific emulator/display configuration.
4. UI — Start Page & project dialogs — makes the project system user-accessible.
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
   - table name constants.

4. **Implement `ISchemaMigration` interface and `V1_InitialSchema`.**
   - `Pandowdy.Project/Migrations/ISchemaMigration.cs` — `FromVersion`, `ToVersion`, `Apply()`.
   - `Pandowdy.Project/Migrations/V1_InitialSchema.cs` — DDL for the 6 V1 tables:
     `project_metadata`, `disk_images`, `mount_configuration`, `emulator_overrides`,
     `display_overrides`, `project_settings`.
   - Deferred tables (breakpoints, watches, symbols, disassembly_cache, applesoft_sources,
     execution_history, workspace_layout, user_annotations) are **not** created in V1.
     They will be added by future migrations when their features are implemented.

5. **Implement `SkilletSchemaManager`.**
   - `Pandowdy.Project/Services/SkilletSchemaManager.cs`
   - `InitializeSchemaAsync(SqliteConnection)` — sets pragmas, runs V1 migration.
   - `MigrateAsync(SqliteConnection, int currentVersion)` — sequential migration runner.
   - Seeds `project_metadata` rows (name, timestamps, schema version, Pandowdy version).
   - Seeds default `mount_configuration` (slot 6, drives 1 & 2, empty).

6. **Implement PIDI blob serializer (`DiskBlobStore`).**
   - `Pandowdy.Project/Services/DiskBlobStore.cs`
   - `Serialize(InternalDiskImage) → byte[]` — PIDI header (10 bytes, uncompressed) +
     Deflate-compressed per-track payload + CRC-32 footer.
   - `Deserialize(byte[]) → InternalDiskImage` — validates magic and CRC-32,
     decompresses Deflate payload, reconstructs tracks.
   - `Deserialize(Stream) → InternalDiskImage` — streaming variant for SQLite blob reads.
   - Compression: `DeflateStream` with `CompressionLevel.Optimal` (`System.IO.Compression`,
     built into .NET runtime — no new package).
   - Uses `System.IO.Hashing.Crc32` (already in `Pandowdy.EmuCore`'s dependency graph).

7. **Implement `ProjectSettingsStore`.**
   - `Pandowdy.Project/Services/ProjectSettingsStore.cs`
   - Generic key-value CRUD against any `(key TEXT, value TEXT)` table.
   - `GetAsync(connection, tableName, key) → string?`
   - `SetAsync(connection, tableName, key, value)` — UPSERT via `INSERT OR REPLACE`.
   - `GetAllAsync(connection, tableName) → Dictionary<string, string>`
   - `RemoveAsync(connection, tableName, key)`

8. **Implement `ISkilletProject` / `SkilletProject`.**
   - `Pandowdy.Project/Interfaces/ISkilletProject.cs` — interface per §5.5.
   - `Pandowdy.Project/Services/SkilletProject.cs` — implementation.
   - Phase 1 scope: metadata queries, settings CRUD, disk image CRUD (metadata rows +
     blob read/write), mount configuration CRUD.
   - Debugger, AppleSoft, and workspace layout methods are not included in the Phase 1
     interface. They will be added to `ISkilletProject` when their features and schema
     tables are implemented in deferred phases.

9. **Implement `ISkilletProjectManager` / `SkilletProjectManager`.**
   - `Pandowdy.Project/Interfaces/ISkilletProjectManager.cs` — interface per §5.6.
   - `Pandowdy.Project/Services/SkilletProjectManager.cs` — implementation.
   - `CreateAsync(filePath, projectName)` — creates file, runs schema init, returns handle.
   - `OpenAsync(filePath)` — validates application_id, runs migrations if needed, returns handle.
   - `CloseAsync()` — disposes current project.

10. **Add project references from host and UI projects.**
    - `Pandowdy.csproj` → `<ProjectReference>` to `Pandowdy.Project`.
    - `Pandowdy.UI.csproj` → `<ProjectReference>` to `Pandowdy.Project`.

11. **Write unit tests for all Phase 1 deliverables.**
    - `SkilletSchemaManagerTests.cs` — schema creation, pragma verification, table existence.
    - `DiskBlobStoreTests.cs` — serialize/deserialize round-trip, CRC validation, corrupt data.
    - `ProjectSettingsStoreTests.cs` — get/set/remove/upsert across key-value tables.
    - `SkilletProjectTests.cs` — disk image CRUD (metadata), mount config CRUD.
    - `SkilletProjectManagerTests.cs` — create/open/close lifecycle, invalid file rejection.
    - Use in-memory SQLite (`Data Source=:memory:`) for unit tests.
    - Use temp files for lifecycle tests (create → close → reopen → verify).

#### Deliverables

| Artifact | State |
|----------|-------|
| `Pandowdy.Project/` class library | New project, compiles, in solution |
| `Pandowdy.Project.Tests/` test project | New project, compiles, all tests green |
| Schema creation | 6 V1 tables created, pragmas verified |
| PIDI blob round-trip | `InternalDiskImage` → blob → `InternalDiskImage`, binary-identical |
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

#### Steps

1. **Implement disk import flow in `SkilletProject`.**
   - `ImportDiskImageAsync(filePath, name)` — uses existing `IDiskImageImporter` to load
     the file, serializes via `DiskBlobStore.Serialize()`, inserts both `original_blob` and
     initial `working_blob` into `disk_images` table.
   - Detects format via `DiskFormatHelper.FromExtension()`.
   - Records `original_filename`, `original_format`, `import_source_path`.

2. **Implement disk mount flow (blob → emulator).**
   - `SkilletProject.OpenWorkingBlobRead(diskImageId)` — streams `working_blob` (falls
     back to `original_blob` if null).
   - Deserializes blob → `InternalDiskImage` via `DiskBlobStore.Deserialize()`.
   - Add `MountDiskMessage(int DriveNumber, InternalDiskImage DiskImage)` to
     `Pandowdy.EmuCore/DiskII/Messages/`.
   - Implement `MountDiskMessage` handling in `DiskIIControllerCard` (both 13- and 16-sector
     variants) — same logic as `InsertDiskMessage` but receives `InternalDiskImage` directly
     instead of a file path.

3. **Implement disk export flow.**
   - Add `ExportDiskMessage(int DriveNumber, string FilePath, DiskFormat Format)` to
     `Pandowdy.EmuCore/DiskII/Messages/`.
   - Implement handler: retrieves in-memory `InternalDiskImage` from drive, uses existing
     `IDiskImageExporter` to write to filesystem.
   - No project state changes — export is non-destructive.

4. **Implement working copy persistence in `SkilletProject.SaveAsync()`.**
   - For each disk with `persist_working = true` AND `working_dirty = true`:
     serialize current in-memory `InternalDiskImage` → `UPDATE disk_images SET working_blob = ?`.
   - Clear `working_dirty` flag after successful write.
   - Wrap in transaction with metadata timestamp update.

5. **Implement working copy regeneration.**
   - `RegenerateWorkingCopyAsync(diskImageId)` — sets `working_blob = NULL`,
     `working_dirty = 0`.
   - Next mount reads from `original_blob` instead.

6. **Write tests for disk lifecycle.**
   - `DiskImportTests.cs` — import WOZ/nibble format/DSK files, verify blob stored correctly.
   - `DiskMountTests.cs` — mount from blob, verify `InternalDiskImage` is usable.
   - `DiskExportTests.cs` — export in-memory disk to file, verify output format.
   - `DiskPersistenceTests.cs` — modify disk, save project, reopen, verify working copy.
   - `DiskRegenerationTests.cs` — regenerate working copy, verify reverts to original.
   - `DiskImportExportRoundTripTests.cs` — full import → mount → modify → save → reopen
     → export cycle.

#### Deliverables

| Artifact | State |
|----------|-------|
| `MountDiskMessage` | New message type in EmuCore |
| `ExportDiskMessage` | New message type in EmuCore |
| Disk import into `.skillet` | External file → blob, metadata recorded |
| Disk mount from `.skillet` | Blob → `InternalDiskImage` → emulator drive |
| Working copy save/restore | Dirty disks persist across sessions |
| Export to filesystem | In-memory disk → external file format |

#### Test Gate

All disk lifecycle tests pass. Blob round-trip is binary-identical. Import/export
works for all supported formats (WOZ, nibble format, DSK/DO/PO).

---

### Phase 3: Settings Resolution & Project Overrides

**Goal:** Implement the four-layer settings resolution model so that emulator
and display settings can be overridden per-project. Add `RecentProjects` and
`ActiveProjectPath` to `GuiSettings`.

**Depends on:** Phase 1 (project settings store).

#### Steps

1. **Implement `ISettingsResolver` / `SettingsResolver`.**
   - `Pandowdy.Project/Interfaces/ISettingsResolver.cs` — interface per §8.
   - `Pandowdy.Project/Services/SettingsResolver.cs` — implementation.
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

### Phase 4: UI — Start Page & Project Dialogs

**Goal:** Give users a way to create, open, and switch `.skillet` projects through
the UI. The Start Page replaces the current "empty launch" state. The File menu
gains project-oriented commands.

**Depends on:** Phases 1–3 (project manager, settings, recent projects).

#### Steps

1. **Create `StartPageViewModel`.**
   - `Pandowdy.UI/ViewModels/StartPageViewModel.cs`
   - Exposes `RecentProjects` collection, `CreateProjectCommand`, `OpenProjectCommand`.
   - Receives `ISkilletProjectManager` and `GuiSettingsService` via DI.

2. **Create `StartPage.axaml`.**
   - `Pandowdy.UI/Views/StartPage.axaml` + code-behind.
   - Layout per §9.1: "Create New Project" panel + "Recent Projects" list.

3. **Create `NewProjectDialogViewModel` and dialog.**
   - `Pandowdy.UI/ViewModels/NewProjectDialogViewModel.cs`
   - `Pandowdy.UI/Views/NewProjectDialog.axaml` + code-behind.
   - Fields: project name, folder location, file name.

4. **Modify `MainWindow` to show Start Page when no project is open.**
   - Add a `ContentControl` that switches between Start Page and the main workspace.
   - When `ISkilletProjectManager.CurrentProject` is null → show Start Page.

5. **Implement startup flow.**
   - On launch: read `ActiveProjectPath` from `GuiSettings`.
   - If file exists → auto-open project, skip Start Page.
   - If not → show Start Page.

6. **Update File menu.**
   - Add: New Project, Open Project, Save Project, Import Disk Image, Export Disk Image,
     Close Project, Recent Projects submenu.
   - Wire commands to `ISkilletProjectManager` and `ISkilletProject`.

7. **Update `DiskStatusWidgetViewModel` commands.**
   - "Insert Disk" → opens file dialog, calls `ISkilletProject.ImportDiskImageAsync()`,
     then mounts via `MountDiskMessage`.
   - "Save" → `ISkilletProject.SaveAsync()` (persists working copy to `.skillet`).
   - "Save As" → becomes "Export Disk" → `ExportDiskMessage`.
   - "Insert Blank Disk" → creates blank `InternalDiskImage`, inserts into project, mounts.

8. **Write tests for Start Page and project commands.**
   - `StartPageViewModelTests.cs` — recent projects list, create/open commands.
   - `NewProjectDialogViewModelTests.cs` — validation, path generation.

#### Deliverables

| Artifact | State |
|----------|-------|
| Start Page | New view + view model, shown on launch |
| New Project Dialog | New dialog for creating `.skillet` files |
| File menu | Project-oriented commands |
| Disk widget commands | Updated for import/mount/export workflow |

#### Test Gate

Start Page renders. Project creation flow works end-to-end. File menu commands
function. Existing UI tests still pass.

---

### Phase 5: Legacy Cleanup

**Goal:** Remove dead code paths that are fully superseded by the `.skillet`
workflow. This phase runs after Phases 2–4 are proven working.

**Depends on:** Phases 2 and 4 (disk lifecycle and UI are fully wired).

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

5. **Remove legacy `InsertDiskMessage` (file-path variant).**
   - Evaluate whether `InsertDiskMessage(DriveNumber, DiskImagePath)` is still needed.
   - If all insertion now flows through `MountDiskMessage` → remove it.
   - If retained for future loose-disk mode → mark with `[Obsolete]` and document intent.

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
   - Time serialize/deserialize for a 35-track disk (~230KB).
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

| Phase | Priority | Depends On | Core Deliverable |
|-------|----------|------------|------------------|
| 1. Foundation | **P0** | — | Project + schema + blob store + lifecycle |
| 2. Disk Lifecycle | **P0** | Phase 1 | Import / mount / export / persist |
| 3. Settings | **P1** | Phase 1 | Four-layer resolution + recent projects |
| 4. UI | **P1** | Phases 1–3 | Start Page + project dialogs + updated menus |
| 5. Legacy Cleanup | **P2** | Phases 2, 4 | Remove dead code (SaveDisk*, DriveState*) |
| 6. Integration Testing | **P2** | Phases 1–5 | End-to-end validation |

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

*Document Created: 2026-07-15*
*Based on: docs/Skillet_Project_File_Development.md*
