namespace UniversalMediaPlayer.Persistence;

using System.Globalization;
using Microsoft.Data.Sqlite;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Core.Persistence;

public sealed class SqliteWatchHistoryStore : IWatchHistoryStore, IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;
    private bool _disposed;

    public SqliteWatchHistoryStore(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dbPath = Path.Combine(localAppData, "UniversalMediaPlayer", "data", "history.db");
        }
        else
        {
            _dbPath = Path.GetFullPath(dbPath);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            DefaultTimeout = 5
        };

        _connectionString = builder.ConnectionString;
    }

    public async Task RecordPositionAsync(WatchHistoryItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.FilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.ShowId);
        ThrowIfDisposed();

        await using var connection = await CreateOpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        bool isCompleted = item.Completed || (item.DurationSeconds > 0 && (item.PositionSeconds / item.DurationSeconds >= 0.90));
        var lastPlayedUtcStr = item.LastPlayedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        // Check if item exists by FilePath
        long? existingId = null;
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.Transaction = tx;
            checkCmd.CommandText = "SELECT Id FROM WatchHistory WHERE FilePath = @FilePath ORDER BY Id DESC LIMIT 1;";
            checkCmd.Parameters.AddWithValue("@FilePath", item.FilePath);

            var result = await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (result is long id)
            {
                existingId = id;
            }
            else if (result is int intId)
            {
                existingId = intId;
            }
        }

        if (existingId.HasValue)
        {
            await using var updateCmd = connection.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = """
                UPDATE WatchHistory
                SET ShowId = @ShowId,
                    SeasonNumber = @SeasonNumber,
                    EpisodeNumber = @EpisodeNumber,
                    PositionSeconds = @PositionSeconds,
                    DurationSeconds = @DurationSeconds,
                    LastPlayedUtc = @LastPlayedUtc,
                    Completed = @Completed
                WHERE Id = @Id;
                """;

            updateCmd.Parameters.AddWithValue("@Id", existingId.Value);
            updateCmd.Parameters.AddWithValue("@ShowId", item.ShowId);
            updateCmd.Parameters.AddWithValue("@SeasonNumber", item.SeasonNumber.HasValue ? item.SeasonNumber.Value : DBNull.Value);
            updateCmd.Parameters.AddWithValue("@EpisodeNumber", item.EpisodeNumber.HasValue ? item.EpisodeNumber.Value : DBNull.Value);
            updateCmd.Parameters.AddWithValue("@PositionSeconds", item.PositionSeconds);
            updateCmd.Parameters.AddWithValue("@DurationSeconds", item.DurationSeconds);
            updateCmd.Parameters.AddWithValue("@LastPlayedUtc", lastPlayedUtcStr);
            updateCmd.Parameters.AddWithValue("@Completed", isCompleted ? 1 : 0);

            await updateCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        else
        {
            await using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = tx;
            insertCmd.CommandText = """
                INSERT INTO WatchHistory (ShowId, SeasonNumber, EpisodeNumber, FilePath, PositionSeconds, DurationSeconds, LastPlayedUtc, Completed)
                VALUES (@ShowId, @SeasonNumber, @EpisodeNumber, @FilePath, @PositionSeconds, @DurationSeconds, @LastPlayedUtc, @Completed);
                """;

            insertCmd.Parameters.AddWithValue("@ShowId", item.ShowId);
            insertCmd.Parameters.AddWithValue("@SeasonNumber", item.SeasonNumber.HasValue ? item.SeasonNumber.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("@EpisodeNumber", item.EpisodeNumber.HasValue ? item.EpisodeNumber.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("@FilePath", item.FilePath);
            insertCmd.Parameters.AddWithValue("@PositionSeconds", item.PositionSeconds);
            insertCmd.Parameters.AddWithValue("@DurationSeconds", item.DurationSeconds);
            insertCmd.Parameters.AddWithValue("@LastPlayedUtc", lastPlayedUtcStr);
            insertCmd.Parameters.AddWithValue("@Completed", isCompleted ? 1 : 0);

            await insertCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<WatchHistoryItem?> GetLatestByShowIdAsync(string showId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showId);
        ThrowIfDisposed();

        await using var connection = await CreateOpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ShowId, SeasonNumber, EpisodeNumber, FilePath, PositionSeconds, DurationSeconds, LastPlayedUtc, Completed
            FROM WatchHistory
            WHERE ShowId = @ShowId
            ORDER BY LastPlayedUtc DESC
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@ShowId", showId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return ReadWatchHistoryItem(reader);
        }

        return null;
    }

    public async Task<WatchHistoryItem?> GetByFilePathAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ThrowIfDisposed();

        await using var connection = await CreateOpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ShowId, SeasonNumber, EpisodeNumber, FilePath, PositionSeconds, DurationSeconds, LastPlayedUtc, Completed
            FROM WatchHistory
            WHERE FilePath = @FilePath
            ORDER BY LastPlayedUtc DESC
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@FilePath", filePath);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return ReadWatchHistoryItem(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<WatchHistoryItem>> GetContinueWatchingAsync(int limit = 10, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (limit <= 0)
        {
            return Array.Empty<WatchHistoryItem>();
        }

        await using var connection = await CreateOpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ShowId, SeasonNumber, EpisodeNumber, FilePath, PositionSeconds, DurationSeconds, LastPlayedUtc, Completed
            FROM WatchHistory
            WHERE Completed = 0
              AND PositionSeconds > 15
              AND (DurationSeconds - PositionSeconds) > 15
            ORDER BY LastPlayedUtc DESC
            LIMIT @Limit;
            """;
        cmd.Parameters.AddWithValue("@Limit", limit);

        var list = new List<WatchHistoryItem>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var item = ReadWatchHistoryItem(reader);
            if (seenPaths.Add(item.FilePath))
            {
                list.Add(item);
                if (list.Count >= limit)
                {
                    break;
                }
            }
        }

        return list;
    }

    public async Task MarkCompletedAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ThrowIfDisposed();

        await using var connection = await CreateOpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE WatchHistory SET Completed = 1 WHERE FilePath = @FilePath;";
        cmd.Parameters.AddWithValue("@FilePath", filePath);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task ClearHistoryAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await using var connection = await CreateOpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM WatchHistory;";

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";
        await pragmaCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return connection;
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA busy_timeout=5000;
                PRAGMA foreign_keys=ON;

                CREATE TABLE IF NOT EXISTS WatchHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ShowId TEXT NOT NULL,
                    SeasonNumber INTEGER,
                    EpisodeNumber INTEGER,
                    FilePath TEXT NOT NULL,
                    PositionSeconds REAL NOT NULL,
                    DurationSeconds REAL NOT NULL,
                    LastPlayedUtc TEXT NOT NULL,
                    Completed INTEGER NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_WatchHistory_ShowId ON WatchHistory(ShowId);
                CREATE INDEX IF NOT EXISTS IX_WatchHistory_FilePath ON WatchHistory(FilePath);
                CREATE INDEX IF NOT EXISTS IX_WatchHistory_LastPlayedUtc ON WatchHistory(LastPlayedUtc);
                """;

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static WatchHistoryItem ReadWatchHistoryItem(SqliteDataReader reader)
    {
        return new WatchHistoryItem
        {
            Id = reader.GetInt64(0),
            ShowId = reader.GetString(1),
            SeasonNumber = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            EpisodeNumber = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            FilePath = reader.GetString(4),
            PositionSeconds = reader.GetDouble(5),
            DurationSeconds = reader.GetDouble(6),
            LastPlayedUtc = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Completed = reader.GetInt32(8) != 0
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _initLock.Dispose();
        }
    }
}
