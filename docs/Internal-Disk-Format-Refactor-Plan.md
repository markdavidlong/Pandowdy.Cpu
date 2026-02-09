**Goal:** Create a unified internal disk image format that all disk image providers convert to/from, simplifying the emulation layer and enabling a future Pandowdy project format.

**Status:** ⏳ NOT STARTED

**Current State:**
- Each disk format (WOZ, NIB, DSK/DO/PO) has its own provider implementation
- `WozDiskImageProvider` uses DiskArc's `CircularBitBuffer` via `INibbleDataAccess`
- `NibDiskImageProvider` uses native byte array implementation
- `SectorDiskImageProvider` synthesizes GCR tracks on-demand using DiskArc's `SectorCodec`
- Each provider has different internal representations and bit-access patterns
- No easy way to convert between formats or save modified disk images

**Problem:**
- Duplicated logic across providers for bit-level access and timing
- No common representation for disk state during emulation
- Write support requires format-specific export logic in each provider
- Future Pandowdy project format (.pdw) needs a canonical disk representation
- Testing is complicated by format-specific behavior differences

**Proposed Solution:**

Create a unified `InternalDiskImage` class that all formats convert to/from:

```
┌─────────────────────────────────────────────────────────────────────┐
│                        External Disk Formats                         │
├─────────────┬─────────────┬─────────────┬─────────────┬─────────────┤
│    .woz     │    .nib     │    .dsk     │    .do      │    .po      │
└──────┬──────┴──────┬──────┴──────┬──────┴──────┬──────┴──────┬──────┘
       │             │             │             │             │
       ▼             ▼             ▼             ▼             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Format-Specific Importers                         │
│  WozImporter    NibImporter    SectorImporter (for .dsk/.do/.po)    │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       InternalDiskImage                              │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │  CircularBitBuffer[] Tracks (35-40 tracks)                   │    │
│  │  int[] TrackBitCounts (variable per track for WOZ)           │    │
│  │  bool IsWriteProtected                                       │    │
│  │  byte OptimalBitTiming (WOZ timing info)                     │    │
│  │  bool IsDirty (modified since load)                          │    │
│  │  string? SourceFilePath                                      │    │
│  │  DiskFormat OriginalFormat                                   │    │
│  └─────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    UnifiedDiskImageProvider                          │
│  - Implements IDiskImageProvider                                     │
│  - Uses CircularBitBuffer for all bit-level operations              │
│  - Single implementation for GetBit/WriteBit/GetByte                │
│  - Consistent timing and position tracking                          │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Format-Specific Exporters                         │
│  WozExporter    NibExporter    SectorExporter (decode GCR→sectors)  │
└──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┘
       │             │             │             │             │
       ▼             ▼             ▼             ▼             ▼
┌─────────────┬─────────────┬─────────────┬─────────────┬─────────────┐
│    .woz     │    .nib     │    .dsk     │    .do      │    .po      │
└─────────────┴─────────────┴─────────────┴─────────────┴─────────────┘
```

**Key Classes:**

```csharp
/// <summary>
/// Unified internal representation of a 5.25" floppy disk.
/// All external formats convert to/from this format.
/// </summary>
public class InternalDiskImage
{
    /// <summary>Bit-level track data using DiskArc's CircularBitBuffer.</summary>
    public CircularBitBuffer[] Tracks { get; }

    /// <summary>Bit count per track (varies for WOZ, fixed 51200 for NIB/synthesized).</summary>
    public int[] TrackBitCounts { get; }

    /// <summary>Number of tracks (typically 35 for DOS 3.3, up to 40 for some disks).</summary>
    public int TrackCount => Tracks.Length;

    /// <summary>Write protection state.</summary>
    public bool IsWriteProtected { get; set; }

    /// <summary>Optimal bit timing in 125ns units (from WOZ, default 32 = 4µs).</summary>
    public byte OptimalBitTiming { get; init; } = 32;

    /// <summary>True if disk has been modified since load.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Original source file path (null for new/embedded disks).</summary>
    public string? SourceFilePath { get; init; }

    /// <summary>Original format this disk was imported from.</summary>
    public DiskFormat OriginalFormat { get; init; }

    /// <summary>Mark disk as modified.</summary>
    public void MarkDirty() => IsDirty = true;

    /// <summary>Clear dirty flag (after save).</summary>
    public void ClearDirty() => IsDirty = false;
}

public enum DiskFormat
{
    Unknown,
    Woz,        // .woz (WOZ 1.0/2.0)
    Nib,        // .nib (nibble)
    Dsk,        // .dsk (DOS order sectors)
    Do,         // .do (DOS order sectors, explicit)
    Po,         // .po (ProDOS order sectors)
    Internal    // Created programmatically or from Pandowdy project
}
```

**Importer/Exporter Interfaces:**

```csharp
public interface IDiskImageImporter
{
    /// <summary>Formats this importer can handle.</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>Import a disk image file to internal format.</summary>
    InternalDiskImage Import(string filePath);

    /// <summary>Import from a stream (for embedded disk images).</summary>
    InternalDiskImage Import(Stream stream, DiskFormat format);
}

public interface IDiskImageExporter
{
    /// <summary>Format this exporter produces.</summary>
    DiskFormat OutputFormat { get; }

    /// <summary>Export internal format to file.</summary>
    void Export(InternalDiskImage disk, string filePath);

    /// <summary>Export to stream (for embedding in project files).</summary>
    void Export(InternalDiskImage disk, Stream stream);
}
```

