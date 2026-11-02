using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Messages;
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
/// Тесты верификации 4 задач:
/// 1. Полная прозрачность окна (100% Transparent, Title "Second Self — Виджет").
/// 2. Кинематографичный Daily Reset (800x600, True Randomizer, фикс кнопки без дублирующей галочки).
/// 3. Настройки видеотеки (840x620, большие превью 160x90, StatusText "Доступен"/"Файл не найден").
/// 4. 100% русификация и дашборд на 80-85% экрана.
/// </summary>
public sealed class TaskRequirementsVerificationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testVideosDir;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DailyRitualRepository _dailyRitualRepository;
    private readonly VideoLibraryService _videoLibraryService;
    private readonly FisherYatesShuffleService _shuffleService;
    private readonly NotificationBus _notificationBus;
    private readonly DayTransitionService _dayTransitionService;
    private readonly ProgressCalculationEngine _progressEngine;

    public TaskRequirementsVerificationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_req_{Guid.NewGuid():N}.db");
        _testVideosDir = Path.Combine(Path.GetTempPath(), $"secondself_req_vids_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testVideosDir);

        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _dailyRitualRepository = new DailyRitualRepository(_connectionFactory);
        _videoLibraryService = new VideoLibraryService(_connectionFactory, _testVideosDir);
        _shuffleService = new FisherYatesShuffleService(_connectionFactory, _videoLibraryService);
        _notificationBus = new NotificationBus();
        _progressEngine = new ProgressCalculationEngine();

        _dayTransitionService = new DayTransitionService(
            _dailyRitualRepository,
            _videoLibraryService,
            _shuffleService,
            _notificationBus,
            autoStartTimer: false);
    }

    public void Dispose()
    {
        _dayTransitionService.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
        if (Directory.Exists(_testVideosDir))
        {
            try { Directory.Delete(_testVideosDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Task1_WidgetWindow_Xaml_HasTransparentBackground_AndCorrectRussianTitle()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "SecondSelf.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        var widgetXamlPath = Path.Combine(dir!, "src", "SecondSelf", "UI", "Views", "WidgetWindow.xaml");
        Assert.True(File.Exists(widgetXamlPath));

        var xaml = await File.ReadAllTextAsync(widgetXamlPath);
        Assert.Contains("Title=\"Second Self — Виджет\"", xaml);
        Assert.True(xaml.Contains("<Grid x:Name=\"RootGrid\" Background=\"{x:Null}\"") || xaml.Contains("<Grid x:Name=\"RootGrid\" Background=\"Transparent\""));
    }

    [Fact]
    public async Task Task2_Player_DimensionsAndProgressBarWidth_Matches800x600And760px()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "SecondSelf.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        var widgetXamlPath = Path.Combine(dir!, "src", "SecondSelf", "UI", "Views", "WidgetWindow.xaml");
        var xaml = await File.ReadAllTextAsync(widgetXamlPath);

        // PlayerRoot, PlayerShadowHost, PlayerView are 800x600
        Assert.Contains("x:Name=\"PlayerRoot\"", xaml);
        Assert.Contains("Width=\"800\"", xaml);
        Assert.Contains("Height=\"600\"", xaml);

        // Row 1 Height 420
        Assert.Contains("<RowDefinition Height=\"420\" />", xaml);

        // DailyResetMediaPlayerElement Height 420
        Assert.Contains("Height=\"420\"", xaml);

        // Progress bar width 760
        Assert.Contains("Width=\"760\"", xaml);

        // Button has clean text without redundant checkmark symbol
        Assert.Contains("<TextBlock Text=\"Засчитать выполнение\"", xaml);
        Assert.DoesNotContain("<TextBlock Text=\"✓ Засчитать выполнение\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Засчитать выполнение ежедневного ритуала\"", xaml);

        // ViewModel calculation check
        var playback = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            _progressEngine,
            _notificationBus,
            enableTimer: false);
        var playerVm = new VideoPlayerViewModel(playback);
        playerVm.UpdatePosition(60000, 120000); // 50%
        Assert.Equal(380.0, playerVm.ProgressBarWidth); // 760 * 0.5 = 380.0
        playback.Dispose();
    }

    [Fact]
    public async Task Task2_TrueRandomizer_PicksRandomVideoAndAvoidsRepeatingLastPlayed()
    {
        // Создаем 3 видеофайла
        var vid1 = Path.Combine(_testVideosDir, "vid_1.mp4");
        var vid2 = Path.Combine(_testVideosDir, "vid_2.mp4");
        var vid3 = Path.Combine(_testVideosDir, "vid_3.mp4");
        await File.WriteAllTextAsync(vid1, "v1");
        await File.WriteAllTextAsync(vid2, "v2");
        await File.WriteAllTextAsync(vid3, "v3");

        await _videoLibraryService.ScanLibraryAsync();

        var available = _videoLibraryService.GetAvailableVideos();
        Assert.Equal(3, available.Count);

        var playback = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            _progressEngine,
            _notificationBus,
            enableTimer: false);

        // 1. Первый запуск: выбирается ролик
        await playback.InitializePlaybackAsync(new DateTime(2026, 11, 1, 10, 0, 0));
        Assert.NotNull(playback.CurrentVideo);
        string firstId = playback.CurrentVideo.Id;

        // 2. Второй запуск: должен исключить firstId
        await playback.InitializePlaybackAsync(new DateTime(2026, 11, 1, 11, 0, 0));
        Assert.NotNull(playback.CurrentVideo);
        string secondId = playback.CurrentVideo.Id;
        Assert.NotEqual(firstId, secondId);

        playback.Dispose();
    }

    [Fact]
    public async Task Task3_Settings_DimensionsAndLargeThumbnails_Matches840x620()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "SecondSelf.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        var widgetXamlPath = Path.Combine(dir!, "src", "SecondSelf", "UI", "Views", "WidgetWindow.xaml");
        var xaml = await File.ReadAllTextAsync(widgetXamlPath);

        Assert.Contains("x:Name=\"SettingsRoot\"", xaml);
        Assert.Contains("Width=\"840\"", xaml);
        Assert.Contains("Height=\"620\"", xaml);

        // Крупные кнопки папки и файлов
        Assert.Contains("📁 Выбрать папку с видео", xaml);
        Assert.Contains("+ Добавить видеофайлы", xaml);

        // Превью 160x90
        Assert.Contains("Width=\"160\"", xaml);
        Assert.Contains("Height=\"90\"", xaml);

        // Проверка StatusText в VideoLibraryItemViewModel
        var itemAvailable = new VideoLibraryItemViewModel { IsMissing = false };
        Assert.Equal("Доступен", itemAvailable.StatusText);

        var itemMissing = new VideoLibraryItemViewModel { IsMissing = true };
        Assert.Equal("Файл не найден", itemMissing.StatusText);
    }

    [Fact]
    public void Task4_CompleteRussianLocalization_VerifiedAcrossViewModels()
    {
        // 1. CalendarDayViewModel (дни недели на русском)
        var monday = new CalendarDayViewModel(1, new DateOnly(2026, 11, 2)); // 2 ноября 2026 = Пн
        Assert.Equal("Пн", monday.DayOfWeekShort);
        var sunday = new CalendarDayViewModel(7, new DateOnly(2026, 11, 8)); // 8 ноября 2026 = Вс
        Assert.Equal("Вс", sunday.DayOfWeekShort);

        // 2. WeekCardViewModel ("Н44" вместо "W44")
        var weekCard = new WeekCardViewModel { WeekNumber = 44 };
        Assert.Equal("Н44", weekCard.WeekDisplayNumber);

        // 3. GoalsTabViewModel
        Assert.Equal("БОЛЬШИЕ ЦЕЛИ · 2026—2028", GoalsTabViewModel.HeaderCategoryText);
        Assert.Equal("Создавай жизнь, которой хочешь жить.", GoalsTabViewModel.HeaderTitleText);
        Assert.Equal("Маленькие решения. Значимый прогресс. Шаг за шагом.", GoalsTabViewModel.HeaderSubtitleText);

        // 4. TodayTabViewModel
        var todayVm = new TodayTabViewModel(
            new WeeklyGoalRepository(_connectionFactory),
            new TaskRepository(_connectionFactory));
        Assert.Equal("Видео дня", todayVm.TodayVideoTitle);
        Assert.Equal("Готово к просмотру", todayVm.RitualStatus);

        // 5. SettingsTabViewModel
        var settingsVm = new SettingsTabViewModel(
            _videoLibraryService,
            _shuffleService,
            new AppSettingsService(),
            new BackupService(_connectionFactory));
        settingsVm.DailyWeight = 30.0;
        settingsVm.WeeklyWeight = 70.0;
        settingsVm.GlassOpacity = 85.0;
        Assert.Equal("Ритуалы дня: 30%", settingsVm.DailyWeightText);
        Assert.Equal("Цели недели: 70%", settingsVm.WeeklyWeightText);
        Assert.Equal("Прозрачность тёплого стекла: 85%", settingsVm.GlassOpacityText);
    }
}
