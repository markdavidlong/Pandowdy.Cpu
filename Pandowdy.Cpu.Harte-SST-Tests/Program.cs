// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Harte_SST_Tests;

/// <summary>
/// Filter for which test results to display.
/// </summary>
public enum ResultsFilter
{
    /// <summary>Show all test results.</summary>
    All,
    /// <summary>Show only passing tests.</summary>
    Pass,
    /// <summary>Show only failing tests.</summary>
    Fail
}

/// <summary>
/// Test mode determining what aspects of execution are validated.
/// </summary>
public enum TestMode
{
    /// <summary>Only verify initial and final states (registers, memory, cycle count).</summary>
    Bookend,
    /// <summary>Verify complete execution including cycle-by-cycle bus activity.</summary>
    Complete
}

internal class Program
{
    // Default path to Tom Harte's SingleStepTests repository
    private const string DefaultTestPath =  "./";
    
    static void Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("  Pandowdy CPU - Tom Harte SingleStepTests");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Allow test path override via command line or environment variable
        var testPath = GetTestPath(args);

        if (!Directory.Exists(testPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Test directory not found: {testPath}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Please ensure the SingleStepTests repository is available.");
            Console.WriteLine("Repository: https://github.com/SingleStepTests/65x02");
            Console.WriteLine();
            Console.WriteLine("You can specify the path via:");
            Console.WriteLine("  - Command line argument: dotnet run -- \"path/to/65x02\"");
            Console.WriteLine("  - Environment variable: HARTE_SST_PATH");
            return;
        }

        Console.WriteLine($"Test path: {testPath}");
        Console.WriteLine();

        var runner = new HarteTestRunner(testPath);
        var availableVariants = runner.GetAvailableVariants();

        if (availableVariants.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No test variants found in the test directory.");
            Console.ResetColor();
            Console.WriteLine("Expected subdirectories: 6502, rockwell65c02, wdc65c02");
            return;
        }

        // Check for command line variant selection
        var selectedVariant = GetVariantFromArgs(args, availableVariants);
        if (selectedVariant != null)
        {
            var verbose = args.Contains("--verbose") || args.Contains("-v");
            var filter = GetResultsFilterFromArgs(args);
            var testMode = GetTestModeFromArgs(args);
            RunVariantTests(runner, selectedVariant, verbose, filter, testMode);
            return;
        }

        // Interactive menu
        RunInteractiveMenu(runner, availableVariants);
    }

    private static string GetTestPath(string[] args)
    {
        // Check command line args first (non-flag arguments)
        foreach (var arg in args)
        {
            if (!arg.StartsWith("-") && Directory.Exists(arg))
            {
                return arg;
            }
        }

        // Check environment variable
        var envPath = Environment.GetEnvironmentVariable("HARTE_SST_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }

        return DefaultTestPath;
    }

    private static string? GetVariantFromArgs(string[] args, List<string> availableVariants)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("--variant="))
            {
                return arg["--variant=".Length..];
            }

