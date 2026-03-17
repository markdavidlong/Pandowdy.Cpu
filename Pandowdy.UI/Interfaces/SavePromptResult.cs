// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.UI.Interfaces;

/// <summary>
/// Result of a three-choice save prompt dialog.
/// </summary>
public enum SavePromptResult
{
    /// <summary>Save changes and proceed with the operation.</summary>
    Save,

    /// <summary>Proceed without saving (discard changes).</summary>
    DontSave,

    /// <summary>Cancel the operation — do not proceed.</summary>
    Cancel
}
