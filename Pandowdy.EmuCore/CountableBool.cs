//------------------------------------------------------------------------------
// CountableBool.cs
//
// Boolean specialization of CountableVariable that adds convenience methods
// (Set, Clear, Toggle) for common boolean operations. Used primarily for soft
// switch state tracking in the Apple IIe emulator.
//
// DESIGN RATIONALE:
// Inherits change-counting functionality from CountableVariable<bool>, adding
// boolean-specific convenience methods that make soft switch management more
// readable and maintainable.
//
// USAGE:
// Ideal for representing on/off states (like soft switches) where tracking
// change frequency is important for debugging and performance analysis.
//------------------------------------------------------------------------------

namespace Pandowdy.EmuCore
{
    /// <summary>
    /// Boolean variable that tracks value changes with a counter.
    /// </summary>
    /// <remarks>
    /// Specialized version of <see cref="CountableVariable{T}"/> for boolean values,
    /// adding convenience methods (Set, Clear, Toggle) for common operations.
    /// Ideal for representing on/off states like soft switches.
    /// </remarks>
    public class CountableBool : CountableVariable<bool>
    {
        /// <summary>
        /// Initializes a new instance with initial value of false.
        /// </summary>
        public CountableBool() : base(false)
        {
        }

        /// <summary>
        /// Sets the value to true (increments counter if currently false).
        /// </summary>
        public void Set()
        {
            Value = true;
        }

        /// <summary>
        /// Sets the value to false (increments counter if currently true).
        /// </summary>
        public void Clear()
        {
            Value = false;
        }

        /// <summary>
        /// Toggles the value between true and false (always increments counter).
        /// </summary>
        public void Toggle()
        {
            Value = !Value;
        }
    }
}
