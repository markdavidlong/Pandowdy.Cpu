# Strategy Pattern Architecture for Pandowdy

## Overview

This document describes the architectural pattern for interpreting different Apple II file types in Pandowdy. Instead of using inheritance (as in the legacy AppleSAWS C++ codebase), we use the **Strategy Pattern** with composition to separate data storage from interpretation logic.

---

## Core Principles

1. **GenericFile** is a pure data holder with no interpretation logic
2. **Interpreters** are strategy classes that know how to parse specific file formats
3. **Viewers** hold references to interpreters and receive GenericFile objects
4. **Interpretations** are the parsed/analyzed results returned by interpreters

---

## Architecture Components

### 1. Data Holder (GenericFile)

The core data container with no format-specific logic:

```csharp
public class GenericFile 
{
    public byte[] Data { get; init; }
    public FileMetadata Metadata { get; init; }
    public string Filename { get; init; }
    public ushort Address { get; init; }
    public ushort Length { get; init; }
}
```

**Responsibilities:**
- Store raw binary data
- Store metadata (file type, disk location, etc.)
- Remain format-agnostic

---

### 2. Interpretation Results

Each file type has a corresponding interpretation result class:

```csharp
// Base marker interface
public interface IFileInterpretation { }

// Applesoft BASIC interpretation
public class ApplesoftInterpretation : IFileInterpretation
{
    public List<ApplesoftLine> Lines { get; init; }
    public List<ApplesoftToken> AllTokens { get; init; }
    public Dictionary<int, string> Variables { get; init; }
}

// Binary file interpretation
public class BinaryInterpretation : IFileInterpretation
{
    public byte[] CodeData { get; init; }
    public ushort LoadAddress { get; init; }
    public DisassemblyInfo Disassembly { get; init; }
}

// Integer BASIC interpretation
public class IntBasicInterpretation : IFileInterpretation
{
    public string DetokenizedText { get; init; }
    public List<IntBasicLine> Lines { get; init; }
}

// Text file interpretation
public class TextInterpretation : IFileInterpretation
{
    public string Text { get; init; }
    public Encoding DetectedEncoding { get; init; }
}

// Relocatable file interpretation
public class RelocatableInterpretation : IFileInterpretation
{
    public byte[] CodeImage { get; init; }
    public List<RelocatableDictItem> Dictionary { get; init; }
    public ushort StartingAddress { get; init; }
}
```

**Responsibilities:**
- Hold parsed/analyzed data
- Provide type-safe access to interpretation results
- Immutable (using `init` accessors)

---

### 3. Interpreter Interface

The strategy interface that all interpreters implement:

```csharp
public interface IFileInterpreter<T> where T : IFileInterpretation
{
    bool CanInterpret(GenericFile file);
    T Interpret(GenericFile file);
}
```

**Responsibilities:**
- Define contract for interpretation strategies
- Enable runtime detection of compatible formats
- Provide type-safe interpretation method

---

### 4. Concrete Interpreters

Strategy implementations for each file format:

```csharp
public class ApplesoftInterpreter : IFileInterpreter<ApplesoftInterpretation>
{
    public bool CanInterpret(GenericFile file)
    {
        return file.Metadata.FileType == FileType.Applesoft 
            && file.Data.Length > 2;
    }
    
    public ApplesoftInterpretation Interpret(GenericFile file)
    {
        if (!CanInterpret(file))
            throw new InvalidOperationException("Cannot interpret file as Applesoft");
        
        // Parsing logic from ApplesoftFile.cxx
        var lines = ParseLines(file.Data);
        var tokens = ExtractTokens(lines);
        var variables = FindVariables(tokens);
        
        return new ApplesoftInterpretation
        {
            Lines = lines,
            AllTokens = tokens,
            Variables = variables
        };
    }
    
    private List<ApplesoftLine> ParseLines(byte[] data) { /* ... */ }
    private List<ApplesoftToken> ExtractTokens(List<ApplesoftLine> lines) { /* ... */ }
    private Dictionary<int, string> FindVariables(List<ApplesoftToken> tokens) { /* ... */ }
}
```

**Similar implementations for:**
- `BinaryInterpreter`
- `IntBasicInterpreter`
- `TextInterpreter`
- `RelocatableInterpreter`

**Responsibilities:**
- Implement format-specific parsing logic
- Validate file format compatibility
- Return structured interpretation results

---

## Viewer Integration

Viewers hold interpreter instances and receive GenericFile objects:

