namespace Pandowdy.EmuCore;

/// <summary>
/// Manages VBlank (vertical blanking) timing state for Apple IIe emulation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> This class tracks the VBlank status by maintaining a countdown
/// counter that determines when the RD_VERTBLANK_ ($C019) soft switch should read as $80
/// (in VBlank) vs $00 (not in VBlank).
/// </para>
/// <para>
/// <strong>Architecture:</strong> Shared between VA2MBus (which decrements the counter each
/// CPU cycle) and SystemIoHandler (which reads InVBlank to service $C019 reads). This
/// decouples VBlank timing from I/O handling while keeping them synchronized.
/// </para>
/// <para>
/// <strong>Apple IIe NTSC Timing:</strong>
/// <list type="bullet">
/// <item>Total frame: 17,030 cycles (262 scanlines × 65 cycles/scanline)</item>
/// <item>Visible display: 192 scanlines (cycles 0-12,479)</item>
/// <item>Vertical blanking: 70 scanlines (cycles 12,480-17,029)</item>
/// <item>VBlank duration: 4,550 cycles</item>
/// </list>
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong>
/// <code>
/// // VA2MBus.Clock() - every CPU cycle
/// _vblank.Counter--;
/// 
/// // VA2MBus.Clock() - every 17,030 cycles at VBlank start
/// _vblank.ResetCounter();
/// 
/// // SystemIoHandler.Read($C019) - when software checks VBlank
/// return _vblank.InVBlank ? (byte)0x80 : (byte)0x00;
/// </code>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not thread-safe. Must be accessed only from the
/// emulator worker thread.
/// </para>
/// </remarks>
public class VBlankStatusHandler
{
    private long _VblankBlackoutCounter = 0;
    
    /// <summary>
    /// Number of cycles during which the vertical blanking flag (RD_VERTBLANK_) reads as $80.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The VBlank blackout period is 4,550 cycles (70 scanlines × 65 cycles/scanline) during
    /// which the video scanner is not drawing visible scanlines. Software can use this period
    /// for graphics updates without causing visual artifacts.
    /// </para>
    /// <para>
    /// <strong>Timing:</strong>
    /// <list type="bullet">
    /// <item>VBlank starts at cycle 12,480 (scanline 192)</item>
    /// <item>VBlank ends at cycle 17,029 (scanline 261)</item>
    /// <item>Duration: 70 scanlines × 65 cycles = 4,550 cycles</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>80-Column Firmware Synchronization:</strong> The 80-column firmware relies on
    /// testing RD_VERTBLANK_ to determine when to start page-flipping. By ensuring VBlank
    /// fires at the correct cycle (12,480), we maintain synchronization with firmware timing
    /// expectations.
    /// </para>
    /// </remarks>
    public const int VBlankBlackoutCycles = 4550;

    /// <summary>
    /// Gets or sets the current VBlank countdown counter.
    /// </summary>
    /// <value>
    /// Number of CPU cycles remaining in the current VBlank period. When greater than 0,
    /// the system is in VBlank. When 0 or negative, VBlank is not active.
    /// </value>
    /// <remarks>
    /// <para>
    /// <strong>Counter Lifecycle:</strong>
    /// <list type="number">
    /// <item>Set to 4,550 when VBlank starts (at cycle 12,480 of frame)</item>
    /// <item>Decremented by 1 every CPU cycle (VA2MBus.Clock)</item>
    /// <item>Reaches 0 after 4,550 cycles (end of VBlank at cycle 17,030)</item>
    /// <item>Continues decrementing (goes negative) until next VBlank</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Why Allow Negative Values:</strong> The counter is allowed to go negative
    /// to simplify the logic. VA2MBus decrements it every cycle, and we only check if
    /// Counter > 0 to determine VBlank status. This avoids needing to stop decrementing
    /// at zero.
    /// </para>
    /// <para>
    /// <strong>Typical Range:</strong>
    /// <list type="bullet">
    /// <item>Max: 4,550 (VBlank just started)</item>
    /// <item>Min: ~-12,480 (VBlank just about to start)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public long Counter
    {
        get => _VblankBlackoutCounter;
        set => _VblankBlackoutCounter = value;
    }

    /// <summary>
    /// Gets a value indicating whether the system is currently in the VBlank period.
    /// </summary>
    /// <value>
    /// <c>true</c> if Counter > 0 (in VBlank); otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// <strong>Usage:</strong> This property is primarily used by SystemIoHandler to
    /// determine the return value for RD_VERTBLANK_ ($C019) reads:
    /// <code>
    /// // SystemIoHandler.Read(0x19)
    /// return _vblank.InVBlank ? (byte)0x80 : (byte)0x00;
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Timing:</strong> Returns <c>true</c> for 4,550 cycles starting when VBlank
    /// is triggered (cycle 12,480 of each frame), then returns <c>false</c> for the
    /// remaining 12,480 cycles of the frame.
    /// </para>
    /// <para>
    /// <strong>Software Use:</strong> Apple IIe software (including DOS, ProDOS, and
    /// 80-column firmware) uses bit 7 of $C019 to detect VBlank for:
    /// <list type="bullet">
    /// <item>Flicker-free page flipping</item>
    /// <item>Timing animation frames</item>
    /// <item>Synchronizing graphics updates</item>
    /// </list>
    /// </para>
    /// </remarks>
    public bool InVBlank
    {
        get => Counter > 0;
    }

    /// <summary>
    /// Resets the VBlank counter to the start of a new VBlank period.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>When Called:</strong> VA2MBus calls this method every time the system clock
    /// reaches a VBlank cycle boundary (every 17,030 cycles).
    /// </para>
    /// <para>
    /// <strong>Effect:</strong> Sets Counter to VBlankBlackoutCycles (4,550), causing
    /// InVBlank to return <c>true</c> for the next 4,550 CPU cycles.
    /// </para>
    /// <para>
    /// <strong>Timing Example:</strong>
    /// <code>
    /// // At cycle 12,480 (VBlank starts)
    /// ResetCounter();  // Counter = 4,550
    /// 
    /// // After 1 cycle (cycle 12,481)
    /// Counter--;       // Counter = 4,549, InVBlank = true
    /// 
    /// // After 4,550 cycles (cycle 17,030)
    /// Counter--;       // Counter = 0, InVBlank = false
    /// 
    /// // After 4,551 cycles (cycle 17,031)
    /// Counter--;       // Counter = -1, InVBlank = false
    /// 
    /// // At cycle 29,510 (next VBlank)
    /// ResetCounter();  // Counter = 4,550, cycle repeats
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from the emulator worker thread only.
    /// </para>
    /// </remarks>
    public void ResetCounter() => Counter = VBlankBlackoutCycles;
}
