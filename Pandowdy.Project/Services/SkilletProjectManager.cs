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

    public async Task<ISkilletProject> CreateAdHocAsync()
    {
        // Close current project if exists (without creating ad hoc - we're creating one now)
        if (_currentProject is not null)
        {
            _currentProject.Dispose();
            _currentProject = null;
        }

        var pandowdyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";

        // Create in-memory project with Data Source=:memory:
        var ioThread = new ProjectIOThread(); // Parameterless = in-memory

        try
        {
            await ioThread.EnqueueAsync(conn =>
            {
                SkilletSchemaManager.InitializeSchema(conn, "untitled", pandowdyVersion);
            });

            var metadata = await LoadMetadataAsync(ioThread);
            _currentProject = new SkilletProject(metadata, ioThread); // Parameterless = ad hoc
            return _currentProject;
        }
        catch
        {
            ioThread.Dispose();
            throw;
        }
    }

    public async Task<ISkilletProject> CreateAsync(string filePath, string projectName)
    {
        // Close current project if exists (without creating ad hoc - we're creating one now)
        if (_currentProject is not null)
        {
            _currentProject.Dispose();
            _currentProject = null;
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
        // Close current project if exists (without creating ad hoc - we're opening one now)
        if (_currentProject is not null)
        {
            _currentProject.Dispose();
            _currentProject = null;
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

    public async Task SaveAsAsync(string filePath)
    {
        if (_currentProject is null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        if (File.Exists(filePath))
        {
            throw new InvalidOperationException($"File already exists: {filePath}");
        }

        // Cast to concrete type to access internal transition method
        var concreteProject = _currentProject as SkilletProject;
        if (concreteProject is null)
        {
            throw new InvalidOperationException("Current project is not a SkilletProject instance.");
        }

        // Flush any dirty mounted disks to the current database before creating the file snapshot.
        // KNOWN RACE: Between this snapshot and the VACUUM INTO below, the emulator may write
        // additional bits to mounted InternalDiskImage objects. Those writes are in the live
        // in-memory objects but NOT in the new file until the next save or eject auto-flush.
        // This is the same staleness window as any regular SaveAsync() — no data is lost,
        // just not yet persisted. See blueprint §5.3 for full analysis.
        await _currentProject.SaveAsync();

        // Transition: VACUUM INTO creates a compacted copy of the current database,
        // then the IO thread swaps its connection to the new file. The SkilletProject
        // instance is preserved — checked-out images, external IDiskImageStore references,
        // and dirty tracking all survive. No dispose/recreate cycle.
        await concreteProject.TransitionToFileAsync(
            filePath,
            preSwapAction: conn =>
            {
                var escapedPath = filePath.Replace("'", "''");
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"VACUUM INTO '{escapedPath}';";
                cmd.ExecuteNonQuery();
            });
    }

    public async Task CloseAsync()
    {
        if (_currentProject is null)
        {
            return;
        }

        _currentProject.Dispose();
        _currentProject = null;

        // Create new ad hoc project per blueprint: "there is never a state with no active project"
        await CreateAdHocAsync();
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
