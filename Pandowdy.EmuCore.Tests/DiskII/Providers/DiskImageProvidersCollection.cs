namespace Pandowdy.EmuCore.Tests.DiskII.Providers;

/// <summary>
/// Collection definition for disk image provider tests.
/// These tests must run sequentially because they access shared disk image files.
/// </summary>
[CollectionDefinition("DiskImageProviders", DisableParallelization = true)]
public class DiskImageProvidersCollection : ICollectionFixture<DiskImageProvidersFixture>
{
}

/// <summary>
/// Shared fixture for disk image provider tests.
/// </summary>
public class DiskImageProvidersFixture
{
    // No shared state needed - just using this to group tests
}
