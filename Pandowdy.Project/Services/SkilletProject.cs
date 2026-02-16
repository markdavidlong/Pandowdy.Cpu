// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.Project.Constants;
using Pandowdy.Project.Interfaces;
using Pandowdy.Project.Models;

namespace Pandowdy.Project.Services;

/// <summary>
/// Implementation of <see cref="ISkilletProject"/> with dedicated IO thread for SQLite access.
/// </summary>
internal sealed class SkilletProject : ISkilletProject
{
    private readonly ProjectIOThread _ioThread;
    private readonly string _filePath;
    private readonly ProjectMetadata _metadata;
    private bool _disposed;

    public SkilletProject(string filePath, ProjectMetadata metadata, ProjectIOThread ioThread)
    {
        _filePath = filePath;
        _metadata = metadata;
        _ioThread = ioThread;
    }

    public string FilePath => _filePath;
    public ProjectMetadata Metadata => _metadata;
    public bool HasUnsavedChanges => EnqueueSync(conn => CheckForUnsavedChanges(conn));

    // Disk image management

    public Task<long> ImportDiskImageAsync(string filePath, string name)
    {
        return EnqueueAsync(conn =>
        {
            // TODO: Use IDiskImageImporter to load the file into InternalDiskImage
            // For now, placeholder implementation returns 0
            // Full implementation in Phase 2
            return 0L;
        });
    }

    public Task<DiskImageRecord> GetDiskImageAsync(long id)
    {
        return EnqueueAsync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT id, name, original_filename, original_format, import_source_path, imported_utc,
                       track_count, optimal_bit_timing, is_write_protected, persist_working, notes,
                       working_dirty, created_utc, modified_utc
                FROM {SkilletConstants.TableDiskImages}
                WHERE id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException($"Disk image {id} not found.");
            }

            return ReadDiskImageRecord(reader);
        });
    }

    public Task<IReadOnlyList<DiskImageRecord>> GetAllDiskImagesAsync()
    {
        return EnqueueAsync(conn =>
        {
            var records = new List<DiskImageRecord>();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT id, name, original_filename, original_format, import_source_path, imported_utc,
                       track_count, optimal_bit_timing, is_write_protected, persist_working, notes,
                       working_dirty, created_utc, modified_utc
                FROM {SkilletConstants.TableDiskImages};
                """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add(ReadDiskImageRecord(reader));
            }

            return (IReadOnlyList<DiskImageRecord>)records;
        });
    }

    public Task RemoveDiskImageAsync(long id)
    {
        return EnqueueAsync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM {SkilletConstants.TableDiskImages} WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        });
    }

    public Task RegenerateWorkingCopyAsync(long id)
    {
        return EnqueueAsync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                UPDATE {SkilletConstants.TableDiskImages}
                SET working_blob = NULL, working_dirty = 0, modified_utc = @now
                WHERE id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        });
    }

    public Stream OpenOriginalBlobRead(long diskImageId)
    {
        return EnqueueSync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT original_blob FROM {SkilletConstants.TableDiskImages} WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", diskImageId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException($"Disk image {diskImageId} not found.");
            }

            var blobStream = reader.GetStream(0);
            var ms = new MemoryStream();
            blobStream.CopyTo(ms);
            ms.Position = 0;
            return ms;
        });
    }

    public Stream OpenWorkingBlobRead(long diskImageId)
    {
        return EnqueueSync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT COALESCE(working_blob, original_blob)
                FROM {SkilletConstants.TableDiskImages}
                WHERE id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", diskImageId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException($"Disk image {diskImageId} not found.");
            }

            var blobStream = reader.GetStream(0);
            var ms = new MemoryStream();
            blobStream.CopyTo(ms);
            ms.Position = 0;
            return ms;
        });
    }

    public Task WriteWorkingBlobAsync(long diskImageId, Stream data)
    {
        return EnqueueAsync(conn =>
        {
            var blobData = new byte[data.Length];
            data.Read(blobData, 0, blobData.Length);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                UPDATE {SkilletConstants.TableDiskImages}
                SET working_blob = @blob, working_dirty = 1, modified_utc = @now
                WHERE id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", diskImageId);
            cmd.Parameters.AddWithValue("@blob", blobData);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        });
    }

    public Task<InternalDiskImage> LoadDiskImageAsync(long diskImageId)
    {
        return EnqueueAsync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT COALESCE(working_blob, original_blob)
                FROM {SkilletConstants.TableDiskImages}
                WHERE id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", diskImageId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException($"Disk image {diskImageId} not found.");
            }

            using var blobStream = reader.GetStream(0);
            return DiskBlobStore.Deserialize(blobStream);
        });
    }

    public Task SaveDiskImageAsync(long diskImageId, InternalDiskImage diskImage)
    {
        return EnqueueAsync(conn =>
        {
            var blob = DiskBlobStore.Serialize(diskImage);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                UPDATE {SkilletConstants.TableDiskImages}
                SET working_blob = @blob, working_dirty = 1, modified_utc = @now
                WHERE id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", diskImageId);
            cmd.Parameters.AddWithValue("@blob", blob);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        });
    }

    // Mount configuration

    public Task<IReadOnlyList<MountConfiguration>> GetMountConfigurationAsync()
    {
        return EnqueueAsync(conn =>
        {
            var configs = new List<MountConfiguration>();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT id, slot, drive_number, disk_image_id, auto_mount
                FROM {SkilletConstants.TableMountConfiguration};
                """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                configs.Add(new MountConfiguration(
                    Id: reader.GetInt64(0),
                    Slot: reader.GetInt32(1),
                    DriveNumber: reader.GetInt32(2),
                    DiskImageId: reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    AutoMount: reader.GetInt32(4) != 0
                ));
            }

            return (IReadOnlyList<MountConfiguration>)configs;
        });
    }

    public Task SetMountAsync(int slot, int driveNumber, long? diskImageId)
    {
        return EnqueueAsync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO {SkilletConstants.TableMountConfiguration} (slot, drive_number, disk_image_id, auto_mount)
                VALUES (@slot, @drive, @diskId, 1)
                ON CONFLICT(slot, drive_number) DO UPDATE SET disk_image_id = excluded.disk_image_id;
                """;
            cmd.Parameters.AddWithValue("@slot", slot);
            cmd.Parameters.AddWithValue("@drive", driveNumber);
            cmd.Parameters.AddWithValue("@diskId", (object?)diskImageId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        });
    }

    // Settings

    public Task<string?> GetSettingAsync(string tableName, string key)
    {
        return EnqueueAsync(conn => ProjectSettingsStore.Get(conn, tableName, key));
    }

    public Task SetSettingAsync(string tableName, string key, string value)
    {
        return EnqueueAsync(conn => ProjectSettingsStore.Set(conn, tableName, key, value));
    }

    // Lifecycle

    public Task SaveAsync()
    {
        return EnqueueAsync(conn =>
        {
            using var transaction = conn.BeginTransaction();
            try
            {
                // Update project metadata modified timestamp
                using var metaCmd = conn.CreateCommand();
                metaCmd.CommandText = $"""
                    UPDATE {SkilletConstants.TableProjectMetadata}
                    SET value = @now
                    WHERE key = 'modified_utc';
                    """;
                metaCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                metaCmd.ExecuteNonQuery();

                // Clear working_dirty flag for all persisted disks
                using var dirtyCmd = conn.CreateCommand();
                dirtyCmd.CommandText = $"""
                    UPDATE {SkilletConstants.TableDiskImages}
                    SET working_dirty = 0
                    WHERE persist_working = 1 AND working_dirty = 1;
                    """;
                dirtyCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ioThread.Dispose();
        _disposed = true;
    }

    // IO thread helpers

    private Task<T> EnqueueAsync<T>(Func<SqliteConnection, T> operation)
    {
        return _ioThread.EnqueueAsync(operation);
    }

    private Task EnqueueAsync(Action<SqliteConnection> operation)
    {
        return _ioThread.EnqueueAsync(operation);
    }

    private T EnqueueSync<T>(Func<SqliteConnection, T> operation)
    {
        return _ioThread.EnqueueAsync(operation).GetAwaiter().GetResult();
    }

    // Helper methods

    private static DiskImageRecord ReadDiskImageRecord(SqliteDataReader reader)
    {
        return new DiskImageRecord
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            OriginalFilename = reader.IsDBNull(2) ? null : reader.GetString(2),
            OriginalFormat = reader.GetString(3),
            ImportSourcePath = reader.IsDBNull(4) ? null : reader.GetString(4),
            ImportedUtc = DateTime.Parse(reader.GetString(5)),
            TrackCount = reader.GetInt32(6),
            OptimalBitTiming = (byte)reader.GetInt32(7),
            IsWriteProtected = reader.GetInt32(8) != 0,
            PersistWorking = reader.GetInt32(9) != 0,
            Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
            WorkingDirty = reader.GetInt32(11) != 0,
            CreatedUtc = DateTime.Parse(reader.GetString(12)),
            ModifiedUtc = DateTime.Parse(reader.GetString(13))
        };
    }

    private static bool CheckForUnsavedChanges(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(*) FROM {SkilletConstants.TableDiskImages}
            WHERE persist_working = 1 AND working_dirty = 1;
            """;
        var result = cmd.ExecuteScalar();
        return result is long count && count > 0;
    }
}