```csharp
public class ApplesoftViewer : ViewerBase
{
    private readonly ApplesoftInterpreter _interpreter;
    private GenericFile? _currentFile;
    private ApplesoftInterpretation? _currentInterpretation;
    
    public ApplesoftViewer()
    {
        _interpreter = new ApplesoftInterpreter();
    }
    
    // Can also inject via constructor for DI:
    // public ApplesoftViewer(ApplesoftInterpreter interpreter)
    // {
    //     _interpreter = interpreter;
    // }
    
    public void SetFile(GenericFile file)
    {
        if (!_interpreter.CanInterpret(file))
        {
            ShowError("This file cannot be interpreted as Applesoft BASIC");
            return;
        }
        
        _currentFile = file;
        _currentInterpretation = _interpreter.Interpret(file);
        
        DisplayInterpretation();
    }
    
    private void DisplayInterpretation()
    {
        if (_currentInterpretation == null) return;
        
        // Render lines with syntax highlighting
        foreach (var line in _currentInterpretation.Lines)
        {
            RenderLine(line);
        }
    }
    
    public void ExportToText()
    {
        if (_currentInterpretation == null) return;
        
        var text = string.Join("\n", 
            _currentInterpretation.Lines.Select(l => l.ToString()));
        
        SaveToFile(text);
    }
}
```

---

## Dependency Injection Setup

### Configuring Services

```csharp
// Pandowdy.UI/App.axaml.cs
public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }
    
    private void ConfigureServices()
    {
        var services = new ServiceCollection();
        
        // Register interpreters as singletons (stateless, can be reused)
        services.AddSingleton<ApplesoftInterpreter>();
        services.AddSingleton<BinaryInterpreter>();
        services.AddSingleton<IntBasicInterpreter>();
        services.AddSingleton<TextInterpreter>();
        services.AddSingleton<RelocatableInterpreter>();
        
        // Register other services
        services.AddSingleton<DiskFileLoader>();
        
        Services = services.BuildServiceProvider();
    }
    
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            mainWindow.SetText("Welcome to Pandowdy!\n");
        }
        
        base.OnFrameworkInitializationCompleted();
    }
}
```

### Using Services

```csharp
// Anywhere in the application
public partial class MainWindow : Window
{
    private void OpenApplesoftFile(GenericFile file)
    {
        // Get interpreter from DI container
        var interpreter = App.Services?.GetRequiredService<ApplesoftInterpreter>();
        
        if (interpreter?.CanInterpret(file) == true)
        {
            var interpretation = interpreter.Interpret(file);
            DisplayApplesoftData(interpretation);
        }
    }
    
    private void CreateViewer(GenericFile file)
    {
        // Get interpreters as needed
        var applesoftInterp = App.Services?.GetRequiredService<ApplesoftInterpreter>();
        var binaryInterp = App.Services?.GetRequiredService<BinaryInterpreter>();
        
        // Create appropriate viewer
        if (applesoftInterp?.CanInterpret(file) == true)
        {
            var viewer = new ApplesoftViewer(applesoftInterp);
            viewer.SetFile(file);
            ShowViewer(viewer);
        }
        else if (binaryInterp?.CanInterpret(file) == true)
        {
            var viewer = new BinaryViewer(binaryInterp);
            viewer.SetFile(file);
            ShowViewer(viewer);
        }
    }
}
```

---

## Benefits of This Architecture

### ? Separation of Concerns
- GenericFile = data storage only
- Interpreters = parsing/analysis logic
- Viewers = presentation logic

### ? Open/Closed Principle
- Add new file types without modifying existing code
- Just create new interpreter and interpretation classes

### ? Single Responsibility
- Each interpreter handles one format
- No god objects or inheritance hierarchies

### ? Testability
- Test interpreters independently
- Mock interpreters in viewer tests
- Clear input/output contracts

```csharp
[Test]
public void ApplesoftInterpreter_ParsesSimpleProgram()
{
    var file = new GenericFile { Data = GetTestData() };
    var interpreter = new ApplesoftInterpreter();
    
    var result = interpreter.Interpret(file);
    
    Assert.AreEqual(10, result.Lines.Count);
    Assert.Contains("PRINT", result.Lines[0].ToString());
}
```

### ? Runtime Flexibility
- User can change interpretation type
- Multiple interpretations of same file possible
- Easy to try different parsers

```csharp
// User says "try this as Binary instead"
var binaryInterp = new BinaryInterpreter();
if (binaryInterp.CanInterpret(currentFile))
{
    var binaryView = binaryInterp.Interpret(currentFile);
    DisplayInBinaryViewer(binaryView);
}
```

### ? Reusability
- Interpreters work standalone (not tied to viewers)
- Use interpreters in batch processing, CLI tools, etc.

```csharp
// Standalone usage example
var interpreter = new ApplesoftInterpreter();
var file = LoadFile("HELLO.BAS");
var interpretation = interpreter.Interpret(file);

// Export all variables to JSON
var json = JsonSerializer.Serialize(interpretation.Variables);
File.WriteAllText("variables.json", json);
```

