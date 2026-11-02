using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
/// ViewModel первого раздела Dashboard — Today (Сегодняшний фокус).
/// Отображает активную цель недели (Weekly Goal) с тегом этапа,
/// расширенный список задач дня Today's Focus с инлайн-добавлением, сортировкой и Undo,
/// виджет Daily Reset с видео дня и мини-сводку прогресса.
/// </summary>
public partial class TodayTabViewModel : ObservableObject
{
    private readonly IWeeklyGoalRepository _weeklyGoalRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IBigGoalRepository? _bigGoalRepository;
    private readonly IDailyRitualRepository? _dailyRitualRepository;
    private readonly IProgressCalculationEngine? _progressEngine;
    private readonly IVideoPlaybackService? _videoPlaybackService;
    private readonly INotificationBus? _notificationBus;
    private readonly IDayTransitionService? _dayTransitionService;

    private CancellationTokenSource? _undoCts;
    private readonly object _undoLock = new();

    public event Action? PlayDailyVideoRequested;

    // --- Weekly Goal ---
    [ObservableProperty]
    private string? _weeklyGoalText;

    [ObservableProperty]
    private string? _weeklyGoalTag;

    [ObservableProperty]
    private bool _isWeeklyGoalLocked;

    [ObservableProperty]
    private bool _hasWeeklyGoal;

    [ObservableProperty]
    private string? _weeklyGoalId;

    // --- Tasks ---
    public ObservableCollection<TaskItemViewModel> Tasks { get; } = new();

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

    public int UndoWindowMs { get; set; } = 5000;
    public int AnimationPhase1Ms { get; set; } = 350;
    public int AnimationPhase2Ms { get; set; } = 350;

    // --- Daily Reset Widget ---
    [ObservableProperty]
    private string _todayVideoTitle = "Видео дня";

    [ObservableProperty]
    private string _todayVideoDuration = "02:00";

    [ObservableProperty]
    private string? _todayVideoPreviewPath;

    [ObservableProperty]
    private bool _isRitualCompleted;

    [ObservableProperty]
    private string _ritualStatus = "Готово к просмотру";

    // --- Mini-summary ---
    [ObservableProperty]
    private int _completedTodayCount;

    [ObservableProperty]
    private int _currentMonthScore;

    [ObservableProperty]
    private string _currentMonthDailyRatio = "0/0";

    [ObservableProperty]
    private string _currentMonthWeeklyRatio = "0/0";

