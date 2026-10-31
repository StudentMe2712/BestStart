using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-09: Расчёт формулы месяца и правило «Честные 100%».
/// 
/// Сценарий 1:
/// Given: в месяце 31 день и 4 недели по ISO-8601. Базовые веса: 30% Daily / 70% Weekly.
/// When: пользователь закрыл 31 день ритуалов (D = 100%) и 3 из 4 недель (W = 75%).
/// Then:
///   1. Математический расчёт: P = 0.30 * 100% + 0.70 * 75% = 30% + 52.5% = 82.5%.
///   2. В UI отображается округлённое значение 83%.
/// 
/// Сценарий 2:
/// When: пользователь закрыл 30 из 31 дня ритуалов и 4 из 4 недель (P = 0.30 * 96.77% + 0.70 * 100% = 29.03% + 70% = 99.03%).
/// Then: в UI отображается 99% (запрет на показ 100% из-за одного пропущенного дня — правило «Честные 100%»).
/// 
/// Сценарий 3:
/// When: закрыты все 31 день и все 4 недели.
/// Then: в UI отображается ровно 100%.
/// </summary>
public sealed class AC09_MonthlyFormulaAndFair100Tests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ProgressCalculationEngine _progressEngine;
    private readonly DailyRitualRepository _dailyRitualRepository;
    private readonly WeeklyGoalRepository _weeklyGoalRepository;
    private readonly MonthlyRecapRepository _monthlyRecapRepository;
    private readonly AppSettingsService _appSettingsService;
    private readonly ProgressTabViewModel _progressTabViewModel;
    private readonly MonthlyRecapViewModel _monthlyRecapViewModel;

    public AC09_MonthlyFormulaAndFair100Tests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac09_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _progressEngine = new ProgressCalculationEngine();
        _dailyRitualRepository = new DailyRitualRepository(_connectionFactory);
        _weeklyGoalRepository = new WeeklyGoalRepository(_connectionFactory);
        _monthlyRecapRepository = new MonthlyRecapRepository(_connectionFactory);

        var settingsPath = Path.Combine(Path.GetTempPath(), $"settings_ac09_{Guid.NewGuid():N}.json");
        _appSettingsService = new AppSettingsService(settingsPath);

        _progressTabViewModel = new ProgressTabViewModel(
            _progressEngine,
            _dailyRitualRepository,
            _weeklyGoalRepository,
            _monthlyRecapRepository,
            bigGoalRepository: null,
            _appSettingsService);

        _monthlyRecapViewModel = new MonthlyRecapViewModel(
            _monthlyRecapRepository,
            _progressEngine,
            _dailyRitualRepository,
            _weeklyGoalRepository,
            _appSettingsService);
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
    public void AC09_Scenario1_FullDays_And_ThreeQuartersWeeks_RoundsTo83Percent()
    {
        // Given: май 2026 (31 день, 4 недели по ISO-8601: недели 19, 20, 21, 22), веса 0.30 / 0.70
        int year = 2026;
        int month = 5;
        int completedDays = 31;
        int completedWeeks = 3;

        // When: производим математический расчёт через ProgressCalculationEngine
        var result = _progressEngine.CalculateMonthlyProgress(year, month, completedDays, completedWeeks, 0.30, 0.70);

        // Then:
        // RawScore = 0.30 * 100% + 0.70 * 75% = 30 + 52.5 = 82.5%
        Assert.Equal(82.5, result.RawScore, precision: 2);
        // В UI отображается округлённое значение 83%
        Assert.Equal(83, result.DisplayScore);
        Assert.False(result.IsHonest100Capped);
        Assert.Equal("31/31", result.DailyRatio);
        Assert.Equal("3/4", result.WeeklyRatio);
    }

    [Fact]
    public void AC09_Scenario2_OneDayMissed_AllWeeksDone_Fair100Rule_CapsAt99Percent()
    {
        // Given: май 2026 (31 день, 4 недели), веса 0.30 / 0.70
        int year = 2026;
        int month = 5;
        // 30 из 31 дня ритуалов и 4 из 4 недель
        int completedDays = 30;
        int completedWeeks = 4;

        // When: производим расчёт по базовым весам 30/70
        var result = _progressEngine.CalculateMonthlyProgress(year, month, completedDays, completedWeeks, 0.30, 0.70);

        // Then:
        // D = (30/31)*100 = 96.77419...
        // RawScore = 0.30 * 96.77419% + 0.70 * 100% = 29.03225% + 70% = 99.03225%
        Assert.True(result.RawScore > 99.0);
        // В UI отображается 99% (запрет на показ 100% из-за одного пропущенного дня)
        Assert.Equal(99, result.DisplayScore);
        Assert.Equal("30/31", result.DailyRatio);
        Assert.Equal("4/4", result.WeeklyRatio);

        // When: веса смещены (10% Daily / 90% Weekly), где RawScore = 99.677%
        // Без правила «Честные 100%» округление дало бы 100%, но правило блокирует 100% и кэпит до 99%
        var cappedResult = _progressEngine.CalculateMonthlyProgress(year, month, completedDays, completedWeeks, 0.10, 0.90);
        Assert.True(cappedResult.RawScore > 99.5);
        Assert.Equal(99, cappedResult.DisplayScore);
        Assert.True(cappedResult.IsHonest100Capped);
    }

    [Fact]
    public void AC09_Scenario3_AllDaysAndAllWeeksDone_ReachesHonest100Percent()
    {
        // Given: май 2026 — все 31 день и все 4 недели закрыты
        int year = 2026;
        int month = 5;
        int completedDays = 31;
        int completedWeeks = 4;

        // When: расчёт прогресса
        var result = _progressEngine.CalculateMonthlyProgress(year, month, completedDays, completedWeeks, 0.30, 0.70);

        // Then: честные 100% без ограничений
        Assert.Equal(100.0, result.RawScore, precision: 2);
        Assert.Equal(100, result.DisplayScore);
        Assert.False(result.IsHonest100Capped);
        Assert.Equal("31/31", result.DailyRatio);
        Assert.Equal("4/4", result.WeeklyRatio);
    }

    [Fact]
    public async Task AC09_ProgressTabViewModel_And_MonthlyRecapViewModel_ReflectFair100Accurately()
    {
        // Given: май 2026 в базе данных (31 день, 4 недели 19, 20, 21, 22):
        // 30 закрытых дней ритуалов и 4 закрытые недели
        const string yearMonth = "2026-05";

        // Добавляем видео в VideoLibrary для удовлетворения внешнего ключа
        await InsertTestVideoAsync("video-1", "path/video1.mp4");

        // Создаем 30 закрытых дней ритуала
        for (int day = 1; day <= 30; day++)
        {
            var dateStr = $"2026-05-{day:D2}";
            await _dailyRitualRepository.UpsertDailyRitualLogAsync(new DailyRitualLog
            {
                LogicalDate = dateStr,
                VideoId = "video-1",
                IsCompleted = true,
                CompletedAt = $"{dateStr}T10:00:00Z"
            });
        }

        // Недели 19, 20, 21, 22 — 4 недели мая 2026
        int[] mayWeeks = [19, 20, 21, 22];
        foreach (var w in mayWeeks)
        {
            await _weeklyGoalRepository.AddOrUpdateGoalAsync(new WeeklyGoal
            {
                IsoYear = 2026,
                IsoWeek = w,
                BelongsToMonth = yearMonth,
                Title = $"Week {w} Goal",
                Status = "Completed",
                CompletedAt = "2026-05-25T18:00:00Z"
            });
        }

        // When: ProgressTabViewModel загружает данные за май 2026
        await _progressTabViewModel.LoadMonthDataAsync(2026, 5);

        // Then:
        // В UI раздела Progress отображается 99% (правило Честные 100%)
        Assert.Equal(99, _progressTabViewModel.ScorePercentage);
        Assert.Equal("30/31", _progressTabViewModel.DailyFraction);
        Assert.Equal("4/4", _progressTabViewModel.WeeklyFraction);

        // When: оверлей MonthlyRecapViewModel открывается для мая 2026
        await _monthlyRecapViewModel.LoadRecapAsync(yearMonth, isReadOnly: false);

        // Then:
        // На карточке Monthly Recap отображается 99%
        Assert.Equal(99, _monthlyRecapViewModel.MonthlyScore);
        Assert.Equal("30/31", _monthlyRecapViewModel.DailyFraction);
        Assert.Equal("4/4", _monthlyRecapViewModel.WeeklyFraction);
    }

    private async Task InsertTestVideoAsync(string id, string filePath)
    {
        await using var conn = await _connectionFactory.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO VideoLibrary (Id, FilePath, FileName, DurationSeconds, PlaybackCount, IsMissing, LastVerifiedAt)
VALUES (@Id, @FilePath, 'test.mp4', 120.0, 0, 0, datetime('now'))
ON CONFLICT(Id) DO NOTHING;";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@FilePath", filePath);
        await cmd.ExecuteNonQueryAsync();
    }
}
