// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Exception thrown when a card message cannot be processed.
/// </summary>
public class CardMessageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CardMessageException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public CardMessageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CardMessageException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CardMessageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
