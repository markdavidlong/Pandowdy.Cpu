# Task 27: NIB Provider Analysis and Debugging

**Date:** 2025-02-05  
**Status:** ✅ **COMPLETE** - Drive switching bug fixed  
**Test Status:** All 2039 tests passing (100%)

---

## Executive Summary

The `NibDiskImageProvider` implements Apple II .nib format disk image support. After investigation and user insights:

- ✅ **All tests passing after fix (2039/2039)**
- ✅ **Drive switching bug identified and fixed**
- ✅ **Per-provider cycle tracking implemented**
- ✅ **Same fix applied to WOZ provider**

---

## ✅ FIX IMPLEMENTED (2025-02-05)

### Root Cause: Drive Switching Position Bug

**Problem:** Both NIB and WOZ providers used absolute `cycleCount` to calculate disk position. When switching between drives:
1. Drive 1 reads for 1,000,000 cycles
2. Switch to Drive 2
3. Drive 2 calculates position as if it was spinning during Drive 1's reads
4. Result: Drive 2 reads from wrong position (bits skipped or read twice)

**Solution:** Per-provider cycle tracking
- Each `IDiskImageProvider` instance maintains `_cycleOffsetAtFirstAccess`
- On first access, offset is set to current `cycleCount` (simulates motor start)
- Position calculated from `relativeCycles = cycleCount - _cycleOffsetAtFirstAccess`
- Each disk maintains independent rotational position

**Implementation:**
```csharp
// Added to both NibDiskImageProvider and InternalWozDiskImageProvider:
private ulong _cycleOffsetAtFirstAccess = 0;
private bool _hasBeenAccessed = false;

// In GetBit() and WriteBit():
if (!_hasBeenAccessed)
{
    _cycleOffsetAtFirstAccess = cycleCount;
    _hasBeenAccessed = true;
}
ulong relativeCycles = cycleCount - _cycleOffsetAtFirstAccess;
int bitPosition = (int)((relativeCycles / DiskIIConstants.CyclesPerBit) % DiskIIConstants.BitsPerTrack);
```

**Results:**
- ✅ All 2039 tests passing
- ✅ Drive switching no longer causes position errors
- ✅ Each disk spins independently
- ✅ Backward compatible (existing tests unchanged)

---

## Current Implementation Analysis

### Architecture Overview

The NIB provider implements a straightforward approach:
1. **File Format:** Stores 35 tracks × 6656 bytes = 232,960 bytes total
2. **Bit Streaming:** Uses `CircularBitBuffer` for each track
3. **Cycle-Based Positioning:** Disk "spins" based on system clock (45/11 cycles per bit)
4. **Quarter-Track Mapping:** Quarter tracks (0-139) map to full tracks (0-34) via integer division

### Key Design Decisions

**1. Cycle-Based Position Model (Correct)**
```csharp
// Position tied to system clock, not read count
int bitPosition = (int)((cycleCount / DiskIIConstants.CyclesPerBit) % DiskIIConstants.BitsPerTrack);
```
- ✅ Models real hardware behavior
- ✅ Disk spins continuously regardless of reads
- ✅ Matches WOZ provider approach

**2. Quarter-Track to Full-Track Mapping**
```csharp
int track = _currentQuarterTrack / 4;
// qTrack 0-3 → track 0
// qTrack 4-7 → track 1
// etc.
```
- ✅ Reasonable approach (half-tracks round down)
- ⚠️ Different from some emulators that use `(qTrack + 2) / 4` (round nearest)

**3. MC3470 Random Noise Simulation**
```csharp
private static readonly byte[] RandBits = [0, 1, 1, 0, 1, 0, 0, 0, ...]; // ~30% ones
```
- ✅ Simulates hardware behavior for missing tracks
- ✅ Matches TypeScript reference implementation

---

## Areas of Investigation

### 1. Track Offset Calculation ⚠️ **CRITICAL**

