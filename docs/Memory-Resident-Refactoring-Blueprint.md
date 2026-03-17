# Memory-Resident Refactoring — Implementation Blueprint

**Date:** 2026-03-14  
**Purpose:** Step-by-step execution checklist for the refactoring defined in `Memory-Resident-Project-Refactoring-Plan.md` (the "Plan").  
**Audience:** Implementation agent (Claude Sonnet). Follow these steps sequentially. Do not skip, reorder, or combine steps unless explicitly noted.

**Authoritative source:** The Plan is the source of truth for all type definitions, pseudocode, behavioral specs, and design rationale. This blueprint tells you *what to do and when*; the Plan tells you *what to write and why*.

---

## Conventions

- **`(Plan: "Section")`** — cross-reference into the Plan. Read the cited section for full type definitions, pseudocode, or rationale before implementing.
- **`[VERIFY]`** — stop and run the indicated verification before proceeding.
- **`[GIT COMMIT POINT]`** — notify the user that now is a good time to commit. Include the suggested message. **Do not execute any git commands.** Wait for the user to confirm before proceeding.
- **`[GIT WARNING]`** — do NOT commit at this point; the codebase is in a transitional state.
- All new C# files must follow `.github/copilot-instructions.md`: curly braces on all control statements, primary constructors where straightforward, `_camelCase` private fields, PascalCase public members, nullable reference types enabled.
- All file paths are relative to the `Pandowdy.Project/` project unless otherwise qualified.
- **Ambiguity policy:** If any step is unclear, contradicts the Plan, or requires a judgment call not covered by either document, **stop and ask the user for clarification** before proceeding. Do not guess or improvise. A wrong assumption mid-rewrite is far more expensive than a pause.

---

## ✅ Phase 0: .NET 10 TFM Upgrade

> This is a mechanical prerequisite. Do it first so that all subsequent code targets .NET 10.

### ✅ Step 0.1 — Upgrade all solution-level `.csproj` files

Change `<TargetFramework>net8.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>` in every `.csproj` file referenced by `Pandowdy.sln`. The projects are:

| Project | Path |
|---------|------|
| Pandowdy (host) | `Pandowdy/Pandowdy.csproj` |
| Pandowdy.UI | `Pandowdy.UI/Pandowdy.UI.csproj` |
| Pandowdy.UI.Tests | `Pandowdy.UI.Tests/Pandowdy.UI.Tests.csproj` |
| Pandowdy.EmuCore | `Pandowdy.EmuCore/Pandowdy.EmuCore.csproj` |
| Pandowdy.EmuCore.Tests | `Pandowdy.EmuCore.Tests/Pandowdy.EmuCore.Tests.csproj` |
| Pandowdy.Cpu | `Pandowdy.Cpu/Pandowdy.Cpu/Pandowdy.Cpu.csproj` |
| Pandowdy.Disassembler | `Pandowdy.Disassembler/Pandowdy.Disassembler.csproj` |
| Pandowdy.Disassembler.Tests | `Pandowdy.Disassembler.Tests/Pandowdy.Disassembler.Tests.csproj` |
| Pandowdy.Project | `Pandowdy.Project/Pandowdy.Project.csproj` |
| Pandowdy.Project.Tests | `Pandowdy.Project.Tests/Pandowdy.Project.Tests.csproj` |

> **Do NOT modify** the `legacy/CiderPress2/` `.csproj` files — those are third-party vendored code.

> **Also check** `Pandowdy.Cpu/Directory.Build.props` for a TFM declaration and update it if present.

> **`DiskImportCode`**: If `Pandowdy.DiskImportCode/` has a `.csproj`, include it in this step too. Search for it.

### ✅ Step 0.2 — [VERIFY] Build the solution

```
dotnet build Pandowdy.sln
```

Confirm zero errors. Fix any TFM-related issues (e.g., NuGet package version incompatibilities with .NET 10). If a package doesn't yet support .NET 10, check for a preview or RC version.

### ✅ Step 0.3 — [VERIFY] Run all tests

```
dotnet test Pandowdy.sln
```

Confirm all tests pass. The TFM change should not cause test failures, but verify.

