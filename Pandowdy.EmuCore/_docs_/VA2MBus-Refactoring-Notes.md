# VA2MBus Refactoring Notes

## Current State

`VA2MBus.cs` is approximately **571 lines** and serves as the central bus coordinator for the Apple IIe emulator. While well-tested (80+ tests in VA2MBusTests.cs), it handles multiple responsibilities that could be separated for improved maintainability.

## Current Responsibilities

The `VA2MBus` class currently manages:

1. **I/O Address Decoding**
   - ~100+ address constants for Apple II I/O space ($C000-$CFFF)
   - Read/write handler dictionaries
   - Address range routing logic

2. **Keyboard Input Management**
   - Keyboard latch state (`_currKey`)
   - High bit manipulation for key available flag
   - KEYSTRB strobe logic ($C010)
   - Public API: `SetKeyValue(byte key)`

3. **Game Controller Port**
   - Button state tracking (`_button0`, `_button1`, `_button2`)
   - Pushbutton I/O registers ($C061-$C063)
   - Public API: `SetPushButton(int num, bool enabled)`, `GetPushButton(int num)`

4. **Soft Switch Coordination**
   - `SoftSwitches` instance management
   - Responder registration and notification
   - Switch state queries for I/O reads

5. **Language Card Banking**
   - Complex two-access write sequence
   - Bank 1/Bank 2 selection logic
   - PreWrite state tracking
   - HighRead/HighWrite flags
   - 16 language card addresses ($C080-$C08F)

6. **VBlank Timing**
   - System clock counter (`_systemClock`)
   - VBlank cycle tracking (`_nextVblankCycle = 17063`)
   - VBlank blackout period (4550 cycles)
   - VBlank event firing (~60 Hz)

7. **CPU Interface**
   - `CpuRead(ushort address)` routing
   - `CpuWrite(ushort address, byte data)` routing
   - Implements `IAppleIIBus` interface

8. **Memory Pool Coordination**
   - Routes non-I/O reads/writes to `_memoryPool`
   - Coordinates with memory banking

9. **Handler Registration**
   - `InitIoReadHandlers()` - sets up read handlers
   - `InitIoWriteHandlers()` - sets up write handlers
   - Dictionary-based dispatch

## Future Responsibilities (Not Yet Implemented)

10. **Expansion Slot Bus System** (Planned)
    - 7 expansion slots (slots 1-7)
    - Slot I/O space routing ($C090-$C0FF, 16 bytes per slot)
    - Slot ROM space routing ($C100-$CFFF, 256 bytes per slot)
    - Peripheral card interface/protocol
    - Card hot-swapping capability
    - Slot-specific soft switches

11. **Floating Bus Emulation** (Planned)
    - Strategy pattern for floating bus behavior
    - Returns "random" or last-read values when accessing empty slots
    - Different strategies for different hardware scenarios:
      - Video scanner position (most accurate)
      - Last data bus value
      - Deterministic pattern (for testing)
      - True random (least accurate but simple)
    - Critical for some copy-protection schemes and games

## Slot Architecture Notes

The Apple IIe expansion slot system adds significant complexity:

### Slot Address Ranges
```
Slot 1: I/O $C090-$C09F, ROM $C100-$C1FF
Slot 2: I/O $C0A0-$C0AF, ROM $C200-$C2FF
Slot 3: I/O $C0B0-$C0BF, ROM $C300-$C3FF (special: SLOTC3ROM switch)
Slot 4: I/O $C0C0-$C0CF, ROM $C400-$C4FF
Slot 5: I/O $C0D0-$C0DF, ROM $C500-$C5FF
Slot 6: I/O $C0E0-$C0EF, ROM $C600-$C6FF (often disk controller)
Slot 7: I/O $C0F0-$C0FF, ROM $C700-$C7FF
```

### Peripheral Card Interface

