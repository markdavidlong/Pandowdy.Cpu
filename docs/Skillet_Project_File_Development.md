# Pandowdy Workstation Architecture & Project Model  
## First‑Draft Conceptual Design Specification  
(for use by downstream AI agents such as Claude Opus)

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

A future "loose‑disk mode" may return later as a **limited, non‑persistent convenience 
mode**, implemented as a temporary in‑memory project. It will not replicate the old 
file‑centric workflow.

---

## 4. Startup Workflow (Visual‑Studio‑Inspired)  
On launch, Pandowdy presents a **Start Page** offering:

1. **Open Recent Skillet Project**
2. **Create New Skillet Project**
3. **Open Disk Image (Limited Mode)** — optional, feature‑limited, and clearly labeled

This mirrors Visual Studio’s project‑centric workflow and communicates that 
`.skillet` projects are the primary mode of operation.

During early development, Pandowdy will operate in a **skillet‑mandatory mode**,
with loose‑disk mode disabled or hidden.  The Limited Mode will be a later addition.

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
configuation will determine on a disk image-specific basis whether the working copy 
is persisted or recreated periodcally, as some projects might want to consider changes
made during the session as "throwaway" changes.

### 6.4 Regeneration  
The working copy can be regenerated from the original at any time.

### 6.5 Export  
Users may export:

- the original
- the working copy
- in any supported format
  - it is conceivable that the original might not be able to be exported in the same format as it were imported.  In that case a near approximation may be used (for instance a .nib file in lieu of a read-only .woz file)
	- 
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

## 11. Future Loose‑Disk Mode  
Loose‑disk mode may be reintroduced later as:

- a temporary in‑memory `.skillet`
- non‑persistent
- feature‑limited
- intended for quick testing

It will not replicate the old file‑centric workflow.

---

## 12. Summary  
Pandowdy is transitioning to a **project‑centric workstation architecture**.

The `.skillet` file, implemented as a SQLite database, becomes the 
authoritative container for all project state.

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
