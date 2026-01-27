using Pandowdy.Cpu;

namespace Pandowdy.Cpu.Example;

internal class Program
{
    static void Main(string[] _)
    {
        Console.WriteLine("=== Pandowdy 65C02 Cycle-Accurate CPU Test ===");
        Console.WriteLine();

        // Create the bus and CPU state
        var bus = new RamBus();
        var cpuBuffer = new CpuStateBuffer();

        // Load test program:
        //   LDA #$42   ; A9 42 - Load 0x42 into A
        //   ADC #$10   ; 69 10 - Add 0x10 to A (result: 0x52)
        //   BRK        ; 00 00 - Break (with signature byte)
        byte[] program =
        [
            0xA9, 0x42,  // LDA #$42
            0x69, 0x10,  // ADC #$10
            0x00, 0x00   // BRK (with signature)
        ];

        const ushort ProgramStart = 0x0400;
        const ushort BrkHandler = 0x0500;

        // Set up memory
        bus.LoadProgram(ProgramStart, program);
        bus.SetResetVector(ProgramStart);
        bus.SetIrqVector(BrkHandler);

        // Put a simple infinite loop at BRK handler: JMP $0500
        bus.Memory[BrkHandler] = 0x4C;      // JMP
        bus.Memory[BrkHandler + 1] = 0x00;  // Low byte
        bus.Memory[BrkHandler + 2] = 0x05;  // High byte

        // Initialize CPU
        Cpu.Reset(cpuBuffer, bus);

        Console.WriteLine("Program loaded at $0400:");
        Console.WriteLine("  $0400: LDA #$42");
        Console.WriteLine("  $0402: ADC #$10");
        Console.WriteLine("  $0404: BRK");
        Console.WriteLine();

        // ========================================
        // Demo 1: Cycle-level stepping
        // ========================================
        Console.WriteLine("=== Demo 1: Cycle-Level Stepping ===");
        Console.WriteLine();
        PrintHeader();

        int totalCycles = 0;
        int instructionCount = 0;
        const int MaxCycles = 20;

        while (totalCycles < MaxCycles)
        {
            var current = cpuBuffer.Current;
            PrintState(totalCycles, current);

            bool instructionComplete = Cpu.Clock(CpuVariant.CMOS65C02, cpuBuffer, bus);
            totalCycles++;

            if (instructionComplete)
            {
                instructionCount++;
                Console.WriteLine($"  << Instruction {instructionCount} complete >>");

                // Stop after BRK handler starts (JMP loop)
                if (instructionCount >= 4)
                {
                    break;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total cycles: {totalCycles}, Instructions: {instructionCount}");
        Console.WriteLine($"Final A register: 0x{cpuBuffer.Prev.A:X2} (expected: 0x52)");
        Console.WriteLine();

        // ========================================
        // Demo 2: Instruction-level stepping (fresh run)
        // ========================================
        Console.WriteLine("=== Demo 2: Instruction-Level Stepping ===");
        Console.WriteLine();

        // Reset for fresh run
        Cpu.Reset(cpuBuffer, bus);

        Console.WriteLine("Stepping through instructions:");
        Console.WriteLine();

        for (int i = 0; i < 4; i++)
        {
            var before = cpuBuffer.Current;
            ushort startPC = before.PC;
            byte opcode = bus.CpuRead(startPC);

            int cycles = Cpu.Step(CpuVariant.CMOS65C02, cpuBuffer, bus);
            var after = cpuBuffer.Prev;

            Console.WriteLine($"  ${startPC:X4}: Opcode ${opcode:X2} - {cycles} cycles");
            Console.WriteLine($"         A=${after.A:X2} X=${after.X:X2} Y=${after.Y:X2} " +
                            $"P=${after.P:X2} SP=${after.SP:X2} PC=${after.PC:X4}");

            // Show what changed
            var changed = cpuBuffer.ChangedRegisters.ToList();
            if (changed.Count > 0)
            {
                Console.WriteLine($"         Changed: {string.Join(", ", changed)}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("=== Test Complete ===");
        Console.WriteLine();

        // ========================================
        // Demo 3: LDA Addressing Modes Test
        // ========================================
        TestLdaAddressingModes();
    }

    static void TestLdaAddressingModes()
    {
        Console.WriteLine("=== Demo 3: LDA Addressing Modes ===");
        Console.WriteLine();

        var bus = new RamBus();
        var cpuBuffer = new CpuStateBuffer();

        // Set up test data in memory
        bus.Memory[0x0010] = 0x11;  // ZP location
        bus.Memory[0x0020] = 0x22;  // ZP location for zp,X test (0x10 + X=0x10)
        bus.Memory[0x1234] = 0x33;  // Absolute location
        bus.Memory[0x1244] = 0x44;  // Absolute + X (0x1234 + 0x10)
        bus.Memory[0x1254] = 0x55;  // Absolute + Y (0x1234 + 0x20)

        // For (zp,X) test: pointer at $30 -> $1300, value at $1300 = 0x66
        bus.Memory[0x0030] = 0x00;  // Low byte of pointer
        bus.Memory[0x0031] = 0x13;  // High byte of pointer
        bus.Memory[0x1300] = 0x66;

        // For (zp),Y test: pointer at $40 -> $1400, value at $1420 = 0x77
        bus.Memory[0x0040] = 0x00;  // Low byte of pointer
        bus.Memory[0x0041] = 0x14;  // High byte of pointer
        bus.Memory[0x1420] = 0x77;  // $1400 + Y($20) = $1420

        // For (zp) test: pointer at $50 -> $1500, value at $1500 = 0x88
        bus.Memory[0x0050] = 0x00;
        bus.Memory[0x0051] = 0x15;
        bus.Memory[0x1500] = 0x88;

        // Test program
        byte[] program =
        [
            0xA2, 0x10,        // LDX #$10
            0xA0, 0x20,        // LDY #$20
            0xA5, 0x10,        // LDA $10       (zp) - expect 0x11
            0xB5, 0x10,        // LDA $10,X     (zp,X) - $10+$10=$20 -> expect 0x22
            0xAD, 0x34, 0x12,  // LDA $1234     (abs) - expect 0x33
            0xBD, 0x34, 0x12,  // LDA $1234,X   (abs,X) - expect 0x44
            0xB9, 0x34, 0x12,  // LDA $1234,Y   (abs,Y) - expect 0x55
            0xA1, 0x20,        // LDA ($20,X)   (izx) - ptr at $30 -> expect 0x66
            0xB1, 0x40,        // LDA ($40),Y   (izy) - ptr at $40, +Y -> expect 0x77
            0xB2, 0x50,        // LDA ($50)     (izp, 65C02) - expect 0x88
            0x00, 0x00         // BRK
        ];

        const ushort start = 0x0600;
        bus.LoadProgram(start, program);
        bus.SetResetVector(start);
        bus.SetIrqVector(0x0700);
        bus.Memory[0x0700] = 0x4C;
        bus.Memory[0x0701] = 0x00;
        bus.Memory[0x0702] = 0x07;

        Cpu.Reset(cpuBuffer, bus);

        // We need LDX and LDY - they're not implemented yet, so let's set registers directly
        cpuBuffer.Current.X = 0x10;
        cpuBuffer.Current.Y = 0x20;
        cpuBuffer.Prev.X = 0x10;
        cpuBuffer.Prev.Y = 0x20;

        // Skip the LDX/LDY instructions (they'll act as NOPs for now)
        cpuBuffer.Current.PC = (ushort)(start + 4);
        cpuBuffer.Prev.PC = (ushort)(start + 4);

        var tests = new (string name, byte expected, int cycles)[]
        {
            ("LDA $10 (zp)", 0x11, 3),
            ("LDA $10,X (zp,X)", 0x22, 4),
            ("LDA $1234 (abs)", 0x33, 4),
            ("LDA $1234,X (abs,X)", 0x44, 4),
            ("LDA $1234,Y (abs,Y)", 0x55, 4),
            ("LDA ($20,X) (izx)", 0x66, 6),
            ("LDA ($40),Y (izy)", 0x77, 5),
            ("LDA ($50) (izp)", 0x88, 5),
        };

        Console.WriteLine("Testing all LDA addressing modes:");
        Console.WriteLine();

        int passed = 0;
        foreach (var test in tests)
        {
            int cycles = Cpu.Step(CpuVariant.CMOS65C02, cpuBuffer, bus);
            byte result = cpuBuffer.Prev.A;
            bool ok = result == test.expected && cycles == test.cycles;

            Console.WriteLine($"  {test.name,-25} A=${result:X2} (exp ${test.expected:X2}) " +
                            $"cycles={cycles} (exp {test.cycles}) {(ok ? "✓" : "✗")}");

            if (ok)
            {
                passed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Results: {passed}/{tests.Length} tests passed");

        // Run Phase 3 & 4 tests
        TestPhase3And4();
    }

    static void TestPhase3And4()
    {
        Console.WriteLine();
        Console.WriteLine("=== Demo 4: Phase 3 & 4 (Inc/Dec/Transfers/Shifts) ===");
        Console.WriteLine();

        var bus = new RamBus();
        var cpuBuffer = new CpuStateBuffer();

        const ushort start = 0x0800;
        bus.SetResetVector(start);
        bus.SetIrqVector(0x0900);
        bus.Memory[0x0900] = 0x4C;
        bus.Memory[0x0901] = 0x00;
        bus.Memory[0x0902] = 0x09;

        // Test data
        bus.Memory[0x0010] = 0x42;  // ZP location for INC/DEC
        bus.Memory[0x1000] = 0x80;  // Abs location for shifts

        // Test Phase 3: Register operations
        byte[] program =
        [
            // Register inc/dec
            0xA9, 0x10,  // LDA #$10
            0xAA,        // TAX        -> X = $10
            0xA8,        // TAY        -> Y = $10
            0xE8,        // INX        -> X = $11
            0xC8,        // INY        -> Y = $11
            0xCA,        // DEX        -> X = $10
            0x88,        // DEY        -> Y = $10
            0x1A,        // INC A      -> A = $11 (65C02)
            0x3A,        // DEC A      -> A = $10 (65C02)
            0x8A,        // TXA        -> A = $10
            0x98,        // TYA        -> A = $10
            0x9A,        // TXS        -> SP = $10
            0xBA,        // TSX        -> X = $10

            // Memory INC/DEC
            0xE6, 0x10,  // INC $10    -> mem[$10] = $43
            0xC6, 0x10,  // DEC $10    -> mem[$10] = $42

            // Shifts on accumulator
            0xA9, 0x55,  // LDA #$55   (01010101)
            0x0A,        // ASL A      -> $AA (10101010), C=0
            0x4A,        // LSR A      -> $55 (01010101), C=0
            0x38,        // SEC (not implemented - skip)
            0x2A,        // ROL A      -> $AA, C=0 (no carry in)
            0x6A,        // ROR A      -> $55, C=0

            0x00, 0x00   // BRK
        ];

        bus.LoadProgram(start, program);
        Cpu.Reset(cpuBuffer, bus);

        Console.WriteLine("Testing Phase 3 & 4 operations:");
        Console.WriteLine();

        var tests = new (string name, string reg, byte expected, int cycles)[]
        {
            ("LDA #$10", "A", 0x10, 2),
            ("TAX", "X", 0x10, 2),
            ("TAY", "Y", 0x10, 2),
            ("INX", "X", 0x11, 2),
            ("INY", "Y", 0x11, 2),
            ("DEX", "X", 0x10, 2),
            ("DEY", "Y", 0x10, 2),
            ("INC A", "A", 0x11, 2),
            ("DEC A", "A", 0x10, 2),
            ("TXA", "A", 0x10, 2),
            ("TYA", "A", 0x10, 2),
            ("TXS", "SP", 0x10, 2),
            ("TSX", "X", 0x10, 2),
            ("INC $10", "M", 0x43, 5),
            ("DEC $10", "M", 0x42, 5),
            ("LDA #$55", "A", 0x55, 2),
            ("ASL A", "A", 0xAA, 2),
            ("LSR A", "A", 0x55, 2),
        };

        int passed = 0;
        foreach (var test in tests)
        {
            int cycles = Cpu.Step(CpuVariant.CMOS65C02, cpuBuffer, bus);
            byte result = test.reg switch
            {
                "A" => cpuBuffer.Prev.A,
                "X" => cpuBuffer.Prev.X,
                "Y" => cpuBuffer.Prev.Y,
                "SP" => cpuBuffer.Prev.SP,
                "M" => bus.Memory[0x0010],
                _ => 0
            };
            bool ok = result == test.expected && cycles == test.cycles;

            Console.WriteLine($"  {test.name,-12} {test.reg}=${result:X2} (exp ${test.expected:X2}) " +
                            $"cycles={cycles} (exp {test.cycles}) {(ok ? "✓" : "✗")}");

            if (ok)
            {
                passed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Results: {passed}/{tests.Length} tests passed");
    }

    static void PrintHeader()
    {
        Console.WriteLine("Cyc  PC    A   X   Y   P    SP   Idx  Complete");
        Console.WriteLine("---  ----  --  --  --  --   --   ---  --------");
    }

    static void PrintState(int cycle, CpuState state)
    {
        Console.WriteLine($"{cycle,3}  ${state.PC:X4}  {state.A:X2}  {state.X:X2}  {state.Y:X2}  " +
                        $"{state.P:X2}   {state.SP:X2}   {state.PipelineIndex,3}  {state.InstructionComplete}");
    }
}
