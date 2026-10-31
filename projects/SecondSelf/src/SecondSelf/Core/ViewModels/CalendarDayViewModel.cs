using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel отдельного дня в календарной сетке месяца раздела Progress (1..28/31).
/// </summary>
public partial class CalendarDayViewModel : ObservableObject
{
    [ObservableProperty]
    private int _dayNumber;

    [ObservableProperty]
    private DateOnly _date;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _isToday;

    [ObservableProperty]
    private string _dayOfWeekShort = string.Empty;

    public CalendarDayViewModel()
    {
    }

    public CalendarDayViewModel(int dayNumber, DateOnly date, bool isCompleted = false, bool isToday = false)
    {
        DayNumber = dayNumber;
        Date = date;
        IsCompleted = isCompleted;
        IsToday = isToday;
        DayOfWeekShort = date.DayOfWeek.ToString().Substring(0, 3);
    }
}
