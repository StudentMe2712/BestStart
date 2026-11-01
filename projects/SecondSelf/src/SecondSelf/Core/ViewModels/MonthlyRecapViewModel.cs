using System;
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
/// ViewModel экрана Monthly Recap (Итоги месяца).
/// Реализует модальный полноэкранный оверлей с тремя ключевыми метриками (Score %, Daily 26/31, Weekly 3/4),
/// блоком осознанной рефлексии (What worked? What needs to change?),
/// и кнопкой «Завершить месяц и начать новый цикл» (IsFinalized = 1).
/// </summary>
public partial class MonthlyRecapViewModel : ObservableObject
{
    private readonly IMonthlyRecapRepository _monthlyRecapRepository;
    private readonly IProgressCalculationEngine? _progressEngine;
    private readonly IDailyRitualRepository? _dailyRitualRepository;
    private readonly IWeeklyGoalRepository? _weeklyGoalRepository;
    private readonly IAppSettingsService? _appSettingsService;

    public event Action? MonthFinalized;
    public event Action? CloseRequested;

    [ObservableProperty]
    private string _yearMonth = string.Empty;

    [ObservableProperty]
    private string _headerMonthYear = DateTime.Now.ToString("MMMM yyyy", new CultureInfo("ru-RU")).ToUpper();

    [ObservableProperty]
    private string _headerSubtitle = "Итоги месяца";

    [ObservableProperty]
    private int _monthlyScore = 0;

    [ObservableProperty]
    private string _dailyFraction = "0 / 0";

    [ObservableProperty]
    private string _weeklyFraction = "0 / 0";

    [ObservableProperty]
    private string _reflectionPrompt = "Что получилось? Что стоит изменить? На чём сфокусироваться в следующем месяце?";

    [ObservableProperty]
    private string _reflectionText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFinalize))]
    private bool _isFinalized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFinalize))]
    private bool _isReadOnly;

    public bool CanFinalize => !IsFinalized && !IsReadOnly;

    public MonthlyRecapViewModel(
        IMonthlyRecapRepository monthlyRecapRepository,
        IProgressCalculationEngine? progressEngine = null,
        IDailyRitualRepository? dailyRitualRepository = null,
        IWeeklyGoalRepository? weeklyGoalRepository = null,
        IAppSettingsService? appSettingsService = null)
    {
        _monthlyRecapRepository = monthlyRecapRepository ?? throw new ArgumentNullException(nameof(monthlyRecapRepository));
        _progressEngine = progressEngine;
        _dailyRitualRepository = dailyRitualRepository;
        _weeklyGoalRepository = weeklyGoalRepository;
        _appSettingsService = appSettingsService;
    }

    public async Task LoadRecapAsync(string yearMonth, bool isReadOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);
        YearMonth = yearMonth;
        IsReadOnly = isReadOnly;

        if (DateTime.TryParseExact(yearMonth, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            HeaderMonthYear = dt.ToString("MMMM yyyy", new CultureInfo("ru-RU")).ToUpper();
        }
        else
        {
            HeaderMonthYear = yearMonth;
        }

        var existing = await _monthlyRecapRepository.GetRecapAsync(yearMonth);
        if (existing != null)
        {
            MonthlyScore = (int)Math.Round(existing.FinalScore);
            DailyFraction = existing.DailyRatio;
            WeeklyFraction = existing.WeeklyRatio;
            ReflectionText = existing.ReflectionText ?? string.Empty;
            IsFinalized = existing.IsFinalized;
        }
        else
        {
            // Рассчитываем актуальные значения на лету
            if (_progressEngine != null && _dailyRitualRepository != null && _weeklyGoalRepository != null &&
                DateTime.TryParseExact(yearMonth, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                int year = parsed.Year;
                int month = parsed.Month;

                double dailyWeight = 0.30;
                double weeklyWeight = 0.70;
                if (_appSettingsService != null)
                {
                    var settings = await _appSettingsService.GetSettingsAsync();
                    dailyWeight = settings.DailyWeight;
                    weeklyWeight = settings.WeeklyWeight;
                }

                var logs = await _dailyRitualRepository.GetLogsForMonthAsync(yearMonth);
                int completedDays = logs.Count(l => l.IsCompleted);

                var weeklyGoals = await _weeklyGoalRepository.GetGoalsForMonthAsync(yearMonth);
                int completedWeeks = weeklyGoals.Count(w => string.Equals(w.Status, "Completed", StringComparison.OrdinalIgnoreCase));

                var progress = _progressEngine.CalculateMonthlyProgress(year, month, completedDays, completedWeeks, dailyWeight, weeklyWeight);
                MonthlyScore = progress.DisplayScore;
                DailyFraction = progress.DailyRatio;
                WeeklyFraction = progress.WeeklyRatio;

                ReflectionText = string.Empty;
                IsFinalized = false;

                // Сохраняем черновик отчета
                var draft = new MonthlyRecap
                {
                    YearMonth = yearMonth,
                    FinalScore = progress.DisplayScore,
                    DailyRatio = progress.DailyRatio,
                    WeeklyRatio = progress.WeeklyRatio,
                    DailyWeight = dailyWeight,
                    WeeklyWeight = weeklyWeight,
                    ReflectionText = null,
                    IsFinalized = false,
                    CompletedAt = DateTime.UtcNow.ToString("O")
                };
                await _monthlyRecapRepository.SaveRecapAsync(draft);
            }
            else
            {
                MonthlyScore = 0;
                DailyFraction = "0 / 0";
                WeeklyFraction = "0 / 0";
                ReflectionText = string.Empty;
                IsFinalized = false;
            }
        }
    }

    [RelayCommand]
    public async Task FinalizeMonthAsync()
    {
        if (IsFinalized) return;

        // Если записи нет в БД, сначала сохраняем её
        var existing = await _monthlyRecapRepository.GetRecapAsync(YearMonth);
        if (existing == null)
        {
            var recap = new MonthlyRecap
            {
                YearMonth = YearMonth,
                FinalScore = MonthlyScore,
                DailyRatio = DailyFraction,
                WeeklyRatio = WeeklyFraction,
                ReflectionText = ReflectionText,
                IsFinalized = true,
                CompletedAt = DateTime.UtcNow.ToString("O")
            };
            await _monthlyRecapRepository.SaveRecapAsync(recap);
        }
        else
        {
            await _monthlyRecapRepository.FinalizeRecapAsync(YearMonth, ReflectionText);
        }

        IsFinalized = true;
        MonthFinalized?.Invoke();
    }

    [RelayCommand]
    public void Close()
    {
        CloseRequested?.Invoke();
    }
}