### ✅ Step 0.4 — [GIT COMMIT POINT]

Notify the user:

> **Good time to commit.** The .NET 10 TFM upgrade is complete — all projects build and tests pass.  
> Suggested message: `chore: upgrade all projects from net8.0 to net10.0`

---

## ✅ Phase 1: Define Storage Abstraction

> All steps in this phase create new files. No existing code is modified. **(Plan: "Phase 1: Define Storage Abstraction")**

### ✅ Step 1.1 — Create `Models/SettingsScope.cs`

Create the `SettingsScope` enum. **(Plan: "SettingsScope Enum")** — copy the full type definition including XML doc comments.

### ✅ Step 1.2 — Create `Models/SavePolicy.cs`

Create the `SavePolicy` enum. **(Plan: "Per-disk save policy" → `SavePolicy` code block)** — copy the full type definition including XML doc comments.

### ✅ Step 1.3 — Create `Models/BlobSaveMode.cs`

Create the `BlobSaveMode` enum. **(Plan: "Layer 3: IProjectStore" → `BlobSaveMode` code block)** — copy the full type definition including XML doc comments.

### ✅ Step 1.4 — Create `Models/BlobVersion.cs`

Create the `BlobVersion` static class. **(Plan: "Layer 3: IProjectStore" → `BlobVersion` code block)** — copy the full type definition.

### ✅ Step 1.5 — Create `Models/AttachmentRecord.cs`

Create the `AttachmentRecord` sealed record. **(Plan: "Attachments (Non-Disk Files)" → `AttachmentRecord` code block)** — copy the full type definition including XML doc comments.

### ✅ Step 1.6 — Create `Models/ProjectManifest.cs`

Create the `ProjectManifest` sealed class. **(Plan: "Layer 3: IProjectStore" section → search for the `ProjectManifest` class definition)** — copy the full type definition. This references `ProjectMetadata`, `DiskImageRecord`, `AttachmentRecord`, `MountConfiguration` — all of which already exist or were created in prior steps.

### ✅ Step 1.7 — Create `Models/ProjectSnapshot.cs`

Create the `ProjectSnapshot` sealed class. **(Plan: "Layer 3: IProjectStore" section → search for the `ProjectSnapshot` class definition)** — copy the full type definition.

### ✅ Step 1.8 — Create `Constants/ManifestConstants.cs`

Create the `ManifestConstants` static class. **(Plan: "DirectoryProjectStore — First Implementation" → `ManifestConstants` code block)** — copy the full type definition. Must include `CurrentSchemaVersion = 1` and the `Disclaimer` constant.

### ✅ Step 1.9 — Create `Interfaces/IProjectStore.cs`

Create the `IProjectStore` interface. **(Plan: "Layer 3: IProjectStore — Storage Abstraction" → full `IProjectStore` code block)** — copy the complete interface definition with all XML doc comments. This is the largest new file in Phase 1. It includes: `Path`, `LoadManifest()`, `LoadBlob(...)`, `Save(...)`, `SaveBlob(...)`, `LoadAttachment(...)`, `SaveAttachment(...)`, `DeleteAttachment(...)`, and `IDisposable`.

### ✅ Step 1.10 — Create `Interfaces/IProjectStoreFactory.cs`

Create the `IProjectStoreFactory` interface. **(Plan: "Stores are created by a factory" → `IProjectStoreFactory` code block)** — copy the full type definition.

### ✅ Step 1.11 — [VERIFY] Build the solution

```
dotnet build Pandowdy.sln
```

Confirm zero errors. All files are additive — no existing code should be affected.

### ✅ Step 1.12 — [GIT COMMIT POINT]

Notify the user:

> **Good time to commit.** Phase 1 is complete — all storage abstraction interfaces and model types are in place. Solution compiles cleanly.  
> Suggested message: `feat(project): add storage abstraction interfaces and model types`

---

## Phase 2: Implement `DirectoryProjectStore`

> Creates the first concrete `IProjectStore` implementation plus its factory and tests. No existing code is modified. **(Plan: "Phase 2: Implement DirectoryProjectStore", "DirectoryProjectStore — First Implementation")**

### Step 2.1 ✅ — Create `Stores/` directory