    public TodayTabViewModel(
        IWeeklyGoalRepository weeklyGoalRepository,
        ITaskRepository taskRepository,
        IBigGoalRepository? bigGoalRepository = null,
        IDailyRitualRepository? dailyRitualRepository = null,
        IProgressCalculationEngine? progressEngine = null,
        IVideoPlaybackService? videoPlaybackService = null,
        INotificationBus? notificationBus = null,
        IDayTransitionService? dayTransitionService = null)
    {
        _weeklyGoalRepository = weeklyGoalRepository ?? throw new ArgumentNullException(nameof(weeklyGoalRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _bigGoalRepository = bigGoalRepository;
        _dailyRitualRepository = dailyRitualRepository;
        _progressEngine = progressEngine;
        _videoPlaybackService = videoPlaybackService;
        _notificationBus = notificationBus;
        _dayTransitionService = dayTransitionService;

        _notificationBus?.Subscribe<DailyRitualCountedMessage>(this, async _ => await RefreshDailyRitualStatusAsync());
        _notificationBus?.Subscribe<TaskCompletedMessage>(this, async _ => await RefreshSummaryAsync());
    }

    [RelayCommand]
    public async Task LoadDataAsync(DateTime? referenceDate = null)
    {
        var date = referenceDate ?? DateTime.UtcNow;
        var dateOnly = DateOnly.FromDateTime(date);
        int isoYear = ISOWeek.GetYear(date);
        int isoWeek = ISOWeek.GetWeekOfYear(date);

        // 1. Загрузка Weekly Goal
        var weeklyGoal = await _weeklyGoalRepository.GetGoalForWeekAsync(isoYear, isoWeek);
        if (weeklyGoal != null && !string.IsNullOrWhiteSpace(weeklyGoal.Title))
        {
            WeeklyGoalId = weeklyGoal.Id;
            WeeklyGoalText = weeklyGoal.Title;
            HasWeeklyGoal = true;
            IsWeeklyGoalLocked = string.Equals(weeklyGoal.Status, "Locked", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(weeklyGoal.MilestoneId) && _bigGoalRepository != null)
            {
                WeeklyGoalTag = await FormatMilestoneTagAsync(weeklyGoal.MilestoneId);
            }
            else
            {
                WeeklyGoalTag = null;
            }
        }
        else
        {
            WeeklyGoalId = null;
            WeeklyGoalText = null;
            HasWeeklyGoal = false;
            WeeklyGoalTag = null;
            IsWeeklyGoalLocked = false;
        }

        // 2. Загрузка Today's Focus
        var activeTasks = await _taskRepository.GetActiveTasksAsync();
        Tasks.Clear();
        foreach (var task in activeTasks.OrderBy(t => t.OrderIndex))
        {
            Tasks.Add(new TaskItemViewModel(task.Id, task.Title, isCompleted: false, orderIndex: task.OrderIndex, postponedDays: task.PostponedDays));
        }

        // 3. Загрузка Daily Reset
        await RefreshDailyRitualStatusAsync(dateOnly);

        // 4. Загрузка Mini-summary
        await RefreshSummaryAsync(dateOnly);
    }

    private async Task<string?> FormatMilestoneTagAsync(string milestoneId)
    {
        if (_bigGoalRepository == null) return null;
        var activeGoals = await _bigGoalRepository.GetActiveGoalsAsync();
        foreach (var goal in activeGoals)
        {
            var ms = goal.Milestones?.FirstOrDefault(m => string.Equals(m.Id, milestoneId, StringComparison.OrdinalIgnoreCase));
            if (ms != null)
            {
                return $"[{goal.Title} · {ms.Title}]";
            }
        }
        return null;
    }

    [RelayCommand]
    public void StartAddingTask()
    {
        IsAddingTask = true;
        NewTaskTitle = string.Empty;
    }

    [RelayCommand]
    public void CancelAddingTask()
    {
        IsAddingTask = false;
        NewTaskTitle = string.Empty;
    }

    [RelayCommand]
    public async Task AddTaskAsync()
    {
        var title = NewTaskTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            CancelAddingTask();
            return;
        }

        // Инлайн-ввод задачи дня: создается с OrderIndex = 0 и сдвигает остальные
        var newTask = new ActiveTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            CreatedAt = DateTime.UtcNow.ToString("O"),
            OrderIndex = 0,
            PostponedDays = 0
        };

        await _taskRepository.AddActiveTaskAsync(newTask);

        var taskVm = new TaskItemViewModel(newTask.Id, newTask.Title, isCompleted: false, orderIndex: 0, postponedDays: 0);
        Tasks.Insert(0, taskVm);

        NewTaskTitle = string.Empty;
        IsAddingTask = false;
    }

