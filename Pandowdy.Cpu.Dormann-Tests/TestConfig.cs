// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pandowdy.Cpu.Dormann_Tests;

/// <summary>
/// Configuration for an individual test.
/// </summary>
public class TestFileConfig
{
    /// <summary>
    /// Name of the hex file for this test.
    /// </summary>
    [JsonPropertyName("hexFile")]
    public string HexFile { get; set; } = string.Empty;

    /// <summary>
    /// Start address for execution (hex string like "0400").
    /// </summary>
    [JsonPropertyName("startAddress")]
    public string StartAddress { get; set; } = "0400";

    /// <summary>
    /// Success address where PC loops on success (hex string like "3469").
    /// Null for tests that use other success criteria (like memory location).
    /// </summary>
    [JsonPropertyName("successAddress")]
    public string? SuccessAddress { get; set; }

    /// <summary>
    /// Gets the start address as a ushort.
    /// </summary>
    [JsonIgnore]
    public ushort StartAddressValue => Convert.ToUInt16(StartAddress, 16);

    /// <summary>
    /// Gets the success address as a ushort, or null if not specified.
    /// </summary>
    [JsonIgnore]
    public ushort? SuccessAddressValue => 
        string.IsNullOrEmpty(SuccessAddress) ? null : Convert.ToUInt16(SuccessAddress, 16);
}

/// <summary>
/// Configuration for the decimal test (uses memory location for success).
/// </summary>
public class DecimalTestConfig : TestFileConfig
{
    /// <summary>
    /// Memory address that contains the error flag (hex string like "000B").
    /// </summary>
    [JsonPropertyName("errorAddress")]
    public string ErrorAddress { get; set; } = "000B";

    /// <summary>
    /// Gets the error address as a ushort.
    /// </summary>
    [JsonIgnore]
    public ushort ErrorAddressValue => Convert.ToUInt16(ErrorAddress, 16);
}

/// <summary>
/// Root configuration for all Dormann tests.
/// </summary>
public class TestConfig
{
    /// <summary>
    /// Default path to the test data directory.
    /// Can be overridden by command line argument or environment variable.
    /// </summary>
    [JsonPropertyName("testDataPath")]
    public string TestDataPath { get; set; } = "./testdata";

    /// <summary>
    /// Configuration for the 6502 functional test (all variants).
    /// </summary>
    [JsonPropertyName("functionalTest")]
    public TestFileConfig FunctionalTest { get; set; } = new()
    {
        HexFile = "6502_functional_test.hex",
        StartAddress = "0400",
        SuccessAddress = "3469"
    };

    /// <summary>
    /// Configuration for the NMOS 6502 decimal test.
    /// </summary>
    [JsonPropertyName("nmosDecimalTest")]
    public DecimalTestConfig NmosDecimalTest { get; set; } = new()
    {
        HexFile = "6502_decimal_test.hex",
        StartAddress = "0200",
        ErrorAddress = "000B"
    };

    /// <summary>
    /// Configuration for the 65C02 decimal test.
    /// </summary>
    [JsonPropertyName("cmosDecimalTest")]
    public DecimalTestConfig CmosDecimalTest { get; set; } = new()
    {
        HexFile = "65c02_decimal_test.hex",
        StartAddress = "0200",
        ErrorAddress = "000B"
    };

    /// <summary>
    /// Configuration for the WDC 65C02 extended opcodes test.
    /// </summary>
    [JsonPropertyName("wdcExtendedTest")]
    public TestFileConfig WdcExtendedTest { get; set; } = new()
    {
        HexFile = "65c02_extended_opcodes_test.hex",
        StartAddress = "0400",
        SuccessAddress = "24F1"  // With wdc_op=1, rkwl_wdc_op=1
    };

    /// <summary>
    /// Configuration for the Rockwell 65C02 extended opcodes test.
    /// </summary>
    [JsonPropertyName("rockwellExtendedTest")]
    public TestFileConfig RockwellExtendedTest { get; set; } = new()
    {
        HexFile = "65c02_extended_opcodes_test.hex",
        StartAddress = "0400",
        SuccessAddress = "24F1"  // With wdc_op=0, rkwl_wdc_op=1
    };

    /// <summary>
    /// Default configuration file name.
    /// </summary>
    public const string DefaultConfigFileName = "dormann-tests.json";

    /// <summary>
    /// Loads configuration from a JSON file, or returns defaults if file doesn't exist.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <returns>Loaded or default configuration.</returns>
    public static TestConfig Load(string? configPath = null)
    {
        string path = configPath ?? DefaultConfigFileName;

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<TestConfig>(json);
                if (config != null)
                {
                    return config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load config from {path}: {ex.Message}");
                Console.WriteLine("Using default configuration.");
            }
        }

        return new TestConfig();
    }

    /// <summary>
    /// Saves configuration to a JSON file.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    public void Save(string? configPath = null)
    {
        string path = configPath ?? DefaultConfigFileName;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Creates a default configuration file if one doesn't exist.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <returns>True if file was created, false if it already exists.</returns>
    public static bool CreateDefaultIfMissing(string? configPath = null)
    {
        string path = configPath ?? DefaultConfigFileName;

        if (!File.Exists(path))
        {
            var config = new TestConfig();
            config.Save(path);
            return true;
        }

        return false;
    }
}
