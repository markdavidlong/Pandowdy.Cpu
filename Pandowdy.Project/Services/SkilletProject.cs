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
    private string? _filePath;
    private ProjectMetadata _metadata;
    private bool _disposed;

    /// <summary>
    /// Tracks whether the project has been modified since it was created, opened, or last saved.
    /// Set exclusively via <see cref="MarkDirty"/>. Cleared by <see cref="SaveAsync"/>.
    /// Volatile for safe cross-thread reads from the UI thread.
    /// </summary>
    private volatile bool _projectDirty;

    /// <summary>
    /// Tracks disk images currently checked out to the emulator.
    /// Populated by <see cref="CheckOutAsync"/>, cleared by <see cref="ReturnAsync"/>.
    /// Used by <see cref="SaveAsync"/> to snapshot-serialize mounted disks.
    /// </summary>
    private readonly ConcurrentDictionary<long, InternalDiskImage> _checkedOutImages = new();

    /// <summary>
    /// Creates an ad hoc (in-memory) project.
    /// </summary>
    public SkilletProject(ProjectMetadata metadata, ProjectIOThread ioThread)
    {
        _filePath = null;
        _metadata = metadata;
        _ioThread = ioThread;
    }

    /// <summary>
    /// Creates a file-based project.
    /// </summary>
    public SkilletProject(string filePath, ProjectMetadata metadata, ProjectIOThread ioThread)
    {
        _filePath = filePath;
        _metadata = metadata;
        _ioThread = ioThread;
    }

    public string? FilePath => _filePath;
    public bool IsAdHoc => string.IsNullOrEmpty(_filePath) || _filePath == ":memory:";
    public ProjectMetadata Metadata => _metadata;
    public bool HasUnsavedChanges => _projectDirty;
    public bool HasDiskImages => EnqueueSync(conn => CheckForDiskImages(conn));

    // Disk image management

    public Task<long> ImportDiskImageAsync(string filePath, string name)
    {
        return EnqueueAsync(conn =>
        {
            // Detect format from file extension
            var format = DiskFormatHelper.GetFormatFromPath(filePath);
            if (format == DiskFormat.Unknown)
            {
                throw new ArgumentException($"Unsupported disk image format: {Path.GetExtension(filePath)}", nameof(filePath));
            }

            // Get importer and load the disk image
            var importer = DiskImageFactory.GetImporter(format);
            var diskImage = importer.Import(filePath);

            // Serialize both original and working copy to PIDI format
            var blob = DiskBlobStore.Serialize(diskImage);

            // Insert disk image record
            var now = DateTime.UtcNow.ToString("o");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO {SkilletConstants.TableDiskImages}
                    (name, original_filename, original_format, import_source_path, imported_utc,
                     whole_track_count, optimal_bit_timing, is_write_protected, persist_working, notes,
                     original_blob, working_blob, working_dirty, created_utc, modified_utc)
                VALUES
                    (@name, @originalFilename, @originalFormat, @importSourcePath, @importedUtc,
                     @wholeTrackCount, @optimalBitTiming, @isWriteProtected, @persistWorking, @notes,
                     @originalBlob, @workingBlob, 0, @createdUtc, @modifiedUtc);
                SELECT last_insert_rowid();
                """;

            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@originalFilename", Path.GetFileName(filePath));
            cmd.Parameters.AddWithValue("@originalFormat", format.ToString());
            cmd.Parameters.AddWithValue("@importSourcePath", filePath);
            cmd.Parameters.AddWithValue("@importedUtc", now);
            cmd.Parameters.AddWithValue("@wholeTrackCount", diskImage.PhysicalTrackCount);
            cmd.Parameters.AddWithValue("@optimalBitTiming", diskImage.OptimalBitTiming);
            cmd.Parameters.AddWithValue("@isWriteProtected", diskImage.IsWriteProtected ? 1 : 0);
            cmd.Parameters.AddWithValue("@persistWorking", 1);
            cmd.Parameters.AddWithValue("@notes", (object?)DBNull.Value);
            cmd.Parameters.AddWithValue("@originalBlob", blob);
            cmd.Parameters.AddWithValue("@workingBlob", blob); // Initial working copy = original
            cmd.Parameters.AddWithValue("@createdUtc", now);
            cmd.Parameters.AddWithValue("@modifiedUtc", now);

            var result = cmd.ExecuteScalar();
            var id = Convert.ToInt64(result);
            MarkDirty();
            return id;
        });
    }

    public Task<DiskImageRecord> GetDiskImageAsync(long id)
    {
        return EnqueueAsync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT id, name, original_filename, original_format, import_source_path, imported_utc,
                       whole_track_count, optimal_bit_timing, is_write_protected, persist_working, notes,
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
                       whole_track_count, optimal_bit_timing, is_write_protected, persist_working, notes,
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
            MarkDirty();
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
            MarkDirty();
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

    // IDiskImageStore implementation

    /// <summary>
    /// Checks out a disk image for use by the emulator (IDiskImageStore.CheckOutAsync).
    /// </summary>
    /// <remarks>
    /// Deserializes the disk image from the project store and sets
    /// <see cref="InternalDiskImage.DiskImageName"/> from the database record.
    /// No filesystem paths are needed — the disk data lives entirely in the project.
    /// </remarks>
    public Task<InternalDiskImage> CheckOutAsync(long diskImageId)
    {
        return EnqueueAsync(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT COALESCE(working_blob, original_blob), name
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
            var diskImage = DiskBlobStore.Deserialize(blobStream);

            // Set display name from project record (no filesystem paths needed)
            diskImage.DiskImageName = reader.GetString(1);

            // Track this image as checked out for snapshot-serialization in SaveAsync
            _checkedOutImages[diskImageId] = diskImage;

            return diskImage;
        });
    }

    /// <summary>
    /// Returns a disk image to the store after ejection (IDiskImageStore.ReturnAsync).
    /// </summary>
    /// <remarks>
    /// Only marks the project dirty and writes the working blob if the disk was
    /// actually modified (<see cref="InternalDiskImage.IsDirty"/>). Returning an
    /// unmodified disk after checkout is not a state change — the stored data is
    /// identical to what was checked out.
    /// </remarks>
    public Task ReturnAsync(long diskImageId, InternalDiskImage image)
    {
        _checkedOutImages.TryRemove(diskImageId, out _);

        if (image.IsDirty)
        {
            MarkDirty();
            return SaveDiskImageAsync(diskImageId, image);
        }

        return Task.CompletedTask;
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
            // Read the current value first to detect real changes.
            // This avoids false dirty when writing empty mount config for
            // newly-discovered controller slots that weren't in the seed data.
            long? currentDiskImageId;
            bool rowExists;

            using (var readCmd = conn.CreateCommand())
            {
                readCmd.CommandText = $"""
                    SELECT disk_image_id FROM {SkilletConstants.TableMountConfiguration}
                    WHERE slot = @slot AND drive_number = @drive;
                    """;
                readCmd.Parameters.AddWithValue("@slot", slot);
                readCmd.Parameters.AddWithValue("@drive", driveNumber);

                using var reader = readCmd.ExecuteReader();
                rowExists = reader.Read();
                currentDiskImageId = rowExists && !reader.IsDBNull(0) ? reader.GetInt64(0) : null;
            }

            // Write the value (UPSERT)
            using (var writeCmd = conn.CreateCommand())
            {
                writeCmd.CommandText = $"""
                    INSERT INTO {SkilletConstants.TableMountConfiguration} (slot, drive_number, disk_image_id, auto_mount)
                    VALUES (@slot, @drive, @diskId, 1)
                    ON CONFLICT(slot, drive_number) DO UPDATE SET disk_image_id = excluded.disk_image_id;
                    """;
                writeCmd.Parameters.AddWithValue("@slot", slot);
                writeCmd.Parameters.AddWithValue("@drive", driveNumber);
                writeCmd.Parameters.AddWithValue("@diskId", (object?)diskImageId ?? DBNull.Value);
                writeCmd.ExecuteNonQuery();
            }

            // Only mark dirty if the effective state actually changed.
            // Inserting a new row with NULL disk_image_id (just discovered a
            // controller slot not in the seed) is not a meaningful mutation.
            if (currentDiskImageId != diskImageId)
            {
                if (rowExists || diskImageId.HasValue)
                {
                    MarkDirty();
                }
            }
        });
    }

    // Settings

    public Task<string?> GetSettingAsync(string tableName, string key)
    {
        return EnqueueAsync(conn => ProjectSettingsStore.Get(conn, tableName, key));
    }

    public Task SetSettingAsync(string tableName, string key, string value)
    {
        return EnqueueAsync(conn =>
        {
            ProjectSettingsStore.Set(conn, tableName, key, value);
            MarkDirty();
        });
    }

    // Lifecycle

    /// <summary>
    /// Saves the project, serializing any currently checked-out (mounted) disk images.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Checked-out disk images are serialized using <see cref="DiskBlobStore.SerializeSnapshot"/>,
    /// which acquires <see cref="InternalDiskImage.SerializationLock"/> briefly (~1ms) to copy
    /// raw quarter-track data, then compresses outside the lock. The emulator continues running
    /// throughout — only the write path is blocked for the brief snapshot window.
    /// </para>
    /// </remarks>
    public async Task SaveAsync()
    {
        // Snapshot-serialize checked-out disk images BEFORE enqueuing to the IO thread.
        // SerializeSnapshot acquires the lock briefly per disk, then compresses outside.
        var serializedDisks = new Dictionary<long, byte[]>();
        foreach (var (id, image) in _checkedOutImages)
        {
            if (image.IsDirty)
            {
                serializedDisks[id] = DiskBlobStore.SerializeSnapshot(image);
            }
        }

        await EnqueueAsync(conn =>
        {
            using var transaction = conn.BeginTransaction();
            try
            {
                // Write serialized mounted disk blobs to working_blob
                foreach (var (id, blob) in serializedDisks)
                {
                    using var blobCmd = conn.CreateCommand();
                    blobCmd.CommandText = $"""
                        UPDATE {SkilletConstants.TableDiskImages}
                        SET working_blob = @blob, working_dirty = 0, modified_utc = @now
                        WHERE id = @id AND persist_working = 1;
                        """;
                    blobCmd.Parameters.AddWithValue("@id", id);
                    blobCmd.Parameters.AddWithValue("@blob", blob);
                    blobCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                    blobCmd.ExecuteNonQuery();
                }

                // Update project metadata modified timestamp
                using var metaCmd = conn.CreateCommand();
                metaCmd.CommandText = $"""
                    UPDATE {SkilletConstants.TableProjectMetadata}
                    SET value = @now
                    WHERE key = 'modified_utc';
                    """;
                metaCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                metaCmd.ExecuteNonQuery();

                // Clear working_dirty flag for any remaining persisted disks
                // (covers ejected disks whose blobs were already written via ReturnAsync)
                using var dirtyCmd = conn.CreateCommand();
                dirtyCmd.CommandText = $"""
                    UPDATE {SkilletConstants.TableDiskImages}
                    SET working_dirty = 0
                    WHERE persist_working = 1 AND working_dirty = 1;
                    """;
                dirtyCmd.ExecuteNonQuery();

                transaction.Commit();
                MarkClean();
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

    // Dirty tracking

    /// <summary>
    /// Marks the project as having unsaved changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the single centralized method for marking the project dirty. All mutation
    /// methods (import, remove, settings change, mount configuration, eject auto-flush,
    /// working copy regeneration) call this method rather than setting the flag directly.
    /// </para>
    /// <para>
    /// This is distinct from the per-disk-image <c>working_dirty</c> SQL column, which
    /// tracks whether a specific disk image's working blob has been modified via eject
    /// auto-flush. The SQL column is used by <see cref="SaveAsync"/> to manage per-disk
    /// persistence (which blobs to flush). <see cref="MarkDirty"/> tracks the project-level
    /// dirty state used by <see cref="HasUnsavedChanges"/> for UI enablement.
    /// </para>
    /// </remarks>
    private void MarkDirty()
    {
        _projectDirty = true;
    }

    /// <summary>
    /// Marks the project as clean (no unsaved changes). Called after a successful save.
    /// </summary>
    private void MarkClean()
    {
        _projectDirty = false;
    }

    /// <summary>
    /// Transitions this project from its current data source to a new file on disk.
    /// The <see cref="SkilletProject"/> instance is preserved — checked-out images,
    /// external references (<see cref="IDiskImageStore"/>), and dirty tracking all
    /// survive the transition. Only the internal SQLite connection, <see cref="FilePath"/>,
    /// and <see cref="Metadata"/> change.
    /// </summary>
    /// <param name="filePath">Destination file path for the new .skillet file.</param>
    /// <param name="preSwapAction">
    /// Action to run on the old connection before the swap (e.g., VACUUM INTO to create
    /// the new file from the current database contents).
    /// </param>
    internal async Task TransitionToFileAsync(
        string filePath,
        Action<SqliteConnection>? preSwapAction)
    {
        var projectName = Path.GetFileNameWithoutExtension(filePath);

        await _ioThread.SwapConnectionAsync(
            preSwapAction,
            filePath,
            conn =>
            {
                SkilletSchemaManager.SetPragmas(conn);

                // Update project name in metadata to match the new file name
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    UPDATE {SkilletConstants.TableProjectMetadata}
                    SET value = @name
                    WHERE key = 'name';
                    """;
                cmd.Parameters.AddWithValue("@name", projectName);
                cmd.ExecuteNonQuery();
            });

        // Update in-memory state to reflect the new file
        _filePath = filePath;
        _metadata = _metadata with { Name = projectName, ModifiedUtc = DateTime.UtcNow };
        MarkClean();
    }

    // IO thread helpers

    /// <summary>
    /// Internal helper for tests to execute operations on the IO thread.
    /// </summary>
    internal Task<T> EnqueueAsync<T>(Func<SqliteConnection, T> operation)
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
            WholeTrackCount = reader.GetInt32(6),
            OptimalBitTiming = (byte)reader.GetInt32(7),
            IsWriteProtected = reader.GetInt32(8) != 0,
            PersistWorking = reader.GetInt32(9) != 0,
            Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
            WorkingDirty = reader.GetInt32(11) != 0,
            CreatedUtc = DateTime.Parse(reader.GetString(12)),
            ModifiedUtc = DateTime.Parse(reader.GetString(13))
        };
    }

    private static bool CheckForDiskImages(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {SkilletConstants.TableDiskImages};";
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
    private readonly string? _filePath;
    private SqliteConnection? _connection;
    private bool _disposed;

    /// <summary>
    /// Creates an ad hoc (in-memory) IO thread.
    /// </summary>
    public ProjectIOThread()
    {
        _filePath = null;
        _thread = new Thread(RunLoop)
        {
            Name = "Pandowdy.Project.IO",
            IsBackground = true
        };
        _thread.Start();
    }

    /// <summary>
    /// Creates a file-based IO thread.
    /// </summary>
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

    /// <summary>
    /// Swaps the underlying SQLite connection to a new data source.
    /// Runs on the IO thread: all pending operations complete on the old connection first,
    /// then the swap happens, then subsequent operations use the new connection.
    /// </summary>
    /// <param name="preSwapAction">
    /// Optional action to run on the old connection before the swap (e.g., VACUUM INTO to
    /// create the new file from the current database).
    /// </param>
    /// <param name="newDataSource">File path for the new connection.</param>
    /// <param name="postSwapAction">
    /// Action to initialize the new connection (e.g., set pragmas, update project name).
    /// </param>
    internal Task SwapConnectionAsync(
        Action<SqliteConnection>? preSwapAction,
        string newDataSource,
        Action<SqliteConnection> postSwapAction)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProjectIOThread));
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(new IORequest<bool>(conn =>
        {
            // Run pre-swap action on old connection (e.g., VACUUM INTO)
            preSwapAction?.Invoke(conn);

            // Close and dispose the old connection
            conn.Close();
            conn.Dispose();

            // Open new connection to the target data source.
            // Assign to _connection so subsequent requests (via RunLoop) use the new one.
            _connection = new SqliteConnection($"Data Source={newDataSource}");
            _connection.Open();

            // Initialize new connection (pragmas, name update, etc.)
            postSwapAction(_connection);

            return true;
        }, tcs));
        return tcs.Task;
    }

    private void RunLoop()
    {
        try
        {
            // For ad hoc projects (null filePath), use in-memory database
            var dataSource = _filePath ?? ":memory:";
            _connection = new SqliteConnection($"Data Source={dataSource}");
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
