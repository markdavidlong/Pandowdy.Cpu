// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using System.Threading.Channels;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Video;

/// <summary>
/// Manages threaded rendering of Apple IIe video frames with automatic frame skipping.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Threading Model:</strong>
/// <list type="bullet">
/// <item><strong>Emulator Thread:</strong> Captures memory snapshots at VBlank (~60 Hz)</item>
/// <item><strong>Render Thread:</strong> Consumes snapshots and renders frames independently</item>
/// <item><strong>Synchronization:</strong> Lock-free via interlocked atomic flag</item>
/// </list>
/// </para>
/// <para>
/// <strong>Frame Skip Policy:</strong> If rendering is still in progress when a new frame
/// arrives, the new frame is skipped. This prevents:
/// <list type="bullet">
/// <item>Memory buildup from queued snapshots</item>
/// <item>Input lag from rendering stale frames</item>
/// <item>Blocking the emulator thread</item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance:</strong> Decouples CPU emulation from rendering, allowing the
/// emulator to run at full speed (11-13 MHz unthrottled) while rendering occurs in parallel.
/// </para>
/// </remarks>
public sealed class RenderingService : IDisposable
{
    private readonly Channel<VideoMemorySnapshot> _snapshotChannel;
    private readonly Thread _renderThread;
    private readonly Thread _renderThread2;  // Second renderer for parallel processing
    private readonly IFrameGenerator _frameGenerator;
    private readonly VideoMemorySnapshotPool _snapshotPool;
    
    // Atomic flag: 0 = idle, 1 = rendering in progress
    private int _renderInProgress = 0;

    // Add this field near the other private fields (around line 43):
    private int _disposed = 0;  // 0 = not disposed, 1 = disposed


    // Diagnostics (accessed via Interlocked for thread safety)
    private long _framesRendered = 0;
    private long _framesSkipped = 0;
    private readonly Stopwatch _diagSw = Stopwatch.StartNew();
    private long _lastReportTicks = 0;
    
    /// <summary>
    /// Initializes a new rendering service instance.
    /// </summary>
    /// <param name="frameGenerator">Frame generator for rendering video output.</param>
    /// <param name="snapshotPool">Pool for reusing memory snapshots.</param>
    public RenderingService(IFrameGenerator frameGenerator, VideoMemorySnapshotPool snapshotPool)
    {
        ArgumentNullException.ThrowIfNull(frameGenerator);
        ArgumentNullException.ThrowIfNull(snapshotPool);
        
        _frameGenerator = frameGenerator;
        _snapshotPool = snapshotPool;
        
        // Unbounded channel since we handle flow control manually via _renderInProgress flag
        _snapshotChannel = Channel.CreateUnbounded<VideoMemorySnapshot>(
            new UnboundedChannelOptions
            {
                SingleReader = false,  // TWO readers now!
                SingleWriter = false   // Multiple VBlanks could write (though typically one emulator thread)
            });
        
        // Spawn first render thread
        _renderThread = new Thread(RenderLoop)
        {
            Name = "Apple IIe Renderer #1",
            Priority = ThreadPriority.Normal,
            IsBackground = true  // Don't prevent app shutdown
        };
        _renderThread.Start();
        
        // Spawn second render thread for parallel processing
        _renderThread2 = new Thread(RenderLoop)
        {
            Name = "Apple IIe Renderer #2",
            Priority = ThreadPriority.Normal,
            IsBackground = true
        };
        _renderThread2.Start();
        
        _lastReportTicks = _diagSw.ElapsedTicks;
    }
    
    /// <summary>
    /// Attempts to enqueue a frame snapshot for rendering.
    /// </summary>
    /// <param name="snapshot">Video memory snapshot to render.</param>
    /// <returns>True if queued successfully; false if skipped (too many frames in flight).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Skip-If-Busy Policy:</strong> If both render threads are busy (2 frames in flight),
    /// this method returns false immediately and returns the snapshot to the pool.
    /// The emulator thread is never blocked.
    /// </para>
    /// <para>
    /// <strong>Dual-Renderer Architecture:</strong> With two render threads, we can process up to
    /// 2 frames concurrently, effectively doubling rendering throughput. This allows the system
    /// to handle frame times up to ~33ms (2Ã— the 16.67ms VBlank interval) while maintaining 60 FPS.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> This method completes in ~100 nanoseconds (atomic check +
    /// channel write). The snapshot copy occurs before this method is called.
    /// </para>
    /// </remarks>
    public bool TryEnqueueSnapshot(VideoMemorySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return false;
        }
        
