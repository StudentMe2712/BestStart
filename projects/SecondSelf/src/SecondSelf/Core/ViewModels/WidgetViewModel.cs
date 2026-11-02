using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondSelf.Core.Messages;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Repositories;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel плавающего виджета рабочего стола (Compact / Expanded режимы).
/// Реализует инлайн-ввод задач дня (OrderIndex = 0), панель действий строки задачи,
/// двухфазную анимацию закрытия (350 мс + 350 мс), компонент UndoToast с 5-секундным окном
/// и сохранение инварианта 0% влияния на рейтинг месяца.
/// </summary>
public partial class WidgetViewModel : ObservableObject
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProgressCalculationEngine _progressEngine;
    private readonly IWindowStateService? _windowStateService;
    private readonly IDailyRitualRepository? _dailyRitualRepository;
    private readonly IVideoPlaybackService? _videoPlaybackService;
    private readonly IDayTransitionService? _dayTransitionService;
    private readonly INotificationBus? _notificationBus;
    private readonly IVideoLibraryService? _videoLibraryService;
    private readonly IWeeklyGoalRepository? _weeklyGoalRepository;

    private CancellationTokenSource? _undoCts;
    private readonly object _undoLock = new();

    public event Action<bool>? ExpandedStateChanged;
    public event Action<bool>? PlayerStateChanged;

    public ObservableCollection<TaskItemViewModel> Tasks { get; } = new();
    public ObservableCollection<TaskItemViewModel> CompactTasks { get; } = new();
    public ObservableCollection<VideoLibraryItemViewModel> VideoItems { get; } = new();

    public VideoPlayerViewModel Player { get; }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isPlayerOpen;

    [ObservableProperty]
    private bool _isShowingTasks;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayScorePercent))]
    [NotifyPropertyChangedFor(nameof(CompactProgressText))]
    private int _displayScore = 0;

    [ObservableProperty]
    private int _completedDays = 0;

    [ObservableProperty]
    private int _totalDaysInMonth = 0;

    [ObservableProperty]
    private int _completedWeeks = 0;

    [ObservableProperty]
    private int _totalWeeksInMonth = 0;

    [ObservableProperty]
    private string _dailyRatio = "Ритуалы 0/0";

    [ObservableProperty]
    private string _weeklyRatio = "Недели 0/0";

    [ObservableProperty]
    private string _monthlySummarySubtitle = "Ритуалы: 0/0 · Недели: 0/0";

    [ObservableProperty]
    private double _progressPercentage = 0.0;

    [ObservableProperty]
    private double _progressBarWidth = 0.0;

    [ObservableProperty]
    private string _resetStatus = "Готово к просмотру";

    [ObservableProperty]
    private string _resetSubtitle = "Готово к просмотру";

    [ObservableProperty]
    private string _videoTitle = "Ритуал дня";

    [ObservableProperty]
    private string _videoSubtitle = "Минута для концентрации";

    [ObservableProperty]
    private string _videoSourceTag = "локально";

    [ObservableProperty]
    private bool _isAddingTask;

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private bool _isUndoToastVisible;

    [ObservableProperty]
    private string _undoToastMessage = "Задача закрыта ·";

    public TaskItemViewModel? PendingArchivedTask { get; private set; }
    public int PendingTaskOriginalIndex { get; private set; }

    /// <summary>
    /// Длительность Фазы 1 закрытия задачи (зачёркивание, галочка, прозрачность 0.45).
    /// По умолчанию 350 мс (может быть уменьшена в тестах).
    /// </summary>
    public int AnimationPhase1Ms { get; set; } = 350;

    /// <summary>
    /// Длительность Фазы 2 закрытия задачи (схлопывание высоты 44px -> 0px).
    /// По умолчанию 350 мс (может быть уменьшена в тестах).
    /// </summary>
    public int AnimationPhase2Ms { get; set; } = 350;

    /// <summary>
    /// Окно отмены закрытия задачи в UndoToast.
    /// По умолчанию 5000 мс (может быть уменьшено в тестах).
    /// </summary>
    public int UndoDelayMs { get; set; } = 5000;

    public string DisplayScorePercent => $"{DisplayScore}%";
    public string CompactProgressText => $"Итог месяца: {DisplayScore}%";

    public WidgetViewModel(
        ITaskRepository taskRepository,
        IProgressCalculationEngine progressEngine,
        IWindowStateService? windowStateService = null,
        IDailyRitualRepository? dailyRitualRepository = null,
        IVideoPlaybackService? videoPlaybackService = null,
        IDayTransitionService? dayTransitionService = null,
        INotificationBus? notificationBus = null,
        IVideoLibraryService? videoLibraryService = null,
        IWeeklyGoalRepository? weeklyGoalRepository = null)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _progressEngine = progressEngine ?? throw new ArgumentNullException(nameof(progressEngine));
        _windowStateService = windowStateService;
        _dailyRitualRepository = dailyRitualRepository;
        _videoPlaybackService = videoPlaybackService;
        _dayTransitionService = dayTransitionService;
        _notificationBus = notificationBus;
        _videoLibraryService = videoLibraryService;
        _weeklyGoalRepository = weeklyGoalRepository;

        if (_videoPlaybackService != null)
        {
            Player = new VideoPlayerViewModel(_videoPlaybackService);
            Player.RequestClosePlayer += () =>
            {
                ClosePlayer();
            };
        }
        else
        {
            Player = new VideoPlayerViewModel(new NullVideoPlaybackService());
        }

        if (_notificationBus != null)
        {
            _notificationBus.Subscribe<DailyRitualCountedMessage>(this, async msg =>
            {
                try
                {
                    DateTime? msgTime = null;
                    if (DateOnly.TryParse(msg.LogicalDate, out var date))
                    {
                        msgTime = date.ToDateTime(new TimeOnly(12, 0));
                    }

                    await RefreshMonthlyProgressAsync(msgTime).ConfigureAwait(false);
                    ResetStatus = "Выполнено";
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Handling warning: {ex.Message}");
                }
            });

            _notificationBus.Subscribe<DayChangedMessage>(this, async msg =>
            {
                try
                {
                    DateTime? msgTime = null;
                    if (DateOnly.TryParse(msg.NewLogicalDate, out var date))
                    {
                        msgTime = date.ToDateTime(new TimeOnly(12, 0));
                    }

                    await RefreshMonthlyProgressAsync(msgTime).ConfigureAwait(false);
                    ResetStatus = "Готово к просмотру";
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Handling warning: {ex.Message}");
                }
            });
        }
    }

    partial void OnIsPlayerOpenChanged(bool value)
    {
        PlayerStateChanged?.Invoke(value);
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value)
        {
            IsSettingsOpen = false;
        }
        ExpandedStateChanged?.Invoke(value);

        if (_windowStateService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var state = await _windowStateService.LoadStateAsync().ConfigureAwait(false);
                    var updated = state with { IsExpanded = value };
                    await _windowStateService.SaveStateAsync(updated).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore background state save errors
                }
            });
        }
    }

    partial void OnDisplayScoreChanged(int value)
    {
        ProgressPercentage = Math.Clamp((double)value, 0.0, 100.0);
        ProgressBarWidth = Math.Round(168.0 * (ProgressPercentage / 100.0), 1);
        OnPropertyChanged(nameof(DisplayScorePercent));
        OnPropertyChanged(nameof(CompactProgressText));
    }

    [RelayCommand]
    public async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        // 1. Загрузка задач дня
        var activeTasks = await _taskRepository.GetActiveTasksAsync(cancellationToken).ConfigureAwait(false);

        Tasks.Clear();
        foreach (var task in activeTasks)
        {
            Tasks.Add(new TaskItemViewModel(task.Id, task.Title, isCompleted: false, task.OrderIndex, task.PostponedDays));
        }

        SyncCompactTasks();

        // 2. Расчет актуального прогресса месяца из реальных логов и целей SQLite
        await RefreshMonthlyProgressAsync(cancellationToken).ConfigureAwait(false);

        // 3. Загрузка статуса ежедневного ритуала текущих суток
        if (_dayTransitionService != null && _dailyRitualRepository != null)
        {
            var log = await _dayTransitionService.GetOrAssignDailyRitualAsync(DateTime.Now, cancellationToken).ConfigureAwait(false);
            if (log != null)
            {
                if (log.IsCompleted)
                {
                    ResetStatus = "Выполнено";
                }
                else if (log.LastPositionMs > 0)
                {
                    ResetStatus = "Пауза";
                }
                else
                {
                    ResetStatus = "Готово к просмотру";
                }
            }
        }

        // 4. Загрузка состояния окна (DEFECT 4.1: всегда сброс в Home при запуске)
        IsShowingTasks = false;
        IsPlayerOpen = false;
        IsSettingsOpen = false;
        if (_windowStateService != null)
        {
            var state = await _windowStateService.LoadStateAsync(cancellationToken).ConfigureAwait(false);
            IsExpanded = state.IsExpanded;
        }

        // 5. Загрузка видеотеки
        await LoadVideoLibraryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task RefreshMonthlyProgressAsync(CancellationToken cancellationToken = default) =>
        RefreshMonthlyProgressAsync(null, cancellationToken);

    public async Task RefreshMonthlyProgressAsync(DateTime? currentTime, CancellationToken cancellationToken = default)
    {
        var now = currentTime ?? DateTime.UtcNow;
        int completedDays = 0;
        int completedWeeks = 0;

        var yearMonth = now.ToString("yyyy-MM");

        if (_dailyRitualRepository != null)
        {
            var logs = await _dailyRitualRepository.GetLogsForMonthAsync(yearMonth, cancellationToken).ConfigureAwait(false);
            completedDays = logs.Count(l => l.IsCompleted);
        }

        if (_weeklyGoalRepository != null)
        {
            var goals = await _weeklyGoalRepository.GetGoalsForMonthAsync(yearMonth, cancellationToken).ConfigureAwait(false);
            completedWeeks = goals.Count(g => string.Equals(g.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        }

        var progressResult = _progressEngine.CalculateMonthlyProgress(now.Year, now.Month, completedDays, completedWeeks);
        DisplayScore = progressResult.DisplayScore;
        CompletedDays = progressResult.CompletedDays;
        TotalDaysInMonth = progressResult.TotalDaysInMonth;
        CompletedWeeks = progressResult.CompletedWeeks;
        TotalWeeksInMonth = progressResult.TotalWeeksInMonth;

        DailyRatio = $"Ритуалы {CompletedDays}/{TotalDaysInMonth}";
        WeeklyRatio = $"Недели {CompletedWeeks}/{TotalWeeksInMonth}";
        MonthlySummarySubtitle = $"Ритуалы: {CompletedDays}/{TotalDaysInMonth} · Недели: {CompletedWeeks}/{TotalWeeksInMonth}";
    }

    [RelayCommand]
    public void OpenTasks()
    {
        if (!IsExpanded)
        {
            Expand();
        }
        if (IsPlayerOpen)
        {
            ClosePlayer();
        }
        IsSettingsOpen = false;
        IsShowingTasks = true;
    }

    [RelayCommand]
    public void BackToHome()
    {
        if (IsPlayerOpen)
        {
            ClosePlayer();
        }
        IsShowingTasks = false;
        IsSettingsOpen = false;
    }

    [RelayCommand]
    public async Task OpenPlayerAsync(CancellationToken cancellationToken = default)
    {
        if (!IsExpanded)
        {
            Expand();
        }
        IsShowingTasks = false;
        IsSettingsOpen = false;

        bool initialized = await Player.InitializeAsync(null, cancellationToken).ConfigureAwait(false);
        IsPlayerOpen = true;

        if (initialized)
        {
            await Player.PlayPauseAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public void ClosePlayer()
    {
        _ = Player.ClosePlayerAsync();
        IsPlayerOpen = false;

        if (Player.IsCompleted)
        {
            ResetStatus = "Выполнено";
        }
        else
        {
            ResetStatus = "Пауза";
        }
    }

    [RelayCommand]
    public void OpenSettings()
    {
        if (!IsExpanded)
        {
            Expand();
        }
        if (IsPlayerOpen)
        {
            ClosePlayer();
        }
        IsShowingTasks = false;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    public void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    public async Task LoadVideoLibraryAsync(CancellationToken cancellationToken = default)
    {
        if (_videoLibraryService != null)
        {
            try
            {
                var list = await _videoLibraryService.GetAllVideosAsync(cancellationToken).ConfigureAwait(false);
                VideoItems.Clear();
                foreach (var item in list)
                {
                    VideoItems.Add(new VideoLibraryItemViewModel(item));
                }
            }
            catch
            {
                // Подавляем в headless/тестовой среде
            }
        }
    }

    public async Task AddVideoFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!System.IO.File.Exists(filePath)) return;

        if (_videoLibraryService != null)
        {
            var destinationFolder = _videoLibraryService.VideosDirectory;
            System.IO.Directory.CreateDirectory(destinationFolder);

            var destPath = System.IO.Path.Combine(destinationFolder, System.IO.Path.GetFileName(filePath));
            if (!string.Equals(System.IO.Path.GetFullPath(filePath), System.IO.Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
            {
                System.IO.File.Copy(filePath, destPath, overwrite: true);
            }

            await _videoLibraryService.ScanLibraryAsync(cancellationToken).ConfigureAwait(false);
            await LoadVideoLibraryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task AddVideosFromFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (!System.IO.Directory.Exists(folderPath)) return;

        if (_videoLibraryService != null)
        {
            var destinationFolder = _videoLibraryService.VideosDirectory;
            System.IO.Directory.CreateDirectory(destinationFolder);

            var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".webm", ".mov" };
            var files = System.IO.Directory.GetFiles(folderPath, "*.*", System.IO.SearchOption.TopDirectoryOnly)
                .Where(f => supported.Contains(System.IO.Path.GetExtension(f)));

            foreach (var file in files)
            {
                var destPath = System.IO.Path.Combine(destinationFolder, System.IO.Path.GetFileName(file));
                if (!string.Equals(System.IO.Path.GetFullPath(file), System.IO.Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.File.Copy(file, destPath, overwrite: true);
                }
            }

            await _videoLibraryService.ScanLibraryAsync(cancellationToken).ConfigureAwait(false);
            await LoadVideoLibraryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DeleteVideoAsync(VideoLibraryItemViewModel item, CancellationToken cancellationToken = default)
    {
        if (item == null) return;

        if (_videoLibraryService != null)
        {
            await _videoLibraryService.DeleteVideoAsync(item.Id, cancellationToken).ConfigureAwait(false);
            await LoadVideoLibraryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task AddTaskAsync(CancellationToken cancellationToken = default)
    {
        var title = NewTaskTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            IsAddingTask = false;
            return;
        }

        // Новая задача всегда получает OrderIndex = 0 и сохраняется в ActiveTasks
        var activeTask = new ActiveTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            OrderIndex = 0,
            PostponedDays = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        // Сдвигаем порядок существующих задач
        foreach (var t in Tasks)
        {
            t.OrderIndex++;
        }

        await _taskRepository.AddActiveTaskAsync(activeTask, cancellationToken).ConfigureAwait(false);

        var taskVm = new TaskItemViewModel(activeTask.Id, activeTask.Title, false, 0, 0);
        Tasks.Insert(0, taskVm);
        SyncCompactTasks();

        // Поле ввода очищается и немедленно закрывается
        NewTaskTitle = string.Empty;
        IsAddingTask = false;

        await RefreshMonthlyProgressAsync(cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    public void StartAddingTask()
    {
        if (!IsExpanded)
        {
            Expand();
        }
        IsShowingTasks = true;
        IsAddingTask = true;
    }

    [RelayCommand]
    public void CancelAddingTask()
    {
        IsAddingTask = false;
        NewTaskTitle = string.Empty;
    }

    public void ToggleTaskActionPanel(TaskItemViewModel? item)
    {
        if (item == null) return;
        item.IsActionPanelOpen = !item.IsActionPanelOpen;
    }

    public void StartEditTask(TaskItemViewModel? item)
    {
        if (item == null) return;
        item.StartEdit();
    }

    public async Task SaveEditTaskAsync(TaskItemViewModel? item, CancellationToken cancellationToken = default)
    {
        if (item == null) return;

        var newTitle = item.EditText?.Trim();
        if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != item.Title)
        {
            item.Title = newTitle;
            var updated = new ActiveTask
            {
                Id = item.Id,
                Title = item.Title,
                OrderIndex = item.OrderIndex,
                PostponedDays = item.PostponedDays,
                CreatedAt = DateTime.UtcNow.ToString("O")
            };
            await _taskRepository.UpdateActiveTaskAsync(updated, cancellationToken).ConfigureAwait(false);
            SyncCompactTasks();
        }

        item.IsEditing = false;
    }

    public void CancelEditTask(TaskItemViewModel? item)
    {
        item?.CancelEdit();
    }

    /// <summary>
    /// Двухфазное завершение задачи с отображением UndoToast на 5 секунд.
    /// Фаза 1 (350 мс): зачёркивание, галочка, прозрачность 0.45.
    /// Фаза 2 (350 мс): схлопывание высоты строки (44px -> 0px).
    /// Окончательная архивация в ArchivedTasks происходит по истечении 5 секунд.
    /// Важнейшее продуктовое правило: изменение задач дня дает ровно 0% в Monthly Score.
    /// </summary>
    [RelayCommand]
    public async Task CompleteTaskAsync(TaskItemViewModel? item, CancellationToken cancellationToken = default)
    {
        if (item == null) return;

        // Если до этого уже была отложенная задача в Undo-окне, немедленно архивируем её
        await CommitPendingArchivedTaskAsync(cancellationToken).ConfigureAwait(false);

        int originalIndex = Tasks.IndexOf(item);
        if (originalIndex < 0) originalIndex = 0;

        // Фаза 1: зачёркивание текста, чекбокс, прозрачность строки 0.45
        item.IsCompleting = true;
        item.IsCheckboxChecked = true;
        item.IsStrikethrough = true;
        item.RowOpacity = 0.45;

        if (AnimationPhase1Ms > 0)
        {
            await Task.Delay(AnimationPhase1Ms, cancellationToken).ConfigureAwait(false);
        }

        // Фаза 2: высота строки схлопывается (44px -> 0px)
        item.RowHeight = 0;
        item.RowOpacity = 0.0;

        if (AnimationPhase2Ms > 0)
        {
            await Task.Delay(AnimationPhase2Ms, cancellationToken).ConfigureAwait(false);
        }

        // Удаление из активного UI-списка
        Tasks.Remove(item);
        SyncCompactTasks();

        // Активация плашки UndoToast на 5 секунд
        lock (_undoLock)
        {
            PendingArchivedTask = item;
            PendingTaskOriginalIndex = originalIndex;
            IsUndoToastVisible = true;
            _undoCts?.Cancel();
            _undoCts = new CancellationTokenSource();
        }

        var currentCts = _undoCts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(UndoDelayMs, currentCts.Token).ConfigureAwait(false);
                if (!currentCts.Token.IsCancellationRequested)
                {
                    await CommitPendingArchivedTaskAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Отмена через UndoLastTaskCompletionAsync
            }
        });
    }

    /// <summary>
    /// Отмена завершения задачи: немедленное восстановление задачи в Today's Focus на ту же позицию.
    /// </summary>
    [RelayCommand]
    public void UndoLastTaskCompletion()
    {
        TaskItemViewModel? taskToRestore = null;
        int targetIndex = 0;

        lock (_undoLock)
        {
            if (PendingArchivedTask == null) return;

            _undoCts?.Cancel();
            IsUndoToastVisible = false;

            taskToRestore = PendingArchivedTask;
            targetIndex = PendingTaskOriginalIndex;
            PendingArchivedTask = null;
        }

        if (taskToRestore != null)
        {
            taskToRestore.ResetAnimationState();
            int insertIndex = Math.Clamp(targetIndex, 0, Tasks.Count);
            Tasks.Insert(insertIndex, taskToRestore);
            SyncCompactTasks();
            _ = RefreshMonthlyProgressAsync();
        }
    }

    /// <summary>
    /// Окончательная фиксация закрытия задачи в БД SQLite (ArchivedTasks).
    /// </summary>
    public async Task CommitPendingArchivedTaskAsync(CancellationToken cancellationToken = default)
    {
        TaskItemViewModel? taskToCommit = null;

        lock (_undoLock)
        {
            if (PendingArchivedTask == null) return;

            taskToCommit = PendingArchivedTask;
            PendingArchivedTask = null;
            IsUndoToastVisible = false;
        }

        if (taskToCommit != null)
        {
            var logicalDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            await _taskRepository.ArchiveTaskAsync(taskToCommit.Id, logicalDate, cancellationToken).ConfigureAwait(false);
            await RefreshMonthlyProgressAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task ToggleTaskAsync(TaskItemViewModel? item, CancellationToken cancellationToken = default)
    {
        await CompleteTaskAsync(item, cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task DeleteTaskAsync(TaskItemViewModel? item, CancellationToken cancellationToken = default)
    {
        if (item == null) return;

        await _taskRepository.DeleteActiveTaskAsync(item.Id, cancellationToken).ConfigureAwait(false);

        Tasks.Remove(item);
        SyncCompactTasks();
        await RefreshMonthlyProgressAsync(cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    public void Expand()
    {
        if (!IsExpanded)
        {
            // DEFECT 4.1: При любом вызове/разворачивании виджета активным ВСЕГДА открывается Главный экран Home
            IsShowingTasks = false;
            if (IsPlayerOpen)
            {
                ClosePlayer();
            }
            IsExpanded = true;
        }
    }

    [RelayCommand]
    public void Collapse()
    {
        if (IsPlayerOpen)
        {
            ClosePlayer();
        }
        IsShowingTasks = false;
        IsAddingTask = false;
        if (IsExpanded)
        {
            IsExpanded = false;
        }
    }

    private void SyncCompactTasks()
    {
        CompactTasks.Clear();
        foreach (var task in Tasks.Take(3))
        {
            CompactTasks.Add(task);
        }
    }
}
