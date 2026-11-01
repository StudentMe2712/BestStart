using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
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
/// Автоматизированные верификационные тесты для СПРИНТ 2.0:
/// ДЕФЕКТ 1. Warm Glassmorphism & геометрия окна.
/// ДЕФЕКТ 2. Top Drop-Down Panel & глобальный хоткей Alt+Shift+S.
/// ДЕФЕКТ 3. Мгновенный Drag & Drop без перехватывающего задержки клика.
/// ДЕФЕКТ 4. Навигация без тупиков (Home Screen First, кнопка Назад, CollapseWidgetMessage).
/// ДЕФЕКТ 5. Стабилизация плеера DailyReset (Zero Crashes, замена на "Файл заменен").
/// </summary>
public sealed class Sprint2DefectVerificationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testVideosDir;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DailyRitualRepository _dailyRitualRepository;
    private readonly VideoLibraryService _videoLibraryService;
    private readonly FisherYatesShuffleService _shuffleService;
    private readonly NotificationBus _notificationBus;
    private readonly DayTransitionService _dayTransitionService;

    public Sprint2DefectVerificationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_sprint2_{Guid.NewGuid():N}.db");
        _testVideosDir = Path.Combine(Path.GetTempPath(), $"secondself_sprint2_vids_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testVideosDir);

        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _dailyRitualRepository = new DailyRitualRepository(_connectionFactory);
        _videoLibraryService = new VideoLibraryService(_connectionFactory, _testVideosDir);
        _shuffleService = new FisherYatesShuffleService(_connectionFactory, _videoLibraryService);
        _notificationBus = new NotificationBus();

        _dayTransitionService = new DayTransitionService(
            _dailyRitualRepository,
            _videoLibraryService,
            _shuffleService,
            _notificationBus,
            autoStartTimer: false);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        try { if (Directory.Exists(_testVideosDir)) Directory.Delete(_testVideosDir, true); } catch { }
    }

    private sealed class StubTaskRepository : ITaskRepository
    {
        public Task<IReadOnlyList<ActiveTask>> GetActiveTasksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActiveTask>>(Array.Empty<ActiveTask>());
        public Task AddActiveTaskAsync(ActiveTask task, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateActiveTaskAsync(ActiveTask task, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteActiveTaskAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ArchivedTask> ArchiveTaskAsync(string taskId, string logicalDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArchivedTask { Id = taskId, Title = "Test" });
        public Task<ActiveTask> RestoreArchivedTaskAsync(string taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ActiveTask { Id = taskId, Title = "Test" });
        public Task<IReadOnlyList<ArchivedTask>> GetArchivedTasksAsync(string? logicalDate = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArchivedTask>>(Array.Empty<ArchivedTask>());
        public Task ReorderTasksAsync(IEnumerable<(string Id, int NewOrder)> orders, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void Defect1_And_2_WindowStateDefault_NormalizedY_IsAnchoredToTopEdge()
    {
        // DEFECT 1 & 2: В IWindowStateService.cs установлено значение по умолчанию NormalizedY = 0.0
        var defaultState = new WindowState();

        Assert.Equal(0.0, defaultState.NormalizedY);
        Assert.Equal(0.5, defaultState.NormalizedX);
        Assert.True(defaultState.IsExpanded);
    }

    [Fact]
    public void Defect2_GlobalHotkey_AltShiftS_TriggersToggleWidgetRequested()
    {
        // DEFECT 2: Регистрация глобального хоткея Alt+Shift+S (HOTKEY_TOGGLE_WIDGET_ID)
        using var trayService = new SystemTrayService();
        bool toggleRequested = false;
        trayService.ToggleWidgetRequested += () => toggleRequested = true;

        trayService.TriggerHotkey(SystemTrayService.HOTKEY_TOGGLE_WIDGET_ID);

        Assert.True(toggleRequested);
    }

    [Fact]
    public void Defect4_HomeScreenFirst_ExpandAlwaysOpensHome_NeverPreservesTasksOrPlayer()
    {
        // DEFECT 4.1: При любом вызове/разворачивании виджета активным ВСЕГДА открывается Главный экран Home
        var vm = new WidgetViewModel(new StubTaskRepository(), new ProgressCalculationEngine());

        // Пользователь открыл задачи
        vm.OpenTasks();
        Assert.True(vm.IsExpanded);
        Assert.True(vm.IsShowingTasks);

        // Пользователь свернул виджет
        vm.Collapse();
        Assert.False(vm.IsExpanded);
        Assert.False(vm.IsShowingTasks);

        // При повторном открытии ВСЕГДА открывается Home (3 карточки)
        vm.Expand();
        Assert.True(vm.IsExpanded);
        Assert.False(vm.IsShowingTasks);
        Assert.False(vm.IsPlayerOpen);
    }

    [Fact]
    public void Defect4_ZeroTrapNavigation_BackToHome_ExitsTasksAndPlayerInOneClick()
    {
        // DEFECT 4.2: Кнопка «← Назад / Свернуть» возвращает на экран 3 карточек в 1 клик
        var vm = new WidgetViewModel(new StubTaskRepository(), new ProgressCalculationEngine());

        vm.OpenTasks();
        Assert.True(vm.IsShowingTasks);

        vm.BackToHome();
        Assert.False(vm.IsShowingTasks);
        Assert.False(vm.IsPlayerOpen);
    }

    [Fact]
    public void Defect4_CollapseWidgetMessage_TriggersSmoothCollapse()
    {
        // DEFECT 4.3: CollapseWidgetMessage при открытии Personal Space
        var vm = new WidgetViewModel(new StubTaskRepository(), new ProgressCalculationEngine());
        vm.Expand();
        Assert.True(vm.IsExpanded);

        // Регистрация получателя в WidgetWindow
        WeakReferenceMessenger.Default.Register<CollapseWidgetMessage>(this, (r, m) =>
        {
            vm.Collapse();
        });

        // DashboardWindow при открытии посылает CollapseWidgetMessage
        WeakReferenceMessenger.Default.Send(new CollapseWidgetMessage());

        Assert.False(vm.IsExpanded);
        Assert.False(vm.IsShowingTasks);

        WeakReferenceMessenger.Default.Unregister<CollapseWidgetMessage>(this);
    }

    [Fact]
    public async Task Defect5_PlayerStabilization_MediaFailed_AutoReplacesVideo_SetsFileReplacedStatus()
    {
        // DEFECT 5: Автозамена битого файла следующим из CycleQueue через ReplaceCorruptedVideoAsync
        // и установка статуса "Файл заменен" без падения приложения.
        string vid1 = Path.Combine(_testVideosDir, "vid1.mp4");
        string vid2 = Path.Combine(_testVideosDir, "vid2.mp4");
        await File.WriteAllTextAsync(vid1, "broken");
        await File.WriteAllTextAsync(vid2, "valid");

        await _videoLibraryService.ScanLibraryAsync();

        var log = await _dayTransitionService.GetOrAssignDailyRitualAsync(DateTime.Now);
        Assert.NotNull(log);

        var playbackService = new VideoPlaybackService(
            _dailyRitualRepository,
            _dayTransitionService,
            _videoLibraryService,
            new ProgressCalculationEngine(),
            _notificationBus,
            enableTimer: false);

        var playerVm = new VideoPlayerViewModel(playbackService);
        await playerVm.InitializeAsync();

        // Имитируем сбой декодирования MediaFailed
        bool replaced = await playerVm.OnMediaFailedAsync();

        // Проверяем: ролик успешно заменен из очереди без падений
        Assert.True(replaced);
        Assert.Equal("Файл заменен", playerVm.StatusText);
        Assert.Equal("Файл заменен", playbackService.StatusText);
        Assert.False(playerVm.IsPlaceholderVisible);
    }
}
