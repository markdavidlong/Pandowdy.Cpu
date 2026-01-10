using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore;

/// <summary>
/// Simple keyboard handler that maintains a single key with strobe bit (Apple IIe style).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Implements the Apple IIe keyboard hardware behavior where a single
/// key latch holds the most recent keypress with a strobe bit (bit 7) indicating whether the
/// key has been read. This class provides both <see cref="IKeyboardReader"/> (for emulator I/O)
/// and <see cref="IKeyboardSetter"/> (for UI input injection).
/// </para>
/// <para>
/// <strong>Apple IIe Keyboard Mechanism:</strong>
/// <list type="bullet">
/// <item><strong>Key Press:</strong> When a key is pressed, its 7-bit ASCII code (0-127) is
/// placed in the keyboard latch with bit 7 set to 1 (strobe set), creating a value >= 128.</item>
/// <item><strong>Reading $C000 (KBD):</strong> Returns the keyboard latch value with strobe bit intact.
/// Multiple reads without clearing strobe return the same value.</item>
/// <item><strong>Reading $C010 (KBDSTRB):</strong> Clears the strobe bit (sets bit 7 to 0), indicating
/// the key has been acknowledged. Implemented by <see cref="ClearStrobe"/>.</item>
/// <item><strong>New Key Press:</strong> If a new key is pressed before the strobe is cleared, the
/// old key is discarded (hardware limitation of single-key latch).</item>
/// </list>
/// </para>
/// <para>
/// <strong>Dual Interface Design:</strong>
/// <list type="bullet">
/// <item><strong>IKeyboardReader:</strong> Used by SystemIoHandler to read keyboard state during
/// CPU memory access to $C000-$C010. Provides non-destructive reads and strobe clearing.</item>
/// <item><strong>IKeyboardSetter:</strong> Used by UI layer (MainWindowViewModel) to inject keypresses
/// when user types on physical keyboard. Sets key value with strobe bit automatically.</item>
/// </list>
/// </para>
/// <para>
/// <strong>Single-Key Limitation:</strong> Like the original Apple IIe hardware, this implementation
/// maintains only one key at a time. If the user presses a key before the software reads $C010,
/// the new key overwrites the old one. This matches authentic Apple IIe behavior (no key rollover,
/// no buffering).
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not thread-safe. All operations (EnqueueKey from UI thread,
/// reads from emulator thread) must be synchronized by the caller. In practice, ReactiveUI's
/// DispatcherScheduler handles this automatically.
/// </para>
/// </remarks>
/// <example>
/// <para>
/// <strong>Typical Usage in Emulator Architecture:</strong>
/// </para>
/// <code>
/// // Dependency injection setup
/// var keyboardHandler = new SingularKeyHandler();
/// var ioHandler = new SystemIoHandler(keyboardHandler, ...);  // Uses IKeyboardReader
/// var uiInput = keyboardHandler as IKeyboardSetter;            // UI uses IKeyboardSetter
/// 
/// // UI injects keypress
/// uiInput.EnqueueKey(0x41);  // User presses 'A' key
/// 
/// // Emulated software reads keyboard
/// // LDA $C000   -> Returns 0xC1 (strobe set)
/// byte kbdValue = ioHandler.ReadByte(0xC000);
/// 
/// // LDA $C010   -> Clears strobe
/// ioHandler.ReadByte(0xC010);
/// 
/// // LDA $C000   -> Returns 0x41 (strobe cleared)
/// kbdValue = ioHandler.ReadByte(0xC000);
/// </code>
/// </example>
public class SingularKeyHandler : IKeyboardReader, IKeyboardSetter
{
    /// <summary>
    /// Internal keyboard latch holding the current key value with strobe bit (bit 7).
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
    /// <strong>Examples:</strong>
    /// <list type="bullet">
    /// <item>0xC1 = 'A' key pressed, unread (0x41 | 0x80)</item>
    /// <item>0x41 = 'A' key, strobe cleared after reading $C010</item>
    /// <item>0x8D = Return key pressed, unread (0x0D | 0x80)</item>
    /// </list>
    /// </para>
    /// </remarks>
    private byte _key;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Tests bit 7 of the internal key latch. This is equivalent
    /// to the Apple IIe hardware checking the strobe flip-flop state.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> O(1) bit test operation, highly efficient for polling loops.
    /// </para>
    /// </remarks>
    public bool StrobePending() => ((_key & 0x80) == 0x80);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Masks off bit 7 using bitwise AND with 0x7F. This returns
    /// the pure ASCII character code without the strobe indicator.
    /// </para>
    /// <para>
    /// <strong>Use Case:</strong> Useful for debugging or UI display of the current key state
    /// without modifying the strobe bit. See <see cref="ClearStrobe"/> for strobe management.
    /// </para>
    /// </remarks>
    public byte PeekCurrentKeyValue() => (byte)(_key & 0x7f);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Returns the raw internal key latch value unchanged.
    /// This directly corresponds to reading address $C000 (KBD) on the Apple IIe.
    /// </para>
    /// <para>
    /// <strong>Non-Destructive Read:</strong> This method does not clear the strobe bit.
    /// Multiple calls return the same value until <see cref="ClearStrobe"/>
    /// or <see cref="EnqueueKey"/> is called.
    /// </para>
    /// </remarks>
    public byte PeekCurrentKeyAndStrobe() => _key;


    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Directly sets the internal key latch with the strobe bit
    /// forcibly set (OR with 0x80). This overwrites any previous unread key, matching the behavior
    /// of the original Apple IIe hardware.
    /// </para>
    /// <para>
    /// <strong>Strobe Override:</strong> Even if the input <paramref name="key"/> already has bit 7 set,
    /// this implementation ensures the strobe is set. For example, calling <c>EnqueueKey(0xC1)</c> or
    /// <c>EnqueueKey(0x41)</c> both result in an internal value of 0xC1.
    /// </para>
    /// <para>
    /// <strong>Key Overwrite Behavior:</strong> If a key was previously enqueued and not yet read
    /// (strobe still set), this method discards that key and replaces it with the new key. This is
    /// authentic Apple IIe behavior - the hardware does not buffer keypresses.
    /// </para>
    /// <para>
    /// <strong>Thread Safety Note:</strong> This method performs a simple assignment and is not
    /// synchronized. Callers must ensure proper thread coordination if injecting keys from a
    /// different thread than the emulator CPU thread.
    /// </para>
    /// <para>
    /// <strong>Typical Input Mapping:</strong>
    /// <code>
    /// // Map Avalonia Key enumeration to Apple IIe key codes
    /// switch (e.Key)
    /// {
    ///     case Key.A: EnqueueKey(0x41); break;  // 'A'
    ///     case Key.Enter: EnqueueKey(0x0D); break;  // Return
    ///     case Key.Escape: EnqueueKey(0x1B); break;  // Escape
    ///     case Key.Space: EnqueueKey(0x20); break;  // Space
    ///     // ... etc.
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public void EnqueueKey(byte key) => _key = (byte)(key | 0x80);


    public byte ClearStrobe() { _key &= 0x7f; return _key; }
}
