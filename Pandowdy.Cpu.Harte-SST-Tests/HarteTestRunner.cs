// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Pandowdy.Cpu;

namespace Pandowdy.Cpu.Harte_SST_Tests;

/// <summary>
/// Runs Tom Harte's SingleStepTests against the Pandowdy CPU emulator.
/// </summary>
public class HarteTestRunner(string testBasePath)
{
    private readonly string _testBasePath = testBasePath;
    private readonly RamBus _bus = new RamBus();
    // CPU is created per-test file run, state accessed via cpu.State

    /// <summary>
    /// Maps menu variant names to (CpuVariant, TestFolder) tuples.
    /// </summary>
    private static readonly Dictionary<string, (CpuVariant Variant, string TestFolder)> VariantMap = new()
    {
        ["Nmos6502"] = (CpuVariant.Nmos6502, "6502"),
        ["Nmos6502Simple"] = (CpuVariant.Nmos6502Simple, "6502"),
        ["Rockwell65C02"] = (CpuVariant.Rockwell65C02, "rockwell65c02"),
        ["Wdc65C02"] = (CpuVariant.Wdc65C02, "wdc65c02")
    };

    /// <summary>
    /// Illegal/undefined opcodes for NMOS 6502 that should be skipped for NO_ILLEGAL variant.
    /// These are the "illegal" or undocumented opcodes.
    /// </summary>
    private static readonly HashSet<byte> IllegalOpcodes =
    [
        // All undefined opcodes on NMOS 6502
        0x02, 0x03, 0x04, 0x07, 0x0B, 0x0C, 0x0F,
        0x12, 0x13, 0x14, 0x17, 0x1A, 0x1B, 0x1C, 0x1F,
        0x22, 0x23, 0x27, 0x2B, 0x2F,
        0x32, 0x33, 0x34, 0x37, 0x3A, 0x3B, 0x3C, 0x3F,
        0x42, 0x43, 0x44, 0x47, 0x4B, 0x4F,
        0x52, 0x53, 0x54, 0x57, 0x5A, 0x5B, 0x5C, 0x5F,
        0x62, 0x63, 0x64, 0x67, 0x6B, 0x6F,
        0x72, 0x73, 0x74, 0x77, 0x7A, 0x7B, 0x7C, 0x7F,
        0x80, 0x82, 0x83, 0x87, 0x89, 0x8B, 0x8F,
        0x92, 0x93, 0x97, 0x9B, 0x9C, 0x9E, 0x9F,
        0xA3, 0xA7, 0xAB, 0xAF,
        0xB2, 0xB3, 0xB7, 0xBB, 0xBF,
        0xC2, 0xC3, 0xC7, 0xCB, 0xCF,
        0xD2, 0xD3, 0xD4, 0xD7, 0xDA, 0xDB, 0xDC, 0xDF,
        0xE2, 0xE3, 0xE7, 0xEB, 0xEF,
        0xF2, 0xF3, 0xF4, 0xF7, 0xFA, 0xFB, 0xFC, 0xFF
    ];

    /// <summary>
    /// Gets available test variants based on directories present in the test path.
    /// </summary>
    public List<string> GetAvailableVariants()
    {
        var variants = new List<string>();

        foreach (var kvp in VariantMap)
        {
            var variantPath = Path.Combine(_testBasePath, kvp.Value.TestFolder);
            if (Directory.Exists(variantPath))
            {
                variants.Add(kvp.Key);
            }
        }

        return variants;
    }

