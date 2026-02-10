// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Collection definition for disk I/O tests.
/// All test classes in this collection will run sequentially to prevent
/// file access conflicts when multiple tests try to read the same disk image files.
/// </summary>
[CollectionDefinition("DiskTests", DisableParallelization = true)]
public class DiskTestsCollection
{
}
