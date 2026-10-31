using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий ежемесячных отчётов на базе SQLite.
/// </summary>
public sealed class MonthlyRecapRepository : IMonthlyRecapRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public MonthlyRecapRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<MonthlyRecap?> GetRecapAsync(string yearMonth, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
SELECT YearMonth, FinalScore, DailyRatio, WeeklyRatio, DailyWeight, WeeklyWeight, ReflectionText, IsFinalized, CompletedAt
FROM MonthlyRecaps
WHERE YearMonth = @YearMonth
LIMIT 1;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@YearMonth", yearMonth);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return ReadMonthlyRecap(reader);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MonthlyRecap>> GetAllRecapsAsync(CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
SELECT YearMonth, FinalScore, DailyRatio, WeeklyRatio, DailyWeight, WeeklyWeight, ReflectionText, IsFinalized, CompletedAt
FROM MonthlyRecaps
ORDER BY YearMonth DESC;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var list = new List<MonthlyRecap>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(ReadMonthlyRecap(reader));
        }

        return list;
    }

    /// <inheritdoc />
    public async Task SaveRecapAsync(MonthlyRecap recap, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recap);
        ArgumentException.ThrowIfNullOrWhiteSpace(recap.YearMonth);

        if (string.IsNullOrWhiteSpace(recap.CompletedAt))
        {
            recap.CompletedAt = DateTime.UtcNow.ToString("O");
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
INSERT INTO MonthlyRecaps (YearMonth, FinalScore, DailyRatio, WeeklyRatio, DailyWeight, WeeklyWeight, ReflectionText, IsFinalized, CompletedAt)
VALUES (@YearMonth, @FinalScore, @DailyRatio, @WeeklyRatio, @DailyWeight, @WeeklyWeight, @ReflectionText, @IsFinalized, @CompletedAt)
ON CONFLICT(YearMonth) DO UPDATE SET
    FinalScore = excluded.FinalScore,
    DailyRatio = excluded.DailyRatio,
    WeeklyRatio = excluded.WeeklyRatio,
    DailyWeight = excluded.DailyWeight,
    WeeklyWeight = excluded.WeeklyWeight,
    ReflectionText = excluded.ReflectionText,
    IsFinalized = excluded.IsFinalized,
    CompletedAt = excluded.CompletedAt;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@YearMonth", recap.YearMonth);
        cmd.Parameters.AddWithValue("@FinalScore", recap.FinalScore);
        cmd.Parameters.AddWithValue("@DailyRatio", recap.DailyRatio);
        cmd.Parameters.AddWithValue("@WeeklyRatio", recap.WeeklyRatio);
        cmd.Parameters.AddWithValue("@DailyWeight", recap.DailyWeight);
        cmd.Parameters.AddWithValue("@WeeklyWeight", recap.WeeklyWeight);
        cmd.Parameters.AddWithValue("@ReflectionText", (object?)recap.ReflectionText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsFinalized", recap.IsFinalized ? 1 : 0);
        cmd.Parameters.AddWithValue("@CompletedAt", recap.CompletedAt);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task FinalizeRecapAsync(string yearMonth, string reflectionText, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);

        var nowIso = DateTime.UtcNow.ToString("O");
        await using var connection = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        const string sql = @"
UPDATE MonthlyRecaps
SET ReflectionText = @ReflectionText,
    IsFinalized = 1,
    CompletedAt = @CompletedAt
WHERE YearMonth = @YearMonth;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@YearMonth", yearMonth);
        cmd.Parameters.AddWithValue("@ReflectionText", (object?)reflectionText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAt", nowIso);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Ежемесячный отчёт за '{yearMonth}' не найден.");
        }
    }

    private static MonthlyRecap ReadMonthlyRecap(DbDataReader reader)
    {
        return new MonthlyRecap
        {
            YearMonth = reader.GetString(reader.GetOrdinal("YearMonth")),
            FinalScore = reader.GetDouble(reader.GetOrdinal("FinalScore")),
            DailyRatio = reader.GetString(reader.GetOrdinal("DailyRatio")),
            WeeklyRatio = reader.GetString(reader.GetOrdinal("WeeklyRatio")),
            DailyWeight = reader.GetDouble(reader.GetOrdinal("DailyWeight")),
            WeeklyWeight = reader.GetDouble(reader.GetOrdinal("WeeklyWeight")),
            ReflectionText = reader.IsDBNull(reader.GetOrdinal("ReflectionText")) ? null : reader.GetString(reader.GetOrdinal("ReflectionText")),
            IsFinalized = reader.GetInt32(reader.GetOrdinal("IsFinalized")) == 1,
            CompletedAt = reader.GetString(reader.GetOrdinal("CompletedAt"))
        };
    }
}
