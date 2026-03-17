// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Input;

/// <summary>
/// Interface for setting keyboard input values, typically used by UI or input event handlers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> This interface provides a write-only contract for external components
/// (such as UI event handlers, test fixtures, or input mappers) to inject keyboard events into the
/// emulator's keyboard system without needing access to the full <see cref="IKeyboardReader"/> interface.
/// </para>
/// <para>
/// <strong>Separation of Concerns:</strong> By separating the setter interface from the reader interface,
/// the emulator core (I/O handlers) can use <see cref="IKeyboardReader"/> while UI/input components
/// use <see cref="IKeyboardSetter"/>, following the principle of least privilege.
/// </para>
/// <para>
/// <strong>Apple IIe Context:</strong> In the real Apple IIe, keyboard input comes from the keyboard
/// hardware scanning matrix. In the emulator, this interface represents the entry point for simulated
/// keyboard events from the host operating system (via Avalonia UI, test code, etc.).
/// </para>
/// <para>
/// <strong>Thread Safety Warning:</strong> Implementations of this interface (such as <see cref="SingularKeyHandler"/>)
/// are typically <em>not</em> thread-safe. The emulator core runs on a dedicated CPU emulation thread,
/// while keyboard events arrive on the UI thread. <strong>Callers must use a thread-safe queueing mechanism</strong>
/// (such as <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> or a dispatcher) to marshal
/// keyboard events from the UI thread to the emulator thread before calling <see cref="EnqueueKey"/>.
/// </para>
/// <para>
/// <strong>Implementation Note:</strong> Classes implementing both <see cref="IKeyboardSetter"/> and
/// <see cref="IKeyboardReader"/> (such as <see cref="SingularKeyHandler"/>) provide the complete
/// keyboard emulation: input injection (setter) and Apple IIe-style reading (reader).
/// </para>
/// </remarks>
public interface IKeyboardSetter : IRestartable
{
    /// <summary>
    /// Enqueues a raw key value with strobe bit automatically set.
    /// </summary>
    /// <param name="key">
    /// The 7-bit ASCII character code (0-127) to inject into the keyboard system.
    /// The strobe bit (bit 7) is automatically set to indicate an unread keypress.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method simulates a physical key press on the Apple IIe keyboard by injecting a character
    /// into the keyboard system. The implementation automatically sets the strobe bit (bit 7) to signal
    /// that a new key is available for the emulated software to read.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This method is <em>not</em> thread-safe and must be called from
    /// the emulator's CPU thread. UI event handlers running on the UI thread should <strong>not</strong>
    /// call this method directly. Instead, use a thread-safe queue or dispatcher to marshal the key event:
    /// <code>
    /// // CORRECT: Queue key event from UI thread
    /// public void OnKeyDown(object sender, KeyEventArgs e)
    /// {
    ///     if (e.Key == Key.A)
    ///     {
    ///         _keyEventQueue.Enqueue(0x41);  // Thread-safe queue
    ///         // Emulator thread will dequeue and call _keyboardSetter.EnqueueKey(0x41)
    ///     }
    /// }
    /// 
    /// // INCORRECT: Direct call from UI thread (NOT THREAD-SAFE)
    /// public void OnKeyDown(object sender, KeyEventArgs e)
    /// {
    ///     if (e.Key == Key.A)
    ///     {
    ///         _keyboardSetter.EnqueueKey(0x41);  // âš ï¸ Race condition!
    ///     }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Typical Usage Scenario:</strong>
    /// <code>
    /// // Step 1: UI thread captures key event
    /// public void OnKeyDown(object sender, KeyEventArgs e)
    /// {
    ///     if (e.Key == Key.A)
    ///     {
    ///         _keyEventQueue.Enqueue(0x41);  // Thread-safe ConcurrentQueue
    ///     }
    ///     else if (e.Key == Key.Enter)
    ///     {
    ///         _keyEventQueue.Enqueue(0x0D);  // Return key
    ///     }
    /// }
    /// 
    /// // Step 2: Emulator thread processes queued keys (called each frame/cycle)
    /// public void ProcessPendingKeyEvents()
    /// {
    ///     while (_keyEventQueue.TryDequeue(out byte key))
    ///     {
    ///         _keyboardSetter.EnqueueKey(key);  // Safe: same thread as emulator
    ///     }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Character Code Notes:</strong>
    /// <list type="bullet">
    /// <item>Standard ASCII codes: 0x20-0x7E (printable characters)</item>
    /// <item>Control characters: 0x00-0x1F (Ctrl+@, Ctrl+A, etc.)</item>
    /// <item>Return key: 0x0D (Apple IIe standard)</item>
    /// <item>Escape key: 0x1B</item>
    /// <item>The strobe bit (0x80) is set automatically - do not OR it into the key parameter</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Single-Key vs. Buffered Implementations:</strong>
    /// <list type="bullet">
    /// <item><see cref="SingularKeyHandler"/> - Overwrites previous unread key (matches original Apple IIe)</item>
    /// <item>Buffered implementations - Enqueues key into FIFO buffer for processing</item>
    /// </list>
    /// </para>
        /// </remarks>
        public void EnqueueKey(byte key);

        /// <summary>
        /// Resets the keyboard state to power-on defaults, clearing pending keystrokes and strobe.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Purpose:</strong> This method simulates a hardware reset (power cycle) of the Apple IIe
        /// keyboard system. It is typically called when the emulator performs a cold boot or system reset
        /// via <see cref="IEmulatorCoreInterface.DoReset"/>.
        /// </para>
        /// <para>
        /// <strong>Reset Behavior:</strong>
        /// <list type="bullet">
        /// <item><strong>Strobe Clearing:</strong> If a key is currently latched with strobe set (bit 7 = 1),
        /// the strobe is cleared (bit 7 set to 0) but the key's low 7 bits are preserved. This matches
        /// the Apple IIe hardware behavior where the keyboard latch retains the last key but clears the
        /// "unread" flag.</item>
        /// <item><strong>Queue Clearing (buffered implementations):</strong> For keyboard handlers with
        /// queuing (such as QueuedKeyHandler), all pending keys in the buffer are discarded. This prevents
        /// stale keystrokes from being injected after reset.</item>
        /// <item><strong>Timer Cancellation (buffered implementations):</strong> Any pending automatic
        /// key feed timers are canceled to prevent scheduled keys from appearing after reset.</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Thread Safety:</strong> Like <see cref="EnqueueKey"/>, this method is <em>not</em>
        /// thread-safe and must be called from the emulator's CPU thread. Use appropriate synchronization
        /// (command queue, dispatcher) when calling from other threads.
        /// </para>
        /// <para>
        /// <strong>Use Cases:</strong>
        /// <list type="bullet">
        /// <item><strong>System Reset:</strong> User presses Reset button or emulator performs cold boot</item>
        /// <item><strong>Clear Stuck Keys:</strong> Recovery from keyboard handler being in unexpected state</item>
        /// <item><strong>Clean State for Tests:</strong> Test fixtures can reset keyboard to known state</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Example Usage:</strong>
        /// <code>
        /// // In VA2M.Reset() - full system reset
        /// public void Reset()
        /// {
        ///     _keyboardSetter.ResetKeyboard();  // Clear pending keys, preserve latch with strobe cleared
        ///     Bus.Reset();                      // Reset CPU, memory, etc.
        ///     // ... other reset operations
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        void ResetKeyboard();
    }