            if (availableVariants.Contains(arg, StringComparer.OrdinalIgnoreCase))
            {
                return arg;
            }
        }

        // Check for --all flag
        if (args.Contains("--all"))
        {
            return "all";
        }

        return null;
    }

    private static ResultsFilter GetResultsFilterFromArgs(string[] args)
    {
        if (args.Contains("--failures-only") || args.Contains("-f") || args.Contains("--fail"))
        {
            return ResultsFilter.Fail;
        }

        if (args.Contains("--pass-only") || args.Contains("-p") || args.Contains("--pass"))
        {
            return ResultsFilter.Pass;
        }

        return ResultsFilter.All;
    }

    private static TestMode GetTestModeFromArgs(string[] args)
    {
        if (args.Contains("--complete") || args.Contains("-c"))
        {
            return TestMode.Complete;
        }

        // Default to bookend mode
        return TestMode.Bookend;
    }

    private static void RunInteractiveMenu(HarteTestRunner runner, List<string> availableVariants)
    {
        while (true)
        {
            Console.WriteLine("Available CPU variants:");
            Console.WriteLine();

            for (int i = 0; i < availableVariants.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {availableVariants[i]}");
            }

            Console.WriteLine($"  {availableVariants.Count + 1}. Run ALL variants");
            Console.WriteLine($"  {availableVariants.Count + 2}. Exit");
            Console.WriteLine();
            Console.Write("Select option: ");

            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (int.TryParse(input, out int selection))
            {
                if (selection >= 1 && selection <= availableVariants.Count)
                {
                    var variant = availableVariants[selection - 1];
                    var (verbose, filter, testMode) = PromptForOutputOptions();

                    RunVariantTests(runner, variant, verbose, filter, testMode);
                    Console.WriteLine();
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    Console.Clear();
                }
                else if (selection == availableVariants.Count + 1)
                {
                    var (verbose, filter, testMode) = PromptForOutputOptions();

                    RunAllVariants(runner, availableVariants, verbose, filter, testMode);
                    Console.WriteLine();
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    Console.Clear();
                }
                else if (selection == availableVariants.Count + 2)
                {
                    Console.WriteLine("Goodbye!");
                    return;
                }
            }
            else if (input.Equals("q", StringComparison.OrdinalIgnoreCase) ||
                     input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                return;
            }
        }
    }

    private static (bool verbose, ResultsFilter filter, TestMode testMode) PromptForOutputOptions()
    {
        Console.Write("Test mode - (B)ookend (state only) or (c)omplete (with cycles): ");
        var modeInput = Console.ReadLine()?.Trim().ToLowerInvariant();
        var testMode = modeInput switch
        {
            "c" or "complete" => TestMode.Complete,
            _ => TestMode.Bookend
        };

        Console.Write("Verbose output? (y/N): ");
        var verboseInput = Console.ReadLine()?.Trim().ToLowerInvariant();
        var verbose = verboseInput == "y" || verboseInput == "yes";

        Console.Write("Results to show - (A)ll, (p)ass only, or (f)ail only: ");
        var filterInput = Console.ReadLine()?.Trim().ToLowerInvariant();

        var filter = filterInput switch
        {
            "p" or "pass" => ResultsFilter.Pass,
            "f" or "fail" or "failures" => ResultsFilter.Fail,
            _ => ResultsFilter.All
        };

        return (verbose, filter, testMode);
    }

    private static void RunVariantTests(HarteTestRunner runner, string variant, bool verbose, ResultsFilter filter, TestMode testMode)
    {
        if (variant.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var variants = runner.GetAvailableVariants();
            RunAllVariants(runner, variants, verbose, filter, testMode);
            return;
        }

        Console.WriteLine();
        var summary = runner.RunVariantTests(variant, verbose, filter, testMode);
        HarteTestRunner.PrintSummary(summary);
    }

    private static void RunAllVariants(HarteTestRunner runner, List<string> variants, bool verbose, ResultsFilter filter, TestMode testMode)
    {
        var allSummaries = new List<VariantTestSummary>();

        foreach (var variant in variants)
        {
            var summary = runner.RunVariantTests(variant, verbose, filter, testMode);
            allSummaries.Add(summary);
            HarteTestRunner.PrintSummary(summary);
        }

        // Print overall summary
        Console.WriteLine();
        Console.WriteLine("############################################################");
        Console.WriteLine("                     OVERALL SUMMARY");
        Console.WriteLine("############################################################");


        int totalTests = allSummaries.Sum(s => s.TotalTests);
        int totalPassed = allSummaries.Sum(s => s.PassedTests);
        int totalFailed = allSummaries.Sum(s => s.FailedTests);
        var totalDuration = TimeSpan.FromTicks(allSummaries.Sum(s => s.Duration.Ticks));

        Console.WriteLine();
        Console.WriteLine($"Total Variants: {allSummaries.Count}");
        Console.WriteLine($"Total Tests:    {totalTests:N0}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Total Passed:   {totalPassed:N0}");
        Console.ResetColor();

        if (totalFailed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Total Failed:   {totalFailed:N0}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"Total Failed:   {totalFailed:N0}");
        }

        var passRate = totalTests > 0 ? (totalPassed * 100.0 / totalTests) : 0;
        Console.WriteLine($"Overall Rate:   {passRate:F2}%");
        Console.WriteLine($"Total Duration: {totalDuration.TotalSeconds:F2} seconds");
        Console.WriteLine();

        // Summary by variant
        foreach (var summary in allSummaries)
        {
            var rate = summary.TotalTests > 0
                ? (summary.PassedTests * 100.0 / summary.TotalTests)
                : 0;
            var statusColor = summary.FailedTests == 0 ? ConsoleColor.Green : ConsoleColor.Red;

            Console.ForegroundColor = statusColor;
            Console.Write(summary.FailedTests == 0 ? "[PASS]" : "[FAIL]");
            Console.ResetColor();
            Console.WriteLine($" {summary.VariantName}: {summary.PassedTests:N0}/{summary.TotalTests:N0} ({rate:F2}%)");
        }

        Console.WriteLine();
    }
}

