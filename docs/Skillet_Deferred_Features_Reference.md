# Pandowdy `.skillet` — Deferred Features Reference

> **Parent Document:** `docs/Skillet_Development_Blueprint.md`  
> **Purpose:** Reference designs for features that will be implemented after the
> core `.skillet` project system (Phases 1–6) is complete. Content here is
> **speculative** — schemas, models, and workflows may change as feature
> requirements are finalized.  
> **Status:** DRAFT — Not yet scheduled for implementation.

---

## Table of Contents

1. [Deferred Schema Tables](#1-deferred-schema-tables)
2. [Deferred C# Data Models](#2-deferred-c-data-models)
3. [Deferred ISkilletProject Interface Members](#3-deferred-iskilletproject-interface-members)
4. [Debugger Subsystem Storage Model](#4-debugger-subsystem-storage-model)
5. [AppleSoft Subsystem Storage Model](#5-applesoft-subsystem-storage-model)
6. [Workspace / UI Layout Persistence — Skillet Layer](#6-workspace--ui-layout-persistence--skillet-layer)
7. [Project-Type Flexibility](#7-project-type-flexibility)
8. [Deferred Setting Classifications](#8-deferred-setting-classifications)
9. [Deferred Implementation Phases](#9-deferred-implementation-phases)

---

## 1. Deferred Schema Tables

The following table definitions are **reference designs** — they document the
current thinking for future features but will **not** be created in the V1 schema.
Each will be created by its own schema migration when the corresponding feature
is implemented. Schemas may change as feature requirements are finalized.

### Table: `breakpoints` *(Debugger, Phase A)*

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

### Table: `watches` *(Debugger, Phase A)*

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

### Table: `symbols` *(Debugger, Phase A)*

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

### Table: `disassembly_cache` *(Debugger, Phase A)*

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

### Table: `applesoft_sources` *(AppleSoft, Phase B)*

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

### Table: `execution_history` *(Debugger, Phase A)*

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

### Table: `workspace_layout` *(Workspace, Phase C)*

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

### Table: `user_annotations` *(TBD)*

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

## 2. Deferred C# Data Models

These models will be added to `Pandowdy.Project/Models/` when their respective
features are implemented.

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

## 3. Deferred ISkilletProject Interface Members

The following methods will be added to `ISkilletProject` when their features
and schema tables are implemented:

```csharp
// Debugger state (Phase A)
Task<IReadOnlyList<BreakpointRecord>> GetBreakpointsAsync();
Task UpsertBreakpointAsync(BreakpointRecord breakpoint);
Task RemoveBreakpointAsync(long id);

Task<IReadOnlyList<WatchRecord>> GetWatchesAsync();
Task UpsertWatchAsync(WatchRecord watch);
Task RemoveWatchAsync(long id);

Task<IReadOnlyList<SymbolRecord>> GetSymbolsAsync();
Task UpsertSymbolAsync(SymbolRecord symbol);
Task ImportSymbolFileAsync(Stream symbolData, string format);

// Workspace layout (Phase C)
Task<WorkspaceLayout> GetWorkspaceLayoutAsync();
Task SaveWorkspaceLayoutAsync(WorkspaceLayout layout);
```

---

## 4. Debugger Subsystem Storage Model

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

## 5. AppleSoft Subsystem Storage Model

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

## 6. Workspace / UI Layout Persistence — Skillet Layer

### Per-Project Overrides

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

## 7. Project-Type Flexibility

The spec defines three project styles. The `.skillet` schema handles all without
branching — tables that aren't used are simply empty, `workspace_layout` controls
which panels are visible, and no `project_type` discriminator is needed.

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

---

## 8. Deferred Setting Classifications

The following settings are tied to deferred features. They will be added to the
Setting Classification table in the blueprint when implemented:

| Setting | Layer | Table |
|---------|-------|-------|
| Panel visibility | JSON (default), Skillet (override) | `workspace_layout` |
| Breakpoints | Skillet only | `breakpoints` |
| Watches | Skillet only | `watches` |
| Symbols | Skillet only | `symbols` |
| AppleSoft sources | Skillet only | `applesoft_sources` |
| User annotations | Skillet only | `user_annotations` |

---

## 9. Deferred Implementation Phases

### Phase A: Debugger Storage Integration

**Deferred because:** The debugger subsystem (Task 19/22) does not yet have
breakpoint management, watch panels, or symbol table UI. Implementing persistence
for data that cannot yet be created or displayed adds no user value.

**Trigger:** Begin when Task 19 implements breakpoint add/remove/toggle UI.

**Scope when triggered:**
1. Design final schema for debugger tables based on actual debugger requirements.
2. Implement schema migration (V*n*) to create `breakpoints`, `watches`, `symbols`,
   `disassembly_cache`, and `execution_history` tables.
3. Implement breakpoint/watch/symbol CRUD in `SkilletProject`.
4. On project open: load debugger state → feed into debugger subsystem.
5. On project save: persist debugger state from in-memory to `.skillet`.
6. Wire disassembly cache into `Pandowdy.Disassembler` for warm-start.

**Tables:** Created by migration when this phase begins (see §1 reference designs).

### Phase B: AppleSoft Subsystem Storage

**Deferred because:** No AppleSoft subsystem exists yet. No tokenizer, detokenizer,
parser, or editor has been implemented.

**Trigger:** Begin when AppleSoft task is started.

**Scope when triggered:**
1. Design final schema for `applesoft_sources` based on actual subsystem requirements.
2. Implement schema migration (V*n*) to create `applesoft_sources` table.
3. Implement AppleSoft source CRUD in `SkilletProject`.
4. Store tokenized blobs, detokenized source, AST JSON, semantic JSON.
5. Link sources to disk images via foreign key.
6. Editor state persistence in `workspace_layout`.

**Tables:** Created by migration when this phase begins (see §1 reference design).

### Phase C: Workspace Layout Persistence

**Deferred because:** The current UI has limited docking/panel state. Meaningful
workspace layout persistence requires the debugger panels, editor panels, and
other UI elements that don't exist yet.

**Trigger:** Begin when multiple resizable/dockable panels are implemented.

**Scope when triggered:**
1. Design final schema for workspace layout and annotation tables.
2. Implement schema migration (V*n*) to create `workspace_layout` and
   `user_annotations` tables.
3. Implement `WorkspaceLayout` load/save in `SkilletProject`.
4. On project open: restore panel visibility, sizes, docking arrangement.
5. On project close: capture current UI state → save to `.skillet`.
6. Handle missing layout gracefully (fall back to JSON defaults → hardcoded defaults).
7. Implement per-project emulator/display override UI (checkbox: "Override for this project").

**Tables:** Created by migration when this phase begins (see §1 reference designs).

---

*Extracted from: docs/Skillet_Development_Blueprint.md*  
*Document Created: 2026-07-15*
