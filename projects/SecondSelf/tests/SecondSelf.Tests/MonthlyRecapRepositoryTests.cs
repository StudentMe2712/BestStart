using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

public sealed class MonthlyRecapRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly MonthlyRecapRepository _repository;

    public MonthlyRecapRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_recaps_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_factory).MigrateAsync().GetAwaiter().GetResult();
        _repository = new MonthlyRecapRepository(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task SaveAndGetRecap_WorksCorrectly()
    {
        var recap = new MonthlyRecap
        {
            YearMonth = "2026-10",
            FinalScore = 84.5,
            DailyRatio = "26/31",
            WeeklyRatio = "4/5",
            DailyWeight = 0.30,
            WeeklyWeight = 0.70,
            ReflectionText = null,
            IsFinalized = false
        };

        await _repository.SaveRecapAsync(recap);

        var retrieved = await _repository.GetRecapAsync("2026-10");
        Assert.NotNull(retrieved);
        Assert.Equal("2026-10", retrieved.YearMonth);
        Assert.Equal(84.5, retrieved.FinalScore);
        Assert.Equal("26/31", retrieved.DailyRatio);
        Assert.Equal("4/5", retrieved.WeeklyRatio);
        Assert.Equal(0.30, retrieved.DailyWeight);
        Assert.Equal(0.70, retrieved.WeeklyWeight);
        Assert.False(retrieved.IsFinalized);
        Assert.Null(retrieved.ReflectionText);
    }

    [Fact]
    public async Task FinalizeRecap_UpdatesReflectionAndFinalizedFlag()
    {
        var recap = new MonthlyRecap
        {
            YearMonth = "2026-10",
            FinalScore = 90.0,
            DailyRatio = "28/31",
            WeeklyRatio = "5/5",
            IsFinalized = false
        };
        await _repository.SaveRecapAsync(recap);

        await _repository.FinalizeRecapAsync("2026-10", "A strong month overall! Keep momentum.");

        var finalized = await _repository.GetRecapAsync("2026-10");
        Assert.NotNull(finalized);
        Assert.True(finalized.IsFinalized);
        Assert.Equal("A strong month overall! Keep momentum.", finalized.ReflectionText);
        Assert.NotNull(finalized.CompletedAt);
    }

    [Fact]
    public async Task GetAllRecaps_ReturnsAllOrderedByDescending()
    {
        var recapSep = new MonthlyRecap
        {
            YearMonth = "2026-09",
            FinalScore = 75.0,
            DailyRatio = "20/30",
            WeeklyRatio = "3/4"
        };

        var recapOct = new MonthlyRecap
        {
            YearMonth = "2026-10",
            FinalScore = 88.0,
            DailyRatio = "27/31",
            WeeklyRatio = "4/4"
        };

        await _repository.SaveRecapAsync(recapSep);
        await _repository.SaveRecapAsync(recapOct);

        var all = await _repository.GetAllRecapsAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal("2026-10", all[0].YearMonth);
        Assert.Equal("2026-09", all[1].YearMonth);
    }

    [Fact]
    public async Task SaveRecap_Upsert_OverwritesPreviousValues()
    {
        var initial = new MonthlyRecap
        {
            YearMonth = "2026-10",
            FinalScore = 50.0,
            DailyRatio = "10/31",
            WeeklyRatio = "1/4"
        };
        await _repository.SaveRecapAsync(initial);

        var updated = new MonthlyRecap
        {
            YearMonth = "2026-10",
            FinalScore = 75.0,
            DailyRatio = "22/31",
            WeeklyRatio = "3/4"
        };
        await _repository.SaveRecapAsync(updated);

        var retrieved = await _repository.GetRecapAsync("2026-10");
        Assert.NotNull(retrieved);
        Assert.Equal(75.0, retrieved.FinalScore);
        Assert.Equal("22/31", retrieved.DailyRatio);
    }
}
