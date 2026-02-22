// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CapabilityAttribute(Type interfaceType, int priority = 0) : Attribute
{
    public int Priority { get; } = priority;
    public Type InterfaceType { get; } = interfaceType;
}