Cards will need:
- Read handler for I/O space (16 bytes)
- Write handler for I/O space (16 bytes)
- ROM data provider (256 bytes)
- Reset notification
- Clock tick notification (optional, for timing-sensitive cards)

Suggested interface:
```csharp
public interface IPeripheralCard
{
    string Name { get; }
    byte ReadIo(byte offset);      // offset 0-15
    void WriteIo(byte offset, byte value);
    byte ReadRom(byte offset);     // offset 0-255
    void Reset();
    void Tick(int cycles);         // optional timing
}
```

### Floating Bus Strategies

When no card is present or responding:

```csharp
public interface IFloatingBusStrategy
{
    byte GetFloatingValue(ushort address);
}

// Implementations:
// - VideoScannerFloatingBus (most accurate, returns video memory)
// - LastBusValueFloatingBus (returns last data bus value)
// - RandomFloatingBus (simple but inaccurate)
// - DeterministicFloatingBus (for testing, returns predictable pattern)
```

### Slot Management

Potential slot manager to handle:
- Card registration/removal
- Address routing to appropriate card
- Floating bus fallback
- INTCXROM switch behavior (internal ROM vs slot ROMs)
- SLOTC3ROM special case (slot 3 independent control)

## Potential Refactoring Options

### Option 1: Extract Peripheral Controllers

Create focused classes for each peripheral:

```
VA2MBus (Coordinator - reduced to ~200 lines)
├── IoAddressDecoder (Address constants, handler registry)
├── KeyboardController (Keyboard latch, strobe, SetKeyValue)
├── GameControllerPort (Buttons 0-2, paddles, SetPushButton/GetPushButton)
├── LanguageCardController (Banking logic, write protection sequence)
├── VBlankTimer (Cycle counting, blackout period, event firing)
├── SlotBusController (NEW - expansion slot management)
│   ├── Manages 7 slots
│   ├── Routes I/O and ROM reads/writes
│   ├── Handles INTCXROM/SLOTC3ROM switches
│   └── Applies floating bus strategy
└── SoftSwitchCoordinator (Already exists as SoftSwitches class)
```

**Benefits:**
- Each controller is 50-150 lines, focused on single responsibility
- Easier to test in isolation
- Controllers can be reused or swapped (e.g., different keyboard models)
- Clearer separation of concerns
- Slot system is complex enough to warrant its own class

**Considerations:**
- More files to maintain
- Need to define clear interfaces for each controller
- Coordination overhead (bus still needs to route to controllers)
- **Slot system alone could be 200+ lines with 7 slots**

### Option 2: Extract Only Complex Subsystems

Only extract the most complex or reusable parts:

```
VA2MBus (Main bus - ~400 lines + slot logic)
├── LanguageCardController (Complex two-access sequence deserves own class)
├── VBlankTimer (Timing logic separate from I/O)
└── SlotBusController (NEW - slot system is complex and self-contained)
    └── IFloatingBusStrategy (Pluggable strategy)
```

Keep keyboard, game controller, and address decoding in VA2MBus.

**Benefits:**
- Reduces complexity where it matters most
- Minimal architectural changes
- Language card logic is notoriously tricky (good candidate for isolation)
- **Slot system with 7 cards + floating bus is substantial subsystem**

**Considerations:**
- VA2MBus still fairly large (would grow to ~600+ lines with slots)
- Partial solution - may need more extraction later

### Option 3: Keep Current Design

Current design has worked well:
- 80+ comprehensive tests all passing
- Well-understood by current developer
- Single file to find all bus logic

**Benefits:**
- No refactoring risk
- No breaking existing tests
- Known, stable design

**Considerations:**
- May become harder to maintain as features are added
- **Adding slot system could push VA2MBus to 800+ lines**
- New developers have more to learn in one place
- Slot system might be easier to develop as separate class

### Option 4: Hybrid Approach (RECOMMENDED for Slots)

Extract slot system from the start:

