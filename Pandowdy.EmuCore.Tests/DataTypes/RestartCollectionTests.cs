// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Tests.DataTypes;

/// <summary>
/// Tests for <see cref="RestartCollection"/> — the flat, priority-ordered batch
/// restart mechanism that replaces tree-based dispatch for cold boot.
/// </summary>
public class RestartCollectionTests
{
    #region Test Helpers

    /// <summary>
    /// Shared log that tracks the order in which <see cref="IRestartable.Restart"/>
    /// is called across all test instances in a single test run.
    /// </summary>
    private readonly List<string> _restartLog = [];

    /// <summary>
    /// Default-priority restartable (no <see cref="CapabilityAttribute"/>).
    /// Priority resolves to 0.
    /// </summary>
    private class PlainRestartable(string name, List<string> log) : IRestartable
    {
        public void Restart() => log.Add(name);
    }

    /// <summary>Priority -10: runs early.</summary>
    [Capability(typeof(IRestartable), priority: -10)]
    private class EarlyRestartable(string name, List<string> log) : IRestartable
    {
        public void Restart() => log.Add(name);
    }

    /// <summary>Priority 0 (explicit default).</summary>
    [Capability(typeof(IRestartable), priority: 0)]
    private class DefaultPriorityRestartable(string name, List<string> log) : IRestartable
    {
        public void Restart() => log.Add(name);
    }

    /// <summary>Priority 50: mid-range.</summary>
    [Capability(typeof(IRestartable), priority: 50)]
    private class MidRestartable(string name, List<string> log) : IRestartable
    {
        public void Restart() => log.Add(name);
    }

    /// <summary>Priority 100: runs late (same as VA2MBus in production).</summary>
    [Capability(typeof(IRestartable), priority: 100)]
    private class LateRestartable(string name, List<string> log) : IRestartable
    {
        public void Restart() => log.Add(name);
    }

    /// <summary>
    /// Has a <see cref="CapabilityAttribute"/> for a different interface type —
    /// should not affect <see cref="IRestartable"/> priority (falls back to 0).
    /// </summary>
    [Capability(typeof(IDisposable), priority: 999)]
    private class UnrelatedCapabilityRestartable(string name, List<string> log) : IRestartable
    {
        public void Restart() => log.Add(name);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithEmptyEnumerable_CreatesEmptyCollection()
    {
        // Arrange & Act
        var collection = new RestartCollection([]);

        // Assert — RestartAll should not throw
        collection.RestartAll();
    }

    [Fact]
    public void Constructor_WithItems_RegistersAll()
    {
        // Arrange
        var a = new PlainRestartable("A", _restartLog);
        var b = new PlainRestartable("B", _restartLog);

        // Act
        var collection = new RestartCollection([a, b]);
        collection.RestartAll();

        // Assert — both were restarted
        Assert.Equal(2, _restartLog.Count);
        Assert.Contains("A", _restartLog);
        Assert.Contains("B", _restartLog);
    }

    #endregion

    #region Priority Ordering Tests

    [Fact]
    public void RestartAll_ExecutesInPriorityOrder_LowestFirst()
    {
        // Arrange — register in arbitrary (non-priority) order
        var late = new LateRestartable("Late-100", _restartLog);
        var early = new EarlyRestartable("Early-neg10", _restartLog);
        var mid = new MidRestartable("Mid-50", _restartLog);
        var def = new DefaultPriorityRestartable("Default-0", _restartLog);

        var collection = new RestartCollection([late, early, mid, def]);

        // Act
        collection.RestartAll();

        // Assert — strict priority order: -10, 0, 50, 100
        Assert.Equal(["Early-neg10", "Default-0", "Mid-50", "Late-100"], _restartLog);
    }

    [Fact]
    public void RestartAll_NegativePriority_RunsBeforeDefault()
    {
        // Arrange
        var normal = new PlainRestartable("Normal", _restartLog);
        var early = new EarlyRestartable("Early", _restartLog);

        var collection = new RestartCollection([normal, early]);

        // Act
        collection.RestartAll();

        // Assert
        Assert.Equal(["Early", "Normal"], _restartLog);
    }