    /// <summary>
    /// Runs all tests for a specific CPU variant.
    /// </summary>
    /// <param name="variantName">The variant name to run tests for.</param>
    /// <param name="verboseOutput">If true, show detailed failure information.</param>
    /// <param name="resultsFilter">Filter for which test results to display.</param>
    /// <param name="testMode">Whether to run bookend (state only) or complete (with cycles) tests.</param>
    public VariantTestSummary RunVariantTests(string variantName, bool verboseOutput = false, ResultsFilter resultsFilter = ResultsFilter.All, TestMode testMode = TestMode.Bookend)
    {
        var summary = new VariantTestSummary
        {
            VariantName = variantName
        };

        if (!VariantMap.TryGetValue(variantName, out var variantInfo))
        {
            Console.WriteLine($"Unknown variant: {variantName}");
            return summary;
        }

        var cpuVariant = variantInfo.Variant;
        var testFolder = variantInfo.TestFolder;
        var skipIllegal = cpuVariant == CpuVariant.Nmos6502Simple;

        var variantPath = Path.Combine(_testBasePath, testFolder);
        if (!Directory.Exists(variantPath))
        {
            Console.WriteLine($"Test directory not found: {variantPath}");
            return summary;
        }

        var stopwatch = Stopwatch.StartNew();

        // Get all JSON files (may be .json or inside .gz files)
        var testFiles = GetTestFiles(variantPath);

        // Filter out illegal opcodes if needed
        if (skipIllegal)
        {
            testFiles = FilterOutIllegalOpcodes(testFiles);
        }

        summary.TotalOpcodes = testFiles.Count;

        var modeNote = testMode == TestMode.Complete ? " [Complete]" : " [Bookend]";
        if (skipIllegal)
        {
            modeNote += " (skipping illegal opcodes)";
        }
        Console.WriteLine($"\nRunning {variantName} tests ({testFiles.Count} opcodes){modeNote}...\n");

        int fileIndex = 0;
        foreach (var testFile in testFiles.OrderBy(f => f))
        {
            fileIndex++;
            var opcodeName = Path.GetFileNameWithoutExtension(testFile);
            if (opcodeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                opcodeName = Path.GetFileNameWithoutExtension(opcodeName);
            }

            // Show loading indicator for Complete mode (which can be slow)
            if (testMode == TestMode.Complete)
            {
                Console.Write($"Loading {opcodeName}... ");
                Console.Out.Flush();
            }

            var opcodeSummary = RunOpcodeTests(testFile, cpuVariant, opcodeName, verboseOutput, testMode);
            summary.OpcodeSummaries.Add(opcodeSummary);
            summary.TotalTests += opcodeSummary.TotalTests;
            summary.PassedTests += opcodeSummary.PassedTests;
            summary.FailedTests += opcodeSummary.FailedTests;

            // Progress indicator - filter based on resultsFilter
            var hasFailed = opcodeSummary.FailedTests > 0;
            var hasPassed = opcodeSummary.PassedTests > 0;

            var shouldShow = resultsFilter switch
            {
                ResultsFilter.Fail => hasFailed,
                ResultsFilter.Pass => !hasFailed,
                _ => true // ResultsFilter.All
            };

            if (shouldShow)
            {
                var status = hasFailed ? "FAIL" : "PASS";
                var color = hasFailed ? ConsoleColor.Red : ConsoleColor.Green;

                // For Complete mode, clear the "Loading..." message with a carriage return
                if (testMode == TestMode.Complete)
                {
                    Console.Write("\r");
                }

                Console.ForegroundColor = color;
                Console.Write($"[{status}]");
                Console.ResetColor();
                Console.WriteLine($" {opcodeName}: {opcodeSummary.PassedTests}/{opcodeSummary.TotalTests} passed");

                if (verboseOutput && hasFailed)
                {
                    foreach (var failed in opcodeSummary.FailedTestDetails.Take(5))
                    {
                        PrintVerboseFailure(failed);
                    }
                    if (opcodeSummary.FailedTestDetails.Count > 5)
                    {
                        Console.WriteLine($"       ... and {opcodeSummary.FailedTestDetails.Count - 5} more failures");
                    }
                }
            }
            else if (testMode == TestMode.Complete)
            {
                // Clear the "Loading..." message if we're not showing results
                Console.Write("\r                              \r");
            }
        }

        stopwatch.Stop();
        summary.Duration = stopwatch.Elapsed;

        return summary;
    }

    /// <summary>
    /// Gets test files from a variant directory, supporting both .json and .json.gz files.
    /// </summary>
    private static List<string> GetTestFiles(string variantPath)
    {
        List<string> files = new();

        // Check for v1 (uncompressed) directory structure
        var v1Path = Path.Combine(variantPath, "v1");
        if (Directory.Exists(v1Path))
        {
            files.AddRange(Directory.GetFiles(v1Path, "*.json"));
            files.AddRange(Directory.GetFiles(v1Path, "*.json.gz"));
        }
        else
        {
            // Direct files in variant directory
            files.AddRange(Directory.GetFiles(variantPath, "*.json"));
            files.AddRange(Directory.GetFiles(variantPath, "*.json.gz"));
        }

        return files;
    }