        // Check how many render threads are busy (allow up to 2 concurrent)
        int renderCount = Interlocked.CompareExchange(ref _renderInProgress, 0, 0);
        if (renderCount >= 2)
        {
            // Both render threads are busy - skip this frame
            Interlocked.Increment(ref _framesSkipped);
            
            // Return snapshot to pool immediately (not rendered)
            _snapshotPool.Return(snapshot);
            
            return false;  // Frame skipped
        }
        
        // At least one render thread is idle - enqueue this frame
        // The render threads will increment _renderInProgress when they start processing
        bool queued = _snapshotChannel.Writer.TryWrite(snapshot);
        
        if (!queued)
        {
            // Channel closed or failed - shouldn't happen with unbounded channel
            _snapshotPool.Return(snapshot);
            return false;
        }
        
        return true;  // Frame queued for rendering
    }
    
    /// <summary>
    /// Render thread loop - continuously processes snapshots from the channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Dual-Renderer Architecture:</strong> Two instances of this method run concurrently
    /// on separate threads, allowing parallel frame rendering. The _renderInProgress counter tracks
    /// how many threads are actively rendering (0, 1, or 2).
    /// </para>
    /// <para>
    /// <strong>Coordination:</strong> Both threads read from the same channel and compete for work.
    /// Whichever thread finishes first gets the next snapshot. This provides automatic load balancing.
    /// </para>
    /// </remarks>
    private async void RenderLoop()
    {
        await foreach (var snapshot in _snapshotChannel.Reader.ReadAllAsync())
        {
            try
            {
                // Increment "renders in progress" counter (atomic operation)
                Interlocked.Increment(ref _renderInProgress);
                
                // Render from snapshot (not live memory!)
                _frameGenerator.RenderFrameFromSnapshot(snapshot);
                
                Interlocked.Increment(ref _framesRendered);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RenderingService] Render error: {ex.Message}");
            }
            finally
            {
                // Return snapshot to pool
                _snapshotPool.Return(snapshot);
                
                // Decrement "renders in progress" counter (atomic operation)
                Interlocked.Decrement(ref _renderInProgress);
            }
            
            // Periodic diagnostics (every 5 seconds)
            long currentTicks = _diagSw.ElapsedTicks;
            if (currentTicks - Interlocked.Read(ref _lastReportTicks) >= Stopwatch.Frequency * 5)
            {
                ReportDiagnostics();
                Interlocked.Exchange(ref _lastReportTicks, currentTicks);
            }
        }
    }
    
    /// <summary>
    /// Reports rendering performance diagnostics to debug output.
    /// </summary>
    private void ReportDiagnostics()
    {
        long currentTicks = _diagSw.ElapsedTicks;
        long lastReportTicks = Interlocked.Read(ref _lastReportTicks);
        
        // Calculate time since LAST REPORT, not since start
        double secondsSinceLastReport = (double)(currentTicks - lastReportTicks) / Stopwatch.Frequency;
        
        long rendered = Interlocked.Read(ref _framesRendered);
        long skipped = Interlocked.Read(ref _framesSkipped);
        long total = rendered + skipped;
        
        if (total == 0 || secondsSinceLastReport <= 0)
        {
            return; // No frames yet or invalid timing
        }
        
#if DebugRendering
        // FPS based on interval, not total runtime
        double effectiveFps = rendered / secondsSinceLastReport;
        double skipRate = (skipped * 100.0) / total;

        // Include actual timestamp and interval for debugging timing issues
        Debug.WriteLine(
            $"[RenderingService @ {DateTime.Now:HH:mm:ss.fff}] " +
            $"Interval: {secondsSinceLastReport:F2}s | " +
            $"FPS: {effectiveFps:F1} | " +
            $"Rendered: {rendered} | Skipped: {skipped} ({skipRate:F1}%) | " +
            $"Total: {total}"
        );
#endif
        // Reset counters for next period
        Interlocked.Exchange(ref _framesRendered, 0);
        Interlocked.Exchange(ref _framesSkipped, 0);
    }
    
    /// <summary>
    /// Disposes the rendering service and waits for the render thread to complete.
    /// </summary>

    public void Dispose()
    {
        // Only dispose once (idempotent)
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            // Signal render thread to stop
            _snapshotChannel.Writer.Complete();

            // Wait for render threads to finish (with timeout)
            _renderThread?.Join(TimeSpan.FromSeconds(2));
            _renderThread2?.Join(TimeSpan.FromSeconds(2));
        }
    }
}
