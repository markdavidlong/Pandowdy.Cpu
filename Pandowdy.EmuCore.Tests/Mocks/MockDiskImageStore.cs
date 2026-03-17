// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;

namespace Pandowdy.EmuCore.Tests.Mocks;

/// <summary>
/// Mock implementation of IDiskImageStore for testing.
/// </summary>
/// <remarks>
/// This mock provides a no-op implementation for tests that need an IDiskImageStore
/// but don't actually use disk checkout/return functionality. Tests that need to verify
/// checkout/return behavior should create custom mocks or use a real SkilletProject instance.
/// </remarks>
public sealed class MockDiskImageStore : IDiskImageStore
{
    /// <summary>
    /// Gets or sets a callback to invoke when CheckOutAsync is called.
    /// </summary>
    public Func<long, Task<InternalDiskImage>>? OnCheckOut { get; set; }

    /// <summary>
    /// Gets or sets a callback to invoke when ReturnAsync is called.
    /// </summary>
    public Func<long, InternalDiskImage, Task>? OnReturn { get; set; }

    /// <inheritdoc />
    public Task<InternalDiskImage> CheckOutAsync(long diskImageId)
    {
        if (OnCheckOut != null)
        {
            return OnCheckOut(diskImageId);
        }

        // Default: return a blank 35-track disk image
        var image = new InternalDiskImage(
            physicalTrackCount: 35,
            standardTrackBitCount: 51200);

        return Task.FromResult(image);
    }

    /// <inheritdoc />
    public Task ReturnAsync(long diskImageId, InternalDiskImage image)
    {
        if (OnReturn != null)
        {
            return OnReturn(diskImageId, image);
        }

        // Default: no-op
        return Task.CompletedTask;
    }
}
