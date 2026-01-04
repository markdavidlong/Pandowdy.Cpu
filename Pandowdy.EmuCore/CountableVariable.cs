//------------------------------------------------------------------------------
// CountableVariable.cs
//
// Generic variable wrapper that tracks value changes with a counter for debugging
// and diagnostics. Part of the soft switch change-tracking system.
//
// DESIGN RATIONALE:
// This class was built in anticipation of debugging and profiling needs:
// - Performance profiling (detect excessive toggling)
// - Compatibility testing (compare usage patterns)
// - Regression detection (track changes in test runs)
// - UI visualization (real-time activity indicators)
// - Save state validation (detect desync issues)
//
// While this violates YAGNI (You Aren't Gonna Need It), the minimal cost
// (single int increment per change) is justified by the anticipated debugging
// value. The feature is passive and can be completely ignored when not needed.
//
// THREAD SAFETY:
// Not thread-safe. Intended for single-threaded use (e.g., CPU thread in emulator).
//
// PERFORMANCE:
// Minimal overhead - single integer increment when value changes. No allocation,
// no extra indirection.
//------------------------------------------------------------------------------

namespace Pandowdy.EmuCore
{
   
      /// <summary>
      /// Generic variable that tracks value changes with a counter.
      /// </summary>
      /// <remarks>
      /// <para>
      /// <strong>Purpose:</strong> Provides change tracking for debugging and diagnostics.
      /// The counter increments each time the value changes, allowing detection of
      /// unexpected state changes or high-frequency toggling.
      /// </para>
      /// <para>
      /// <strong>Design Philosophy:</strong> This feature was built anticipating future debugging
      /// needs, even if not fully utilized currently. The minimal overhead (single int increment
      /// per change) is justified by the anticipated diagnostic value. This is a deliberate
      /// exception to YAGNI principles based on known future requirements.
      /// </para>
      /// </remarks>
        public class CountableVariable<T>(T initialValue)
        {
            /// <summary>
            /// The current value being tracked.
            /// </summary>
            protected T _value = initialValue;

            /// <summary>
            /// Count of how many times the value has changed since construction or last reset.
            /// </summary>
            protected int _count = 0;

            /// <summary>
            /// Gets or sets the current value. Setting a new value increments the change counter.
            /// </summary>
            /// <remarks>
            /// The counter only increments when the value actually changes. Setting the same
            /// value multiple times does not increment the counter.
            /// </remarks>
            public T Value
            {
                get => _value;
                set
                {
                    if (!Equals(_value, value))
                    {
                        _value = value;
                        _count++;
                    }
                }
            }

            /// <summary>
            /// Gets the number of times the value has changed since construction or last reset.
            /// </summary>
            public int Count => _count;

            /// <summary>
            /// Resets the change counter to zero without changing the value.
            /// </summary>
            public void ResetCount() => _count = 0;

            /// <summary>
            /// Returns a string representation showing the value and change count.
            /// </summary>
            /// <returns>String in format "value (count)" or "null (count)" if value is null.</returns>
            public override string ToString()
            {
                if (Value != null)
                {
                    return Value.ToString() + $" ({_count})";
                }
                else
                {
                    return $"null ({_count})";
                }
            }

            /// <summary>
            /// Returns a detailed debug string showing the value and change count.
            /// </summary>
            /// <param name="variable">The CountableVariable instance to format.</param>
            /// <returns>String in format "Value: value, ChangeCount: count".</returns>
            public static string ToDebugString(CountableVariable<T> variable)
            {
                return $"Value: {variable._value}, ChangeCount: {variable._count}";
            }
        }


    
}
