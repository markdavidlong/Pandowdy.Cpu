# Pandowdy Workstation Architecture & Project Model  

---

## 1. Introduction  

Pandowdy is evolving from a traditional Apple II emulator into a full workstation 
for analysis, debugging, and development. Central to this evolution is the 
introduction of the `.skillet` project file, which serves as the authoritative 
container for all project‑specific state. A `.skillet` file is implemented as a 
**SQLite database**, providing a structured, transactional, and portable foundation 
for storing disk images, metadata, debugger state, AppleSoft ASTs, annotations, 
and other workstation data.

This document defines the conceptual architecture of the new project‑centric model, 
outlines the philosophy behind the `.skillet` system, and describes how Pandowdy will 
manage state, disk lifecycles, and advanced tooling within this framework.

Because Pandowdy remains in **pre‑release**, this transition is implemented as a 
**clean architectural break**. No backward compatibility with earlier file‑centric 
workflows is required or maintained.

---

## 2. Philosophy of the Pandowdy Workstation  
Pandowdy is no longer "an emulator that opens disk images." It is a **project‑based Apple II development and 
analysis environment**. The `.skillet` file is the authoritative container for:

- Disk images (original + working copies)
- Debugger state
- Symbol tables
- AppleSoft source, ASTs, and semantic data
- Disassembly caches
- Profiling data
- User annotations
- Per‑project UI layout
- Per‑project emulator configuration

The `.skillet` is the **source of truth**.
External disk images become **imported artifacts**, not live dependencies.

---

## 3. No Legacy Mode: A Clean Break  
Pandowdy is still in pre‑release, so this transition is implemented as a clean break:

- The old workflow of opening, modifying, and saving disk image files is removed.
- The new workflow is based entirely on importing disk images into a .skillet project.
- All disk modifications occur internally within the project.
- Exporting is the only way to write disk images back to the filesystem.
- No backward compatibility with earlier versions is required.

Pandowdy always has an active project. When no named project file is open, the
system automatically creates an **ad hoc project** — an in‑memory SQLite database
(`Data Source=:memory:`) that behaves identically to any file‑based `.skillet`
project. The ad hoc project can be persisted to a file at any time via
"Save Project As...". This replaces the previously envisioned "loose‑disk mode"
with a simpler, unified model: there is always a project, and it always supports
the full feature set.

---

## 4. Startup Workflow  
On launch, Pandowdy checks for a previously active project:

1. If an `active_project_path` exists in settings and the file is present,
   the project is opened automatically and the workspace is shown.
2. Otherwise, an **ad hoc project** (in‑memory SQLite) is created automatically
   and the workspace is shown immediately.

The emulator workspace is **always accessible** — even with an ad hoc project,
users can import disk images, mount them, and use the emulator. There is no
gated Start Page that blocks the workspace.

An optional **Start Page** panel (implemented in a later phase) provides
project management actions: "Create New Project", "Open Project", and
"Recent Projects". It is an overlay or sidebar, not a prerequisite for
using the emulator.

The key invariant: `ISkilletProjectManager.CurrentProject` is **never null**.
This eliminates null guards throughout the codebase and simplifies DI wiring.

---

## 5. JSON vs `.skillet`: A Two‑Layer State Model

### 5.1 JSON = Global Defaults (Workstation Environment)  
JSON files store user‑specific, environment‑specific preferences:

- Window geometry
- Panel visibility and layout
- Display preferences (scanlines, monochrome, fringing reduction)
- Global emulator defaults (throttle, caps lock)
- Recently used .skillet files
- Recently imported disk images
- Active project path

JSON represents **how Pandowdy behaves by default**.

### 5.2 `.skillet` = Project Overrides (Workstation State)  
The `.skillet` stores project‑specific configuration:

- Disk originals and working copies
- Mount configuration
- Project‑specific UI layout (open files, panel arrangement)
- Per‑project emulator overrides
- Breakpoints, watches, and debugger state
- Symbol tables
- AppleSoft source, ASTs, and semantic data
- Disassembly caches
- Profiling data
- User annotations
- Other project-related data to be determined

The .skillet represents what the project is.

### 5.2.1 `.skillet` File Format  
The .skillet project file is a **SQLite database**.
It stores structured tables for disk images, metadata, debugger state, 
AppleSoft ASTs, annotations, profiling data, and project‑specific UI layout.

SQLite provides ACID guarantees, cross‑platform portability, and efficient storage of both structured data and binary blobs.

### 5.3 Resolution Order

1. Hard-coded defaults (failsafe default values when needed, such as for geometry and default emulator and operational defaults)
2. JSON global defaults
3. `.skillet` project overrides
4. Runtime user changes (persisted to the appropriate layer)

---

## 6. Disk Lifecycle in the Project‑Centric Model

