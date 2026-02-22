// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Runtime.CompilerServices;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DataTypes
{
    /// <summary>
    /// Simple contiguous memory block implementation for testing and basic memory emulation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Purpose:</strong> Provides a straightforward, flat memory space without the
    /// complexity of bank switching, soft switches, or memory mapping. Ideal for unit testing,
    /// CPU instruction validation, and scenarios where simplified memory is sufficient.
    /// </para>
    /// <para>
    /// <strong>Key Characteristics:</strong>
    /// <list type="bullet">
    /// <item><strong>Size:</strong> Configurable from 1 byte to 64KB (0x10000 bytes maximum)</item>
    /// <item><strong>Memory Model:</strong> Flat, linear address space with no bank switching</item>
    /// <item><strong>Access Control:</strong> No write protection - all addresses are read/write</item>
    /// <item><strong>Thread Safety:</strong> Not thread-safe - intended for single-threaded use</item>
    /// <item><strong>Performance:</strong> Direct array access with zero overhead</item>
    /// <item><strong>Bounds Checking:</strong> None - throws <see cref="IndexOutOfRangeException"/> on invalid access</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Usage Example:</strong>
    /// <code>
    /// // Create 64KB memory block
    /// var memory = new MemoryBlock(0x10000);
    /// 
    /// // Or use the convenience class
    /// var memory64k = new MemoryBlock64k();
    /// 
    /// // Write test program
    /// memory.Write(0x0000, 0xA9); // LDA #$42
    /// memory.Write(0x0001, 0x42);
    /// 
    /// // Read back
    /// byte opcode = memory.Read(0x0000); // Returns 0xA9
    /// 
    /// // Write and read using explicit methods
    /// memory.Write(0x0200, 0xFF);
    /// byte value = memory.Read(0x0200); // Returns 0xFF
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Design Philosophy:</strong>
    /// This class prioritizes simplicity and performance over features. It provides the bare
    /// minimum needed to implement the <see cref="IPandowdyMemory"/> interface, making it ideal for:
    /// <list type="bullet">
    /// <item>Unit testing CPU instructions in isolation</item>
    /// <item>Simple emulators that don't need memory mapping</item>
    /// <item>Rapid prototyping and experimentation</item>
    /// <item>Educational purposes and learning 6502 programming</item>
    /// </list>
    /// For production emulation requiring bank switching, soft switches, or advanced memory
    /// management, consider implementing a more sophisticated memory manager tailored to
    /// your specific hardware architecture.
    /// </para>
    /// <para>
    /// <strong>Size Limit:</strong> Maximum size is 65,536 bytes (0x10000) because addresses
    /// are <see cref="ushort"/> (16-bit). Attempting to create a larger block throws
    /// <see cref="ArgumentOutOfRangeException"/>. This matches the 6502 processor's
    /// addressing capabilities.
    /// </para>
    /// </remarks>
    /// <param name="size">
    /// Size of the memory block in bytes. Common values:
    /// <list type="bullet">
    /// <item>64KB (0x10000) - Full 6502 address space (maximum)</item>
    /// <item>16KB (0x4000) - Typical ROM size</item>
    /// <item>4KB (0x1000) - Common RAM bank size</item>
    /// <item>256 bytes (0x100) - Zero page + stack</item>
    /// <item>Custom sizes for testing specific memory layouts</item>
    /// </list>
    /// Must be between 1 and 65,536 (0x10000) inclusive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="size"/> is less than 1 or greater than 65,536 (0x10000).
    /// </exception>
    public class MemoryBlock(int size) : ISystemRam
    {
        /// <summary>
        /// Maximum allowed memory size in bytes (64KB - the full 16-bit address space).
        /// </summary>
        public const int MaxSize = 0x10000; // 65,536 bytes

        /// <summary>
        /// Backing array holding the memory contents. Initialized to all zeros.
        /// </summary>
        private byte[] _data = size <= 0 || size > MaxSize
            ? throw new ArgumentOutOfRangeException(nameof(size), size,
                $"Memory size must be between 1 and {MaxSize} (0x{MaxSize:X}) bytes. ushort addresses cannot access beyond 64KB.")
            : new byte[size];


        public void CopyIntoSpan(Span<byte> destination)
        {
            _data.AsSpan().CopyTo(destination);
        }

        /// <summary>
        /// Gets the size of the memory block in bytes.
        /// </summary>
        /// <value>
        /// The length of the underlying byte array. This is the size specified in the constructor.
        /// </value>
        public int Size { get => _data.Length; }

        /// <summary>
        /// Reads a byte from the specified memory address.
        /// </summary>
        /// <param name="address">16-bit memory address (0x0000-0xFFFF).</param>
        /// <returns>Byte value at the specified address.</returns>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown if <paramref name="address"/> is beyond the memory block size.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <strong>Performance:</strong> Direct array access with no overhead. Fastest possible
        /// memory read implementation - no validation, no mapping, no locking.
        /// </para>
        /// <para>
        /// <strong>No Bounds Checking:</strong> This method does not validate the address range.
        /// Accessing beyond <see cref="Size"/> will throw <see cref="IndexOutOfRangeException"/>.
        /// This is intentional for maximum performance in tight CPU emulation loops.
        /// </para>
        /// </remarks>
        public byte Read(ushort address) => _data[address];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Peek(ushort address) => Read(address);

        /// <summary>
        /// Writes a byte to the specified memory address.
        /// </summary>
        /// <param name="address">16-bit memory address (0x0000-0xFFFF).</param>
        /// <param name="data">Byte value to write.</param>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown if <paramref name="address"/> is beyond the memory block size.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <strong>No Write Protection:</strong> All addresses within the memory block are
        /// writable. There is no ROM simulation, write protection, or access control.
        /// If you need write-protected regions, implement wrapper logic or use a more
        /// sophisticated memory manager.
        /// </para>
        /// <para>
        /// <strong>Performance:</strong> Direct array access with no overhead. Fastest possible
        /// memory write implementation - no validation, no mapping, no locking.
        /// </para>
        /// </remarks>
        public void Write(ushort address, byte data)
        {
            _data[address] = data;
        }

        /// <inheritdoc />
        public void Clear() => Array.Clear(_data);

    }


    /// <summary>
    /// Convenience class that creates a 64KB (0x10000 bytes) memory block - the full 6502 address space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Purpose:</strong> Provides a simple way to create a full-sized memory block without
    /// specifying the size parameter. Equivalent to <c>new MemoryBlock(0x10000)</c>.
    /// </para>
    /// <para>
    /// <strong>6502 Address Space:</strong> The 6502 processor uses 16-bit addresses (ushort),
    /// allowing access to 65,536 bytes (64KB) of memory from $0000 to $FFFF. This class creates
    /// a memory block that covers the entire addressable range.
    /// </para>
    /// <para>
    /// <strong>Usage Example:</strong>
    /// <code>
    /// // Create full 64KB memory - no size parameter needed
    /// var memory = new MemoryBlock64k();
    /// 
    /// // Can address the entire 6502 range
    /// memory[0x0000] = 0xFF; // Zero page
    /// memory[0x0100] = 0xFF; // Stack
    /// memory[0xFFFF] = 0xFF; // End of memory
    /// 
    /// // Size property returns 65,536
    /// int size = memory.Size; // Returns 0x10000
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Identical to <see cref="MemoryBlock"/>. This is purely
    /// a convenience wrapper with no additional overhead or functionality.
    /// </para>
    /// <para>
    /// <strong>Common Use Cases:</strong>
    /// <list type="bullet">
    /// <item>CPU instruction testing with full address space</item>
    /// <item>ROM/RAM simulation for 6502-based systems</item>
    /// <item>Retro computer emulation (Apple II, Commodore 64, NES, etc.)</item>
    /// <item>6502 programming education and experimentation</item>
    /// <item>Rapid prototyping of memory-mapped I/O systems</item>
    /// </list>
    /// </para>
    /// </remarks>
    public class MemoryBlock64k() : MemoryBlock(0x10000)
    {

    }
}
