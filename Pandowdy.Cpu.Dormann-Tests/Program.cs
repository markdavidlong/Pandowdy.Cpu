// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using Pandowdy.Cpu.Example;

namespace Pandowdy.Cpu.Dormann_Tests;

/// <summary>
/// Test result for external test suites.
/// </summary>
public enum TestResult
{
    Passed,
    Failed,
    Error
}

/// <summary>
/// Test runner for Klaus Dormann's 6502/65C02 test suites.
/// </summary>
internal class Program
{
    // Environment variable name for test data path
    private const string TestDataPathEnvVar = "DORMANN_TEST_PATH";

    // Currently selected CPU variant
    private static CpuVariant _selectedVariant = CpuVariant.WDC65C02;

    // Loaded configuration
    private static TestConfig _config = new();

    static int Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     Pandowdy 65C02 CPU - External Test Suite Runner          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Load configuration
        _config = LoadConfiguration(args);

        string testDataPath = GetTestDataPath(args, _config);

        if (!Directory.Exists(testDataPath))
        {
            Console.WriteLine($"ERROR: Test data directory not found: {Path.GetFullPath(testDataPath)}");
            Console.WriteLine();
            PrintUsage();
            return 1;
        }

        Console.WriteLine($"Test data path: {Path.GetFullPath(testDataPath)}");
        Console.WriteLine();

        ShowMenu();
        ShowCurrentVariant();

        while (true)
        {
            Console.Write("\nSelect test (1-3, A=all, M=menu, V=variant, Q=quit): ");
            string? input = Console.ReadLine()?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(input))
            {
                continue;
            }

            if (input == "Q")
            {
                Console.WriteLine("Exiting...");
                return 0;
            }

            TestResult result;
            switch (input)
            {
                case "1":
                    result = RunFunctionalTest(testDataPath, _selectedVariant, _config);
                    break;
                case "2":
                    result = RunDecimalTest(testDataPath, _selectedVariant, _config);
                    break;
                case "3":
                    result = RunExtendedOpcodesTest(testDataPath, _selectedVariant, _config);
                    break;
                case "A":
                    Console.WriteLine("\n=== Running All Tests ===\n");
                    var results = new List<(string name, TestResult result)>
                    {
                        ("6502 Functional Test", RunFunctionalTest(testDataPath, _selectedVariant, _config)),
                        ("6502 Decimal Test", RunDecimalTest(testDataPath, _selectedVariant, _config))
                    };

                    // Only run extended opcodes test for 65C02 variants
                    bool is65C02 = _selectedVariant == CpuVariant.WDC65C02 || _selectedVariant == CpuVariant.ROCKWELL65C02;
                    if (is65C02)
                    {
                        string testName = _selectedVariant == CpuVariant.ROCKWELL65C02
                            ? "65C02 Extended Opcodes Test (Rockwell)"
                            : "65C02 Extended Opcodes Test (WDC)";
                        results.Add((testName, RunExtendedOpcodesTest(testDataPath, _selectedVariant, _config)));
                    }

                    Console.WriteLine("\n=== Summary ===\n");
                    int passed = 0;
                    int tested = 0;
                    foreach (var (name, res) in results)
                    {
                        string status = res == TestResult.Passed ? "PASSED" : res == TestResult.Failed ? "FAILED" : "ERROR";
                        Console.WriteLine($"  {name,-45} {status}");
                        tested++;
                        if (res == TestResult.Passed)
                        {
                            passed++;
                        }
                    }
                    if (!is65C02)
                    {
                        Console.WriteLine($"  {"65C02 Extended Opcodes Test",-45} SKIPPED (NMOS variant)");
                    }
                    Console.WriteLine($"\n  Total: {passed}/{tested} passed");
                    continue;
                case "M":
                    ShowMenu();
                    ShowCurrentVariant();
                    continue;
                case "V":
                    SelectVariant();
                    continue;
                default:
                    Console.WriteLine("Invalid selection. Press M for menu.");
                    continue;
            }

