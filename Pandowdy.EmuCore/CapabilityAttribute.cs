// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore;

/// <summary>
/// Declares that a class implements an optional capability interface, enabling
/// automatic discovery and registration via DI infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Applied to concrete classes to advertise which optional
/// interfaces they support (e.g., <see cref="Interfaces.IRestartable"/>). The
/// <c>CapabilityAwareServiceCollection</c> in the host project scans registered types
/// for this attribute and registers them under the declared capability interfaces.
/// </para>
/// <para>
/// <strong>Multiple Capabilities:</strong> A class may carry multiple
/// <see cref="CapabilityAttribute"/> instances to declare support for several
/// capability interfaces simultaneously.
/// </para>
/// <para>
/// <strong>Priority:</strong> The optional <see cref="Priority"/> value controls
/// execution order when all registrants of a capability are iterated together
/// (e.g., in <see cref="DataTypes.RestartCollection.RestartAll"/>). Higher values
/// run earlier. Defaults to 0 when not specified.
/// </para>
/// <example>
/// <code>
/// [Capability(typeof(IRestartable), priority: 100)]
/// public sealed class VA2MBus : IAppleIIBus, IDisposable { ... }
///
/// [Capability(typeof(IRestartable))]
/// public sealed class SoftSwitches : IRestartable { ... }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CapabilityAttribute(Type interfaceType, int priority = 0) : Attribute
{
    /// <summary>
    /// Gets the execution priority for this capability registration.
    /// Higher values execute before lower values during batch operations.
    /// Defaults to 0.
    /// </summary>
    public int Priority { get; } = priority;

    /// <summary>
    /// Gets the capability interface type that the decorated class implements.
    /// </summary>
    public Type InterfaceType { get; } = interfaceType;
}
