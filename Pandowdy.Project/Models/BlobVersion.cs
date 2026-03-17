// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Well-known blob version constants.
/// </summary>
public static class BlobVersion
{
    /// <summary>Version 0: the immutable original import.</summary>
    public const int Original = 0;

    /// <summary>
    /// Resolves to the highest working version number,
    /// or <see cref="Original"/> if no working versions exist.
    /// </summary>
    public const int Latest = -1;
}
