// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Memory;

/// <summary>
/// Manages the Apple IIe Language Card banking for the $D000-$FFFF address space.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> The Language Card provides 16KB of additional RAM that can be
/// mapped into the normally ROM-occupied $D000-$FFFF address space, with two switchable 4KB
/// banks for $D000-$DFFF and a shared 8KB region for $E000-$FFFF. This RAM can replace ROM,
/// enabling programs to load custom firmware or Applesoft replacements.
/// </para>
/// <para>
/// <strong>Implements:</strong>
/// <list type="bullet">
/// <item><see cref="IPandowdyMemory"/> — Standard CPU read/write access to $D000-$FFFF with
/// bank switching applied based on current soft switch state.</item>
/// <item><see cref="IRestartable"/> — Clears Language Card RAM to power-on state during cold boot.</item>
/// </list>
/// </para>
/// <para>
/// <strong>Banking Control:</strong> Language Card banking state is read from
/// <see cref="ISystemStatusProvider"/> (HighRead, HighWrite, UseBank1). Soft switch transitions
/// are managed by <see cref="SoftSwitches"/>; this interface only handles memory access routing.
/// </para>
/// </remarks>
/// <seealso cref="ISystemStatusProvider"/>
/// <seealso cref="IPandowdyMemory"/>
public interface ILanguageCard : IPandowdyMemory, IRestartable
{
}
