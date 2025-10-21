# Pandowdy Development Roadmap (C# + Avalonia)

---
## Phases:

### Phase 1: Core Infrastructure
- Integrate CiderPress2 for disk image parsing
- Create PandowdyProject class to manage loaded files and scenarios
- Define file types: **AppleSoft BASIC**, Integer BASIC, Binary
- Implement basic UI in Avalonia to browse and load files

### Phase 2: AppleSoft Token Parser
- Build a TokenParser that reads AppleSoft tokenized lines
- Map tokens to readable commands
- Handle string literals, numeric formats, control characters
- Display parsed lines in a readable format in the UI

### Phase 3: Abstract Syntax Tree (AST) Builder
- Create AST node types: Statement, Expression, Loop, Subroutine
- Build parser that converts token list into AST
- Track variable usage, control flow, and line references
- Display AST in a collapsible tree view in Avalonia

### Phase 4: AppleSoft Debugger Engine
- Build a virtual AppleSoft interpreter
- Implement debugger controls: Step, Run, Pause, Inspect
- Display current line, memory state, and variable values

### Phase 5: Scenario Loader + Memory Model
- Create Scenario class to represent memory layout
- Load BASIC and assembler files into virtual 64K memory
- Support shared labels and annotations across files
- Visualize memory map in UI

### Phase 6: Annotation System
- Enable inline annotations for variables, constants, subroutines
- Support label sharing between files
- Add UI for editing and viewing annotations
- Implement auto-tagging heuristics

### Phase 7: 6502bench Integration
- Parse 6502bench symbol and disassembly files
- Link assembler routines to BASIC calls
- Display imported annotations alongside native ones

### Phase 8: Visualization Tools
- Top-down memory map of 64K layout
- Flow diagrams for BASIC and assembler (Graphviz-compatible)
- Variable usage tables and control flow graphs

### Phase 9: Packaging and Virtual File System
- Implement virtual file system (optional)
- Serialize project, scenarios, annotations into a single archive
- Add import/export functionality

----------------
## Suggested Folder Structure

- Pandowdy/
  - Core/
    - PandowdyProject.cs
    - Scenario.cs
    - MemoryModel.cs
  - Parsers/
    - AppleSoftParser.cs
    - TokenParser.cs
    - ASTBuilder.cs
  - Debugger/
    - AppleSoftInterpreter.cs
    - DebuggerEngine.cs
  - UI/
    - MainWindow.axaml
    - FileBrowserView.axaml
    - ASTView.axaml
    - DebuggerView.axaml
  - Integration/
    - CiderPressAdapter.cs
    - Bench6502Importer.cs
  - Annotations/
    - AnnotationManager.cs
    - LabelStore.cs
