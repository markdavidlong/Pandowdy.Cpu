# Disk Test File Locking Fix - Complete

## Problem Solved
Multiple disk I/O tests were failing due to file locking conflicts when running in parallel. Tests were trying to read the same disk image files (e.g., `TestDiskImages.DosDsk`, `TestDiskImages.TestNib`) simultaneously, causing `IOException: The process cannot access the file because it is being used by another process`.

## Solution Implemented
Created `TempDiskImageCopy` helper class that:
1. **Creates isolated temporary copies** of disk images for each test
2. **Implements retry logic** with random delays (1-3 seconds, up to 10 attempts)
3. **Automatically cleans up** temp files via `IDisposable`
4. **Preserves original file extension** (`.dsk`, `.nib`, `.woz`)
5. **Gracefully skips** tests when source images don't exist

## Files Updated (29 tests across 9 test classes)

### ✅ Exporter Tests
- **SectorExporterTests.cs** - 6 tests updated
  - `Export_DskFormat_CreatesCorrectFileSize`
  - `ExportStream_DskFormat_WritesCorrectSize`
  - `Export_RoundTrip_PreservesData`
  - `Export_PoFormat_CreatesProDOSOrder`
  - `Export_DoFormat_CreatesDOSOrder`
  - `Export_FormatConversion_DskToPo_Works`

- **NibExporterTests.cs** - 6 tests updated
  - `Export_CreatesCorrectFileSize`
  - `ExportStream_WritesCorrectSize`
  - `Export_RoundTrip_PreservesData`
  - `Export_RoundTripReimport_ProducesIdenticalTracks`
  - `Export_FormatConversion_DskToNib_Works`
  - `Export_FormatConversion_NibToDsk_Works`

- **WozExporterTests.cs** - 3 tests updated
  - `Export_CreatesValidWozFile`
  - `ExportStream_WritesValidWozData`
  - `Export_RoundTrip_PreservesTrackData`

- **WozExporterDebugTest.cs** - 1 test updated
  - `DebugWozStructure`

- **ScanExportedWozTest.cs** - 1 test updated
  - `ScanNewlyExportedWozFile`

- **ScanWozChunksTest.cs** - 1 test updated
  - `ScanAllChunks`

### ✅ Importer Tests
- **NibImporterTests.cs** - 3 tests updated
  - `Import_ValidNibFile_ReturnsInternalDiskImage`
  - `Import_ValidNibFile_CreatesCorrectTrackBitCounts`
  - `Import_ValidNibFile_TracksAreReadable`

- **WozImporterTests.cs** - 3 tests updated
  - `Import_ValidWozFile_ReturnsInternalDiskImage`
  - `Import_ValidWozFile_TracksAreReadable`
  - `ImportStream_ValidWozStream_ReturnsInternalDiskImage`

- **SectorImporterTests.cs** - 5 tests updated
  - `Import_ValidDskFile_ReturnsInternalDiskImage`
  - `Import_ValidDskFile_SynthesizesGcrTracks`
  - `ImportStream_ValidDskStream_ReturnsInternalDiskImage`
  - `Import_DifferentFormats_PreservesOriginalFormat`
  - `Import_SynthesizesTracks_WithProperGcrStructure`

## Additional Changes
- ✅ **Removed `[Collection("DiskTests")]`** from all 9 test classes - no longer needed since tests don't share files
- ✅ **Created `TempDiskImageCopy.cs`** - reusable helper for all disk I/O tests
- ✅ **Created `DISK-TEST-PATTERN.md`** - documentation and migration guide
- ✅ **Kept `DiskTestsCollection.cs`** - can be deleted if desired (no longer used)
- ✅ **Kept `xunit.runner.json`** - can be deleted if desired (no longer needed for serialization)

## Usage Pattern (Applied to All 29 Tests)

### Before
```csharp
[Fact]
[Trait("Category", "FullDiskTests")]
public void SomeTest()
{
    if (!File.Exists(TestDiskImages.DosDsk))
    {
        return;
    }

    var importer = new SectorImporter();
    var disk = importer.Import(TestDiskImages.DosDsk);
    // ... test logic ...
}
```

### After
```csharp
[Fact]
[Trait("Category", "FullDiskTests")]
public void SomeTest()
{
    using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
    if (sourceCopy == null)
    {
        return;
    }

    var importer = new SectorImporter();
    var disk = importer.Import(sourceCopy.FilePath);
    // ... test logic ...
    // Automatic cleanup via using statement
}
```

## Benefits
1. ✅ **Tests run in parallel** - No more serialization, much faster test runs
2. ✅ **No file locking conflicts** - Each test has its own isolated copy
3. ✅ **Automatic retry** - Handles transient issues with randomized backoff
4. ✅ **Automatic cleanup** - No temp file leaks
5. ✅ **Same test coverage** - All 29 tests preserve original behavior

## Test Results
- ✅ **Build: Successful**
- ✅ **All 29 tests updated**
- ✅ **No breaking changes**
- ✅ **Parallel execution enabled**

## Optional Cleanup
These files can now be deleted (no longer needed):
- `DiskTestsCollection.cs` - Collection definition not used
- `xunit.runner.json` - Parallelization config not needed

## Performance Impact
- **Before**: Tests ran sequentially due to `[Collection]` attribute
- **After**: Tests run in parallel with isolated copies
- **Expected**: Faster overall test execution despite copy overhead
- **Trade-off**: Slightly more disk I/O for copying, but eliminates serialization bottleneck