Create the `Pandowdy.Project/Stores/` directory if it doesn't exist.

### Step 2.2 ✅ — Create `Stores/DirectoryProjectStore.cs`

Implement the `DirectoryProjectStore` class. **(Plan: "DirectoryProjectStore — First Implementation" for directory layout, "Phase 2: Implement DirectoryProjectStore" for method-by-method behavior)**

Key implementation details from the Plan:
- Constructor takes a `string path` (directory path); store is bound to that path.
- `Dispose()` is a no-op.
- `LoadManifest()`: reads `manifest.json` using `System.Text.Json`. Validates `SchemaVersion` against `ManifestConstants.CurrentSchemaVersion`. Self-heals version gaps (renumber blob files, rewrite manifest). Throws `InvalidOperationException` on malformed data.
- `LoadBlob(id, version)`: reads `disks/{id}_v{version}.pidi`. `BlobVersion.Latest` resolves to highest version file present. Throws `FileNotFoundException` if expected blob is missing.
- `LoadAttachment(id)`: reads `attachments/{id}.dat`. Throws `FileNotFoundException` if record exists but file is missing.
- `Save(snapshot)`: writes `manifest.json` + all resident blobs + attachment data. Performs orphan cleanup (delete files not in snapshot).
- `SaveBlob(id, data, mode)`: `OverwriteActive` overwrites latest pidi file; `CreateNewVersion` writes `disks/{id}_v{latest+1}.pidi`, then deserializes the full manifest, increments `HighestWorkingVersion` on the matching `DiskImageRecord`, and rewrites `manifest.json` — a read-modify-write cycle. **(Plan: "Manifest sync policy")**
- `SaveAttachment(id, data)`: writes `attachments/{id}.dat`.
- `DeleteAttachment(id)`: deletes `attachments/{id}.dat`.
- Uses a `static readonly JsonSerializerOptions` with `JsonStringEnumConverter`. PascalCase convention — no `JsonNamingPolicy.CamelCase`. **(Plan: "JSON serialization convention", "JSON serializer instance")**

### Step 2.3 ✅ — Create `Stores/DirectoryProjectStoreFactory.cs`

Implement the `DirectoryProjectStoreFactory` class. **(Plan: "Phase 2" → factory description)**
- `Open(path)`: validates directory exists + `manifest.json` exists, returns `new DirectoryProjectStore(path)`.
- `Create(path)`: validates path does NOT exist, creates the directory structure (`disks/`, `attachments/` subdirs), returns `new DirectoryProjectStore(path)`.

### Step 2.4 ✅ — Create `DirectoryProjectStoreTests.cs`

Create `Pandowdy.Project.Tests/Stores/DirectoryProjectStoreTests.cs`. **(Plan: "Phase 2 tests")**

Test coverage should include:
- Save then LoadManifest round-trip fidelity (metadata, settings, disk records, mount configs, attachments, notes)
- Save then LoadBlob round-trip for multiple disks and versions
- `BlobVersion.Latest` resolution
- Save then LoadAttachment / SaveAttachment / DeleteAttachment round-trip
- Factory `Open` with valid directory
- Factory `Open` with non-existent directory (should throw)
- Factory `Create` with new path (should create directory structure)
- Factory `Create` with existing path (should throw)
- Version gap self-healing (create files with gaps, verify renumbering on load)
- Orphan cleanup on Save (extra blob files not in snapshot are deleted)
- `manifest.json` format: PascalCase, enums as strings
- Schema version validation (mismatched version throws)

Use temp directories (`Path.GetTempPath()` + unique subdirectory). Clean up in `Dispose()`.

### Step 2.5 ✅ — [VERIFY] Build and run Phase 2 tests

```
dotnet build Pandowdy.sln
dotnet test Pandowdy.Project.Tests
```

Confirm zero build errors and all tests pass (including the new `DirectoryProjectStoreTests`).

### Step 2.6 ✅ — [GIT COMMIT POINT]

Notify the user:

> **Good time to commit.** Phase 2 is complete — `DirectoryProjectStore`, factory, and tests are all in place and passing.  
> Suggested message: `feat(project): implement DirectoryProjectStore with tests`

---