```
VA2MBus (Current responsibilities - ~600 lines with slot routing)
└── SlotBusController (NEW - developed separately from start)
    ├── 7 IPeripheralCard slots
    ├── IFloatingBusStrategy interface
    └── Slot-specific soft switch handling
```

**Benefits:**
- Develop slot system cleanly without polluting VA2MBus
- Easier to test slot behavior independently
- Can add cards iteratively (disk controller, serial card, etc.)
- Floating bus strategies can be swapped/tested easily
- **Prevents VA2MBus from growing beyond maintainability**

**Rationale:**
- Slot system is new feature (no legacy code to refactor)
- Naturally bounded responsibility (7 slots, clear interface)
- Complex enough to justify own class (~200-300 lines likely)
- Will need independent testing anyway

## Recommended Approach

### Immediate (Slot System)
**Extract SlotBusController from the start** when implementing expansion slots:
- Don't add slot logic to VA2MBus
- Create `SlotBusController` to manage all 7 slots
- Define `IPeripheralCard` interface for cards
- Implement `IFloatingBusStrategy` with at least 2 strategies (simple and accurate)
- VA2MBus delegates slot I/O and ROM reads to SlotBusController

### Deferred (Existing Code)
**Not urgent** for current VA2MBus responsibilities. Consider refactoring when:

1. **Adding new peripherals** - If adding disk controller, serial card, etc., extract a pattern first
2. **Language card issues** - If bugs appear in banking logic, isolate it then
3. **Performance profiling** - If hotspots identified, optimize individual controllers
4. **Team growth** - If multiple developers working on bus, split for parallel work
5. **Size becomes unwieldy** - If VA2MBus exceeds 800 lines, extract controllers

## Testing Considerations

Any refactoring must preserve:
- ✅ All 80+ existing tests in `VA2MBusTests.cs`
- ✅ VBlank timing accuracy (17063 cycles = ~60 Hz at 1.023 MHz)
- ✅ Language card two-access write sequence
- ✅ Keyboard latch high-bit behavior
- ✅ Soft switch notification to responders

**New tests needed for slot system:**
- Card registration/removal
- I/O read/write routing to correct slot
- ROM read routing to correct slot
- Floating bus behavior with no card present
- INTCXROM switch (internal ROM vs slot ROMs)
- SLOTC3ROM switch (slot 3 special case)
- Multiple cards active simultaneously

## Related Files

- **Implementation**: `Pandowdy.EmuCore/VA2MBus.cs` (571 lines)
- **Interface**: `Pandowdy.EmuCore/Interfaces/IAppleIIBus.cs` (documented)
- **Tests**: `Pandowdy.EmuCore.Tests/VA2MBusTests.cs` (80+ tests, well-organized)
- **Soft Switches**: `Pandowdy.EmuCore/SoftSwitch.cs` (already extracted)
- **Documentation**: Comprehensive XML docs added to interfaces

**Future files for slot system:**
- `Pandowdy.EmuCore/SlotBusController.cs` (NEW)
- `Pandowdy.EmuCore/Interfaces/IPeripheralCard.cs` (NEW)
- `Pandowdy.EmuCore/Interfaces/IFloatingBusStrategy.cs` (NEW)
- `Pandowdy.EmuCore/FloatingBusStrategies/` (NEW directory)
  - `VideoScannerFloatingBus.cs`
  - `LastBusValueFloatingBus.cs`
  - `RandomFloatingBus.cs`
- `Pandowdy.EmuCore.Tests/SlotBusControllerTests.cs` (NEW)

## Decision

**Existing VA2MBus**: Refactoring deferred  
**Reason**: Current design is working well and fully tested  
**Status**: Analysis complete

**Slot System**: Extract from the start  
**Reason**: New feature, naturally bounded, complex enough to warrant own class  
**Status**: Design documented, ready to implement when needed  
**Next Step**: Create `IPeripheralCard` and `SlotBusController` interfaces first

**Date**: 2025-01-02

---

*This document serves as a reference for future refactoring decisions. The analysis and options are preserved for when the need arises.*
