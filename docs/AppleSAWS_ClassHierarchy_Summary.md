# AppleSAWS Class Hierarchy - Quick Reference

## Generated Files Summary

This documentation package includes:

### Source Files (Graphviz DOT format)
- **AppleSAWS_ClassHierarchy.dot** (7.6 KB) - Complete hierarchy with all classes
- **AppleSAWS_ClassHierarchy_Simple.dot** (2.8 KB) - Simplified main inheritance chains
- **AppleSAWS_FileViewer_Architecture.dot** (5.6 KB) - File/viewer relationship architecture

### Generated Diagrams
Each diagram is available in three formats:

#### 1. Complete Class Hierarchy
- AppleSAWS_ClassHierarchy.png (311 KB)
- AppleSAWS_ClassHierarchy.svg (51 KB)
- AppleSAWS_ClassHierarchy.pdf (64 KB)

Shows all classes organized into clusters:
- Qt Framework Classes
- File Type Classes (GenericFile hierarchy)
- Viewer Classes (FileViewerInterface hierarchy)
- Main Window Classes
- Dialog Classes
- Widget Classes
- Model Classes
- Memory Role Classes
- QObject Derivatives
- Utility Classes
- Standalone Classes

#### 2. Simplified Class Hierarchy
- AppleSAWS_ClassHierarchy_Simple.png (96 KB)
- AppleSAWS_ClassHierarchy_Simple.svg (30 KB)
- AppleSAWS_ClassHierarchy_Simple.pdf (46 KB)

Focuses on the main inheritance chains without excessive detail.

#### 3. File and Viewer Architecture
- AppleSAWS_FileViewer_Architecture.png (184 KB)
- AppleSAWS_FileViewer_Architecture.svg (30 KB)
- AppleSAWS_FileViewer_Architecture.pdf (54 KB)

Shows the relationships between:
- File types (GenericFile and subclasses)
- Viewer types (FileViewerInterface and subclasses)
- Disk management (DiskFile, DiskExplorer, ViewerBase)
- Supporting classes (formatters, parsers, etc.)

## Key Architecture Points

### File Type Hierarchy
```
GenericFile (base class)
??? ApplesoftFile      - Applesoft BASIC programs
??? BinaryFile         - Binary/machine code files
??? IntBasicFile       - Integer BASIC programs
??? RelocatableFile    - Relocatable object files
??? TextFile           - Text files
```

### Viewer Hierarchy
```
QWidget ? FileViewerInterface (abstract base)
??? ApplesoftFileViewer    - View Applesoft programs with syntax highlighting
??? CharSetViewer          - Character set viewer/explorer
??? DisassemblerViewer     - 6502 disassembler
??? HexDumpViewer          - Hex dump viewer
??? HiresViewer            - Hi-res graphics viewer (mono/NTSC/hybrid)
??? IntBasicFileViewer     - Integer BASIC viewer
??? MazeViewer             - Specialized maze viewer
??? TextHexDumpViewer      - Text with hex dump
```

### Main Application Structure
- **DiskExplorer** - Main window for exploring disk images
- **ViewerBase** - Container window that hosts FileViewerInterface instances
- **DiskFile** - Manages disk images, creates GenericFile instances

## Class Count Summary
- **File Types:** 5 classes (all inherit from GenericFile)
- **Viewers:** 8 classes (all inherit from FileViewerInterface)
- **Main Windows:** 2 classes (DiskExplorer, ViewerBase)
- **Dialogs:** 8 classes (utility dialogs)
- **Widgets:** 5 classes (custom UI components)
- **Models:** 2 classes (Qt table models)
- **QObject Derivatives:** 4 classes (non-widget Qt objects)
- **Standalone Classes:** ~14 classes (utility/data classes)

## Technologies Used
- **Qt 6** - UI framework
- **C++20** - Modern C++ with move semantics, constexpr, etc.
- **6502 Assembly** - Disassembly support
- **Apple II DOS 3.3** - Disk format support

## Usage Tips

1. **For documentation:** Use the PDF or SVG versions - they're scalable and print well
2. **For web/GitHub:** Use the PNG versions - widely compatible
3. **For editing:** Modify the .dot files and regenerate using `generate_diagrams.ps1`
4. **For presentations:** SVG files can be embedded in slides and scale perfectly

## Quick Commands

Generate all diagrams:
```powershell
.\generate_diagrams.ps1
```

Generate single diagram:
```bash
dot -Tpng AppleSAWS_ClassHierarchy.dot -o output.png
```

View DOT file online:
- http://www.webgraphviz.com/
- https://dreampuf.github.io/GraphvizOnline/

## Color Legend

| Color | Represents |
|-------|------------|
| Yellow | Qt Framework base classes |
| Cyan | File type classes |
| Green | Viewer classes |
| Pink | Main window classes |
| Wheat | Dialog classes |
| Lavender | Widget classes |
| Azure | Model classes |
| Blue | Utility/helper classes |
| Gray | Standalone classes |

## Notes

- Classes marked with **bold outline** are abstract or primary base classes
- **Solid arrows** indicate inheritance (is-a relationship)
- **Dashed arrows** indicate usage/association (uses/contains relationship)
- All Qt-derived classes follow Qt's object model (Q_OBJECT macro, signals/slots)
- Most classes implement the Rule of Five for modern C++ resource management
- File viewer classes cannot be copied or moved (Qt restriction for QObject-derived classes)

---

**Generated:** 2025-10-17
**Project:** AppleSAWS (Apple Software Analysis WorkShop)
**Repository:** https://github.com/markdavidlong/AppleSAWS
