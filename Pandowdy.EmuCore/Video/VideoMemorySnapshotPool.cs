// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.Concurrent;
using Pandowdy.EmuCore.DataTypes;

namespace Pandowdy.EmuCore.Video;

/// <summary>
/// Object pool for VideoMemorySnapshot instances to avoid GC pressure.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Reuses VideoMemorySnapshot instances (each ~96KB) to minimize
/// allocations and reduce garbage collection overhead during rendering.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Uses ConcurrentBag for lock-free thread-safe pooling.
/// </para>
/// </remarks>
public sealed class VideoMemorySnapshotPool(int maxPoolSize = 8)
{
    private readonly ConcurrentBag<VideoMemorySnapshot> _pool = [];
    private readonly int _maxPoolSize = maxPoolSize;
    
    /// <summary>
    /// Rents a snapshot from the pool or creates a new one if pool is empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Clear on Rent:</strong> Snapshots are cleared HERE (when rented) rather than
    /// when returned to the pool. This prevents race conditions where the emulator thread
    /// rents a snapshot that is still being cleared by a render thread.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Clearing takes ~0.5ms, but happens on the emulator thread
    /// which has plenty of time between VBlanks (1.4ms at 700 FPS). This is safer than clearing
    /// on the render thread where it could overlap with the next Rent().
    /// </para>
    /// </remarks>
    public VideoMemorySnapshot Rent()
    {
        if (_pool.TryTake(out VideoMemorySnapshot? snapshot))
        {
            // Snapshot from pool - clear it before returning
            // This ensures no race condition with render threads
            snapshot.Clear();
            return snapshot;
        }
        
        // Pool empty - create new snapshot (already zeroed)
        return new VideoMemorySnapshot();
    }
    
    /// <summary>
    /// Returns a snapshot to the pool for reuse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>No Clear on Return:</strong> We do NOT clear the snapshot here because:
    /// <list type="bullet">
    /// <item>Clearing takes ~0.5ms which could delay the render thread</item>
    /// <item>The emulator thread might Rent() the snapshot while it's being cleared (race!)</item>
    /// <item>Clearing on Rent() is safer - happens on emulator thread before use</item>
    /// </list>
    /// </para>
    /// </remarks>
    public void Return(VideoMemorySnapshot? snapshot)
    {
        if (snapshot == null)
        {
            return;
        }
        
        // Only pool if under capacity
        if (_pool.Count < _maxPoolSize)
        {
            // DO NOT CLEAR HERE - will be cleared on next Rent()
            _pool.Add(snapshot);
        }
        // Otherwise let it be garbage collected
    }
}
