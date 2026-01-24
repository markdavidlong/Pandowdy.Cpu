using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests.Helpers;

/// <summary>
/// Test implementation of ISlots for unit testing.
/// </summary>
/// <remarks>
/// Provides a minimal slots implementation that doesn't require ROM files or card factories.
/// Returns floating bus values (0x00) for all reads and ignores all writes.
/// </remarks>
public class TestSlots(ISystemStatusProvider status) : ISlots
{
    private readonly ISystemStatusProvider _status = status ?? throw new ArgumentNullException(nameof(status));
    private readonly byte[] _memory = new byte[0x1000]; // $C000-$CFFF space

    public int Size => 0x1000;
    
    public byte BankSelect { get; set; }

    public byte Read(ushort address)
    {
        // Return memory value (defaults to 0x00 - floating bus)
        if (address < _memory.Length)
        {
            return _memory[address];
        }
        return 0x00;
    }

    public void Write(ushort address, byte val)
    {
        // Store write in memory (ROMs are read-only but test allows writes for verification)
        if (address < _memory.Length)
        {
            _memory[address] = val;
        }
    }

    public byte this[ushort address]
    {
        get => Read(address);
        set => Write(address, value);
    }

    public void InstallCard(int id, SlotNumber slot)
    {
        // No-op for testing
    }

    public void InstallCard(string name, SlotNumber slot)
    {
        // No-op for testing
    }

    public void RemoveCard(SlotNumber slot)
    {
        // No-op for testing
    }

    public ICard GetCardIn(SlotNumber slot)
    {
        // Return null card for testing
        throw new NotImplementedException("TestSlots does not support card management");
    }

    public bool IsEmpty(SlotNumber slot)
    {
        return true; // All slots empty in test implementation
    }

        public string GetMetadata()
        {
            return string.Empty;
        }

        public bool ApplyMetadata(string metadata)
        {
            return true;
        }

        public void Reset()
        {
            // No-op for testing
        }
    }
