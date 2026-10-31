using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondSelf.Data.Repositories;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel четвертого раздела Dashboard — Archive (Архив выполненного, «Хроника побед»).
/// Отображает верхние сводные счётчики (Сегодня: X · Неделя: Y · Месяц: Z),
/// хронологическую ленту закрытых задач с тегами источников и живой поиск по названию.
/// </summary>
public partial class ArchiveTabViewModel : ObservableObject
{
    private readonly ITaskRepository _taskRepository;

    [ObservableProperty]
    private int _todayCompletedCount;

    [ObservableProperty]
    private int _weekCompletedCount;

    [ObservableProperty]
    private int _monthCompletedCount;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public ObservableCollection<ArchivedTaskItemViewModel> AllTasks { get; } = new();
    public ObservableCollection<ArchivedTaskItemViewModel> FilteredTasks { get; } = new();

    public ArchiveTabViewModel(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
    }

    [RelayCommand]
    public async Task LoadArchiveAsync(DateTime? referenceDate = null)
    {
        var now = referenceDate ?? DateTime.UtcNow;
        var todayStr = now.ToString("yyyy-MM-dd");
        var monthStr = now.ToString("yyyy-MM");
        int currentIsoYear = ISOWeek.GetYear(now);
        int currentIsoWeek = ISOWeek.GetWeekOfYear(now);

        var archivedEntities = await _taskRepository.GetArchivedTasksAsync();

        AllTasks.Clear();
        int todayCount = 0;
        int weekCount = 0;
        int monthCount = 0;

        foreach (var entity in archivedEntities.OrderByDescending(t => t.CompletedAt))
        {
            var vm = new ArchivedTaskItemViewModel(entity);
            AllTasks.Add(vm);

            if (string.Equals(entity.LogicalDate, todayStr, StringComparison.OrdinalIgnoreCase))
            {
                todayCount++;
            }

            if (entity.LogicalDate.StartsWith(monthStr, StringComparison.OrdinalIgnoreCase))
            {
                monthCount++;
            }

            if (DateTime.TryParse(entity.CompletedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
            {
                if (ISOWeek.GetYear(dt) == currentIsoYear && ISOWeek.GetWeekOfYear(dt) == currentIsoWeek)
                {
                    weekCount++;
                }
            }
        }

        TodayCompletedCount = todayCount;
        WeekCompletedCount = weekCount;
        MonthCompletedCount = monthCount;

        ApplyFilter();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredTasks.Clear();
        var query = SearchQuery?.Trim();

        var queryable = string.IsNullOrWhiteSpace(query)
            ? AllTasks
            : AllTasks.Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var task in queryable)
        {
            FilteredTasks.Add(task);
        }
    }
}
