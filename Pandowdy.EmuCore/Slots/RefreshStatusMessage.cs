// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Message requesting a card to push a fresh status update through its dedicated
/// status channel (e.g., IDiskStatusMutator for disk controllers).
/// </summary>
/// <remarks>
/// <para>
/// This message is for cards that have their own dedicated status push infrastructure.
/// For example, a Disk II controller calls IDiskStatusMutator.MutateDrive
/// for each drive, causing IDiskStatusProvider to emit a new snapshot through its observable stream.
/// </para>
/// <para>
/// Cards that have no dedicated status push mechanism can silently ignore this message
/// (they already respond to <see cref="IdentifyCardMessage"/> via the response channel).
/// </para>
/// </remarks>
public record RefreshStatusMessage() : ICardMessage;