    [Fact]
    public void RestartAll_HighPriority_RunsAfterDefault()
    {
        // Arrange
        var late = new LateRestartable("Late", _restartLog);
        var normal = new PlainRestartable("Normal", _restartLog);

        var collection = new RestartCollection([late, normal]);

        // Act
        collection.RestartAll();

        // Assert
        Assert.Equal(["Normal", "Late"], _restartLog);
    }

    [Fact]
    public void RestartAll_ItemWithoutCapabilityAttribute_DefaultsToZero()
    {
        // Arrange — PlainRestartable has no [Capability] at all
        var plain = new PlainRestartable("Plain", _restartLog);
        var early = new EarlyRestartable("Early", _restartLog);
        var late = new LateRestartable("Late", _restartLog);

        var collection = new RestartCollection([late, plain, early]);

        // Act
        collection.RestartAll();

        // Assert — plain sorts with default (0), between -10 and 100
        Assert.Equal(["Early", "Plain", "Late"], _restartLog);
    }

    [Fact]
    public void RestartAll_UnrelatedCapabilityAttribute_IgnoredForPriority()
    {
        // Arrange — has [Capability(typeof(IDisposable), priority: 999)] which
        // should be ignored; IRestartable priority falls back to 0
        var unrelated = new UnrelatedCapabilityRestartable("Unrelated", _restartLog);
        var early = new EarlyRestartable("Early", _restartLog);
        var late = new LateRestartable("Late", _restartLog);

        var collection = new RestartCollection([late, unrelated, early]);

        // Act
        collection.RestartAll();

        // Assert — unrelated sorts at 0, not 999
        Assert.Equal(["Early", "Unrelated", "Late"], _restartLog);
    }

    [Fact]
    public void RestartAll_SamePriority_AllExecuted()
    {
        // Arrange — three items all at default priority 0
        var a = new PlainRestartable("A", _restartLog);
        var b = new PlainRestartable("B", _restartLog);
        var c = new PlainRestartable("C", _restartLog);

        var collection = new RestartCollection([a, b, c]);

        // Act
        collection.RestartAll();

        // Assert — all three executed (order among same-priority is stable but not guaranteed)
        Assert.Equal(3, _restartLog.Count);
        Assert.Contains("A", _restartLog);
        Assert.Contains("B", _restartLog);
        Assert.Contains("C", _restartLog);
    }

    #endregion

    #region Register / Unregister Tests

    [Fact]
    public void Register_NewItem_IncludedInRestartAll()
    {
        // Arrange
        var collection = new RestartCollection([]);
        var item = new PlainRestartable("Dynamic", _restartLog);

        // Act
        collection.Register(item);
        collection.RestartAll();

        // Assert
        Assert.Single(_restartLog);
        Assert.Equal("Dynamic", _restartLog[0]);
    }

    [Fact]
    public void Register_DuplicateItem_SilentlyIgnored()
    {
        // Arrange
        var item = new PlainRestartable("OnlyOnce", _restartLog);
        var collection = new RestartCollection([item]);

        // Act — register same instance again
        collection.Register(item);
        collection.RestartAll();

        // Assert — restarted only once
        Assert.Single(_restartLog);
    }

    [Fact]
    public void Register_NullItem_ThrowsArgumentNullException()
    {
        // Arrange
        var collection = new RestartCollection([]);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => collection.Register(null!));
    }

    [Fact]
    public void Register_DynamicItem_RespectsPriority()
    {
        // Arrange — start with a late item, dynamically register an early one
        var late = new LateRestartable("Late", _restartLog);
        var collection = new RestartCollection([late]);

        var early = new EarlyRestartable("Early", _restartLog);
        collection.Register(early);

        // Act
        collection.RestartAll();

        // Assert — early (-10) still runs before late (100)
        Assert.Equal(["Early", "Late"], _restartLog);
    }

    [Fact]
    public void Unregister_ExistingItem_RemovesFromCollection()
    {
        // Arrange
        var item = new PlainRestartable("Removed", _restartLog);
        var collection = new RestartCollection([item]);

        // Act
        bool removed = collection.Unregister(item);
        collection.RestartAll();

        // Assert
        Assert.True(removed);
        Assert.Empty(_restartLog);
    }

    [Fact]
    public void Unregister_NonExistentItem_ReturnsFalse()
    {
        // Arrange
        var collection = new RestartCollection([]);
        var item = new PlainRestartable("Never", _restartLog);

        // Act
        bool removed = collection.Unregister(item);

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void Unregister_NullItem_ThrowsArgumentNullException()
    {
        // Arrange
        var collection = new RestartCollection([]);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => collection.Unregister(null!));
    }