    /// <summary>
    /// Filters out test files for illegal/undefined opcodes.
    /// </summary>
    private static List<string> FilterOutIllegalOpcodes(List<string> testFiles)
    {
        return [.. testFiles.Where(file =>
        {
            // Extract opcode from filename (e.g., "02.json" -> 0x02)
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }

            // Try to parse as hex opcode
            if (byte.TryParse(fileName, System.Globalization.NumberStyles.HexNumber, null, out byte opcode))
            {
                return !IllegalOpcodes.Contains(opcode);
            }

            // If we can't parse it, include it
            return true;
        })];
    }

    /// <summary>
    /// Runs all tests for a specific opcode.
    /// </summary>
    private OpcodeTestSummary RunOpcodeTests(string testFilePath, CpuVariant variant, string opcodeName, bool _, TestMode testMode)
    {
        var summary = new OpcodeTestSummary
        {
            OpcodeName = opcodeName
        };

        try
        {
            var testCases = LoadTestCases(testFilePath);
            summary.TotalTests = testCases.Count;

            var state = new CpuState();
            var cpu = CpuFactory.Create(variant, state);

            for (int testIndex = 0; testIndex < testCases.Count; testIndex++)
            {
                var testCase = testCases[testIndex];
                var result = RunSingleTest(testCase, cpu, testMode);
                result.TestIndex = testIndex;
                if (result.Passed)
                {
                    summary.PassedTests++;
                }
                else
                {
                    summary.FailedTests++;
                    summary.FailedTestDetails.Add(result);
                }
            }
        }
        catch (Exception ex)
        {
            summary.FailedTests = 1;
            summary.TotalTests = 1;
            summary.FailedTestDetails.Add(new HarteTestResult
            {
                TestName = opcodeName,
                Passed = false,
                FailureReason = $"Failed to load test file: {ex.Message}"
            });
        }

        return summary;
    }

    /// <summary>
    /// Loads test cases from a JSON file (supports gzip compression).
    /// Returns an empty list for empty files (e.g., WAI/STP opcodes have no tests).
    /// </summary>
    private static List<HarteTestCase> LoadTestCases(string filePath)
    {
        // Check for empty file (some opcodes like WAI/STP have no tests)
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            return [];
        }

        if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var fileStream = File.OpenRead(filePath);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<List<HarteTestCase>>(gzipStream) ?? [];
        }
        else
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<List<HarteTestCase>>(stream) ?? [];
        }
    }

    /// <summary>
    /// Runs a single test case and returns the result.
    /// </summary>
    /// <param name="testCase">The test case to run.</param>
    /// <param name="variant">The CPU variant to test.</param>
    /// <param name="testMode">Whether to run bookend or complete validation.</param>
    private HarteTestResult RunSingleTest(HarteTestCase testCase, IPandowdyCpu cpu, TestMode testMode)
    {
        var result = new HarteTestResult
        {
            TestName = testCase.Name,
            ExpectedCycles = testCase.Cycles?.Count ?? 0
        };

        try
        {
            // Initialize bus with test RAM state
            _bus.Clear();
            if (testCase.Initial.Ram != null)
            {
                foreach (var ramEntry in testCase.Initial.Ram)
                {
                    if (ramEntry.Count >= 2)
                    {
                        _bus.SetMemory((ushort)ramEntry[0], (byte)ramEntry[1]);
                    }
                }
            }

            // Initialize CPU state
            InitializeCpuState(cpu, testCase.Initial);

            // Capture initial state for diagnostics
            result.InitialPC = (ushort)testCase.Initial.PC;
            result.InitialA = (byte)testCase.Initial.A;
            result.InitialX = (byte)testCase.Initial.X;
            result.InitialY = (byte)testCase.Initial.Y;
            result.InitialSP = (byte)testCase.Initial.S;
            result.InitialP = (byte)testCase.Initial.P;

            // Capture instruction bytes (up to 3 bytes from PC)
            var instrBytes = new List<byte>();
            for (int i = 0; i < 3; i++)
            {
                var addr = (ushort)(result.InitialPC + i);
                instrBytes.Add(_bus.Peek(addr));
            }
            result.InstructionBytes = [.. instrBytes];

            // Enable cycle tracking for complete mode
            if (testMode == TestMode.Complete)
            {
                _bus.StartCycleTracking();
            }

            // Execute one instruction
            result.ActualCycles = cpu.Step(_bus);

            // Stop cycle tracking
            if (testMode == TestMode.Complete)
            {
                _bus.StopCycleTracking();
            }

            // Capture expected cycles for diagnostics
            if (testCase.Cycles != null)
            {
                for (int i = 0; i < testCase.Cycles.Count; i++)
                {
                    var cycle = testCase.Cycles[i];
                    if (cycle.Count >= 3)
                    {
                        result.ExpectedCycleDetails.Add(new()
                        {
                            CycleIndex = i,
                            Address = GetUInt16FromJsonElement(cycle[0]),
                            Value = GetByteFromJsonElement(cycle[1]),
                            IsWrite = GetStringFromJsonElement(cycle[2]).Equals("write", StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }

            // Capture actual cycles for diagnostics
            if (testMode == TestMode.Complete)
            {
                for (int i = 0; i < _bus.RecordedCycles.Count; i++)
                {
                    var cycle = _bus.RecordedCycles[i];
                    result.ActualCycleDetails.Add(new()
                    {
                        CycleIndex = i,
                        Address = cycle.Address,
                        Value = cycle.Value,
                        IsWrite = cycle.IsWrite
                    });
                }
            }

            // Verify final state
            var verificationResult = VerifyFinalState(cpu, testCase.Final);
            if (verificationResult != null)
            {
                result.Passed = false;
                result.FailureReason = verificationResult;
                return result;
            }

            // Verify cycle count (if cycles are specified)
            if (testCase.Cycles != null && result.ActualCycles != result.ExpectedCycles)
            {
                result.Passed = false;
                result.FailureReason = $"Cycle mismatch: expected {result.ExpectedCycles}, got {result.ActualCycles}";
                return result;
            }

            // For complete mode, verify cycle-by-cycle bus activity
            if (testMode == TestMode.Complete && testCase.Cycles != null)
            {
                var cycleVerification = VerifyCycles(testCase.Cycles, _bus.RecordedCycles);
                if (cycleVerification != null)
                {
                    result.Passed = false;
                    result.FailureReason = cycleVerification;
                    return result;
                }
            }

            result.Passed = true;
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.FailureReason = $"Exception: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Verifies cycle-by-cycle bus activity matches expected cycles.
    /// </summary>
    /// <param name="expectedCycles">Expected cycles from test case.</param>
    /// <param name="actualCycles">Recorded bus cycles.</param>
    /// <returns>Null if verification passes, error message otherwise.</returns>
    private static string? VerifyCycles(List<List<object>> expectedCycles, IReadOnlyList<BusCycle> actualCycles)
    {
        if (expectedCycles.Count != actualCycles.Count)
        {
            return $"Cycle count mismatch: expected {expectedCycles.Count} bus operations, got {actualCycles.Count}";
        }

        for (int i = 0; i < expectedCycles.Count; i++)
        {
            var expected = expectedCycles[i];
            var actual = actualCycles[i];

            if (expected.Count < 3)
            {
                continue; // Skip malformed cycle entries
            }

            // Parse expected values: [address, value, "read"|"write"]
            // Note: System.Text.Json deserializes numbers as JsonElement, so we need to handle that
            var expectedAddr = GetUInt16FromJsonElement(expected[0]);
            var expectedValue = GetByteFromJsonElement(expected[1]);
            var expectedType = GetStringFromJsonElement(expected[2]);
            var expectedIsWrite = expectedType.Equals("write", StringComparison.OrdinalIgnoreCase);

            if (actual.Address != expectedAddr)
            {
                return $"Cycle {i}: address mismatch - expected ${expectedAddr:X4}, got ${actual.Address:X4}";
            }

            if (actual.Value != expectedValue)
            {
                return $"Cycle {i}: value mismatch at ${expectedAddr:X4} - expected ${expectedValue:X2}, got ${actual.Value:X2}";
            }

            if (actual.IsWrite != expectedIsWrite)
            {
                var expectedOp = expectedIsWrite ? "write" : "read";
                var actualOp = actual.IsWrite ? "write" : "read";
                return $"Cycle {i}: operation mismatch at ${expectedAddr:X4} - expected {expectedOp}, got {actualOp}";
            }
        }

        return null; // All cycles match
    }

    /// <summary>
    /// Extracts a ushort value from an object that may be a JsonElement or numeric type.
    /// </summary>
    private static ushort GetUInt16FromJsonElement(object obj)
    {
        if (obj is JsonElement jsonElement)
        {
            return jsonElement.GetUInt16();
        }
        return Convert.ToUInt16(obj);
    }

    /// <summary>
    /// Extracts a byte value from an object that may be a JsonElement or numeric type.
    /// </summary>
    private static byte GetByteFromJsonElement(object obj)
    {
        if (obj is JsonElement jsonElement)
        {
            return jsonElement.GetByte();
        }
        return Convert.ToByte(obj);
    }

    /// <summary>
    /// Extracts a string value from an object that may be a JsonElement or string type.
    /// </summary>
    private static string GetStringFromJsonElement(object obj)
    {
        if (obj is JsonElement jsonElement)
        {
            return jsonElement.GetString() ?? "";
        }
        return obj?.ToString() ?? "";
    }

    /// <summary>
    /// Initializes CPU state from a HarteState.
    /// </summary>
    private static void InitializeCpuState(IPandowdyCpu cpu, HarteState state)
    {
        // Reset CPU state to clear any previous state
        cpu.State.Reset();

        // Set registers on CPU state
        var cpuState = cpu.State;
        cpuState.PC = (ushort)state.PC;
        cpuState.SP = (byte)state.S;
        cpuState.A = (byte)state.A;
        cpuState.X = (byte)state.X;
        cpuState.Y = (byte)state.Y;
        cpuState.P = (byte)state.P;
    }

    /// <summary>
    /// Verifies the final CPU and memory state matches expected.
    /// Returns null if verification passes, or an error message if it fails.
    /// </summary>
    private string? VerifyFinalState(IPandowdyCpu cpu, HarteState expected)
    {
        var actual = cpu.State; // After Step, the result state is in cpu.State

        // Check registers
        if (actual.PC != (ushort)expected.PC)
        {
            return $"PC mismatch: expected ${expected.PC:X4}, got ${actual.PC:X4}";
        }

        if (actual.SP != (byte)expected.S)
        {
            return $"SP mismatch: expected ${expected.S:X2}, got ${actual.SP:X2}";
        }

        if (actual.A != (byte)expected.A)
        {
            return $"A mismatch: expected ${expected.A:X2}, got ${actual.A:X2}";
        }

        if (actual.X != (byte)expected.X)
        {
            return $"X mismatch: expected ${expected.X:X2}, got ${actual.X:X2}";
        }

        if (actual.Y != (byte)expected.Y)
        {
            return $"Y mismatch: expected ${expected.Y:X2}, got ${actual.Y:X2}";
        }

        if (actual.P != (byte)expected.P)
        {
            return $"P mismatch: expected ${expected.P:X2}, got ${actual.P:X2}";
        }

        // Check RAM
        if (expected.Ram != null)
        {
            foreach (var ramEntry in expected.Ram)
            {
                if (ramEntry.Count >= 2)
                {
                    var address = (ushort)ramEntry[0];
                    var expectedValue = (byte)ramEntry[1];
                    var actualValue = _bus.Memory[address];

                    if (actualValue != expectedValue)
                    {
                        return $"RAM[${address:X4}] mismatch: expected ${expectedValue:X2}, got ${actualValue:X2}";
                    }
                }
            }
        }

        return null; // All checks passed
    }

    /// <summary>
    /// Prints detailed diagnostic information for a failed test.
    /// </summary>
    private static void PrintVerboseFailure(HarteTestResult failed)
    {
        Console.WriteLine($"       ─────────────────────────────────────────────────────────");
        Console.WriteLine($"       [#{failed.TestIndex}] {failed.TestName}");
        Console.WriteLine($"       Failure: {failed.FailureReason}");
        Console.WriteLine($"       Initial: {failed.GetInitialStateString()}");
        Console.WriteLine($"       Instruction bytes: {failed.GetInstructionBytesString()}");

        // Calculate derived values for branch instructions
        if (failed.InstructionBytes.Length >= 2)
        {
            var opcode = failed.InstructionBytes[0];
            // Check if this is a branch instruction (opcode & 0x1F == 0x10)
            if ((opcode & 0x1F) == 0x10)
            {
                var offset = (sbyte)failed.InstructionBytes[1];
                var pcAfterFetch = (ushort)(failed.InitialPC + 2);
                var branchTarget = (ushort)(pcAfterFetch + offset);
                var wrongAddr = (ushort)((pcAfterFetch & 0xFF00) | (branchTarget & 0x00FF));
                var crossesPage = (pcAfterFetch >> 8) != (branchTarget >> 8);

                Console.WriteLine($"       Branch analysis:");
                Console.WriteLine($"         Offset: ${failed.InstructionBytes[1]:X2} (signed: {offset})");
                Console.WriteLine($"         PC after fetch: ${pcAfterFetch:X4}");
                Console.WriteLine($"         Branch target: ${branchTarget:X4}");
                Console.WriteLine($"         Page cross: {(crossesPage ? "YES" : "NO")}");
                if (crossesPage)
                {
                    Console.WriteLine($"         Wrong addr (old PCH : new PCL): ${wrongAddr:X4}");
                }
            }
        }

        // Show cycle comparison
        var maxCycles = Math.Max(failed.ExpectedCycleDetails.Count, failed.ActualCycleDetails.Count);
        if (maxCycles > 0)
        {
            Console.WriteLine($"       Cycles (Expected vs Actual):");
            for (int i = 0; i < maxCycles; i++)
            {
                var expected = i < failed.ExpectedCycleDetails.Count ? failed.ExpectedCycleDetails[i] : null;
                var actual = i < failed.ActualCycleDetails.Count ? failed.ActualCycleDetails[i] : null;

                var expStr = expected != null
                    ? $"${expected.Address:X4}=${expected.Value:X2} ({(expected.IsWrite ? "W" : "R")})"
                    : "(none)";
                var actStr = actual != null
                    ? $"${actual.Address:X4}=${actual.Value:X2} ({(actual.IsWrite ? "W" : "R")})"
                    : "(none)";

                // Highlight mismatches
                var match = expected != null && actual != null &&
                            expected.Address == actual.Address &&
                            expected.Value == actual.Value &&
                            expected.IsWrite == actual.IsWrite;

                if (!match)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                Console.WriteLine($"         T{i}: {expStr,-22} | {actStr}");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Prints a summary of test results.
    /// </summary>
    public static void PrintSummary(VariantTestSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"Test Summary: {summary.VariantName}");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"Total Opcodes:  {summary.TotalOpcodes}");
        Console.WriteLine($"Total Tests:    {summary.TotalTests:N0}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Passed:         {summary.PassedTests:N0}");
        Console.ResetColor();

        if (summary.FailedTests > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed:         {summary.FailedTests:N0}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"Failed:         {summary.FailedTests:N0}");
        }

        var passRate = summary.TotalTests > 0
            ? (summary.PassedTests * 100.0 / summary.TotalTests)
            : 0;
        Console.WriteLine($"Pass Rate:      {passRate:F2}%");
        Console.WriteLine($"Duration:       {summary.Duration.TotalSeconds:F2} seconds");
        Console.WriteLine(new string('=', 60));

        // List failing opcodes
        var failingOpcodes = summary.OpcodeSummaries
            .Where(o => o.FailedTests > 0)
            .OrderByDescending(o => o.FailedTests)
            .ToList();

        if (failingOpcodes.Count > 0)
        {
            // Sort by opcode value ($00 to $FF)
            var sortedOpcodes = failingOpcodes
                .OrderBy(o =>
                {
                    // Parse opcode name as hex value for proper sorting
                    if (byte.TryParse(o.OpcodeName, System.Globalization.NumberStyles.HexNumber, null, out byte opcodeValue))
                    {
                        return opcodeValue;
                    }
                    return 256; // Put non-parseable names at the end
                })
                .ToList();

            Console.WriteLine("\nFailing Opcodes:");
            foreach (var opcode in sortedOpcodes)
            {
                Console.WriteLine($"  ${opcode.OpcodeName}: {opcode.FailedTests}/{opcode.TotalTests} failed");
            }
        }
    }
}