**Implementation Phases:**

**Phase 1: Core Infrastructure**
- Create `InternalDiskImage` class
- Create `DiskFormat` enum
- Create `IDiskImageImporter` and `IDiskImageExporter` interfaces
- Create `UnifiedDiskImageProvider` implementing `IDiskImageProvider`

**Phase 2: Importers**
- `WozImporter`: Extract `CircularBitBuffer` tracks from WOZ via DiskArc
- `NibImporter`: Convert raw NIB bytes to `CircularBitBuffer` tracks
- `SectorImporter`: Synthesize GCR tracks from sector data (reuse existing `SectorDiskImageProvider` logic)

**Phase 3: Single Provider**
- Replace `WozDiskImageProvider`, `NibDiskImageProvider`, `SectorDiskImageProvider` with `UnifiedDiskImageProvider`
- `UnifiedDiskImageProvider` operates solely on `InternalDiskImage`
- Factory creates appropriate importer, imports to internal format, wraps with provider

**Phase 4: Exporters (Write Support)**
- `WozExporter`: Write internal format back to WOZ (preserve metadata)
- `NibExporter`: Write raw NIB format
- `SectorExporter`: Decode GCR to sectors, write DSK/DO/PO (lossy for copy-protected disks)

**Phase 5: Pandowdy Project Format**
- Define `.pdw` project file format (JSON/binary container)
- Embed `InternalDiskImage` data in project files
- Support disk image versioning within projects
- Import external disk images into project
- Export embedded disks to standard formats

**Files to Create:**

*Core:*
- `Pandowdy.EmuCore\DiskII\InternalDiskImage.cs` - Unified internal format
- `Pandowdy.EmuCore\DiskII\DiskFormat.cs` - Format enumeration
- `Pandowdy.EmuCore\DiskII\IDiskImageImporter.cs` - Importer interface
- `Pandowdy.EmuCore\DiskII\IDiskImageExporter.cs` - Exporter interface
- `Pandowdy.EmuCore\DiskII\Providers\UnifiedDiskImageProvider.cs` - Single provider implementation

*Importers:*
- `Pandowdy.EmuCore\DiskII\Importers\WozImporter.cs`
- `Pandowdy.EmuCore\DiskII\Importers\NibImporter.cs`
- `Pandowdy.EmuCore\DiskII\Importers\SectorImporter.cs`

*Exporters:*
- `Pandowdy.EmuCore\DiskII\Exporters\WozExporter.cs`
- `Pandowdy.EmuCore\DiskII\Exporters\NibExporter.cs`
- `Pandowdy.EmuCore\DiskII\Exporters\SectorExporter.cs`

*Tests:*
- `Pandowdy.EmuCore.Tests\DiskII\InternalDiskImageTests.cs`
- `Pandowdy.EmuCore.Tests\DiskII\Importers\*ImporterTests.cs`
- `Pandowdy.EmuCore.Tests\DiskII\Exporters\*ExporterTests.cs`

**Files to Modify:**
- `Pandowdy.EmuCore\DiskII\Providers\DiskImageFactory.cs` - Use importers + unified provider

**Files to Eventually Remove/Archive:**
- `Pandowdy.EmuCore\DiskII\Providers\WozDiskImageProvider.cs` - Replaced by unified provider
- `Pandowdy.EmuCore\DiskII\Providers\NibDiskImageProvider.cs` - Replaced by unified provider
- `Pandowdy.EmuCore\DiskII\Providers\SectorDiskImageProvider.cs` - Replaced by unified provider
- `Pandowdy.EmuCore\DiskII\GcrEncoder.cs` - Functionality moved to `SectorImporter`

**Benefits:**
- ✅ Single `IDiskImageProvider` implementation to maintain
- ✅ Guaranteed consistent behavior across all formats
- ✅ Easier testing - test the single provider, test importers/exporters separately
- ✅ Write support becomes format-agnostic (modify internal, export to any format)
- ✅ Enables Pandowdy project format with embedded disk images
- ✅ Import from any format, export to any format (format conversion)
- ✅ CircularBitBuffer provides battle-tested bit-level operations from DiskArc
- ✅ Copy-protected disks preserved in internal format (bit-perfect tracks)

**Technical Considerations:**
- `CircularBitBuffer` is from `CommonUtil` (DiskArc dependency already exists)
- WOZ track lengths vary; internal format preserves per-track bit counts
- Sector export is lossy for copy-protected disks (GCR → sector decode may fail)
- Internal format is transient (not serialized directly, use exporters)
- Future `.pdw` format will need versioning for internal format changes

**Priority:** Medium

**Dependencies:**
- None (builds on existing DiskArc integration)

**Related:**
- Complements Task 10 (SectorDiskImageProvider Debugging) - will be replaced by this architecture
- Enables future Pandowdy project format (.pdw)
- May simplify Task 11 (Conditional Debug Output) - fewer providers to instrument

---
