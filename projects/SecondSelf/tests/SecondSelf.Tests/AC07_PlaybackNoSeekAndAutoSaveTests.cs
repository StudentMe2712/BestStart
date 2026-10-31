using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.Messages;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-07: Воспроизведение без перемотки и автосохранение позиции.
/// Given: пользователь нажал Play на карточке Daily Reset.
/// When: открылся встроенный плеер и видео длительностью 02:00 воспроизводится.
/// Then:
///   1. Полоса прогресса воспроизведения имеет свойство IsHitTestVisible = false (перемотка заблокирована).
///   2. Автосохранение позиции каждые 2 секунды (2000 мс) в таблицу DailyRitualLogs.
///   3. При нажатии на паузу или закрытии плеера на 00:45 (45000 мс) позиция сохраняется в SQLite.
///   4. При повторном открытии плеера видео стартует с 00:44 (44000 мс, с 1 секундой контекста).
/// </summary>
public sealed class AC07_PlaybackNoSeekAndAutoSaveTests : IDisposable
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

    public AC07_PlaybackNoSeekAndAutoSaveTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac07_{Guid.NewGuid():N}.db");
        _testVideosDir = Path.Combine(Path.GetTempPath(), $"secondself_ac07_vids_{Guid.NewGuid():N}");
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
    public async Task AC07_AutoSave_Every2Seconds_PersistsPositionToSqlite()
    {
        // Given: ролик длительностью 120 секунд в библиотеке
        var videoPath = Path.Combine(_testVideosDir, "reset_120s.mp4");
        await File.WriteAllTextAsync(videoPath, "video-bytes");
        await _videoLibraryService.ScanLibraryAsync();

        var playbackService = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            _progressEngine,
            _notificationBus,
            enableTimer: true)
        {
            // Устанавливаем интервал автосохранения 50 мс для ускорения теста
            AutoSaveIntervalMs = 50
        };

        var now = new DateTime(2026, 11, 1, 10, 0, 0);
        bool initialized = await playbackService.InitializePlaybackAsync(now);
        Assert.True(initialized);

        // When: видео запускается и продвигается до позиции 18000 мс (00:18)
        await playbackService.PlayAsync();
        playbackService.UpdatePosition(18000, 120000);

        // Ожидаем срабатывания периодического таймера автосохранения
        await Task.Delay(120);

        // Then: позиция 18000 мс записана в DailyRitualLogs в SQLite
        var log = await _dailyRitualRepository.GetDailyRitualLogAsync("2026-11-01");
        Assert.NotNull(log);
        Assert.Equal(18000, log.LastPositionMs);

        playbackService.Dispose();
    }

    [Fact]
    public async Task AC07_PauseAndStop_ImmediatelySavesPosition()
    {
        // Given: назначенное видео на текущие сутки
        var videoPath = Path.Combine(_testVideosDir, "focus_video.mp4");
        await File.WriteAllTextAsync(videoPath, "video-bytes");
        await _videoLibraryService.ScanLibraryAsync();

        var playbackService = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            _progressEngine,
            _notificationBus,
            enableTimer: false); // Таймер отключен, чтобы проверить явное сохранение при паузе

        var now = new DateTime(2026, 11, 1, 12, 0, 0);
        await playbackService.InitializePlaybackAsync(now);
        await playbackService.PlayAsync();

        // When: воспроизведение доходит до 00:45 (45000 мс) и нажимается Pause
        playbackService.UpdatePosition(45000, 120000);
        await playbackService.PauseAsync();

        // Then: в БД немедленно сохранена позиция 45000 мс
        var log = await _dailyRitualRepository.GetDailyRitualLogAsync("2026-11-01");
        Assert.NotNull(log);
        Assert.Equal(45000, log.LastPositionMs);
        Assert.Equal(PlaybackState.Paused, playbackService.State);
        Assert.Equal("Paused", playbackService.StatusText);

        playbackService.Dispose();
    }

    [Fact]
    public async Task AC07_Resume_StartsFrom_LastPositionMinusOneSecondContext()
    {
        // Given: сохраненная позиция 45000 мс (00:45)
        var videoPath = Path.Combine(_testVideosDir, "resume_video.mp4");
        await File.WriteAllTextAsync(videoPath, "video-bytes");
        await _videoLibraryService.ScanLibraryAsync();

        var logDate = "2026-11-01";
        var log = await _dayTransitionService.GetOrAssignDailyRitualAsync(new DateTime(2026, 11, 1, 10, 0, 0));
        Assert.NotNull(log);

        await _dailyRitualRepository.UpdateLastPositionAsync(logDate, 45000);

        // When: плеер открывается повторно в те же сутки
        var playbackService = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            _progressEngine,
            _notificationBus,
            enableTimer: false);

        await playbackService.InitializePlaybackAsync(new DateTime(2026, 11, 1, 15, 0, 0));

        // Then: видео стартует с позиции 44000 мс (45000 - 1000 мс контекста)
        Assert.Equal(44000, playbackService.CurrentPositionMs);
        Assert.Equal("00:44 / 02:00", playbackService.FormattedTimecode);

        playbackService.Dispose();
    }

    [Fact]
    public async Task AC07_Resume_WhenPositionLessThanOneSecond_StartsFromZero()
    {
        // Given: сохраненная позиция 800 мс (меньше 1 секунды)
        var videoPath = Path.Combine(_testVideosDir, "short_pos.mp4");
        await File.WriteAllTextAsync(videoPath, "video-bytes");
        await _videoLibraryService.ScanLibraryAsync();

        var logDate = "2026-11-01";
        var log = await _dayTransitionService.GetOrAssignDailyRitualAsync(new DateTime(2026, 11, 1, 10, 0, 0));
        Assert.NotNull(log);

        await _dailyRitualRepository.UpdateLastPositionAsync(logDate, 800);

        var playbackService = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            _progressEngine,
            _notificationBus,
            enableTimer: false);

        await playbackService.InitializePlaybackAsync(new DateTime(2026, 11, 1, 15, 0, 0));

        // Then: Math.Max(0, 800 - 1000) = 0 мс
        Assert.Equal(0, playbackService.CurrentPositionMs);
        Assert.Equal("00:00 / 02:00", playbackService.FormattedTimecode);

        playbackService.Dispose();
    }

    [Fact]
    public void AC07_ProgressBar_NonSeekable_IsHitTestVisibleFalse_Verified()
    {
        // Given: разметка окна WidgetWindow.xaml
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "SecondSelf.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        var widgetWindowXamlPath = Path.Combine(dir!, "src", "SecondSelf", "UI", "Views", "WidgetWindow.xaml");
        Assert.True(File.Exists(widgetWindowXamlPath), $"Файл {widgetWindowXamlPath} должен существовать.");

        var xamlContent = File.ReadAllText(widgetWindowXamlPath);

        // Then: полоса прогресса воспроизведения и родительский контейнер имеют IsHitTestVisible = false
        Assert.Contains("IsHitTestVisible=\"False\"", xamlContent);
        Assert.Contains("Width=\"{x:Bind ViewModel.Player.ProgressBarWidth, Mode=OneWay}\"", xamlContent);
    }
}
