// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Reflection;
using Microsoft.Data.Sqlite;
using Pandowdy.Project.Constants;
using Pandowdy.Project.Interfaces;
using Pandowdy.Project.Models;

namespace Pandowdy.Project.Services;

/// <summary>
/// Manages the lifecycle of .skillet project files.
/// </summary>
public sealed class SkilletProjectManager : ISkilletProjectManager
{
    private ISkilletProject? _currentProject;

    public ISkilletProject? CurrentProject => _currentProject;

    public async Task<ISkilletProject> CreateAsync(string filePath, string projectName)
    {
        if (_currentProject is not null)
        {
            throw new InvalidOperationException("A project is already open. Close it before creating a new one.");
        }

        if (File.Exists(filePath))
        {
            throw new InvalidOperationException($"File already exists: {filePath}");
        }

        var pandowdyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";

        var ioThread = new ProjectIOThread(filePath);

        try
        {
            await ioThread.EnqueueAsync(conn =>
            {
                SkilletSchemaManager.InitializeSchema(conn, projectName, pandowdyVersion);
            });

            var metadata = await LoadMetadataAsync(ioThread);
            _currentProject = new SkilletProject(filePath, metadata, ioThread);
            return _currentProject;
        }
        catch
        {
            ioThread.Dispose();
            throw;
        }
    }

    public async Task<ISkilletProject> OpenAsync(string filePath)
    {
        if (_currentProject is not null)
        {
            throw new InvalidOperationException("A project is already open. Close it before opening another.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Project file not found: {filePath}");
        }

        var ioThread = new ProjectIOThread(filePath);

        try
        {
            await ioThread.EnqueueAsync(conn =>
            {
                if (!SkilletSchemaManager.ValidateApplicationId(conn))
                {
                    throw new InvalidDataException($"File is not a valid .skillet project: {filePath}");
                }

                var currentVersion = SkilletSchemaManager.GetSchemaVersion(conn);
                if (currentVersion > SkilletConstants.SchemaVersion)
                {
                    throw new InvalidOperationException(
                        $"Project schema version {currentVersion} is newer than supported version {SkilletConstants.SchemaVersion}. " +
                        $"Please upgrade Pandowdy to open this project.");
                }

                if (currentVersion < SkilletConstants.SchemaVersion)
                {
                    SkilletSchemaManager.Migrate(conn, currentVersion);
                }

                SkilletSchemaManager.SetPragmas(conn);
            });

            var metadata = await LoadMetadataAsync(ioThread);
            _currentProject = new SkilletProject(filePath, metadata, ioThread);
            return _currentProject;
        }
        catch
        {
            ioThread.Dispose();
            throw;
        }
    }

    public async Task CloseAsync()
    {
        if (_currentProject is null)
        {
            return;
        }

        _currentProject.Dispose();
        _currentProject = null;
        await Task.CompletedTask;
    }

    private static Task<ProjectMetadata> LoadMetadataAsync(ProjectIOThread ioThread)
    {
        return ioThread.EnqueueAsync(conn =>
        {
            var metadata = new Dictionary<string, string>();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT key, value FROM {SkilletConstants.TableProjectMetadata};";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                metadata[reader.GetString(0)] = reader.GetString(1);
            }

            return new ProjectMetadata(
                Name: metadata["name"],
                CreatedUtc: DateTime.Parse(metadata["created_utc"]),
                ModifiedUtc: DateTime.Parse(metadata["modified_utc"]),
                SchemaVersion: int.Parse(metadata["schema_version"]),
                PandowdyVersion: metadata["pandowdy_version"]
            );
        });
    }
}
