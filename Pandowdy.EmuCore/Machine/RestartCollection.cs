// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using Pandowdy.EmuCore.Slots;

namespace Pandowdy.EmuCore.Machine;

/// <summary>
/// Collects all <see cref="IRestartable"/> components and provides batch restart.
/// </summary>
/// <remarks>
/// <para>
/// <strong>DI Discovery:</strong> Components tagged with
/// <c>[Capability(typeof(IRestartable))]</c> are automatically registered via
/// <c>CapabilityAwareServiceCollection</c> and injected at construction time.
/// </para>
/// <para>
/// <strong>Dynamic Registration:</strong> Factory-generated objects (e.g., cards created by
/// <see cref="Slots.CardFactory"/>) that implement <see cref="IRestartable"/> can be
/// registered after construction via <see cref="Register"/>.
/// </para>
/// </remarks>
public class RestartCollection(IEnumerable<IRestartable> restartables)
{
    private readonly List<IRestartable> _restartables = new(restartables);

    /// <summary>
    /// Registers an additional <see cref="IRestartable"/> component for batch restart.
    /// </summary>
    /// <param name="item">The restartable component to add.</param>
    /// <remarks>
    /// Used by factories that create objects after DI construction (e.g., cards installed
    /// into slots at runtime). Duplicate registrations are silently ignored.
    /// </remarks>
    public void Register(IRestartable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_restartables.Contains(item))
        {
            _restartables.Add(item);
            Debug.WriteLine($"[RestartCollection] Registered {item.GetType().Name} ({_restartables.Count} total)");
        }
    }

    /// <summary>
    /// Removes a previously registered <see cref="IRestartable"/> component.
    /// </summary>
    /// <param name="item">The restartable component to remove.</param>
    /// <returns><c>true</c> if the item was found and removed; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Used when a card is removed from a slot and replaced with a NullCard.
    /// </remarks>
    public bool Unregister(IRestartable item)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool removed = _restartables.Remove(item);
        if (removed)
        {
            Debug.WriteLine($"[RestartCollection] Unregistered {item.GetType().Name} ({_restartables.Count} remaining)");
        }
        return removed;
    }

    /// <summary>
    /// Calls <see cref="IRestartable.Restart"/> on every registered component.
    /// </summary>
    public void RestartAll()
    {
        Debug.WriteLine($"[RestartCollection] Calling RestartAll() ({_restartables.Count} item(s))");

        var ordered = _restartables.OrderBy(r => GetPriority(r)).ToList();
        foreach ( var r in ordered)
        {
            Debug.WriteLine($"[RestartCollection]  ... restarting {r.GetType().Name}");
            r.Restart();
        }
    }

    private static int GetPriority(IRestartable item)
    {
        var type = item.GetType();
        var attr = type
            .GetCustomAttributes(typeof(CapabilityAttribute), inherit: false)
            .Cast<CapabilityAttribute>()
            .FirstOrDefault(a => a.InterfaceType == typeof(IRestartable));

        return attr?.Priority ?? 0;
    }
}