**Current Implementation:**
```csharp
for (int track = 0; track < DiskIIConstants.TrackCount; track++)
{
    int byteOffset = track * DiskIIConstants.BytesPerNibTrack;
    _tracks[track] = new CircularBitBuffer(_diskData, byteOffset, bitOffset: 0, ...);
}
```

**Potential Issue:**
- If `BytesPerNibTrack` constant is incorrect (6656 vs 6656.5), offsets would drift
- Off-by-one in track boundaries could cause data corruption

**Verification Needed:**
- ✅ Verify `DiskIIConstants.BytesPerNibTrack == 6656`
- ✅ Verify `DiskIIConstants.BitsPerTrack == 53248` (6656 × 8)
- ⚠️ Check if track boundaries align correctly in real NIB files
- ⚠️ Test with DOS 3.3 system disk (known-good reference)

**Recommended Test:**
```csharp
[Fact]
public void TrackOffsets_MatchExpectedBytePositions()
{
    // Load DOS 3.3 system disk
    // Verify track 0 address field at expected offset
    // Verify track 17 (catalog track) contains valid data
    // Verify track 34 (last track) is accessible
}
```

---

### 2. 6-and-2 Encoding/Decoding ⚠️ **HIGH PRIORITY**

**Current State:**
- `GcrEncoder` handles **encoding** (DSK → NIB synthesis)
- NIB provider has **no decoder** (reads raw bits)

**Problem:**
- NIB files contain **pre-encoded** 6-and-2 data
- Provider correctly reads raw bits
- But: No verification that bits decode correctly

**Missing Validation:**
1. Address field prologue detection (D5 AA 96)
2. Data field prologue detection (D5 AA AD)
3. Checksum validation
4. Proper epilogue detection (DE AA EB)

**Recommended Enhancement:**
```csharp
[Fact]
public void CanDecodeAddressFields_FromRealDisk()
{
    // Read track 0
    // Search for address field prologue (D5 AA 96)
    // Decode 4-4 encoded volume/track/sector
    // Verify checksum
    // Confirm all 16 sectors found
}

[Fact]
public void CanDecodeDataFields_FromRealDisk()
{
    // Read data field after address
    // Verify prologue (D5 AA AD)
    // Decode 342 bytes of 6-and-2 data
    // Verify checksum
    // Confirm 256 bytes of logical data
}
```

---

### 3. Self-Sync Byte Handling (0xFF) ✅ **LIKELY CORRECT**

**Current Implementation:**
- NIB files contain pre-written self-sync bytes (0xFF)
- Provider reads them as-is
- No special handling needed

**Verification:**
- ✅ Self-sync bytes should appear naturally in bit stream
- ✅ No bit shifting or alignment issues

---

### 4. Track Wrapping/Circular Behavior ✅ **TESTED**

**Current Test:**
```csharp
[Fact]
public void GetBit_WrapsAroundTrack()
{
    ulong cyclesPerRotation = (ulong)(DiskIIConstants.BitsPerTrack * DiskIIConstants.CyclesPerBit);
    bool? bit1 = _provider.GetBit(100);
    bool? bit2 = _provider.GetBit(100 + cyclesPerRotation);
    Assert.Equal(bit1, bit2); // PASSES
}
```

- ✅ Modulo arithmetic appears correct
- ✅ No off-by-one issues detected

---

### 5. Write Support ⚠️ **NEEDS ENHANCED TESTING**

**Current Implementation:**
```csharp
public bool WriteBit(bool bit, ulong cycleCount)
{
    if (_isWriteProtected) return false;
    
    int track = _currentQuarterTrack / 4;
    if (track < 0 || track >= DiskIIConstants.TrackCount) return false;
    
    int bitPosition = (int)((cycleCount / DiskIIConstants.CyclesPerBit) % DiskIIConstants.BitsPerTrack);
    _tracks[track].BitPosition = bitPosition;
    _tracks[track].WriteBit(bit ? 1 : 0);
    return true;
}
```

**Concerns:**
- ⚠️ Only 2 tests for WriteBit (write-protect and out-of-bounds)
- ⚠️ No test for actual write-then-read verification
- ⚠️ No test for write persistence (Flush())