## Phases 3-6: Core Rewrite (Atomic Commit)

> ⛔ **[GIT WARNING] DO NOT COMMIT between any steps in this section.** Phases 3, 4, 5, and 6 form a single atomic commit. The codebase will not compile in intermediate states.
>
> **Step ordering rationale:** The steps below are ordered so that types and interfaces are updated *before* the code that depends on them. This avoids writing `SkilletProject` against stale type definitions and then fixing it later. The Phase numbers from the Plan are preserved in the headings for traceability, but the execution order within this atomic block is: **5 (partial) → 3 → 4 → 5 (remainder) → 6**.

### Phase 5a: Update Types and Interface First

> These steps update the types and interface that `SkilletProject` and `SkilletProjectManager` depend on. Doing them first means Step 3.1 can be written against the correct definitions on the first pass. **(Plan: "Phase 5: Update ISkilletProject Interface")**

#### Step 5a.1 ✅ — Update `Interfaces/ISkilletProject.cs`

- `string? FilePath` (make nullable) — sourced from `_store?.Path`.
- `bool IsAdHoc` — `_store == null`.
- Change `GetSettingAsync(string tableName, string key)` → `GetSettingAsync(SettingsScope scope, string key)`.
- Change `SetSettingAsync(string tableName, string key, string value)` → `SetSettingAsync(SettingsScope scope, string key, string value)`.
- Add `Task SaveAsAsync(IProjectStore newStore)`.
- Remove `RegenerateWorkingCopyAsync`.
- Remove `WriteWorkingBlobAsync`.

**(Plan: "Phase 5: Update ISkilletProject Interface")**

#### Step 5a.2 ✅ — Update `Models/DiskImageRecord.cs`

Apply the property delta. **(Plan: "DiskImageRecord — Updated Definition" → updated code block)**

- Add: `SavePolicy SavePolicy` (with default), `int HighestWorkingVersion`
- Remove: `PersistWorking`, `WorkingDirty`, `ModifiedUtc`
- Result: 13 properties.

#### Step 5a.3 ✅ — Update `Models/ProjectMetadata.cs`

Remove `ModifiedUtc` parameter. **(Plan: "ProjectMetadata — Updated Definition")**

- Old: 5-parameter positional record
- New: 4-parameter positional record (`Name`, `CreatedUtc`, `SchemaVersion`, `PandowdyVersion`)

#### Step 5a.4 ✅ — Update `Pandowdy.Project.csproj`

- Removed `System.Reactive` PackageReference (unused).
- `Microsoft.Data.Sqlite` remains until deferred SQLite files (`SkilletSchemaManager`, `ProjectSettingsStore`, `Migrations/`) are removed for the future `SqliteProjectStore` work.

### Phase 3: Rewrite `SkilletProject`

> Now that the types and interface are in their final form, rewrite the implementation. **(Plan: "Phase 3: Rewrite SkilletProject (Memory-Resident Core)", "Session Operations", "Lifecycle", "Blob Residency")**

#### Step 3.1 — Rewrite `Services/SkilletProject.cs`

Replace the body of `SkilletProject.cs` with the memory-resident implementation. This is the largest single change. The Plan provides:

- **State tree** **(Plan: "Layer 2: SkilletProject — Memory-Resident Model" → state tree)** — all fields: `_store`, `_metadata`, `_projectNotes`, `_projectDirty`, `_collectionLock`, `_defaultSavePolicy`, three settings dictionaries, `_diskImages`, `_blobs`, `_nextDiskId`, `_mountConfigs`, `_nextMountId`, `_attachments`, `_attachmentData`, `_nextAttachmentId`, `_checkedOutImages`.
- **Private constructor** — initialize from `ProjectManifest` or empty, with optional `IProjectStore`.
- **Static factories:**
  - `FromManifest(ProjectManifest manifest, IProjectStore store)` — for Open (store-backed). Throws `InvalidOperationException` on corrupt data (duplicate IDs, negative IDs, dangling mount refs). **(Plan: Phase 3 item 2)**
  - `CreateNew(string name)` — for CreateAdHoc (no store). **(Plan: Phase 3 item 3)**
  - `CreateNew(string name, IProjectStore store)` — for Create (with store). **(Plan: Phase 3 item 4)**
