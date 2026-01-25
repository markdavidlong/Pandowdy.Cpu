namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Interface for reading keyboard input with Apple IIe hardware-accurate strobe behavior.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Apple IIe Keyboard Mechanism:</strong> The Apple IIe keyboard uses a strobe bit (bit 7)
/// to indicate when a new key has been pressed but not yet read by software. When a key is pressed,
/// the 7-bit ASCII code (0-127) is placed in bits 0-6, and bit 7 is set to 1 (creating a value >= 128).
/// </para>
/// <para>
/// <strong>Hardware Addresses:</strong>
/// <list type="bullet">
/// <item>$C000 (KBD) - Read keyboard latch with strobe (returns value with bit 7 set if unread)</item>
/// <item>$C010 (KBDSTRB) - Clear keyboard strobe (reading this address clears bit 7)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Strobe Lifecycle:</strong>
/// <list type="number">
/// <item>Key pressed â†’ Strobe set (bit 7 = 1), value = ASCII + 128</item>
/// <item>Program reads $C000 â†’ Returns key with strobe (e.g., 0xC1 for 'A')</item>
/// <item>Program reads $C010 â†’ Strobe cleared (bit 7 = 0), subsequent reads return ASCII only (e.g., 0x41)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Implementation Note:</strong> This interface supports both single-key and buffered keyboard
/// implementations. The <see cref="Services.SingularKeyHandler"/> provides a simple single-key implementation
/// matching original Apple IIe behavior (new keypress overwrites previous unread key).
/// </para>
/// </remarks>
public interface IKeyboardReader
{
    /// <summary>
    /// Checks if there is an unread key with strobe bit set.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a key is pending (strobe bit 7 is set); <c>false</c> if no unread key or strobe was cleared.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method checks the internal keyboard latch for the strobe bit without modifying the state.
    /// It corresponds to testing bit 7 of the value that would be read from $C000 (KBD).
    /// </para>
    /// <para>
    /// <strong>Apple IIe Usage Pattern:</strong>
    /// <code>
    /// ; Assembly code polling for keypress
    /// WAIT_KEY:
    ///     LDA $C000      ; Read keyboard
    ///     BPL WAIT_KEY   ; Loop if bit 7 clear (no key)
    ///     STA $C010      ; Clear strobe
    ///     AND #$7F       ; Mask off strobe bit
    /// </code>
    /// </para>
    /// </remarks>
    public bool StrobePending();

    /// <summary>
    /// Returns the 7-bit ASCII character code of the current key without the strobe bit.
    /// </summary>
    /// <returns>The ASCII character code (0-127) with bit 7 cleared, regardless of strobe state.</returns>
    /// <remarks>
    /// <para>
    /// This method returns the lower 7 bits of the current key value, equivalent to reading $C000
    /// and masking with 0x7F (AND #$7F). The strobe bit is always cleared in the returned value.
    /// </para>
    /// <para>
    /// <strong>Use Case:</strong> Useful for inspecting the key value without clearing the strobe,
    /// or for reading the key after the strobe has already been cleared via <see cref="ClearStrobe"/>.
    /// </para>
    /// <para>
    /// <strong>Example:</strong> If the internal value is 0xC1 (strobe set, 'A' key), this returns 0x41.
    /// </para>
    /// </remarks>
    public byte PeekCurrentKeyValue();

    /// <summary>
    /// Returns the raw keyboard latch value including the strobe bit (bit 7).
    /// </summary>
    /// <returns>The full 8-bit keyboard value with strobe bit intact (0-255).</returns>
    /// <remarks>
    /// <para>
    /// This method simulates reading from $C000 (KBD) on the Apple IIe. The returned value includes
    /// bit 7 (the strobe bit), which indicates whether the key has been read:
    /// <list type="bullet">
    /// <item>Bit 7 = 1 (value >= 128): Key is unread, strobe active</item>
    /// <item>Bit 7 = 0 (value &lt; 128): Key has been read, strobe cleared</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example Values:</strong>
    /// <list type="bullet">
    /// <item>0xC1 = 'A' key pressed, unread (0x41 | 0x80)</item>
    /// <item>0x41 = 'A' key, strobe cleared</item>
    /// <item>0x8D = Return key pressed, unread (0x0D | 0x80)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public byte PeekCurrentKeyAndStrobe();

    /// <summary>
    /// Clears the keyboard strobe bit (bit 7) and returns the key value without strobe.
    /// </summary>
    /// <returns>The 7-bit ASCII character code (0-127) with strobe cleared.</returns>
    /// <remarks>
    /// <para>
    /// This method simulates reading from $C010 (KBDSTRB) on the Apple IIe. The strobe bit
    /// is cleared, indicating the key has been acknowledged by software. Subsequent reads
    /// from $C000 (via <see cref="PeekCurrentKeyAndStrobe"/>) will return the key without strobe
    /// until a new key is pressed.
    /// </para>
    /// <para>
    /// <strong>Buffered Implementations:</strong> In buffered keyboard implementations like
    /// <see cref="Services.QueuedKeyHandler"/>, calling this method may trigger automatic loading of
    /// the next queued key after a configurable delay. This simulates natural typing where
    /// keys arrive sequentially with pauses between.
    /// </para>
    /// </remarks>
    public byte ClearStrobe();

    /// <summary>
    /// Returns the number of keys waiting in the queue with strobe bits set.
    /// </summary>
    /// <returns>
    /// Number of keys pending. Returns 0 for simple implementations that don't buffer keys.
    /// Buffered implementations return the queue depth (excluding the current key in the latch).
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Implementation Differences:</strong>
    /// <list type="bullet">
    /// <item><strong>SingularKeyHandler:</strong> Always returns 0 or 1 (single-key buffer)</item>
    /// <item><strong>QueuedKeyHandler:</strong> Returns number of keys queued (may be > 1)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>UI display: "X keys pending" indicator during paste operations</item>
    /// <item>Test verification: Ensure all keys were queued correctly</item>
    /// <item>Throttling: Pause key injection if queue is full</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> This count does NOT include the current key in the keyboard latch.
    /// Use <see cref="StrobePending"/> to check if a key is currently loaded and unread.
    /// </para>
    /// </remarks>
    public int NumKeysPending();
}