### 6.1 Import  
When a disk image is imported:

- The pristine original is stored internally (immutable).
- A working copy is created (mutable).
- Metadata about the import is recorded.

### 6.2 Internal Use  
All reads/writes occur against the working copy.
The original is never modified.

### 6.3 Persistence  
Saving the project may persist the working copy into the .skillet. However, the project 
configuration will determine on a disk image-specific basis whether the working copy 
is persisted or recreated periodically, as some projects might want to consider changes
made during the session as "throwaway" changes.

Mounted disk images (currently in use by the emulator) are serialized using a
snapshot‑under‑lock strategy that does not require pausing or interrupting the
emulator. The emulator continues running throughout the save operation.
See `docs/Skillet_Development_Blueprint.md` §5.3 for implementation details.

### 6.4 Regeneration  
The working copy can be regenerated from the original at any time.

### 6.5 Export  
Users may export:

- the original
- the working copy
- in any supported format
  - it is conceivable that the original might not be able to be exported in the same format as it were imported.  In that case a near approximation may be used (for instance a nibble format file in lieu of a read-only .woz file)
Export is:

- explicit
- user‑initiated
- non‑destructive
- not part of the project’s internal lifecycle

### 6.6 Removal of Legacy Save Logic  

The old “Save As…” filename logic (_new, _new2, etc.) is obsolete.

Disk images are no longer saved directly to the filesystem except via explicit export.

---

## 7. AppleSoft Subsystem  
As these features are developed, the `.skillet` will store:

- De‑tokenized source
- Tokenized representation
- AST (JSON or structured tables)
- Semantic inference (variable types, const detection, flow analysis)
- Editor state (cursor, folds, open files)

This enables:

- static analysis
- cross‑referencing
- symbolic debugging
- project‑specific editor layout

---

## 8. Debugger Subsystem  
As the features are developed, the `.skillet` will store:

Breakpoints
- Watch expressions
- Execution history (optional)
- Symbol tables
- Memory maps
- Disassembly caches

This enables reproducible debugging sessions and project‑specific debugging context.

---

## 9. UI Layout and Workspace State  
The .skillet stores project‑specific UI state:

- Which files are open
- Which panels are visible
- Panel docking and arrangement
- Emulator or editor window visibility (some projects may not use the emulator or editors, etc.)

JSON stores global UI defaults.

The `.skillet` stores project‑specific overrides.

---

## 10. Project Types  
Pandowdy supports multiple project styles:

- Emulator‑centric projects (full workstation mode)
- Editor‑centric projects (little emulator usage required)
- Multi‑disk projects
- Combinations of some or all of these styles

The `.skillet` architecture supports all of these without branching logic.

---

## 11. Ad Hoc Project Model  
The previously envisioned "loose‑disk mode" has been superseded by the **ad hoc
project** — an in‑memory `.skillet` that is always created when no named project
is open:

- Backed by `Data Source=:memory:` (SQLite in‑memory database)
- Full V1 schema — all features available (import, mount, settings overrides)
- Non‑persistent by default (data is lost when the connection is closed)
- Persistable via "Save Project As..." using `VACUUM INTO` to atomically
  copy the in‑memory database to a new file on disk
- The `SkilletProject` instance is preserved across the Save As transition;
  only the internal `SqliteConnection` and `FilePath` / `IsAdHoc` properties
  change. References held by other services remain valid.
- The ad hoc project name is `"untitled"`

This unified model eliminates the need for a separate "limited mode" and ensures
that all code paths operate against a single project abstraction.

---

## 12. Summary  
Pandowdy is transitioning to a **project‑centric workstation architecture**.

The `.skillet` file, implemented as a SQLite database, becomes the 
authoritative container for all project state.

Pandowdy **always** has an active project. When no named project file is open,
an in‑memory ad hoc project provides the full feature set. The ad hoc project
can be persisted to a file at any time via "Save Project As...".

JSON files store global defaults and workstation preferences.

Disk images are imported, internalized, and exported — never saved directly.

This is a clean break from the old model, enabled by pre‑release development.

The new architecture enables advanced features such as AST analysis, symbolic debugging, 
profiling, annotations, and reproducible multi‑disk workflows.

---

You are reading the Pandowdy Workstation Architecture & Project Model document.
Treat this document as the authoritative design specification.
Your task is to generate implementation details, schemas, code structures, and
migration logic that follow the philosophy and architecture described.

Do not reinterpret or modify the design philosophy.
Do not reintroduce legacy file-centric workflows.
All advanced features must operate through the .skillet project model.
JSON represents global defaults; .skillet represents project state.

Your output should include:
- SQLite schema proposals
- C# data models
- Import/export logic
- Project load/save logic
- UI workflow outlines
- Any additional implementation details needed

Follow the document strictly.