- **`CreateForTest` internal factory** **(Plan: Phase 3 → `CreateForTest` code block)** — for unit tests.
- **`ToSnapshot()`** — produces `ProjectSnapshot`. **(Plan: Phase 3 item 5)**
- **All interface methods** — implemented as pure in-memory operations. **(Plan: "Session Operations" table)** — use the "New (in-memory)" column for each method's implementation.
- **Lifecycle methods** — `SaveAsync()`, `SaveAsAsync(IProjectStore)`, `Dispose()`. **(Plan: "Lifecycle" section)**
- **`SnapshotCheckedOutDisks()`** — **(Plan: "Lifecycle" → `SnapshotCheckedOutDisks` pseudocode).** Note: `activeVersion` resolves to `disk.HighestWorkingVersion` for all save policies. Both `OverwriteLatest` and `AppendVersion` overwrite the current high-water mark during automatic saves — `AppendVersion` does NOT auto-increment. New versions are only created by explicit `CreateSnapshotAsync` calls.
- **`EnsureAllBlobsResident()`** — **(Plan: "SaveAs Requires Full Residency")**
- **`CheckOutAsync`** with lazy loading — **(Plan: "Lazy Loading" → `CheckOutAsync` pseudocode)**
- **`ReturnAsync`** — skip serialization for `DiscardChanges`. **(Plan: "DiscardChanges behavior" → intentional behavioral change)**
- **`FlushBlob`** — skip v0, use `OverwriteActive`. **(Plan: "Flushing" → `FlushBlob` pseudocode)**
- **`CreateSnapshotAsync`** — **(Plan: "Manual Snapshot" → pseudocode)**
- **`CloneBlobAsNewDiskAsync`** — **(Plan: "Clone Blob" → pseudocode, including precondition and TODO comment)**

Thread-safety requirements **(Plan: "Risk Assessment" → thread safety row)**:
- `ConcurrentDictionary` for settings, blobs, attachment data, checked-out images. **Do not downgrade.** Add code comment explaining rationale.
- `lock (_collectionLock)` for all `_diskImages`, `_mountConfigs`, `_attachments` access.
- `volatile` for `_projectDirty`.

#### Step 3.2 — Delete dead code from `SkilletProject.cs`

Remove from the file (verify these are deleted, not just commented out):
- `ProjectIOThread` inner class (entire class)
- `IORequest` / `IORequest<T>` classes
- `EnqueueAsync<T>` / `EnqueueAsync` / `EnqueueSync<T>` helper methods
- `TransitionToFileAsync` method
- In-memory vs file-based connection bifurcation logic

**(Plan: "Current Code to Delete" table — in the section preceding "Changes to Existing Types")**

### Phase 4: Simplify `SkilletProjectManager`

> **(Plan: "Phase 4: Simplify SkilletProjectManager")**

#### Step 4.1 ✅ — Rewrite `Services/SkilletProjectManager.cs`

The existing constructor has no DI dependencies — `IProjectStoreFactory` is the only new injection. Use a primary constructor per `.github/copilot-instructions.md`.

- Constructor receives `IProjectStoreFactory` via DI.
- `CreateAdHocAsync()` — `SkilletProject.CreateNew("untitled")`
- `CreateAsync(path, name)` — `factory.Create(path)` → `SkilletProject.CreateNew(name, store)` → `store.Save(snapshot)`
- `OpenAsync(path)` — `factory.Open(path)` → `store.LoadManifest()` → `SkilletProject.FromManifest(manifest, store)`
- `SaveAsAsync(path)` — `factory.Create(path)` → `project.SaveAsAsync(newStore)`
- `CloseAsync()` — `Dispose()` current → `CreateAdHocAsync()`

Remove all SQLite-specific code (connection strings, `VACUUM INTO`, etc.).

### Phase 5b: Remaining Cleanup

> These steps handle UI bindings, DI registration, file deletion, and auditing — none of which block the core rewrite.

#### Step 5b.1 ✅ — Update UI bindings

