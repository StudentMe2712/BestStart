using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondSelf.Data.Repositories;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// Главный ViewModel окна Personal Dashboard (1120x780px).
/// Координирует навигацию по 5 разделам (Today, Goals, Progress, Archive, Settings),
/// модальные окна (Weekly Goal, Monthly Recap, Add Goal, Goal Details),
/// и автоматическую активацию Monthly Recap в конце месяца.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IMonthlyRecapRepository _monthlyRecapRepository;

    public const string TabToday = "Today";
    public const string TabGoals = "Goals";
    public const string TabProgress = "Progress";
    public const string TabArchive = "Archive";
    public const string TabSettings = "Settings";

    // --- Child ViewModels ---
    public TodayTabViewModel TodayViewModel { get; }
    public GoalsTabViewModel GoalsViewModel { get; }
    public ProgressTabViewModel ProgressViewModel { get; }
    public ArchiveTabViewModel ArchiveViewModel { get; }
    public SettingsTabViewModel SettingsViewModel { get; }
    public MonthlyRecapViewModel MonthlyRecapViewModel { get; }
    public WeeklyGoalModalViewModel WeeklyGoalModalViewModel { get; }

    // --- Navigation & State ---
    [ObservableProperty]
    private string _currentTab = TabToday;

    // --- Modal overlays state ---
    [ObservableProperty]
    private bool _isWeeklyGoalModalOpen;

    [ObservableProperty]
    private bool _isMonthlyRecapOpen;

    [ObservableProperty]
    private bool _isAddGoalModalOpen;

    [ObservableProperty]
    private BigGoalViewModel? _selectedGoalDetail;

    public DashboardViewModel(
        TodayTabViewModel todayViewModel,
        GoalsTabViewModel goalsViewModel,
        ProgressTabViewModel progressViewModel,
        ArchiveTabViewModel archiveViewModel,
        SettingsTabViewModel settingsViewModel,
        MonthlyRecapViewModel monthlyRecapViewModel,
        WeeklyGoalModalViewModel weeklyGoalModalViewModel,
        IMonthlyRecapRepository monthlyRecapRepository)
    {
        TodayViewModel = todayViewModel ?? throw new ArgumentNullException(nameof(todayViewModel));
        GoalsViewModel = goalsViewModel ?? throw new ArgumentNullException(nameof(goalsViewModel));
        ProgressViewModel = progressViewModel ?? throw new ArgumentNullException(nameof(progressViewModel));
        ArchiveViewModel = archiveViewModel ?? throw new ArgumentNullException(nameof(archiveViewModel));
        SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        MonthlyRecapViewModel = monthlyRecapViewModel ?? throw new ArgumentNullException(nameof(monthlyRecapViewModel));
        WeeklyGoalModalViewModel = weeklyGoalModalViewModel ?? throw new ArgumentNullException(nameof(weeklyGoalModalViewModel));
        _monthlyRecapRepository = monthlyRecapRepository ?? throw new ArgumentNullException(nameof(monthlyRecapRepository));

        // Подписка на события дочерних VM
        MonthlyRecapViewModel.MonthFinalized += OnMonthFinalized;
        MonthlyRecapViewModel.CloseRequested += () => IsMonthlyRecapOpen = false;

        ProgressViewModel.RecapOpened += (recap) =>
        {
            _ = MonthlyRecapViewModel.LoadRecapAsync(recap.YearMonth, isReadOnly: true);
            IsMonthlyRecapOpen = true;
        };
    }

    [RelayCommand]
    public async Task InitializeAsync(DateTime? referenceDate = null)
    {
        await TodayViewModel.LoadDataAsync(referenceDate);
        await GoalsViewModel.LoadGoalsAsync();
        await ProgressViewModel.LoadMonthDataAsync();
        await ArchiveViewModel.LoadArchiveAsync(referenceDate);
        await SettingsViewModel.LoadSettingsAsync();

        await CheckMonthlyRecapActivationAsync(referenceDate);
    }

    /// <summary>
    /// Проверяет критерии автоматической активации оверлея Monthly Recap (AC-11):
    /// Наступило 18:00 последнего дня месяца или утро 1-го числа нового месяца,
    /// и предыдущий месяц ещё не финализирован.
    /// </summary>
    public async Task<bool> CheckMonthlyRecapActivationAsync(DateTime? customNow = null)
    {
        var now = customNow ?? DateTime.UtcNow;
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

        string? monthToRecap = null;

        // Вариант 1: Последний день месяца, время >= 18:00
        if (now.Day == daysInMonth && now.Hour >= 18)
        {
            monthToRecap = $"{now.Year:D4}-{now.Month:D2}";
        }
        // Вариант 2: 1-е число месяца (утро или день)
        else if (now.Day == 1)
        {
            var prevMonth = now.AddMonths(-1);
            monthToRecap = $"{prevMonth.Year:D4}-{prevMonth.Month:D2}";
        }

        if (monthToRecap != null)
        {
            var recap = await _monthlyRecapRepository.GetRecapAsync(monthToRecap);
            if (recap == null || !recap.IsFinalized)
            {
                await MonthlyRecapViewModel.LoadRecapAsync(monthToRecap, isReadOnly: false);
                IsMonthlyRecapOpen = true;
                return true;
            }
        }

        return false;
    }

    private void OnMonthFinalized()
    {
        IsMonthlyRecapOpen = false;
        // Обновляем текущие экраны
        _ = TodayViewModel.LoadDataAsync();
        _ = ProgressViewModel.LoadMonthDataAsync();
    }

    [RelayCommand]
    public void NavigateToTab(string tabName)
    {
        if (!string.IsNullOrWhiteSpace(tabName))
        {
            CurrentTab = tabName;
        }
    }

    [RelayCommand]
    public void OpenWeeklyGoalModal()
    {
        var now = DateTime.UtcNow;
        int isoYear = ISOWeek.GetYear(now);
        int isoWeek = ISOWeek.GetWeekOfYear(now);
        _ = WeeklyGoalModalViewModel.LoadForWeekAsync(isoYear, isoWeek);
        IsWeeklyGoalModalOpen = true;
    }

    [RelayCommand]
    public void CloseWeeklyGoalModal()
    {
        IsWeeklyGoalModalOpen = false;
        _ = TodayViewModel.LoadDataAsync();
    }

    [RelayCommand]
    public void OpenMonthlyRecap()
    {
        var now = DateTime.UtcNow;
        var yearMonth = $"{now.Year:D4}-{now.Month:D2}";
        _ = MonthlyRecapViewModel.LoadRecapAsync(yearMonth, isReadOnly: false);
        IsMonthlyRecapOpen = true;
    }

    [RelayCommand]
    public void CloseMonthlyRecap()
    {
        IsMonthlyRecapOpen = false;
    }

    [RelayCommand]
    public void OpenAddGoalModal()
    {
        GoalsViewModel.OpenAddGoalModal();
        IsAddGoalModalOpen = GoalsViewModel.IsAddGoalModalOpen;
    }

    [RelayCommand]
    public void CloseAddGoalModal()
    {
        GoalsViewModel.CloseAddGoalModal();
        IsAddGoalModalOpen = false;
    }

    [RelayCommand]
    public void SelectGoalDetail(BigGoalViewModel goal)
    {
        SelectedGoalDetail = goal;
        GoalsViewModel.SelectGoal(goal);
    }

    [RelayCommand]
    public void CloseGoalDetail()
    {
        SelectedGoalDetail = null;
        GoalsViewModel.CloseGoalDetail();
    }
}
