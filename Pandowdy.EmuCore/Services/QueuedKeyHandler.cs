// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.Concurrent;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Buffered keyboard handler that queues multiple keystrokes with automatic timed feeding.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Extends the simple <see cref="SingularKeyHandler"/> with a queue
/// to buffer multiple keystrokes. This enables "paste" operations where many keys can be injected
/// rapidly, and the emulator automatically feeds them to the software at a controlled rate.
/// </para>
/// <para>
/// <strong>Automatic Key Feeding:</strong> When <see cref="ClearStrobe"/> is called (simulating
/// a read of $C010), a timer starts. After the configured delay, the next queued key is automatically
/// loaded into the keyboard latch with strobe set. This gives the emulated software time to process
/// the current key before the next one appears.
/// </para>
/// <para>
/// <strong>Apple IIe Compatibility:</strong> The delay prevents overwhelming software that polls
/// the keyboard. Real Apple IIe software expects human typing speeds (100-300ms between keys).
/// A configurable delay (default 50ms) balances paste speed with software compatibility.
/// </para>
/// <para>
/// <strong>Use Cases:</strong>
/// <list type="bullet">
/// <item><strong>Paste operations:</strong> User pastes text, keys are queued and fed automatically</item>
/// <item><strong>Automated input:</strong> Test scripts or demos can inject keystroke sequences</item>
/// <item><strong>Manual typing:</strong> Works transparently for individual keystrokes</item>
/// </list>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Uses ConcurrentQueue for thread-safe key injection from UI thread.
/// Timer callbacks execute on thread pool, synchronized via lock for key buffer updates.
/// </para>
/// <para>
/// <strong>Disposal:</strong> Implements IDisposable to clean up timer resources. Always dispose
/// when switching keyboard handlers or shutting down emulator.
/// </para>
/// </remarks>
/// <example>
/// <para>
/// <strong>Basic Usage:</strong>
/// </para>
/// <code>
/// // Create with 50ms delay between keys
/// var keyboard = new QueuedKeyHandler(delayMilliseconds: 50);
/// 
/// // Paste operation - queue multiple keys
/// foreach (char c in "HELLO\r")
/// {
///     keyboard.EnqueueKey((byte)c);
/// }
/// 
/// // Keys automatically feed to emulator at 50ms intervals after each ClearStrobe()
/// </code>
/// </example>
[Capability(typeof(IRestartable))]
public sealed class QueuedKeyHandler : IKeyboardReader, IKeyboardSetter, IDisposable
{
    /// <summary>
    /// Queue of pending keystrokes waiting to be fed to the keyboard latch.
    /// </summary>
    /// <remarks>
    /// ConcurrentQueue provides thread-safe enqueue from UI thread without blocking.
    /// Keys are stored without strobe bit (0-127). Strobe is added when loaded into latch.
    /// </remarks>
    private readonly ConcurrentQueue<byte> _keyQueue = new();

    /// <summary>
    /// Current key in the keyboard latch (0-255, bit 7 = strobe).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Value Range:</strong>
    /// <list type="bullet">
    /// <item>0x80-0xFF (128-255) - Key with strobe set (unread)</item>
    /// <item>0x00-0x7F (0-127) - Key with strobe cleared (read) or no key</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Access Synchronization:</strong> Protected by _lock to coordinate timer thread
    /// and emulator thread access.
    /// </para>
    /// </remarks>
    private byte _currentKey;

    /// <summary>
    /// Timer that automatically loads the next queued key after configured delay.
    /// </summary>
    /// <remarks>
    /// Created as disabled timer (Timeout.Infinite). Started by ClearStrobe() to fire once
    /// after _delayMilliseconds. Timer callback loads next key from queue with strobe set.
    /// </remarks>
    private readonly Timer _feedTimer;

    /// <summary>
    /// Delay in milliseconds before feeding next queued key after strobe clear.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Timing Considerations:</strong>
    /// <list type="bullet">
    /// <item><strong>Too short (&lt; 10ms):</strong> May overwhelm slow software (DOS 3.3 input routines)</item>
    /// <item><strong>Optimal (30-100ms):</strong> Balances paste speed with software compatibility</item>
    /// <item><strong>Too long (&gt; 200ms):</strong> Paste feels sluggish, simulates slow human typing</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Default (50ms):</strong> Allows 20 keys/second paste rate, compatible with most software.
    /// </para>
    /// </remarks>
    private readonly int _delayMilliseconds;