Edit `Pandowdy.UI/Controls/MountFromLibraryDialog.axaml`:
- Line 33: change `IsVisible="{Binding WorkingDirty}"` to `IsVisible="{Binding HasWorkingVersions}"`.

The `HasWorkingVersions` property must be a `bool` computed as `HighestWorkingVersion > 0`. Inspect the `DataContext` of the `ListView` item template containing this binding to determine the correct owner type (likely a ViewModel wrapping `DiskImageRecord`). Add the property there. If the binding target cannot be determined from the AXAML and its code-behind, **stop and ask the user**.

**(Plan: "UI bindings" note under Phases 3-5, "Modified Files" table)**

Then audit all `.axaml` files for any other references to `WorkingDirty` or `PersistWorking`:

```
grep -rn "WorkingDirty\|PersistWorking" --include="*.axaml" .
```

Fix any additional hits.

#### Step 5b.2 ✅ — Update DI registrations in `Pandowdy/Program.cs`

Add the `IProjectStoreFactory` registration:

```csharp
services.AddSingleton<IProjectStoreFactory, DirectoryProjectStoreFactory>();
```

**(Plan: "DI registration site" note, "Modified Files" table → `Pandowdy/Program.cs`)**

#### Step 5b.3 ✅ — Delete `Interfaces/ISettingsResolver.cs` and `Services/SettingsResolver.cs`

These are unimplemented stubs. **(Plan: "Deleted" table → `ISettingsResolver.cs`, `SettingsResolver.cs`)**

Use `git rm` if the user has granted git permission, otherwise just delete the files.

#### Step 5b.4 ✅ — Audit for remaining references to deleted code

Search for compile errors or stale references:

```
grep -rn "ISettingsResolver\|SettingsResolver\|RegenerateWorkingCopyAsync\|WriteWorkingBlobAsync\|ProjectIOThread\|IORequest\|EnqueueAsync\|EnqueueSync\|TransitionToFileAsync\|PersistWorking\|WorkingDirty\|ModifiedUtc" --include="*.cs" .
```

Fix any remaining references. Expect hits in:
- Test files (will be updated in Phase 6)
- Deferred files (`SkilletConstants.cs`, `SkilletSchemaManager.cs`, etc.) — leave these alone
- `DiskBlobStore.cs`, `DiskImageFactory.cs` — should be unchanged; verify no breakage

### Phase 6: Update Tests

> **(Plan: "Phase 6: Update Tests")**

#### Step 6.1 — Update `Pandowdy.Project.Tests/Services/SkilletProjectDiskTests.cs`

Replace `InsertMockDiskImageAsync` (which used `EnqueueAsync` + raw SQL) with calls to the `CreateForTest` internal factory. Build `ProjectManifest` objects with the desired `DiskImageRecord` entries and pass pre-serialized PIDI blobs.

**(Plan: Phase 3 → `CreateForTest` factory description, Phase 6 → "Tests that change" table)**

#### Step 6.2 — Update `Pandowdy.Project.Tests/Services/SkilletProjectManagerTests.cs`

- Constructor now takes `IProjectStoreFactory` — use `DirectoryProjectStoreFactory` with temp directories.
- Update `GetSettingAsync`/`SetSettingAsync` calls: change `"project_settings"` → `SettingsScope.ProjectSettings` (and similar for other scopes).
- Remove `RegenerateWorkingCopyAsync_DirtyDisk_ClearsWorkingBlob` test.
- Update any tests that reference `ModifiedUtc`, `PersistWorking`, or `WorkingDirty` on records/metadata.

**(Plan: Phase 5 → known callers, Phase 6 → "Tests that change" table)**

#### Step 6.3 — Evaluate `Pandowdy.Project.Tests/Services/ProjectSettingsStoreTests.cs`

This test file tests `ProjectSettingsStore`, which is SQLite-specific. **(Plan: Phase 6 → "Tests that change" table, listed with a "may be removed or repurposed" note)**