            if (result == TestResult.Passed)
            {
                Console.WriteLine("\n>>> TEST PASSED <<<");
            }
            else
            {
                Console.WriteLine("\n>>> TEST FAILED <<<");
            }
        }
    }

    static void ShowMenu()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Available Tests:                                     ║");
        Console.WriteLine("║  1. 6502 Functional Test                              ║");
        Console.WriteLine("║  2. 6502 Decimal Test                                 ║");
        Console.WriteLine("║  3. 65C02 Extended Opcodes Test (WDC or Rockwell)     ║");
        Console.WriteLine("║  A. Run All Tests                                     ║");
        Console.WriteLine("║  V. Select CPU Variant                                ║");
        Console.WriteLine("║  M. Show Menu                                         ║");
        Console.WriteLine("║  Q. Quit                                              ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: Pandowdy.Cpu.Dormann-Tests [testdata-path] [--config <config-file>]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  [testdata-path]       Path to directory containing Dormann test hex files");
        Console.WriteLine("  --config <file>       Path to configuration file (default: dormann-tests.json)");
        Console.WriteLine("  --create-config       Create default configuration file and exit");
        Console.WriteLine();
        Console.WriteLine("Environment variable:");
        Console.WriteLine($"  {TestDataPathEnvVar}  Alternative way to specify the test data path");
        Console.WriteLine();
        Console.WriteLine("Configuration file (dormann-tests.json):");
        Console.WriteLine("  Contains hex file names, start addresses, and success addresses.");
        Console.WriteLine("  Edit this file if you build tests with different assembly settings.");
        Console.WriteLine();
        Console.WriteLine("See README.md for instructions on assembling test files from source.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Pandowdy.Cpu.Dormann-Tests");
        Console.WriteLine("  Pandowdy.Cpu.Dormann-Tests C:\\testdata\\dormann");
        Console.WriteLine("  Pandowdy.Cpu.Dormann-Tests --config my-config.json");
        Console.WriteLine("  Pandowdy.Cpu.Dormann-Tests --create-config");
    }

    /// <summary>
    /// Loads configuration from file, handling command line options.
    /// </summary>
    private static TestConfig LoadConfiguration(string[] args)
    {
        string? configPath = null;

        // Check for --config argument
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--config")
            {
                configPath = args[i + 1];
                break;
            }
        }

        // Check for --create-config argument
        if (args.Contains("--create-config"))
        {
            string path = configPath ?? TestConfig.DefaultConfigFileName;
            if (File.Exists(path))
            {
                Console.WriteLine($"Configuration file already exists: {path}");
            }
            else
            {
                var config = new TestConfig();
                config.Save(path);
                Console.WriteLine($"Created default configuration file: {path}");
            }
            Environment.Exit(0);
        }

        var loadedConfig = TestConfig.Load(configPath);

        if (configPath != null && File.Exists(configPath))
        {
            Console.WriteLine($"Loaded configuration from: {configPath}");
        }
        else if (File.Exists(TestConfig.DefaultConfigFileName))
        {
            Console.WriteLine($"Loaded configuration from: {TestConfig.DefaultConfigFileName}");
        }

        return loadedConfig;
    }

    /// <summary>
    /// Gets the test data path from command line, environment variable, or config.
    /// </summary>
    private static string GetTestDataPath(string[] args, TestConfig config)
    {
        // First priority: command line argument (skip --config and its value)
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config")
            {
                i++; // Skip the next argument (config file path)
                continue;
            }
            if (args[i] == "--create-config")
            {
                continue;
            }
            if (!args[i].StartsWith('-'))
            {
                return args[i];
            }
        }

        // Second priority: environment variable
        string? envPath = Environment.GetEnvironmentVariable(TestDataPathEnvVar);
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }

        // Default from config
        return config.TestDataPath;
    }

    static void ShowCurrentVariant()
    {
        Console.WriteLine($"\n  Current CPU variant: {_selectedVariant}");
    }

    static void SelectVariant()
    {
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║  Select CPU Variant:                       ║");
        Console.WriteLine("║  1. NMOS6502 (with undocumented opcodes)   ║");
        Console.WriteLine("║  2. NMOS6502_NO_UNDOC (NOPs for undefined) ║");
        Console.WriteLine("║  3. WDC65C02 (WDC 65C02)                   ║");
        Console.WriteLine("║  4. ROCKWELL65C02                          ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");
        Console.Write("\nSelect variant (1-4): ");

        string? input = Console.ReadLine()?.Trim();
        switch (input)
        {
            case "1":
                _selectedVariant = CpuVariant.NMOS6502;
                break;
            case "2":
                _selectedVariant = CpuVariant.NMOS6502_NO_ILLEGAL;
                break;
            case "3":
                _selectedVariant = CpuVariant.WDC65C02;
                break;
            case "4":
                _selectedVariant = CpuVariant.ROCKWELL65C02;
                break;
            default:
                Console.WriteLine("Invalid selection. Variant unchanged.");
                return;
        }

        Console.WriteLine($"\n  CPU variant set to: {_selectedVariant}");
    }

    /// <summary>
    /// Runs the 6502 Functional Test (Klaus Dormann).
    /// Tests all valid opcodes and addressing modes of the NMOS 6502.
    /// </summary>
    static TestResult RunFunctionalTest(string testDataPath, CpuVariant variant, TestConfig config)
    {
        const string TestName = "6502 Functional Test";
        var testConfig = config.FunctionalTest;

        string hexFile = testConfig.HexFile;
        ushort startAddress = testConfig.StartAddressValue;
        ushort successAddress = testConfig.SuccessAddressValue ?? 0x3469;

        Console.WriteLine($"\n=== {TestName} ===");
        Console.WriteLine($"  Variant: {variant}");
        Console.WriteLine($"  Start:   ${startAddress:X4}");
        Console.WriteLine($"  Success: PC = ${successAddress:X4}");
        Console.WriteLine();

        string hexPath = Path.Combine(testDataPath, hexFile);
        if (!File.Exists(hexPath))
        {
            Console.WriteLine($"  ERROR: Hex file not found: {hexPath}");
            return TestResult.Error;
        }

        var bus = new RamBus();
        var cpuBuffer = new CpuStateBuffer();

        // Load test program
        Console.Write($"  Loading {hexFile}... ");
        LoadIntelHex(hexPath, bus.Memory);
        Console.WriteLine("OK");

        // Set up vectors - the test doesn't use reset vector, it starts at $0400
        // We manually set PC after reset
        bus.SetResetVector(startAddress);
        Cpu.Reset(cpuBuffer, bus);

        // Run until we detect an infinite loop (PC stops changing)
        Console.WriteLine("  Running test...");
        var sw = Stopwatch.StartNew();

        ushort previousPC = cpuBuffer.Current.PC;
        int sameAddressCount = 0;
        const int LoopThreshold = 3;
        long totalCycles = 0;
        long instructionCount = 0;
        const long MaxCycles = 200_000_000; // Safety limit

        while (totalCycles < MaxCycles)
        {
            Cpu.Step(variant, cpuBuffer, bus);
            totalCycles++;
            instructionCount++;

            ushort currentPC = cpuBuffer.Prev.PC;

            // Check for infinite loop
            if (currentPC == previousPC)
            {
                sameAddressCount++;
                if (sameAddressCount >= LoopThreshold)
                {
                    // Detected infinite loop
                    break;
                }
            }
            else
            {
                sameAddressCount = 0;
                previousPC = currentPC;
            }

            // Progress indicator every 10M instructions
            if (instructionCount % 10_000_000 == 0)
            {
                Console.WriteLine($"    Progress: {instructionCount:N0} instructions, PC=${currentPC:X4}");
            }
        }

        sw.Stop();
        ushort finalPC = cpuBuffer.Prev.PC;

        Console.WriteLine();
        Console.WriteLine($"  Execution stopped at PC = ${finalPC:X4}");
        Console.WriteLine($"  Instructions: {instructionCount:N0}");
        Console.WriteLine($"  Time: {sw.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"  Speed: {instructionCount / sw.Elapsed.TotalSeconds:N0} instructions/sec");

        if (finalPC == successAddress)
        {
            Console.WriteLine($"  Result: PASSED (stopped at success address ${successAddress:X4})");
            return TestResult.Passed;
        }
        else
        {
            Console.WriteLine($"  Result: FAILED (expected ${successAddress:X4}, got ${finalPC:X4})");
            Console.WriteLine($"  Tip: Look up ${finalPC:X4} in 6502_functional_test.lst to find the failing test");
            return TestResult.Failed;
        }
    }

    /// <summary>
    /// Runs the 6502 Decimal Test (Bruce Clark).
    /// Verifies BCD (decimal mode) behavior for ADC and SBC.
    /// Uses variant-specific test files since NMOS and CMOS have different BCD behavior.
    /// </summary>
    static TestResult RunDecimalTest(string testDataPath, CpuVariant variant, TestConfig config)
    {
        // Select appropriate test config based on variant (NMOS vs CMOS have different BCD behavior)
        bool isCmos = variant == CpuVariant.WDC65C02 || variant == CpuVariant.ROCKWELL65C02;
        var testConfig = isCmos ? config.CmosDecimalTest : config.NmosDecimalTest;

        string testName = isCmos ? "65C02 Decimal Test" : "6502 Decimal Test";
        string hexFile = testConfig.HexFile;
        ushort startAddress = testConfig.StartAddressValue;
        ushort errorAddress = testConfig.ErrorAddressValue;

        Console.WriteLine($"\n=== {testName} ===");
        Console.WriteLine($"  Variant: {variant}");
        Console.WriteLine($"  Start:   ${startAddress:X4}");
        Console.WriteLine($"  Result:  mem[${errorAddress:X4}] = 0 for success");
        Console.WriteLine();

        string hexPath = Path.Combine(testDataPath, hexFile);
        if (!File.Exists(hexPath))
        {
            Console.WriteLine($"  ERROR: Hex file not found: {hexPath}");
            if (isCmos)
            {
                Console.WriteLine($"  Note: Assemble decimal test with 'cputype = 1' for 65C02");
            }
            else
            {
                Console.WriteLine($"  Note: Assemble decimal test with 'cputype = 0' for NMOS 6502");
            }
            return TestResult.Error;
        }

        var bus = new RamBus();
        var cpuBuffer = new CpuStateBuffer();

        // Load test program
        Console.Write($"  Loading {hexFile}... ");
        LoadIntelHex(hexPath, bus.Memory);
        Console.WriteLine("OK");

        bus.SetResetVector(startAddress);
        Cpu.Reset(cpuBuffer, bus);

        // Run until we hit STP instruction or CPU status changes
        Console.WriteLine("  Running test (this may take a minute)...");
        var sw = Stopwatch.StartNew();

        long totalCycles = 0;
        long instructionCount = 0;
        const long MaxCycles = 50_000_000; // Safety limit - decimal test completes in ~15M instructions
        const ushort TestEndAddress = 0x024B; // Address where test ends with STP ($DB)

        // Check if the test has STP at the expected endpoint (for 65C02 test ROMs)
        bool hasStpEndpoint = bus.Memory[TestEndAddress] == 0xDB;

        while (totalCycles < MaxCycles)
        {
            Cpu.Step(variant, cpuBuffer, bus);
            totalCycles++;
            instructionCount++;

            // Check for STP/WAI/Jam which indicates test end
            var status = cpuBuffer.Prev.Status;
            if (status == CpuStatus.Stopped || status == CpuStatus.Jammed || status == CpuStatus.Waiting)
            {
                break;
            }

            // Check for reaching the test endpoint (handles NMOS 6502 which doesn't have STP)
            ushort pc = cpuBuffer.Prev.PC;
            if (hasStpEndpoint && pc == TestEndAddress)
            {
                break;
            }

            // Also check for infinite loop at a JMP $xxxx instruction
            byte opcode = bus.CpuRead(pc);
            if (opcode == 0x4C) // JMP absolute
            {
                ushort target = (ushort)(bus.CpuRead((ushort)(pc + 1)) | (bus.CpuRead((ushort)(pc + 2)) << 8));
                if (target == pc)
                {
                    // Infinite loop detected
                    break;
                }
            }

            // Progress indicator every 10M instructions
            if (instructionCount % 10_000_000 == 0)
            {
                Console.WriteLine($"    Progress: {instructionCount:N0} instructions");
            }
        }

        sw.Stop();

        byte errorValue = bus.Memory[errorAddress];
        byte n1 = bus.Memory[0x0000];
        byte n2 = bus.Memory[0x0001];

        Console.WriteLine();
        Console.WriteLine($"  Instructions: {instructionCount:N0}");
        Console.WriteLine($"  Time: {sw.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"  Speed: {instructionCount / sw.Elapsed.TotalSeconds:N0} instructions/sec");

        if (errorValue == 0)
        {
            Console.WriteLine($"  Error flag (mem[${errorAddress:X4}]): $00 (no errors)");
            Console.WriteLine($"  Result: PASSED (all 131,072 BCD test cases correct)");
            return TestResult.Passed;
        }
        else
        {
            Console.WriteLine($"  Error flag (mem[${errorAddress:X4}]): ${errorValue:X2} (error detected)");
            Console.WriteLine($"  Result: FAILED");
            Console.WriteLine($"  Failed at N1=${n1:X2}, N2=${n2:X2}");
            Console.WriteLine($"  DA (actual):   ${bus.Memory[0x0004]:X2}");
            Console.WriteLine($"  AR (expected): ${bus.Memory[0x0006]:X2}");
            return TestResult.Failed;
        }
    }

    /// <summary>
    /// Runs the 65C02 Extended Opcodes Test (Klaus Dormann).
    /// Tests all 65C02-specific opcodes not in the original NMOS 6502.
    /// Uses the appropriate test file based on variant.
    /// </summary>
    static TestResult RunExtendedOpcodesTest(string testDataPath, CpuVariant variant, TestConfig config)
    {
        // Select appropriate test config based on variant
        string testName;
        TestFileConfig testConfig;

        if (variant == CpuVariant.ROCKWELL65C02)
        {
            testName = "65C02 Extended Opcodes Test (Rockwell)";
            testConfig = config.RockwellExtendedTest;
        }
        else if (variant == CpuVariant.WDC65C02)
        {
            testName = "65C02 Extended Opcodes Test (WDC)";
            testConfig = config.WdcExtendedTest;
        }
        else
        {
            Console.WriteLine($"\n=== 65C02 Extended Opcodes Test ===");
            Console.WriteLine($"  Variant: {variant}");
            Console.WriteLine();
            Console.WriteLine("  ERROR: This is a 65C02 test - cannot run on NMOS 6502!");
            Console.WriteLine("         Use 'V' menu to select WDC65C02 or ROCKWELL65C02.");
            return TestResult.Error;
        }

        string hexFile = testConfig.HexFile;
        ushort startAddress = testConfig.StartAddressValue;
        ushort successAddress = testConfig.SuccessAddressValue ?? 0x24F1;

        Console.WriteLine($"\n=== {testName} ===");
        Console.WriteLine($"  Variant: {variant}");
        Console.WriteLine($"  Start:   ${startAddress:X4}");
        Console.WriteLine($"  Success: PC = ${successAddress:X4}");
        Console.WriteLine();
        Console.WriteLine("  Note: Run 6502 Functional Test first as prerequisite.");
        Console.WriteLine();

        string hexPath = Path.Combine(testDataPath, hexFile);
        if (!File.Exists(hexPath))
        {
            Console.WriteLine($"  ERROR: Hex file not found: {hexPath}");
            if (variant == CpuVariant.ROCKWELL65C02)
            {
                Console.WriteLine($"  Note: Assemble extended test with 'rkwl_wdc_op = 1' for Rockwell");
            }
            else
            {
                Console.WriteLine($"  Note: Assemble extended test with 'wdc_op = 1' for WDC");
            }
            return TestResult.Error;
        }

        var bus = new RamBus();
        var cpuBuffer = new CpuStateBuffer();

        // Load test program
        Console.Write($"  Loading {hexFile}... ");
        LoadIntelHex(hexPath, bus.Memory);
        Console.WriteLine("OK");

        bus.SetResetVector(startAddress);
        Cpu.Reset(cpuBuffer, bus);

        // Run until we detect an infinite loop
        Console.WriteLine("  Running test...");
        var sw = Stopwatch.StartNew();

        ushort previousPC = cpuBuffer.Current.PC;
        int sameAddressCount = 0;
        const int LoopThreshold = 3;
        long totalCycles = 0;
        long instructionCount = 0;
        const long MaxCycles = 100_000_000; // Safety limit

        while (totalCycles < MaxCycles)
        {
            Cpu.Step(variant, cpuBuffer, bus);
            totalCycles++;
            instructionCount++;

            ushort currentPC = cpuBuffer.Prev.PC;

            // Check for infinite loop
            if (currentPC == previousPC)
            {
                sameAddressCount++;
                if (sameAddressCount >= LoopThreshold)
                {
                    break;
                }
            }
            else
            {
                sameAddressCount = 0;
                previousPC = currentPC;
            }

            // Progress indicator every 1M instructions
            if (instructionCount % 1_000_000 == 0)
            {
                Console.WriteLine($"    Progress: {instructionCount:N0} instructions, PC=${currentPC:X4}");
            }
        }

        sw.Stop();
        ushort finalPC = cpuBuffer.Prev.PC;

        Console.WriteLine();
        Console.WriteLine($"  Execution stopped at PC = ${finalPC:X4}");
        Console.WriteLine($"  Instructions: {instructionCount:N0}");
        Console.WriteLine($"  Time: {sw.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"  Speed: {instructionCount / sw.Elapsed.TotalSeconds:N0} instructions/sec");

        if (finalPC == successAddress)
        {
            Console.WriteLine($"  Result: PASSED (stopped at success address ${successAddress:X4})");
            return TestResult.Passed;
        }
        else
        {
            string lstFile = "65c02_extended__opcodes_test.lst";
            Console.WriteLine($"  Result: FAILED (expected ${successAddress:X4}, got ${finalPC:X4})");
            Console.WriteLine($"  Tip: Look up ${finalPC:X4} in {lstFile} to find the failing test");
            return TestResult.Failed;
        }
    }

    /// <summary>
    /// Loads an Intel HEX format file into memory.
    /// </summary>
    public static void LoadIntelHex(string hexFile, byte[] memory)
    {
        int recordsLoaded = 0;
        int bytesLoaded = 0;

        foreach (string line in File.ReadAllLines(hexFile))
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith(':'))
            {
                continue;
            }

            if (line.Length < 11)
            {
                throw new FormatException($"Invalid Intel HEX record (too short): {line}");
            }

            int byteCount = Convert.ToInt32(line.Substring(1, 2), 16);
            int address = Convert.ToInt32(line.Substring(3, 4), 16);
            int recordType = Convert.ToInt32(line.Substring(7, 2), 16);

            if (recordType == 0x00) // Data record
            {
                if (line.Length < 9 + byteCount * 2 + 2)
                {
                    throw new FormatException($"Invalid Intel HEX data record (truncated): {line}");
                }

                for (int i = 0; i < byteCount; i++)
                {
                    byte data = Convert.ToByte(line.Substring(9 + i * 2, 2), 16);
                    memory[address + i] = data;
                    bytesLoaded++;
                }
                recordsLoaded++;
            }
            else if (recordType == 0x01) // EOF
            {
                break;
            }
            // Ignore other record types (extended address, etc.)
        }

        Console.Write($"({recordsLoaded} records, {bytesLoaded} bytes) ");
    }
}
