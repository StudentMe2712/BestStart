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
/// BDD-тесты приёмочного критерия AC-11: Активация и финализация Monthly Recap.
/// 
/// Given: наступило 18:00 последнего дня месяца (или утро 1-го числа нового месяца).
/// When: пользователь открывает Desktop Widget или Personal Dashboard.
/// Then:
///   1. Разворачивается полноэкранный оверлей Monthly Recap с итоговым процентом (например, 78%), дробями 26/31 и 3/4.
///   2. Доступно многострочное текстовое поле для рефлексии (Что сработало? Что изменить?).
///   3. При нажатии «Завершить месяц и начать новый цикл»: отчёт сохраняется в MonthlyRecaps со статусом IsFinalized = 1,
///      счётчики нового месяца сбрасываются.
/// </summary>
public sealed class AC11_MonthlyRecapActivationAndFinalizationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ProgressCalculationEngine _progressEngine;
    private readonly DailyRitualRepository _dailyRitualRepository;
    private readonly WeeklyGoalRepository _weeklyGoalRepository;
    private readonly MonthlyRecapRepository _monthlyRecapRepository;
    private readonly TaskRepository _taskRepository;
    private readonly BigGoalRepository _bigGoalRepository;
    private readonly AppSettingsService _appSettingsService;
    private readonly VideoLibraryService _videoLibraryService;
    private readonly FisherYatesShuffleService _shuffleService;
    private readonly BackupService _backupService;

    private readonly DashboardViewModel _dashboardViewModel;

    public AC11_MonthlyRecapActivationAndFinalizationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac11_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _progressEngine = new ProgressCalculationEngine();
        _dailyRitualRepository = new DailyRitualRepository(_connectionFactory);
        _weeklyGoalRepository = new WeeklyGoalRepository(_connectionFactory);
        _monthlyRecapRepository = new MonthlyRecapRepository(_connectionFactory);
        _taskRepository = new TaskRepository(_connectionFactory);
        _bigGoalRepository = new BigGoalRepository(_connectionFactory);

        var tempDir = Path.Combine(Path.GetTempPath(), $"ac11_settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _appSettingsService = new AppSettingsService(Path.Combine(tempDir, "settings.json"));
        _videoLibraryService = new VideoLibraryService(_connectionFactory, tempDir);
        _shuffleService = new FisherYatesShuffleService(_connectionFactory, _videoLibraryService);
        _backupService = new BackupService(_connectionFactory, tempDir);

        var todayTabVm = new TodayTabViewModel(_weeklyGoalRepository, _taskRepository, _bigGoalRepository, _dailyRitualRepository, _progressEngine);
        var goalsTabVm = new GoalsTabViewModel(_bigGoalRepository);
        var progressTabVm = new ProgressTabViewModel(_progressEngine, _dailyRitualRepository, _weeklyGoalRepository, _monthlyRecapRepository, _bigGoalRepository, _appSettingsService);
        var archiveTabVm = new ArchiveTabViewModel(_taskRepository);
        var settingsTabVm = new SettingsTabViewModel(_videoLibraryService, _shuffleService, _appSettingsService, _backupService);
        var monthlyRecapVm = new MonthlyRecapViewModel(_monthlyRecapRepository, _progressEngine, _dailyRitualRepository, _weeklyGoalRepository, _appSettingsService);
        var weeklyGoalModalVm = new WeeklyGoalModalViewModel(_weeklyGoalRepository, _bigGoalRepository);

        _dashboardViewModel = new DashboardViewModel(
            todayTabVm,
            goalsTabVm,
            progressTabVm,
            archiveTabVm,
            settingsTabVm,
            monthlyRecapVm,
            weeklyGoalModalVm,
            _monthlyRecapRepository);
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
    public async Task AC11_LastDayOfMonth_At1800_ActivatesMonthlyRecapOverlay()
    {
        // Given: 31 марта 2026, время 18:30 (последний день месяца >= 18:00).
        // Март 2026 имеет 31 день и ровно 4 недели по ISO-8601 (недели 10, 11, 12, 13).
        var lastDay1830 = new DateTime(2026, 3, 31, 18, 30, 0, DateTimeKind.Utc);

        await InsertTestVideoAsync("video-test", "videos/test.mp4");

        // В базе создано 26 закрытых дней ритуалов из 31
        for (int i = 1; i <= 26; i++)
        {
            await _dailyRitualRepository.UpsertDailyRitualLogAsync(new DailyRitualLog
            {
                LogicalDate = $"2026-03-{i:D2}",
                VideoId = "video-test",
                IsCompleted = true
            });
        }

        // 3 закрытых недели из 4 (недели 10, 11, 12)
        int[] weeks = [10, 11, 12];
        foreach (var w in weeks)
        {
            await _weeklyGoalRepository.AddOrUpdateGoalAsync(new WeeklyGoal
            {
                IsoYear = 2026,
                IsoWeek = w,
                BelongsToMonth = "2026-03",
                Title = $"Goal {w}",
                Status = "Completed"
            });
        }
        // 4-я неделя (13) осталась в процессе
        await _weeklyGoalRepository.AddOrUpdateGoalAsync(new WeeklyGoal
        {
            IsoYear = 2026,
            IsoWeek = 13,
            BelongsToMonth = "2026-03",
            Title = "Goal 13",
            Status = "Active"
        });

        // When: пользователь открывает Dashboard в 18:30 последнего дня месяца
        bool activated = await _dashboardViewModel.CheckMonthlyRecapActivationAsync(lastDay1830);

        // Then:
        // Оверлей активирован автоматически
        Assert.True(activated);
        Assert.True(_dashboardViewModel.IsMonthlyRecapOpen);

        // Проверяем метрики оверлея: 78%, 26/31, 3/4
        var recapVm = _dashboardViewModel.MonthlyRecapViewModel;
        Assert.Equal("МАРТ 2026", recapVm.HeaderMonthYear);
        Assert.Equal("26/31", recapVm.DailyFraction);
        Assert.Equal("3/4", recapVm.WeeklyFraction);
        // Математика: 0.30*(26/31)*100 + 0.70*(3/4)*100 = 25.16 + 52.5 = 77.66% -> округление 78%
        Assert.Equal(78, recapVm.MonthlyScore);
        Assert.False(recapVm.IsFinalized);
        Assert.True(recapVm.CanFinalize);
    }

    [Fact]
    public async Task AC11_FirstDayOfNextMonth_ActivatesMonthlyRecapOverlay_ForPreviousMonth()
    {
        // Given: 1 апреля 2026, время 09:00 (утро 1-го числа нового месяца)
        var firstDay0900 = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

        // Предыдущий месяц 2026-03 не финализирован
        // When: пользователь открывает Dashboard
        bool activated = await _dashboardViewModel.CheckMonthlyRecapActivationAsync(firstDay0900);

        // Then:
        // Активируется оверлей для предыдущего месяца (МАРТ 2026)
        Assert.True(activated);
        Assert.True(_dashboardViewModel.IsMonthlyRecapOpen);
        Assert.Equal("МАРТ 2026", _dashboardViewModel.MonthlyRecapViewModel.HeaderMonthYear);
        Assert.Equal("2026-03", _dashboardViewModel.MonthlyRecapViewModel.YearMonth);
    }

    [Fact]
    public async Task AC11_MidMonth_DoesNotActivateMonthlyRecapOverlay()
    {
        // Given: 15 марта 2026, 14:00 (середина месяца)
        var midMonth = new DateTime(2026, 3, 15, 14, 0, 0, DateTimeKind.Utc);

        // When: открытие Dashboard
        bool activated = await _dashboardViewModel.CheckMonthlyRecapActivationAsync(midMonth);

        // Then: оверлей не активируется
        Assert.False(activated);
        Assert.False(_dashboardViewModel.IsMonthlyRecapOpen);
    }

    [Fact]
    public async Task AC11_FinalizeMonth_SavesReflection_SetsIsFinalized_AndClosesOverlay()
    {
        // Given: открыт оверлей итогов марта 2026
        const string yearMonth = "2026-03";
        var recapVm = _dashboardViewModel.MonthlyRecapViewModel;
        await recapVm.LoadRecapAsync(yearMonth, isReadOnly: false);
        _dashboardViewModel.IsMonthlyRecapOpen = true;

        // When: пользователь вводит рефлексию и нажимает «Завершить месяц и начать новый цикл»
        const string reflection = "Сфокусироваться на недельных целях и ритуале утром!";
        recapVm.ReflectionText = reflection;

        await recapVm.FinalizeMonthAsync();

        // Then:
        // 1. Статус IsFinalized = true
        Assert.True(recapVm.IsFinalized);
        Assert.False(recapVm.CanFinalize);

        // 2. Оверлей закрывается
        Assert.False(_dashboardViewModel.IsMonthlyRecapOpen);

        // 3. Запись сохранена в таблице MonthlyRecaps в SQLite
        var inDb = await _monthlyRecapRepository.GetRecapAsync(yearMonth);
        Assert.NotNull(inDb);
        Assert.True(inDb.IsFinalized);
        Assert.Equal(reflection, inDb.ReflectionText);

        // 4. Повторная проверка активации больше не открывает оверлей для финализированного месяца
        var nextDay = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        bool reactivated = await _dashboardViewModel.CheckMonthlyRecapActivationAsync(nextDay);
        Assert.False(reactivated);
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