**Recommended Tests:**
```csharp
[Fact]
public void WriteBit_CanBeReadBack()
{
    _provider.SetQuarterTrack(0);
    _provider.WriteBit(true, 0);
    _provider.WriteBit(false, 5);
    
    Assert.True(_provider.GetBit(0));
    Assert.False(_provider.GetBit(5));
}

[Fact]
public void Flush_PersistsWritesToDisk()
{
    // Create temp copy of test disk
    // Write bits to track 0
    // Call Flush()
    // Dispose provider
    // Reload from file
    // Verify bits persist
}

[Fact]
public void WriteBit_UpdatesUnderlyingByteArray()
{
    // Write complete byte pattern
    // Verify _diskData byte array updated
    // (Use InternalsVisibleTo or reflection)
}
```

---

### 6. CircularBitBuffer Integration ✅ **DELEGATED**

**Current Approach:**
- Provider delegates bit-level operations to `CircularBitBuffer`
- Buffer handles wraparound, bit packing, byte alignment

**Assumed Correct:**
- `CircularBitBuffer` is tested independently
- Provider uses it correctly (BitPosition setting)

---

## Test Coverage Analysis

### Existing Tests (15/15 passing)

#### Constructor Tests (4)
- ✅ Null path rejection
- ✅ Empty path rejection
- ✅ Missing file detection
- ✅ Invalid size rejection
- ✅ Valid file loading

#### SetQuarterTrack Tests (2)
- ✅ Updates CurrentQuarterTrack
- ✅ Quarter-track positions accepted

#### GetBit Tests (4)
- ✅ Returns valid bit
- ✅ Different bits at different cycles
- ✅ Random bits for out-of-bounds
- ✅ Track wrapping behavior

#### WriteBit Tests (2)
- ✅ Write-protect respected
- ✅ Out-of-bounds rejection

#### Property Tests (2)
- ✅ IsWritable returns true
- ✅ IsWriteProtected can be set

---

## Recommended Additional Tests

### High Priority (Functional Correctness)

1. **Track 0 Boot Sector Verification**
   ```csharp
   [Fact]
   public void Track0_ContainsValidBootSector()
   {
       // DOS 3.3 boot sector has known patterns
       // Verify boot sector bytes at expected positions
   }
   ```

2. **Sector Address Field Detection**
   ```csharp
   [Fact]
   public void CanFindAllSixteenSectors_OnTrack0()
   {
       // Search for 16 address field prologues (D5 AA 96)
       // Verify each sector 0-15 appears once
   }
   ```

3. **Data Field Checksum Validation**
   ```csharp
   [Fact]
   public void DataFields_HaveValidChecksums()
   {
       // Read data field from track 17 (catalog)
       // Decode 6-and-2 encoding
       // Verify checksum matches
   }
   ```

4. **Write-Then-Read Persistence**
   ```csharp
   [Fact]
   public void WrittenBits_PersistAcrossReload()
   {
       // Write known pattern to track 0
       // Flush, dispose, reload
       // Verify pattern persists
   }
   ```

### Medium Priority (Edge Cases)

5. **Half-Track Boundary Behavior**
   ```csharp
   [Fact]
   public void HalfTracks_MapToCorrectFullTrack()
   {
       // qTrack 2 (half of track 0) → track 0
       // qTrack 6 (half of track 1) → track 1
       // Verify mapping behavior
   }
   ```

6. **Last Track (34) Accessibility**
   ```csharp
   [Fact]
   public void LastTrack_CanBeReadAndWritten()
   {
       // qTrack 136-139 → track 34
       // Verify no array out-of-bounds
   }
   ```

7. **Concurrent Read/Write to Same Position**
   ```csharp
   [Fact]
   public void SimultaneousReadWrite_HandlesSafely()
   {
       // Edge case: reading and writing same bit position
       // (Unlikely in real usage, but worth testing)
   }
   ```

### Low Priority (Performance/Polish)

