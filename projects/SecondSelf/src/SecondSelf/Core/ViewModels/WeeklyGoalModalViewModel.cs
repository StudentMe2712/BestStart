using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Repositories;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel модального окна планирования Weekly Goal.
/// Реализует выбор промежуточного этапа (Milestone) из активных Больших целей,
/// инвариант фиксации обязательства (Commitment Lock: автоблокировка через 2 часа или по кнопке),
/// и только явные действия (Перенос с причиной / Отмена с причиной) после блокировки.
/// </summary>
public partial class WeeklyGoalModalViewModel : ObservableObject
{
    private readonly IWeeklyGoalRepository _weeklyGoalRepository;
    private readonly IBigGoalRepository _bigGoalRepository;

    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private int _isoYear;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekAndMonthHeader))]
    private int _isoWeek;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekAndMonthHeader))]
    private string _belongsToMonth = string.Empty;

    public string WeekAndMonthHeader => $"Неделя {IsoWeek} · {BelongsToMonth}";

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private string _status = "Active"; // Active, Locked, Completed, Postponed, Cancelled

    [ObservableProperty]
    private string? _lockedAt;

    [ObservableProperty]
    private string? _completedAt;

    [ObservableProperty]
    private string? _cancellationReason;

    [ObservableProperty]
    private DateTime? _createdAtUtc;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isLocked;

    public bool CanEdit => !IsLocked;

    // --- Available Milestones ---
    public ObservableCollection<MilestoneLookupItem> AvailableMilestones { get; } = new();

    [ObservableProperty]
    private MilestoneLookupItem? _selectedMilestone;

    // --- Actions reasons ---
    [ObservableProperty]
    private string _actionReason = string.Empty;

    /// <summary>
    /// Опциональное время для детерминированного тестирования правила 2 часов (Commitment Lock).
    /// </summary>
    public DateTime? CurrentTimeForTesting { get; set; }

    public WeeklyGoalModalViewModel(
        IWeeklyGoalRepository weeklyGoalRepository,
        IBigGoalRepository bigGoalRepository)
    {
        _weeklyGoalRepository = weeklyGoalRepository ?? throw new ArgumentNullException(nameof(weeklyGoalRepository));
        _bigGoalRepository = bigGoalRepository ?? throw new ArgumentNullException(nameof(bigGoalRepository));
    }

    public async Task LoadForWeekAsync(int isoYear, int isoWeek)
    {
        IsoYear = isoYear;
        IsoWeek = isoWeek;

        var thursday = ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Thursday);
        BelongsToMonth = $"{thursday.Year:D4}-{thursday.Month:D2}";

        // 1. Загрузка доступных этапов активных целей
        await LoadAvailableMilestonesAsync();

        // 2. Загрузка существующей цели на эту неделю
        var existing = await _weeklyGoalRepository.GetGoalForWeekAsync(isoYear, isoWeek);
        if (existing != null)
        {
            Id = existing.Id;
            Title = existing.Title;
            Status = existing.Status;
            LockedAt = existing.LockedAt;
            CompletedAt = existing.CompletedAt;
            CancellationReason = existing.CancellationReason;

            if (DateTime.TryParse(existing.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedCreated))
            {
                CreatedAtUtc = DateTime.SpecifyKind(parsedCreated, DateTimeKind.Utc);
            }
            else
            {
                CreatedAtUtc = null;
            }

            if (!string.IsNullOrWhiteSpace(existing.MilestoneId))
            {
                SelectedMilestone = AvailableMilestones.FirstOrDefault(m => string.Equals(m.MilestoneId, existing.MilestoneId, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                SelectedMilestone = null;
            }
        }
        else
        {
            Id = Guid.NewGuid().ToString();
            Title = string.Empty;
            Status = "Active";
            LockedAt = null;
            CompletedAt = null;
            CancellationReason = null;
            CreatedAtUtc = null;
            SelectedMilestone = null;
        }

        CheckAndApplyCommitmentLock();
    }

    public async Task LoadAvailableMilestonesAsync()
    {
        AvailableMilestones.Clear();
        var activeGoals = await _bigGoalRepository.GetActiveGoalsAsync();
        foreach (var goal in activeGoals)
        {
            if (goal.Milestones == null) continue;
            foreach (var ms in goal.Milestones.Where(m => !string.Equals(m.Status, "Completed", StringComparison.OrdinalIgnoreCase)))
            {
                AvailableMilestones.Add(new MilestoneLookupItem
                {
                    MilestoneId = ms.Id,
                    GoalId = goal.Id,
                    GoalTitle = goal.Title,
                    MilestoneTitle = ms.Title,
                    OrderIndex = ms.OrderIndex
                });
            }
        }
    }

    public bool CheckAndApplyCommitmentLock()
    {
        var now = CurrentTimeForTesting ?? DateTime.UtcNow;

        bool isExplicitlyLocked = string.Equals(Status, "Locked", StringComparison.OrdinalIgnoreCase);
        bool isTerminalStatus = string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(Status, "Postponed", StringComparison.OrdinalIgnoreCase);

        bool isTwoHoursExpired = false;
        if (CreatedAtUtc.HasValue)
        {
            isTwoHoursExpired = (now - CreatedAtUtc.Value) >= TimeSpan.FromHours(2);
        }

        IsLocked = isExplicitlyLocked || isTerminalStatus || isTwoHoursExpired;
        return IsLocked;
    }

    [RelayCommand]
    public async Task SaveDraftAsync()
    {
        CheckAndApplyCommitmentLock();
        if (IsLocked)
        {
            throw new InvalidOperationException("Цель заблокирована (Commitment Lock) и не может быть отредактирована.");
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            return;
        }

        var now = CurrentTimeForTesting ?? DateTime.UtcNow;
        if (!CreatedAtUtc.HasValue)
        {
            CreatedAtUtc = now;
        }

        var entity = new WeeklyGoal
        {
            Id = Id,
            IsoYear = IsoYear,
            IsoWeek = IsoWeek,
            BelongsToMonth = BelongsToMonth,
            MilestoneId = SelectedMilestone?.MilestoneId,
            Title = Title.Trim(),
            Status = "Active",
            LockedAt = null,
            CompletedAt = null,
            CancellationReason = null,
            CreatedAt = CreatedAtUtc.Value.ToString("O")
        };

        await _weeklyGoalRepository.AddOrUpdateGoalAsync(entity);
        Status = "Active";
        CheckAndApplyCommitmentLock();
    }

    [RelayCommand]
    public async Task SaveAndLockAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            return;
        }

        var now = CurrentTimeForTesting ?? DateTime.UtcNow;
        if (!CreatedAtUtc.HasValue)
        {
            CreatedAtUtc = now;
        }

        var nowIso = now.ToString("O");
        LockedAt = nowIso;
        Status = "Locked";

        var entity = new WeeklyGoal
        {
            Id = Id,
            IsoYear = IsoYear,
            IsoWeek = IsoWeek,
            BelongsToMonth = BelongsToMonth,
            MilestoneId = SelectedMilestone?.MilestoneId,
            Title = Title.Trim(),
            Status = "Locked",
            LockedAt = nowIso,
            CompletedAt = null,
            CancellationReason = null,
            CreatedAt = CreatedAtUtc.Value.ToString("O")
        };

        await _weeklyGoalRepository.AddOrUpdateGoalAsync(entity);
        IsLocked = true;
    }

    [RelayCommand]
    public async Task PostponeAsync(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        // Определяем следующую неделю
        var monday = ISOWeek.ToDateTime(IsoYear, IsoWeek, DayOfWeek.Monday).AddDays(7);
        int nextYear = ISOWeek.GetYear(monday);
        int nextWeek = ISOWeek.GetWeekOfYear(monday);

        await _weeklyGoalRepository.PostponeGoalAsync(Id, reason, nextYear, nextWeek);
        Status = "Postponed";
        CancellationReason = reason;
        IsLocked = true;
    }

    [RelayCommand]
    public async Task CancelAsync(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await _weeklyGoalRepository.CancelGoalAsync(Id, reason);
        Status = "Cancelled";
        CancellationReason = reason;
        IsLocked = true;
    }
}