    /// <summary>
    /// Lock object synchronizing access to _currentKey between emulator and timer threads.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Flag indicating whether this instance has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new QueuedKeyHandler with configurable key feed delay.
    /// </summary>
    /// <param name="delayMilliseconds">
    /// Delay in milliseconds before feeding next queued key after strobe clear.
    /// Default is 50ms (20 keys/second). Valid range: 1-1000ms.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if delayMilliseconds is less than 1 or greater than 1000.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Delay Guidelines:</strong>
    /// <list type="bullet">
    /// <item><strong>10-30ms:</strong> Fast paste, may not work with slow software</item>
    /// <item><strong>50ms (default):</strong> Good balance, works with most Apple IIe software</item>
    /// <item><strong>100-200ms:</strong> Conservative, simulates moderately fast human typing</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Timer Initialization:</strong> Timer created in disabled state (Timeout.Infinite).
    /// Timer starts only when ClearStrobe() is called and keys are queued.
    /// </para>
    /// </remarks>
    public QueuedKeyHandler(int delayMilliseconds = 30)
    {
        if (delayMilliseconds < 1 || delayMilliseconds > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayMilliseconds),
                delayMilliseconds,
                "Delay must be between 1 and 1000 milliseconds");
        }

        _delayMilliseconds = delayMilliseconds;
        _feedTimer = new Timer(
            FeedNextKey,
            null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Tests bit 7 of the current key latch. Returns true
    /// if strobe is set (key unread), false if strobe cleared or no key present.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Read of byte field is atomic, no lock required.
    /// </para>
    /// </remarks>
    public bool StrobePending()
    {
        return (_currentKey & 0x80) == 0x80;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Masks off bit 7 (strobe) by ANDing with 0x7F.
    /// Returns pure ASCII code (0-127) regardless of strobe state.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Read of byte field is atomic, no lock required.
    /// </para>
    /// </remarks>
    public byte PeekCurrentKeyValue()
    {
        return (byte)(_currentKey & 0x7F);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Returns raw keyboard latch value unchanged,
    /// including strobe bit. Corresponds to reading $C000 (KBD) on Apple IIe.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Read of byte field is atomic, no lock required.
    /// </para>
    /// </remarks>
    public byte PeekCurrentKeyAndStrobe()
    {
        return _currentKey;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Clears bit 7 (strobe) by ANDing with 0x7F.
    /// If keys are queued, starts timer to automatically feed next key after configured delay.
    /// </para>
    /// <para>
    /// <strong>Automatic Key Feeding:</strong> After delay expires, timer callback loads
    /// next queued key with strobe set. This simulates natural typing where keys arrive
    /// sequentially with pauses between.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Lock protects _currentKey update and timer state check.
    /// </para>
    /// </remarks>
    public byte ClearStrobe()
    {
        lock (_lock)
        {
            _currentKey &= 0x7F; // Clear strobe bit

            // If keys are queued and not disposed, start timer to feed next key
            if (!_disposed && !_keyQueue.IsEmpty)
            {
                _feedTimer.Change(_delayMilliseconds, Timeout.Infinite);
            }

            return _currentKey;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Returns count of keys in queue plus one if the current
    /// key in the latch has strobe set (unread). This gives the total number of pending keystrokes
    /// waiting to be processed by the emulated software.
    /// </para>
    /// <para>
    /// <strong>Calculation:</strong>
    /// <list type="bullet">
    /// <item>Queue count: Keys waiting to be fed</item>
    /// <item>+1 if _currentKey strobe set: Unread key in latch</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong> UI can display "X keys pending" indicator during paste.
    /// Test code can verify queue state.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> ConcurrentQueue.Count is thread-safe snapshot.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// keyboard.EnqueueKey(0x41); // 'A' - loaded with strobe
    /// keyboard.EnqueueKey(0x42); // 'B' - queued
    /// keyboard.EnqueueKey(0x43); // 'C' - queued
    /// Assert.Equal(3, keyboard.NumKeysPending()); // 1 in latch + 2 in queue
    /// 
    /// keyboard.ClearStrobe(); // Clear 'A'
    /// Assert.Equal(2, keyboard.NumKeysPending()); // 0 in latch + 2 in queue
    /// 
    /// // After timer fires, 'B' loads
    /// Assert.Equal(2, keyboard.NumKeysPending()); // 1 in latch + 1 in queue
    /// </code>
    /// </para>
    /// </remarks>
    public int NumKeysPending()
    {
        int queueCount = _keyQueue.Count;
        bool currentKeyUnread = (_currentKey & 0x80) == 0x80;
        return queueCount + (currentKeyUnread ? 1 : 0);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Adds key to queue with strobe bit cleared (0-127).
    /// If keyboard latch is empty (no current key), immediately loads this key with strobe set.
    /// Otherwise, key waits in queue until fed by timer.
    /// </para>
    /// <para>
    /// <strong>Immediate vs Queued:</strong>
    /// <list type="bullet">
    /// <item><strong>Latch empty:</strong> Key loaded immediately with strobe (ready to read)</item>
    /// <item><strong>Latch occupied:</strong> Key queued, will be fed after current key cleared and delay expires</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> ConcurrentQueue.Enqueue is thread-safe. Lock protects
    /// _currentKey update when loading immediately.
    /// </para>
    /// <para>
    /// <strong>Disposal Safety:</strong> If handler has been disposed, EnqueueKey is safely ignored.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// keyboard.EnqueueKey(0x41); // 'A' - loads immediately with strobe
    /// keyboard.EnqueueKey(0x42); // 'B' - queued, will appear after ClearStrobe + delay
    /// keyboard.EnqueueKey(0x43); // 'C' - queued
    /// </code>
    /// </para>
    /// </remarks>
    public void EnqueueKey(byte key)
    {
        // Ensure only ASCII values (0-127) are stored in queue
        byte cleanKey = (byte)(key & 0x7F);

        lock (_lock)
        {
            // Ignore enqueue after disposal
            if (_disposed)
            {
                return;
            }

            // If no current key, load immediately with strobe
            if (_currentKey == 0 || (_currentKey & 0x80) == 0)
            {
                _currentKey = (byte)(cleanKey | 0x80); // Set strobe
            }
            else
            {
                // Current key unread, queue this one
                _keyQueue.Enqueue(cleanKey);
            }
        }
    }

    /// <summary>
    /// Timer callback that loads the next queued key into the keyboard latch.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    /// <remarks>
    /// <para>
    /// <strong>Execution Context:</strong> Runs on thread pool thread. Lock synchronizes
    /// with emulator thread accessing _currentKey.
    /// </para>
    /// <para>
    /// <strong>Behavior:</strong> Dequeues next key from queue and loads into latch with
    /// strobe set (bit 7 = 1). If no keys queued, does nothing.
    /// </para>
    /// <para>
    /// <strong>Disposal Safety:</strong> Checks _disposed flag to prevent loading keys
    /// after disposal.
    /// </para>
    /// </remarks>
    private void FeedNextKey(object? state)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_keyQueue.TryDequeue(out byte nextKey))
            {
                _currentKey = (byte)(nextKey | 0x80); // Load with strobe set
            }
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// <strong>Implementation:</strong> Performs a comprehensive reset of the keyboard handler state:
        /// <list type="number">
        /// <item><strong>Cancel Timer:</strong> Stops any pending automatic key feed timer</item>
        /// <item><strong>Clear Queue:</strong> Drains all pending keys from the queue</item>
        /// <item><strong>Clear Strobe:</strong> Clears strobe bit on current key (if present) while preserving
        /// the low 7 bits, matching Apple IIe hardware behavior where the latch retains the last key but
        /// clears the "unread" indicator</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Example:</strong>
        /// <code>
        /// // Before reset: 
        /// // _currentKey = 0xC1 ('A' with strobe set)
        /// // _keyQueue = [0x42 ('B'), 0x43 ('C')]
        /// 
        /// keyboard.Reset();
        /// 
        /// // After reset:
        /// // _currentKey = 0x41 ('A' with strobe cleared)
        /// // _keyQueue = [] (empty)
        /// // Timer canceled
        /// </code>
        /// </para>
        /// <para>
        /// <strong>Thread Safety:</strong> Lock protects all state changes to prevent race conditions
        /// between emulator thread calling Reset() and timer thread attempting key feed.
        /// </para>
        /// <para>
        /// <strong>Use Case:</strong> Called during emulator system reset (power cycle) to clear
        /// any pending keystrokes that shouldn't persist after reset. Prevents stale buffered keys
        /// from being injected into the freshly reset system.
        /// </para>
        /// </remarks>
        public void ResetKeyboard()
        {
            lock (_lock)
            {
                // Cancel any pending timer callback
                _feedTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Clear all queued keys
                while (_keyQueue.TryDequeue(out _))
                {
                    // Drain queue
                }

                // Clear strobe bit on current key, preserve low 7 bits
                _currentKey &= 0x7F;
            }
        }

        /// <summary>
        /// Restores the keyboard handler to its initial power-on state (cold boot).
        /// </summary>
        /// <remarks>
        /// Delegates to <see cref="ResetKeyboard"/> — for the keyboard subsystem,
        /// warm reset and cold boot are equivalent (clear latch, drain queue, cancel timer).
        /// </remarks>
        public void Restart() => ResetKeyboard();

        /// <summary>
        /// Disposes the QueuedKeyHandler, stopping timer and releasing resources.
        /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Cleanup Operations:</strong>
    /// <list type="bullet">
    /// <item>Set _disposed flag to prevent further operations</item>
    /// <item>Dispose timer to stop pending callbacks</item>
    /// <item>Clear current key in latch</item>
    /// <item>Clear keyboard queue</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Idempotent:</strong> Safe to call multiple times.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Lock protects disposal state check.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _feedTimer?.Dispose();

            // Clear current key
            _currentKey = 0;

            // Clear queue
            while (_keyQueue.TryDequeue(out _))
            {
                // Drain queue
            }
        }
    }
}