    #endregion

    #region RestartAll Execution Tests

    [Fact]
    public void RestartAll_CallsRestartOnEveryItem()
    {
        // Arrange
        var items = Enumerable.Range(0, 10)
            .Select(i => new PlainRestartable($"Item-{i}", _restartLog))
            .ToList();

        var collection = new RestartCollection(items);

        // Act
        collection.RestartAll();

        // Assert
        Assert.Equal(10, _restartLog.Count);
    }

    [Fact]
    public void RestartAll_CanBeCalledMultipleTimes()
    {
        // Arrange
        var item = new PlainRestartable("Repeat", _restartLog);
        var collection = new RestartCollection([item]);

        // Act
        collection.RestartAll();
        collection.RestartAll();
        collection.RestartAll();

        // Assert — called three times
        Assert.Equal(3, _restartLog.Count);
        Assert.All(_restartLog, name => Assert.Equal("Repeat", name));
    }

    #endregion

    #region Production Component Priority Tests

    [Fact]
    public void VA2MBus_HasHigherPriorityThanDefaultComponents()
    {
        // Verify that the real VA2MBus has priority > 0 so it restarts after
        // SoftSwitches (priority 0) — ensuring ROM is active for CPU reset vector read.
        var attr = typeof(VA2MBus)
            .GetCustomAttributes(typeof(CapabilityAttribute), inherit: false)
            .Cast<CapabilityAttribute>()
            .FirstOrDefault(a => a.InterfaceType == typeof(IRestartable));

        Assert.NotNull(attr);
        Assert.True(attr.Priority > 0,
            $"VA2MBus priority should be > 0 to run after default-priority components, but was {attr.Priority}");
    }

    [Fact]
    public void SoftSwitches_HasDefaultPriority()
    {
        // SoftSwitches must restart at default priority (0) so that ROM is active
        // before VA2MBus resets the CPU and reads the reset vector.
        var attr = typeof(SoftSwitches)
            .GetCustomAttributes(typeof(CapabilityAttribute), inherit: false)
            .Cast<CapabilityAttribute>()
            .FirstOrDefault(a => a.InterfaceType == typeof(IRestartable));

        Assert.NotNull(attr);
        Assert.Equal(0, attr.Priority);
    }

    [Fact]
    public void VA2MBus_RestartsAfterSoftSwitches()
    {
        // Verify the priority relationship: SoftSwitches < VA2MBus
        var softSwitchAttr = typeof(SoftSwitches)
            .GetCustomAttributes(typeof(CapabilityAttribute), inherit: false)
            .Cast<CapabilityAttribute>()
            .First(a => a.InterfaceType == typeof(IRestartable));

        var busAttr = typeof(VA2MBus)
            .GetCustomAttributes(typeof(CapabilityAttribute), inherit: false)
            .Cast<CapabilityAttribute>()
            .First(a => a.InterfaceType == typeof(IRestartable));

        Assert.True(softSwitchAttr.Priority < busAttr.Priority,
            $"SoftSwitches priority ({softSwitchAttr.Priority}) must be less than " +
            $"VA2MBus priority ({busAttr.Priority}) so soft switches are reset before " +
            "the CPU reads the reset vector from ROM.");
    }

    [Fact]
    public void AllRestartableComponents_VA2MBusHasHighestPriority()
    {
        // Scan all types in EmuCore that have [Capability(typeof(IRestartable))]
        // and verify VA2MBus has the highest priority among them.
        var allPriorities = typeof(VA2MBus).Assembly
            .GetTypes()
            .SelectMany(t => t
                .GetCustomAttributes(typeof(CapabilityAttribute), inherit: false)
                .Cast<CapabilityAttribute>()
                .Where(a => a.InterfaceType == typeof(IRestartable))
                .Select(a => new { Type = t, a.Priority }))
            .ToList();

        Assert.True(allPriorities.Count >= 2,
            "Expected at least 2 types with [Capability(typeof(IRestartable))]");

        var maxPriority = allPriorities.Max(x => x.Priority);
        var busEntry = allPriorities.First(x => x.Type == typeof(VA2MBus));

        Assert.Equal(maxPriority, busEntry.Priority);
    }

    #endregion
}
