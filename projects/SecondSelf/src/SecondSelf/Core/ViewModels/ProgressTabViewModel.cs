using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Repositories;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel третьего раздела Dashboard — Progress (Аналитика и история месяцев).
/// Включает глубокий разбор текущего месяца (гексагональный/числовой процент, дроби 26/31 и 3/4),
/// интерактивную сетку календаря ритуалов месяца (1..28/31),
/// карточки 4 или 5 недель по стандарту ISO-8601 и галерею архивных месяцев.
/// </summary>
public partial class ProgressTabViewModel : ObservableObject
{
    private readonly IProgressCalculationEngine _progressEngine;
    private readonly IDailyRitualRepository _dailyRitualRepository;
    private readonly IWeeklyGoalRepository _weeklyGoalRepository;
    private readonly IMonthlyRecapRepository _monthlyRecapRepository;
    private readonly IBigGoalRepository? _bigGoalRepository;
    private readonly IAppSettingsService? _appSettingsService;

    public event Action<MonthlyRecap>? RecapOpened;

    [ObservableProperty]
    private string _monthName = string.Empty;

    [ObservableProperty]
    private int _scorePercentage;

    [ObservableProperty]
    private string _dailyFraction = "0 / 0";

    [ObservableProperty]
    private string _weeklyFraction = "0 / 0";

    [ObservableProperty]
    private double _dailyWeight = 0.30;

    [ObservableProperty]
    private double _weeklyWeight = 0.70;

    [ObservableProperty]
    private int _year;

    [ObservableProperty]
    private int _month;

    public ObservableCollection<CalendarDayViewModel> CalendarDays { get; } = new();
    public ObservableCollection<WeekCardViewModel> WeekCards { get; } = new();
    public ObservableCollection<MonthlyRecap> HistoricalRecaps { get; } = new();

    public ProgressTabViewModel(
        IProgressCalculationEngine progressEngine,
        IDailyRitualRepository dailyRitualRepository,
        IWeeklyGoalRepository weeklyGoalRepository,
        IMonthlyRecapRepository monthlyRecapRepository,
        IBigGoalRepository? bigGoalRepository = null,
        IAppSettingsService? appSettingsService = null)
    {
        _progressEngine = progressEngine ?? throw new ArgumentNullException(nameof(progressEngine));
        _dailyRitualRepository = dailyRitualRepository ?? throw new ArgumentNullException(nameof(dailyRitualRepository));
        _weeklyGoalRepository = weeklyGoalRepository ?? throw new ArgumentNullException(nameof(weeklyGoalRepository));
        _monthlyRecapRepository = monthlyRecapRepository ?? throw new ArgumentNullException(nameof(monthlyRecapRepository));
        _bigGoalRepository = bigGoalRepository;
        _appSettingsService = appSettingsService;
    }

    public async Task LoadMonthDataAsync(int? targetYear = null, int? targetMonth = null)
    {
        var now = DateTime.UtcNow;
        Year = targetYear ?? now.Year;
        Month = targetMonth ?? now.Month;

        var dt = new DateTime(Year, Month, 1);
        MonthName = dt.ToString("MMMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

        // 1. Загрузка весов
        if (_appSettingsService != null)
        {
            var settings = await _appSettingsService.GetSettingsAsync();
            DailyWeight = settings.DailyWeight;
            WeeklyWeight = settings.WeeklyWeight;
        }

        var yearMonth = $"{Year:D4}-{Month:D2}";

        // 2. Загрузка ритуалов
        var logs = await _dailyRitualRepository.GetLogsForMonthAsync(yearMonth);
        var completedDates = new HashSet<string>(
            logs.Where(l => l.IsCompleted).Select(l => l.LogicalDate),
            StringComparer.OrdinalIgnoreCase);

        int totalDaysInMonth = DateTime.DaysInMonth(Year, Month);
        CalendarDays.Clear();
        var today = DateOnly.FromDateTime(now);

        for (int day = 1; day <= totalDaysInMonth; day++)
        {
            var date = new DateOnly(Year, Month, day);
            var dateStr = date.ToString("yyyy-MM-dd");
            bool isCompleted = completedDates.Contains(dateStr);
            bool isToday = (date == today);

            CalendarDays.Add(new CalendarDayViewModel(day, date, isCompleted, isToday));
        }

        // 3. Загрузка недель по ISO-8601
        var isoWeeks = _progressEngine.GetIsoWeeksForMonth(Year, Month);
        var weeklyGoals = await _weeklyGoalRepository.GetGoalsForMonthAsync(yearMonth);
        var goalsByWeek = weeklyGoals.ToDictionary(g => g.IsoWeek);

        WeekCards.Clear();
        int completedWeeksCount = 0;

        foreach (var isoWeek in isoWeeks)
        {
            goalsByWeek.TryGetValue(isoWeek.IsoWeek, out var goal);

            string status = "InProgress";
            string goalTitle = "Нет цели на неделю";
            string? milestoneTag = null;

            if (goal != null)
            {
                goalTitle = goal.Title;
                status = goal.Status;

                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    completedWeeksCount++;
                }

                if (!string.IsNullOrWhiteSpace(goal.MilestoneId) && _bigGoalRepository != null)
                {
                    milestoneTag = await FormatMilestoneTagAsync(goal.MilestoneId);
                }
            }
            else
            {
                // Если неделя уже в прошлом, а цели не было
                if (isoWeek.EndDateSunday < today)
                {
                    status = "Missed";
                }
            }

            var startStr = isoWeek.StartDateMonday.ToString("MMM d", CultureInfo.InvariantCulture);
            var endStr = isoWeek.EndDateSunday.ToString("MMM d", CultureInfo.InvariantCulture);

            WeekCards.Add(new WeekCardViewModel
            {
                WeekNumber = isoWeek.IsoWeek,
                IsoYear = isoWeek.IsoYear,
                DateRange = $"{startStr} — {endStr}",
                GoalTitle = goalTitle,
                MilestoneTag = milestoneTag,
                Status = status
            });
        }

        // 4. Расчет прогресса
        int completedDaysCount = completedDates.Count;
        var progressResult = _progressEngine.CalculateMonthlyProgress(
            Year, Month, completedDaysCount, completedWeeksCount, DailyWeight, WeeklyWeight);

        ScorePercentage = progressResult.DisplayScore;
        DailyFraction = progressResult.DailyRatio;
        WeeklyFraction = progressResult.WeeklyRatio;

        // 5. Загрузка архивных отчетов прошлых месяцев
        HistoricalRecaps.Clear();
        var recaps = await _monthlyRecapRepository.GetAllRecapsAsync();
        foreach (var r in recaps.Where(rc => rc.YearMonth != yearMonth))
        {
            HistoricalRecaps.Add(r);
        }
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
    public void OpenRecap(MonthlyRecap recap)
    {
        if (recap != null)
        {
            RecapOpened?.Invoke(recap);
        }
    }
}
