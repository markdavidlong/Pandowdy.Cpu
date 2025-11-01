using System;
using System.Security.Authentication.ExtendedProtection;
using Emulator;

namespace Pandowdy.Core;

/// <summary>
/// VA2M Memory implementation: fulfills Emulator.IMemory for the CPU/bus,
/// and exposes Pandowdy.Core.IMappedMemory for UI updates.
/// Supports optional read-only regions and non-zero start address.
/// </summary>
public sealed class VA2MMemory(int startAddress, int size, VA2MMemory.MemAccessType accessType = VA2MMemory.MemAccessType.ReadWrite) : IMemory, IMappedMemory
{
    private readonly byte[] _data = new byte[size];

    public enum MemAccessType
    {
        ReadWrite,
        ReadOnly
    }

    public int StartAddress { get; init; } = startAddress;

    public VA2MMemory(int size) : this(0, size) { }

    private int Translate(ushort address)
    {
        return address - StartAddress;
    }

    private bool InRange(ushort address)
    {
        int idx = address - StartAddress;
        return idx >= 0 && idx < _data.Length;
    }

    // Emulator.IMemory
    public int Size => _data.Length;
    public byte[] DataArray() => _data;

    public byte this[ushort address]
    {
        get
        {
            if (!InRange(address))
            {
                return 0;
            }

            return _data[Translate(address)];
        }
        set
        {
            if (!InRange(address))
            {
                return;
            }

            if (accessType == MemAccessType.ReadOnly)
            {
                return;
            }

            int idx = Translate(address);
            _data[idx] = value;
            // Raise mapped-memory event
            MemoryWritten?.Invoke(this, new MemoryAccessEventArgs { Address = address, Value = value, Length = 1 });
        }
    }

    public byte Read(ushort address)
    {
        if (!InRange(address))
        {
            return 0;
        }

        return _data[Translate(address)];
    }

    public void Write(ushort address, byte data)
    {
        Write(address, data, false);
    }

    public void Write(ushort address, byte data, bool force)
    {
        if (!InRange(address))
        {
            return;
        }

        if (!force && accessType == MemAccessType.ReadOnly)
        {
            return;
        }

        int idx = Translate(address);
        _data[idx] = data;
        MemoryWritten?.Invoke(this, new MemoryAccessEventArgs { Address = address, Value = data, Length = 1 });
    }

    public void WriteBlock(ushort offset, params byte[] data)
    {
        // Treat offset as absolute address
        if (!InRange(offset) || data.Length == 0)
        {
            return;
        }

        if (accessType == MemAccessType.ReadOnly)
        {
            return;
        }

        int idx = Translate(offset);
        int copyLen = Math.Min(data.Length, _data.Length - idx);
        Array.Copy(data, 0, _data, idx, copyLen);
        MemoryBlockWritten?.Invoke(this, new MemoryAccessEventArgs { Address = offset, Value = null, Length = copyLen });
    }

    public byte[] ReadBlock(ushort address, int length)
    {
        if (!InRange(address) || length <= 0)
        {
            return [];
        }

        int idx = Translate(address);
        int readLen = Math.Min(length, _data.Length - idx);
        var buffer = new byte[readLen];
        Array.Copy(_data, idx, buffer, 0, readLen);
        return buffer;
    }

    // Pandowdy.Core.IMappedMemory
    public event EventHandler<MemoryAccessEventArgs>? MemoryWritten;
    public event EventHandler<MemoryAccessEventArgs>? MemoryBlockWritten;

    // IMappedMemory requires Read for UI after block writes
    byte IMappedMemory.Read(ushort address) => Read(address);
}
