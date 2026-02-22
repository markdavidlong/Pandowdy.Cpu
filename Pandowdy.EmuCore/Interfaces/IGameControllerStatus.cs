// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Interfaces
{
    /// <summary>
    /// Interface for Apple IIe game controller (joystick/paddle) status and control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Apple IIe Game Controller Hardware:</strong> The Apple IIe supports analog
    /// paddles (4 channels) and pushbuttons (3 channels). Paddles use resistive position
    /// sensing, while buttons are simple digital inputs.
    /// </para>
    /// <para>
    /// <strong>Change Events:</strong> Implementations should fire events only when values
    /// actually change to avoid unnecessary I/O handler updates and event spam.
    /// </para>
    /// </remarks>
    public interface IGameControllerStatus : IRestartable
    {
        /// <summary>
        /// Event raised when a button state changes.
        /// </summary>
        /// <remarks>
        /// Event args contain the button number (0-2) and new state (pressed/released).
        /// Fired only when the button state actually changes.
        /// </remarks>
        event EventHandler<GameControllerButtonChangedEventArgs>? ButtonChanged;

        /// <summary>
        /// Event raised when a paddle value changes.
        /// </summary>
        /// <remarks>
        /// Event args contain the paddle number (0-3) and new value (0-255).
        /// Fired only when the paddle value actually changes.
        /// </remarks>
        event EventHandler<GameControllerPaddleChangedEventArgs>? PaddleChanged;

        /// <summary>
        /// Sets a button state.
        /// </summary>
        /// <param name="button">Button number (0-2). Out of range values are ignored.</param>
        /// <param name="value">True if button is pressed, false if released.</param>
        /// <remarks>
        /// If the controller doesn't handle buttons or button is out of range, this is a no-op.
        /// The <see cref="ButtonChanged"/> event is fired only if the state actually changes.
        /// </remarks>
        public void SetButton(int button, bool value);

        /// <summary>
        /// Gets a button state.
        /// </summary>
        /// <param name="button">Button number (0-2).</param>
        /// <returns>True if button is pressed, false if released or out of range.</returns>
        /// <remarks>
        /// If the controller doesn't handle buttons or button is out of range, returns false.
        /// </remarks>
        public bool GetButton(int button);

        /// <summary>
        /// Sets a paddle value (or joystick axis - joysticks are 2 ganged paddles).
        /// </summary>
        /// <param name="paddle">Paddle number (0-3). Out of range values are ignored.</param>
        /// <param name="value">Paddle position (0-255). 0 = fully left/up, 255 = fully right/down.</param>
        /// <remarks>
        /// If the controller doesn't handle paddles or paddle is out of range, this is a no-op.
        /// The <see cref="PaddleChanged"/> event is fired only if the value actually changes.
        /// </remarks>
        public void SetPaddle(int paddle, byte value);

        /// <summary>
        /// Gets a paddle value.
        /// </summary>
        /// <param name="paddle">Paddle number (0-3).</param>
        /// <returns>Paddle position (0-255), or 0 if out of range.</returns>
        /// <remarks>
        /// If the controller doesn't handle paddles or paddle is out of range, returns 0.
        /// </remarks>
        public byte GetPaddle(int paddle);
    }

    /// <summary>
    /// Event arguments for button state change events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="GameControllerButtonChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="buttonNumber">Button number (0-2).</param>
    /// <param name="isPressed">New button state.</param>
    public class GameControllerButtonChangedEventArgs(int buttonNumber, bool isPressed) : EventArgs
    {
        /// <summary>
        /// Gets the button number that changed (0-2).
        /// </summary>
        public int ButtonNumber { get; } = buttonNumber;

        /// <summary>
        /// Gets the new button state (true = pressed, false = released).
        /// </summary>
        public bool IsPressed { get; } = isPressed;
    }

    /// <summary>
    /// Event arguments for paddle value change events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="GameControllerPaddleChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="paddleNumber">Paddle number (0-3).</param>
    /// <param name="value">New paddle value.</param>
    public class GameControllerPaddleChangedEventArgs(int paddleNumber, byte value) : EventArgs
    {
        /// <summary>
        /// Gets the paddle number that changed (0-3).
        /// </summary>
        public int PaddleNumber { get; } = paddleNumber;

        /// <summary>
        /// Gets the new paddle value (0-255).
        /// </summary>
        public byte Value { get; } = value;
    }
}