---

## Common Patterns

### Pattern 1: Safe Interpretation with Null Checking

```csharp
if (App.Services?.GetService<ApplesoftInterpreter>() is { } interp 
    && interp.CanInterpret(file))
{
    var result = interp.Interpret(file);
    // Use result...
}
```

### Pattern 2: Auto-Detecting File Type

```csharp
public IFileInterpretation? InterpretFile(GenericFile file)
{
    var interpreters = new IFileInterpreter<IFileInterpretation>[]
    {
        App.Services!.GetRequiredService<ApplesoftInterpreter>(),
        App.Services!.GetRequiredService<BinaryInterpreter>(),
        App.Services!.GetRequiredService<IntBasicInterpreter>(),
        // etc...
    };
    
    foreach (var interpreter in interpreters)
    {
        if (interpreter.CanInterpret(file))
        {
            return interpreter.Interpret(file);
        }
    }
    
    return null; // No suitable interpreter found
}
```

### Pattern 3: Cached Interpretations

```csharp
public class FileCache
{
    private readonly Dictionary<GenericFile, IFileInterpretation> _cache = new();
    
    public T GetOrCreate<T>(GenericFile file, IFileInterpreter<T> interpreter) 
        where T : IFileInterpretation
    {
        if (_cache.TryGetValue(file, out var cached))
        {
            return (T)cached;
        }
        
        var interpretation = interpreter.Interpret(file);
        _cache[file] = interpretation;
        return interpretation;
    }
}
```

---

## Migration from Legacy C++ Code

### Legacy Approach (Inheritance)
```cpp
class GenericFile { /* ... */ };
class ApplesoftFile : public GenericFile { 
    void setData() override { /* parse here */ }
};
```

### New Approach (Strategy)
```csharp
class GenericFile { /* pure data */ }
class ApplesoftInterpreter { 
    ApplesoftInterpretation Interpret(GenericFile file) { /* parse here */ }
}
```

### Key Differences

| Aspect | Legacy (Inheritance) | New (Strategy) |
|--------|---------------------|----------------|
| **Data + Logic** | Coupled in subclass | Separated |
| **Extensibility** | Modify class hierarchy | Add new interpreter |
| **Reinterpretation** | Create new object | Call different interpreter |
| **Testing** | Test entire class | Test interpreter alone |
| **Multiple Views** | Difficult | Natural |

---

## Required NuGet Packages

- `Microsoft.Extensions.DependencyInjection` (version 8.0.1 or later)

---

## Project Structure

```
Pandowdy.Core/
??? Models/
?   ??? GenericFile.cs
?   ??? FileMetadata.cs
??? Interpretations/
?   ??? IFileInterpretation.cs
?   ??? ApplesoftInterpretation.cs
?   ??? BinaryInterpretation.cs
?   ??? IntBasicInterpretation.cs
?   ??? TextInterpretation.cs
?   ??? RelocatableInterpretation.cs
??? Interpreters/
    ??? IFileInterpreter.cs
    ??? ApplesoftInterpreter.cs
    ??? BinaryInterpreter.cs
    ??? IntBasicInterpreter.cs
    ??? TextInterpreter.cs
    ??? RelocatableInterpreter.cs

Pandowdy.UI/
??? Viewers/
?   ??? ViewerBase.cs
?   ??? ApplesoftViewer.cs
?   ??? BinaryViewer.cs
?   ??? IntBasicViewer.cs
?   ??? TextViewer.cs
?   ??? HexDumpViewer.cs
??? App.axaml.cs (DI configuration)
```

---

## Future Enhancements

### Potential Extensions
- **Validator interpreters** - Check file integrity before parsing
- **Transformer interpreters** - Convert between formats
- **Composite interpreters** - Chain multiple interpreters

### Example: Async Interpretation
```csharp
public interface IAsyncFileInterpreter<T> where T : IFileInterpretation
{
    Task<bool> CanInterpretAsync(GenericFile file);
    Task<T> InterpretAsync(GenericFile file, IProgress<int>? progress = null);
}
```

---

## Summary

The Strategy Pattern provides a clean, extensible architecture for Pandowdy's file interpretation system. By separating data storage (`GenericFile`) from interpretation logic (interpreter classes), we achieve:

- Better testability
- Easier maintenance
- Runtime flexibility
- Clear separation of concerns
- Natural C# idioms (vs. C++ inheritance)

This design will scale well as new Apple II file formats are added and provides a solid foundation for the Pandowdy rewrite.

---

**Document Version:** 1.0  
**Last Updated:** 2025-10-17  
**Related Documents:**
- `AppleSAWS_ClassHierarchy_Summary.md` - Legacy architecture reference
- `AppleSAWS_FileViewer_Architecture.dot` - Visual architecture diagram