- If all tests reference SQLite directly, leave the file as-is (it validates code that's deferred, not deleted).
- If any tests reference removed APIs (`EnqueueAsync`, etc.), update or annotate them.

#### Step 6.4 — Create `Pandowdy.Project.Tests/Services/SkilletProjectTests.cs`

New test file for the memory-resident `SkilletProject`. **(Plan: Phase 6 → "Phases 3-5 tests" table)**

Test coverage:
- `CreateNew` (ad hoc) — verify `IsAdHoc == true`, `FilePath == null`, empty collections
- `CreateNew` (with store) — verify `IsAdHoc == false`, `FilePath == store.Path`
- `FromManifest` round-trip — build manifest, create project, verify state matches
- Settings CRUD — `GetSettingAsync`/`SetSettingAsync` for each `SettingsScope`
- Disk CRUD — `ImportDiskImageAsync`, `GetDiskImageAsync`, `GetAllDiskImagesAsync`, `RemoveDiskImageAsync`
- `CheckOutAsync` / `ReturnAsync` lifecycle
- `ReturnAsync` with `DiscardChanges` — verify no blob written
- Mount config CRUD — `SetMountAsync`, `GetMountConfigurationAsync`
- Dirty tracking — `MarkDirty`/`MarkClean`, `HasUnsavedChanges`
- `ToSnapshot` fidelity
- Attachment CRUD — add, get, update, delete
- `SaveAsync` throws when ad hoc
- `SaveAsAsync` swaps store, updates `FilePath`

#### Step 6.5 — Create `Pandowdy.Project.Tests/Services/ProjectSnapshotTests.cs`

New test file. **(Plan: Phase 6 → "Phases 3-5 tests" table)**

Test coverage:
- `ToSnapshot()` then `FromManifest()` round-trip: all fields preserved
- Blob dictionary structure (keyed by `(diskId, version)`)
- Attachment data structure (keyed by `attachmentId`)

#### Step 6.6 — [VERIFY] Build the full solution

```
dotnet build Pandowdy.sln
```

Confirm zero errors. This is the critical verification — all phases must be consistent.

#### Step 6.7 — [VERIFY] Run all tests

```
dotnet test Pandowdy.sln
```

Confirm all tests pass, including:
- New: `DirectoryProjectStoreTests`, `SkilletProjectTests`, `ProjectSnapshotTests`
- Updated: `SkilletProjectDiskTests`, `SkilletProjectManagerTests`
- Unchanged: `DiskBlobStoreTests`, `SkilletSchemaManagerTests`
- All non-Project tests (EmuCore, UI, Disassembler, Cpu) — regression check

#### Step 6.8 — [GIT COMMIT POINT]

Notify the user:

> **Good time to commit.** Phases 3-6 are complete — the core rewrite, manager simplification, interface update, UI binding fixes, and all test updates are done. Solution compiles and all tests pass.  
> Suggested message: `feat(project): rewrite SkilletProject to memory-resident model`

---

## Post-Implementation Checklist

After the final commit, verify these invariants:

- [ ] `ProjectIOThread` class no longer exists in the codebase
- [ ] `IORequest` / `IORequest<T>` classes no longer exist
- [ ] No references to `:memory:` connection strings in `Pandowdy.Project/`
- [ ] No references to `VACUUM INTO` in `Pandowdy.Project/`
- [ ] `Microsoft.Data.Sqlite` is not referenced in `Pandowdy.Project.csproj`
- [ ] `System.Reactive` is not referenced in `Pandowdy.Project.csproj`
- [ ] `ISettingsResolver.cs` and `SettingsResolver.cs` are deleted
- [ ] `MountFromLibraryDialog.axaml` binds to `HasWorkingVersions` (not `WorkingDirty`)
- [ ] `DiskImageRecord` has 13 properties (no `PersistWorking`, `WorkingDirty`, `ModifiedUtc`)
- [ ] `ProjectMetadata` has 4 constructor parameters (no `ModifiedUtc`)
- [ ] `Program.cs` registers `IProjectStoreFactory → DirectoryProjectStoreFactory`
- [ ] Deferred files still exist: `SkilletConstants.cs`, `SkilletSchemaManager.cs`, `V1_InitialSchema.cs`, `ISchemaMigration.cs`, `ProjectSettingsStore.cs`

---

*This blueprint is a disposable execution artifact. Delete it after the refactoring is complete. The Plan (`Memory-Resident-Project-Refactoring-Plan.md`) remains the authoritative design reference.*