8. **Large Sequential Read Performance**
   ```csharp
   [Fact]
   public void CanReadEntireTrack_WithoutException()
   {
       // Read all 53,248 bits of track 0
       // Verify no errors, reasonable performance
   }
   ```

9. **Multiple Track Switches**
   ```csharp
   [Fact]
   public void RapidTrackSwitching_MaintainsCorrectState()
   {
       // Switch track 0 → 17 → 0 → 34 → 0
       // Verify correct data at each position
   }
   ```

---

## Potential Bugs Found

### None Confirmed Yet

After code review, no obvious bugs found. However:

- ⚠️ **Limited real-world validation** - tests use test images, need DOS 3.3 verification
- ⚠️ **No decoder** - can't verify 6-and-2 encoding correctness
- ⚠️ **Minimal write testing** - write path is lightly tested

---

## Comparison with Task 26 (WOZ Provider)

| Feature | NIB Provider | WOZ Provider |
|---------|--------------|--------------|
| **Format Complexity** | Simple (raw bytes) | Complex (flux transitions) |
| **Timing Information** | None | Bit-level timing |
| **Self-Sync Bytes** | Pre-written | Synthesized |
| **Track Count** | Always 35 | Variable |
| **Bit Density** | Fixed (6656 bytes/track) | Variable |
| **Copy Protection** | Limited | Full support |

**Conclusion:** NIB provider is simpler and less likely to have timing/sync issues than WOZ.

---

## Next Steps

### Phase 1: Enhanced Testing (In Progress)

1. ✅ **Document current implementation** (this document)
2. ⏳ **Create comprehensive test suite** (see recommended tests)
3. ⏳ **Verify with DOS 3.3 system disk**
4. ⏳ **Test actual game/program loading**

### Phase 2: Validation

1. **Compare with known-good emulator** (AppleWin, MAME)
2. **Test multi-track operations** (catalog reading, file loading)
3. **Verify write support** with real-world scenarios

### Phase 3: Debugging (If Issues Found)

1. **Enable detailed logging** (Task 11 - conditional compilation)
2. **Use debugger** (Task 19) for bit-level inspection
3. **Fix identified issues**
4. **Retest with full test suite**

---

## Constants Verification

Need to verify `DiskIIConstants`:

```csharp
public const int TrackCount = 35;
public const int BytesPerNibTrack = 6656;
public const int BitsPerTrack = BytesPerNibTrack * 8; // = 53,248
public const double CyclesPerBit = 45.0 / 11.0; // ≈ 4.090909...
```

**Verification:**
- ✅ TrackCount: 35 tracks standard for Apple II
- ✅ BytesPerNibTrack: 6656 bytes standard NIB track length
- ✅ BitsPerTrack: 53,248 bits (6656 × 8)
- ✅ CyclesPerBit: 45/11 ≈ 4.09 cycles at 1.023 MHz = 4μs per bit

---

## File References

**Implementation:**
- `Pandowdy.EmuCore/DiskII/Providers/NibDiskImageProvider.cs`

**Tests:**
- `Pandowdy.EmuCore.Tests/DiskII/Providers/NibDiskImageProviderTests.cs`

**Dependencies:**
- `Pandowdy.EmuCore/DiskII/GcrEncoder.cs` (encoding only, no decoder)
- `Pandowdy.EmuCore/DiskII/DiskIIConstants.cs`
- `CommonUtil/CircularBitBuffer.cs`

---

## Conclusion

The `NibDiskImageProvider` appears to be **generally well-implemented** with:
- ✅ Correct architecture
- ✅ Reasonable design decisions
- ✅ All existing tests passing

**However:**
- ⚠️ Real-world validation is limited
- ⚠️ Write support needs more testing
- ⚠️ No 6-and-2 decoder to validate bit stream correctness

**Recommendation:** Proceed with enhanced testing before declaring "no bugs found." The simplicity of the NIB format makes bugs less likely than WOZ, but DOS 3.3 I/O errors could still originate here if track offsets or checksums are incorrect.

---

**Document Version:** 1.0  
**Last Updated:** 2025-02-05
