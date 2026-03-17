// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Project.Interfaces;

namespace Pandowdy.Project.Services;

/// <summary>
/// Manages the lifecycle of Skillet projects (create, open, close, save-as).
/// Delegates all persistence to <see cref="IProjectStore"/> instances obtained
/// from the injected <see cref="IProjectStoreFactory"/>.
/// </summary>
public sealed class SkilletProjectManager(IProjectStoreFactory storeFactory) : ISkilletProjectManager
{
    private ISkilletProject? _currentProject;

    public ISkilletProject? CurrentProject => _currentProject;

    /// <inheritdoc/>
    public Task<ISkilletProject> CreateAdHocAsync()
    {
        _currentProject?.Dispose();
        _currentProject = SkilletProject.CreateNew("untitled");
        return Task.FromResult(_currentProject);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Creates a store at <paramref name="filePath"/>, constructs an empty project
    /// bound to that store, and immediately persists the initial empty state so the
    /// store on disk is consistent with the in-memory project.
    /// </remarks>
    public Task<ISkilletProject> CreateAsync(string filePath, string projectName)
    {
        _currentProject?.Dispose();
        _currentProject = null;

        var store = storeFactory.Create(filePath);

        try
        {
            var project = SkilletProject.CreateNew(projectName, store);
            store.Save(project.ToSnapshot());
            _currentProject = project;
            return Task.FromResult(_currentProject);
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Opens the store at <paramref name="filePath"/>, loads its manifest, and
    /// constructs a project from the manifest. Blobs are NOT loaded at this point —
    /// they are lazy-loaded on first access via the project's blob residency logic.
    /// </remarks>
    public Task<ISkilletProject> OpenAsync(string filePath)
    {
        _currentProject?.Dispose();
        _currentProject = null;

        var store = storeFactory.Open(filePath);

        try
        {
            var manifest = store.LoadManifest();
            _currentProject = SkilletProject.FromManifest(manifest, store);
            return Task.FromResult(_currentProject);
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Creates a new store at <paramref name="filePath"/> and delegates the full
    /// "copy everything + swap backing store" logic to
    /// <see cref="ISkilletProject.SaveAsAsync"/>. The in-memory project instance is
    /// preserved — checked-out images, dirty state, and all in-memory collections
    /// survive the save-as operation.
    /// </remarks>
    public async Task SaveAsAsync(string filePath)
    {
        if (_currentProject is null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        var newStore = storeFactory.Create(filePath);

        try
        {
            await _currentProject.SaveAsAsync(newStore);
        }
        catch
        {
            newStore.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Disposes the current project and immediately creates a fresh ad hoc project
    /// so that <see cref="CurrentProject"/> is never null after this call.
    /// </remarks>
    public async Task CloseAsync()
    {
        _currentProject?.Dispose();
        _currentProject = null;

        await CreateAdHocAsync();
    }
}
