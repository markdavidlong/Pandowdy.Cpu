// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Text.Json.Serialization;

namespace Pandowdy.Cpu.Harte_SST_Tests;

/// <summary>
/// Represents a single test case from Tom Harte's SingleStepTests.
/// </summary>
public class HarteTestCase
{
    /// <summary>
    /// Test case name/identifier.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Initial CPU and memory state before instruction execution.
    /// </summary>
    [JsonPropertyName("initial")]
    public HarteState Initial { get; set; } = new();

    /// <summary>
    /// Expected CPU and memory state after instruction execution.
    /// </summary>
    [JsonPropertyName("final")]
    public HarteState Final { get; set; } = new();

    /// <summary>
    /// Expected bus cycles during instruction execution.
    /// Each entry is [address, value, type] where type is "read" or "write".
    /// </summary>
    [JsonPropertyName("cycles")]
    public List<List<object>>? Cycles { get; set; }
}

/// <summary>
/// Represents CPU state (registers and memory) at a point in time.
/// </summary>
public class HarteState
{
    /// <summary>
    /// Program Counter (16-bit).
    /// </summary>
    [JsonPropertyName("pc")]
    public int PC { get; set; }

    /// <summary>
    /// Stack Pointer (8-bit).
    /// </summary>
    [JsonPropertyName("s")]
    public int S { get; set; }

    /// <summary>
    /// Accumulator (8-bit).
    /// </summary>
    [JsonPropertyName("a")]
    public int A { get; set; }

    /// <summary>
    /// X Index Register (8-bit).
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// Y Index Register (8-bit).
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>
    /// Processor Status Register (8-bit).
    /// </summary>
    [JsonPropertyName("p")]
    public int P { get; set; }

    /// <summary>
    /// RAM state as list of [address, value] pairs.
    /// </summary>
    [JsonPropertyName("ram")]
    public List<List<int>>? Ram { get; set; }
}

/// <summary>
/// Represents a single cycle for diagnostic output.
/// </summary>
public class CycleDiagnostic
{
    public int CycleIndex { get; set; }
    public ushort Address { get; set; }
    public byte Value { get; set; }
    public bool IsWrite { get; set; }

    public override string ToString() =>
        $"T{CycleIndex}: ${Address:X4} = ${Value:X2} ({(IsWrite ? "W" : "R")})";
}

/// <summary>
/// Result of running a single test case.
/// </summary>
public class HarteTestResult
{
    public string TestName { get; set; } = string.Empty;
    public int TestIndex { get; set; }
    public bool Passed { get; set; }
    public string? FailureReason { get; set; }
    public int ExpectedCycles { get; set; }
    public int ActualCycles { get; set; }

    // Diagnostic data for verbose output
    public ushort InitialPC { get; set; }
    public byte InitialA { get; set; }
    public byte InitialX { get; set; }
    public byte InitialY { get; set; }
    public byte InitialSP { get; set; }
    public byte InitialP { get; set; }
    public byte[] InstructionBytes { get; set; } = [];
    public List<CycleDiagnostic> ExpectedCycleDetails { get; set; } = [];
    public List<CycleDiagnostic> ActualCycleDetails { get; set; } = [];

    /// <summary>
    /// Gets a formatted string showing initial CPU state.
    /// </summary>
    public string GetInitialStateString()
    {
        var flags = FormatFlags(InitialP);
        return $"PC=${InitialPC:X4} A=${InitialA:X2} X=${InitialX:X2} Y=${InitialY:X2} SP=${InitialSP:X2} P=${InitialP:X2} [{flags}]";
    }

    /// <summary>
    /// Gets a formatted string showing instruction bytes.
    /// </summary>
    public string GetInstructionBytesString()
    {
        if (InstructionBytes.Length == 0)
        {
            return "";
        }
        return string.Join(" ", InstructionBytes.Select(b => $"${b:X2}"));
    }

    private static string FormatFlags(byte p)
    {
        var sb = new System.Text.StringBuilder(8);
        sb.Append((p & 0x80) != 0 ? 'N' : '-');
        sb.Append((p & 0x40) != 0 ? 'V' : '-');
        sb.Append('-'); // Bit 5 is unused
        sb.Append((p & 0x10) != 0 ? 'B' : '-');
        sb.Append((p & 0x08) != 0 ? 'D' : '-');
        sb.Append((p & 0x04) != 0 ? 'I' : '-');
        sb.Append((p & 0x02) != 0 ? 'Z' : '-');
        sb.Append((p & 0x01) != 0 ? 'C' : '-');
        return sb.ToString();
    }
}

/// <summary>
/// Summary of running all tests for an opcode.
/// </summary>
public class OpcodeTestSummary
{
    public string OpcodeName { get; set; } = string.Empty;
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public List<HarteTestResult> FailedTestDetails { get; set; } = [];
}

/// <summary>
/// Summary of running all tests for a CPU variant.
/// </summary>
public class VariantTestSummary
{
    public string VariantName { get; set; } = string.Empty;
    public int TotalOpcodes { get; set; }
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedOpcodes { get; set; }
    public TimeSpan Duration { get; set; }
    public List<OpcodeTestSummary> OpcodeSummaries { get; set; } = [];
}