    [RelayCommand]
    public async Task CompleteTaskAsync(TaskItemViewModel task)
    {
        ArgumentNullException.ThrowIfNull(task);

        lock (_undoLock)
        {
            if (PendingArchivedTask != null && _undoCts != null)
            {
                _undoCts.Cancel();
                _undoCts.Dispose();
                _undoCts = null;
                _ = CommitArchivedTaskImmediatelyAsync(PendingArchivedTask);
            }
        }

        PendingArchivedTask = task;
        PendingTaskOriginalIndex = Tasks.IndexOf(task);

        // Фаза 1: зачёркивание, галочка, прозрачность 0.45 (350 мс)
        task.IsCompleting = true;
        task.IsCheckboxChecked = true;
        task.IsStrikethrough = true;
        task.RowOpacity = 0.45;

        if (AnimationPhase1Ms > 0)
        {
            await Task.Delay(AnimationPhase1Ms);
        }

        // Фаза 2: высота схлопывается до 0 (350 мс)
        task.RowHeight = 0;
        if (AnimationPhase2Ms > 0)
        {
            await Task.Delay(AnimationPhase2Ms);
        }

        Tasks.Remove(task);

        // Показ плашки Undo на 5 секунд
        IsUndoToastVisible = true;
        UndoToastMessage = $"Задача закрыта · {task.Title}";

        var cts = new CancellationTokenSource();
        lock (_undoLock)
        {
            _undoCts = cts;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(UndoWindowMs, cts.Token);
                await CommitArchivedTaskImmediatelyAsync(task);
            }
            catch (OperationCanceledException)
            {
                // Отмена через Undo
            }
        });
    }

    [RelayCommand]
    public void UndoCompleteTask()
    {
        lock (_undoLock)
        {
            if (_undoCts == null || PendingArchivedTask == null)
            {
                return;
            }

            _undoCts.Cancel();
            _undoCts.Dispose();
            _undoCts = null;
        }

        var task = PendingArchivedTask;
        task.ResetAnimationState();

        int targetIndex = Math.Clamp(PendingTaskOriginalIndex, 0, Tasks.Count);
        Tasks.Insert(targetIndex, task);

        PendingArchivedTask = null;
        IsUndoToastVisible = false;
    }

    private async Task CommitArchivedTaskImmediatelyAsync(TaskItemViewModel task)
    {
        try
        {
            var logicalDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            await _taskRepository.ArchiveTaskAsync(task.Id, logicalDate);
            _notificationBus?.Publish(new TaskCompletedMessage(task.Id, task.Title, DateTime.UtcNow.ToString("O"), logicalDate, "Today"));
        }
        catch
        {
            // Логирование ошибки архивации
        }
        finally
        {
            lock (_undoLock)
            {
                if (PendingArchivedTask == task)
                {
                    PendingArchivedTask = null;
                    IsUndoToastVisible = false;
                }
            }
        }
    }

    [RelayCommand]
    public async Task DeleteTaskAsync(TaskItemViewModel task)
    {
        ArgumentNullException.ThrowIfNull(task);
        Tasks.Remove(task);
        await _taskRepository.DeleteActiveTaskAsync(task.Id);
    }

    public async Task ReorderTasksAsync(IEnumerable<(string Id, int NewOrder)> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        await _taskRepository.ReorderTasksAsync(orders);
        var active = await _taskRepository.GetActiveTasksAsync();
        Tasks.Clear();
        foreach (var task in active.OrderBy(t => t.OrderIndex))
        {
            Tasks.Add(new TaskItemViewModel(task.Id, task.Title, isCompleted: false, orderIndex: task.OrderIndex, postponedDays: task.PostponedDays));
        }
    }

    [RelayCommand]
    public void PlayDailyVideo()
    {
        PlayDailyVideoRequested?.Invoke();
    }

    public async Task RefreshDailyRitualStatusAsync(DateOnly? dateOnly = null)
    {
        if (_dailyRitualRepository == null) return;
        var date = dateOnly ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var logicalDate = date.ToString("yyyy-MM-dd");

        var ritual = await _dailyRitualRepository.GetDailyRitualLogAsync(logicalDate);
        if (ritual != null && ritual.IsCompleted)
        {
            IsRitualCompleted = true;
            RitualStatus = "Просмотр завершён";
        }
        else
        {
            IsRitualCompleted = false;
            RitualStatus = "Готово к просмотру";
        }
    }

    public async Task RefreshSummaryAsync(DateOnly? dateOnly = null)
    {
        var date = dateOnly ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var logicalDate = date.ToString("yyyy-MM-dd");

        var archivedToday = await _taskRepository.GetArchivedTasksAsync(logicalDate);
        CompletedTodayCount = archivedToday.Count;

        if (_progressEngine != null && _dailyRitualRepository != null && _weeklyGoalRepository != null)
        {
            var year = date.Year;
            var month = date.Month;
            var yearMonth = $"{year:D4}-{month:D2}";

            var logs = await _dailyRitualRepository.GetLogsForMonthAsync(yearMonth);
            int completedDays = logs.Count(l => l.IsCompleted);

            var weeklyGoals = await _weeklyGoalRepository.GetGoalsForMonthAsync(yearMonth);
            int completedWeeks = weeklyGoals.Count(w => string.Equals(w.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            var result = _progressEngine.CalculateMonthlyProgress(year, month, completedDays, completedWeeks);
            CurrentMonthScore = result.DisplayScore;
            CurrentMonthDailyRatio = result.DailyRatio;
            CurrentMonthWeeklyRatio = result.WeeklyRatio;
        }
    }
}