/// <summary>
/// Dedicated IO thread for SQLite operations. All SQLite access goes through this thread.
/// </summary>
internal sealed class ProjectIOThread : IDisposable
{
    private readonly Thread _thread;
    private readonly BlockingCollection<IORequest> _queue = new();
    private readonly string _filePath;
    private SqliteConnection? _connection;
    private bool _disposed;

    public ProjectIOThread(string filePath)
    {
        _filePath = filePath;
        _thread = new Thread(RunLoop)
        {
            Name = "Pandowdy.Project.IO",
            IsBackground = true
        };
        _thread.Start();
    }

    public Task<T> EnqueueAsync<T>(Func<SqliteConnection, T> operation)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProjectIOThread));
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(new IORequest<T>(operation, tcs));
        return tcs.Task;
    }

    public Task EnqueueAsync(Action<SqliteConnection> operation)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProjectIOThread));
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(new IORequest<bool>(conn =>
        {
            operation(conn);
            return true;
        }, tcs));
        return tcs.Task;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _queue.CompleteAdding();
        _thread.Join();
        _disposed = true;
    }

    private void RunLoop()
    {
        try
        {
            _connection = new SqliteConnection($"Data Source={_filePath}");
            _connection.Open();

            foreach (var request in _queue.GetConsumingEnumerable())
            {
                request.Execute(_connection);
            }
        }
        finally
        {
            _connection?.Dispose();
        }
    }
}

/// <summary>
/// Base class for IO requests.
/// </summary>
internal abstract class IORequest
{
    public abstract void Execute(SqliteConnection connection);
}

/// <summary>
/// Typed IO request with result.
/// </summary>
internal sealed class IORequest<T>(
    Func<SqliteConnection, T> operation,
    TaskCompletionSource<T> tcs) : IORequest
{
    public override void Execute(SqliteConnection connection)
    {
        try
        {
            var result = operation(connection);
            tcs.SetResult(result);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
    }
}
