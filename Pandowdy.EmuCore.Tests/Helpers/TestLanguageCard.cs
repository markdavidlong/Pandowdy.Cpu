using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Emulator;

namespace Pandowdy.EmuCore.Tests.Helpers;

/// <summary>
/// Mock Language Card implementation for testing.
/// </summary>
/// <remarks>
/// <para>
/// This simple mock returns 0xFF for all reads and ignores all writes,
/// allowing tests to focus on memory regions outside the Language Card
/// address space ($D000-$FFFF) without needing full Language Card complexity.
/// </para>
/// <para>
/// For tests that specifically need to test Language Card functionality,
/// use the real LanguageCard class with appropriate test dependencies.
/// </para>
/// </remarks>
public sealed class TestLanguageCard : ILanguageCard
{
    /// <summary>
    /// Size of Language Card address space (12KB: $D000-$FFFF).
    /// </summary>
    public int Size => 0x3000;
    
    /// <summary>
    /// Returns 0xFF for all reads (unmapped memory behavior).
    /// </summary>
    public byte Read(ushort address) => 0xFF;
    
    /// <summary>
    /// Ignores all writes (no-op).
    /// </summary>
    public void Write(ushort address, byte value) { /* No-op */ }
    
    /// <summary>
    /// Indexer that returns 0xFF for reads and ignores writes.
    /// </summary>
    public byte this[ushort address]
    {
        get => 0xFF;
        set { /* No-op */ }
    }
}

/// <summary>
/// Test implementation of ISystemRam for Language Card testing.
/// </summary>
public sealed class TestSystemRam : ISystemRam
{
    private readonly byte[] _memory;
    
    public TestSystemRam(int size)
    {
        _memory = new byte[size];
    }
    
    public int Size => _memory.Length;
    
    public byte Read(ushort address) => _memory[address];
    
    public void Write(ushort address, byte value) => _memory[address] = value;
    
    public byte this[ushort address]
    {
        get => _memory[address];
        set => _memory[address] = value;
    }
}

/// <summary>
/// Test implementation of IFloatingBusProvider for Language Card testing.
/// </summary>
public sealed class TestFloatingBusProvider : IFloatingBusProvider
{
    public byte Read() => 0xFF;
}

/// <summary>
/// Factory methods for creating Language Card instances for testing.
/// </summary>
public static class LanguageCardTestFactory
{
    /// <summary>
    /// Creates a fully functional Language Card for testing with all required dependencies.
    /// </summary>
    /// <param name="statusProvider">System status provider for soft switch state.</param>
    /// <param name="rom">Optional 16KB ROM data to install. If null, ROM will be zero-filled.</param>
    /// <returns>A Language Card instance configured for testing.</returns>
    public static LanguageCard CreateTestLanguageCard(ISystemStatusProvider statusProvider, byte[]? rom = null)
    {
        var mainRam = new TestSystemRam(0x4000); // 16KB main LC RAM
        var auxRam = new TestSystemRam(0x4000);  // 16KB aux LC RAM
        var systemRom = new TestSystemRam(0x4000); // 16KB system ROM
        
        // Install ROM if provided
        if (rom != null)
        {
            if (rom.Length != 0x4000)
            {
                throw new ArgumentException("ROM must be exactly 16KB", nameof(rom));
            }
            
            // Copy ROM data into the system ROM
            for (int i = 0; i < rom.Length; i++)
            {
                systemRom.Write((ushort)i, rom[i]);
            }
        }
        
        var floatingBus = new TestFloatingBusProvider();
        
        return new LanguageCard(mainRam, auxRam, systemRom, floatingBus, statusProvider);
    }
}
