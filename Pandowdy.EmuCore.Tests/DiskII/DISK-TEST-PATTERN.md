# Disk Test File Handling Pattern

## Problem
Multiple tests accessing the same disk image files simultaneously causes file locking conflicts when tests run in parallel.

## Solution
Use `TempDiskImageCopy` to create isolated temporary copies of disk images for each test.

## Usage Pattern

### Basic Pattern (with error handling)
```csharp
[Fact]
[Trait("Category", "FullDiskTests")]
public void YourTest()
{
    // Create temp copy - returns null if source doesn't exist
    using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
    if (sourceCopy == null)
    {
        return; // Skip test if image not available
    }

    // Use sourceCopy.FilePath instead of TestDiskImages.DosDsk
    var importer = new SectorImporter();
    var disk = importer.Import(sourceCopy.FilePath);
    
    // ... rest of test logic ...
    
    // Cleanup is automatic via using statement
}
```

### Advanced Pattern (custom retry settings)
```csharp
[Fact]
[Trait("Category", "FullDiskTests")]
public void YourTest()
{
    // Customize retry behavior if needed
    using var sourceCopy = TempDiskImageCopy.TryCreate(
        TestDiskImages.DosDsk,
        maxRetries: 15,      // default: 10
        minDelayMs: 500,     // default: 1000
        maxDelayMs: 2000     // default: 3000
    );
    
    if (sourceCopy == null)
    {
        return;
    }

    var disk = importer.Import(sourceCopy.FilePath);
    // ...
}
```

## Benefits

1. **Parallel Execution**: Tests can run in parallel without file conflicts
2. **Isolation**: Each test works with its own copy
3. **Automatic Cleanup**: Temp files deleted via `Dispose()`
4. **Retry Logic**: Handles transient file locking with randomized backoff
5. **Skip-If-Missing**: `TryCreate()` returns null for missing files

## Implementation Details

- Creates temp copy with same extension as source (e.g., `.dsk`, `.nib`, `.woz`)
- Random delay between retries (1-3 seconds) prevents thundering herd
- Default 10 retry attempts before giving up
- Thread-safe random number generation for delays
- Automatic cleanup even if test throws exception

## Migration Checklist

For each test that reads disk image files:

1. ✅ Replace `if (!File.Exists(...)) return;` with `using var copy = TempDiskImageCopy.TryCreate(...); if (copy == null) return;`
2. ✅ Change `importer.Import(TestDiskImages.XXX)` to `importer.Import(copy.FilePath)`
3. ✅ Remove `[Collection("DiskTests")]` attribute (no longer needed)
4. ✅ Verify test still passes

## Files to Update

### Importers
- `NibImporterTests.cs` - 3 tests
- `WozImporterTests.cs` - 3 tests  
- `SectorImporterTests.cs` - 5 tests

### Exporters
- `NibExporterTests.cs` - 6 tests
- `WozExporterTests.cs` - 3 tests
- `SectorExporterTests.cs` - 6 tests (1 done)
- `WozExporterDebugTest.cs` - 1 test
- `ScanExportedWozTest.cs` - 1 test
- `ScanWozChunksTest.cs` - 1 test

**Total**: 29 tests to update
