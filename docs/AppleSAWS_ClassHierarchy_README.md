# AppleSAWS Class Hierarchy Documentation

This directory contains Graphviz DOT files documenting the class hierarchy and architecture of the AppleSAWS project.

## Files

1. **AppleSAWS_ClassHierarchy.dot** - Complete class hierarchy with all classes organized by category
2. **AppleSAWS_ClassHierarchy_Simple.dot** - Simplified view showing only main inheritance chains
3. **AppleSAWS_FileViewer_Architecture.dot** - Detailed architecture showing file types, viewers, and their relationships

## Generating Diagrams

To generate PNG images from these files, you need to have Graphviz installed.

### Installation

**Windows:**
- Download from: https://graphviz.org/download/
- Or use Chocolatey: `choco install graphviz`
- Or use Scoop: `scoop install graphviz`

**macOS:**
```bash
brew install graphviz
```

**Linux:**
```bash
# Ubuntu/Debian
sudo apt-get install graphviz

# Fedora
sudo dnf install graphviz

# Arch
sudo pacman -S graphviz
```

### Generating Output

Once Graphviz is installed, generate images using the `dot` command:

```bash
# Generate PNG files
dot -Tpng AppleSAWS_ClassHierarchy.dot -o AppleSAWS_ClassHierarchy.png
dot -Tpng AppleSAWS_ClassHierarchy_Simple.dot -o AppleSAWS_ClassHierarchy_Simple.png
dot -Tpng AppleSAWS_FileViewer_Architecture.dot -o AppleSAWS_FileViewer_Architecture.png

# Generate SVG files (scalable, better for web/documentation)
dot -Tsvg AppleSAWS_ClassHierarchy.dot -o AppleSAWS_ClassHierarchy.svg
dot -Tsvg AppleSAWS_ClassHierarchy_Simple.dot -o AppleSAWS_ClassHierarchy_Simple.svg
dot -Tsvg AppleSAWS_FileViewer_Architecture.dot -o AppleSAWS_FileViewer_Architecture.svg

# Generate PDF files
dot -Tpdf AppleSAWS_ClassHierarchy.dot -o AppleSAWS_ClassHierarchy.pdf
dot -Tpdf AppleSAWS_ClassHierarchy_Simple.dot -o AppleSAWS_ClassHierarchy_Simple.pdf
dot -Tpdf AppleSAWS_FileViewer_Architecture.dot -o AppleSAWS_FileViewer_Architecture.pdf
```

## PowerShell Script to Generate All

Save this as `generate_diagrams.ps1`:

```powershell
# Generate all diagrams in multiple formats
$formats = @('png', 'svg', 'pdf')
$files = @(
    'AppleSAWS_ClassHierarchy',
    'AppleSAWS_ClassHierarchy_Simple',
    'AppleSAWS_FileViewer_Architecture'
)

foreach ($file in $files) {
    foreach ($format in $formats) {
        $input = "$file.dot"
        $output = "$file.$format"
        Write-Host "Generating $output..."
        dot -T$format $input -o $output
    }
}

Write-Host "All diagrams generated successfully!"
```

Then run:
```powershell
.\generate_diagrams.ps1
```

## Class Hierarchy Overview

### Main Categories

1. **File Types (GenericFile hierarchy)**
   - ApplesoftFile - Applesoft BASIC programs
   - BinaryFile - Binary/machine code files
   - IntBasicFile - Integer BASIC programs
   - RelocatableFile - Relocatable object files
   - TextFile - Text files

2. **Viewers (FileViewerInterface hierarchy)**
   - ApplesoftFileViewer - View Applesoft programs
   - CharSetViewer - Character set viewer
   - DisassemblerViewer - 6502 disassembler
   - HexDumpViewer - Hex dump viewer
   - HiresViewer - Hi-res graphics viewer
   - IntBasicFileViewer - Integer BASIC viewer
   - MazeViewer - Specialized maze viewer
   - TextHexDumpViewer - Text with hex dump

3. **Main Windows**
   - DiskExplorer - Main disk exploration interface
   - ViewerBase - Container for file viewers

4. **Dialogs**
   - Various utility dialogs for ASCII info, character set exploration, etc.

5. **Widgets**
   - Custom Qt widgets for UI components

6. **Models**
   - Qt table models for data display

7. **Supporting Classes**
   - Formatters, parsers, and utility classes

## Architecture Notes

- The project uses Qt 6 as its UI framework
- File types inherit from GenericFile (not a Qt class)
- All viewers inherit from FileViewerInterface which inherits from QWidget
- The DiskFile class manages disk images and creates GenericFile instances
- ViewerBase hosts FileViewerInterface instances for displaying files

## Color Coding

- **Yellow** - Qt framework classes
- **Cyan** - File type classes
- **Green** - Viewer classes
- **Pink** - Main window classes
- **Wheat** - Dialog classes
- **Lavender** - Widget classes
- **Gray** - Standalone utility classes

## Viewing DOT Files

You can view and edit DOT files with:
- Any text editor
- VS Code with Graphviz extension
- Online viewers: http://www.webgraphviz.com/ or https://dreampuf.github.io/GraphvizOnline/

## Questions or Issues?

If you find any missing classes or incorrect relationships, please update the DOT files accordingly.
