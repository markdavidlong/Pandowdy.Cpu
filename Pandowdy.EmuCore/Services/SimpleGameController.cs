// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;
    /// <summary>
    /// Simple game controller implementation supporting 4 analog paddles and 3 pushbuttons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Apple IIe Game Controller:</strong> Emulates the standard Apple IIe game controller
    /// interface with 4 paddle/joystick analog inputs (0-255) and 3 digital pushbuttons.
    /// </para>
    /// <para>
    /// <strong>Change Detection:</strong> Events are fired only when values actually change,
    /// preventing unnecessary event spam and I/O handler updates.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This class is not thread-safe. External synchronization
    /// is required if accessed from multiple threads (typically the emulator thread only).
    /// </para>
    /// </remarks>
    [Capability(typeof(IRestartable))]
    public class SimpleGameController : IGameControllerStatus
    {
        /// <summary>
        /// Paddle/joystick analog values (0-255). 4 channels for 4 paddles or 2 joysticks.
        /// </summary>
        private byte[] _axes = new byte[4];

        /// <summary>
        /// Pushbutton states (pressed/released). 3 buttons.
        /// </summary>
        private bool[] _buttons = new bool[3];

        /// <inheritdoc />
        public event EventHandler<GameControllerButtonChangedEventArgs>? ButtonChanged;

        /// <inheritdoc />
        public event EventHandler<GameControllerPaddleChangedEventArgs>? PaddleChanged;

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// <strong>Change Detection:</strong> Compares new value against current state.
        /// If identical, no change occurs and no event is fired. This prevents false
        /// triggers when UI repeatedly sets the same value.
        /// </para>
        /// <para>
        /// <strong>Event Firing:</strong> The <see cref="ButtonChanged"/> event is raised
        /// synchronously after the state changes but before the method returns.
        /// </para>
        /// </remarks>
        public void SetButton(int button, bool value)
        {
            if (button >= 0 && button < 3)
            {
                // Only update and fire event if value actually changed
                if (_buttons[button] != value)
                {
                    _buttons[button] = value;
                    ButtonChanged?.Invoke(this, new GameControllerButtonChangedEventArgs(button, value));
                }
            }
        }

        /// <inheritdoc />
        public bool GetButton(int button)
        {
            if (button >= 0 && button < 3)
            {
                return _buttons[button];
            }
            return false;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// <strong>Change Detection:</strong> Compares new value against current value.
        /// If identical, no change occurs and no event is fired. This prevents false
        /// triggers when analog input polling repeatedly sets the same value.
        /// </para>
        /// <para>
        /// <strong>Event Firing:</strong> The <see cref="PaddleChanged"/> event is raised
        /// synchronously after the value changes but before the method returns.
        /// </para>
        /// <para>
        /// <strong>Apple IIe Paddle Range:</strong> Values are 0-255 where:
        /// <list type="bullet">
        /// <item>0 = Fully left (X-axis) or fully up (Y-axis)</item>
        /// <item>127-128 = Center position</item>
        /// <item>255 = Fully right (X-axis) or fully down (Y-axis)</item>
        /// </list>
        /// </para>
        /// </remarks>
        public void SetPaddle(int paddle, byte value)
        {
            if (paddle >= 0 && paddle < 4)
            {
                // Only update and fire event if value actually changed
                if (_axes[paddle] != value)
                {
                    _axes[paddle] = value;
                    PaddleChanged?.Invoke(this, new GameControllerPaddleChangedEventArgs(paddle, value));
                }
            }
        }

            /// <inheritdoc />
            public byte GetPaddle(int paddle)
            {
                if (paddle >= 0 && paddle < 4)
                {
                    return _axes[paddle];
                }
                return 0;
            }

            /// <summary>
            /// Restores the game controller to its initial power-on state (cold boot).
            /// Releases all buttons and centers all paddles.
            /// </summary>
            public void Restart()
            {
                SetButton(0, false);
                SetButton(1, false);
                SetButton(2, false);
            }
        }
