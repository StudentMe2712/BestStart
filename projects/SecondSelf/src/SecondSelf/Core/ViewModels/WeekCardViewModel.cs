using CommunityToolkit.Mvvm.ComponentModel;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel карточки недели по стандарту ISO-8601 в разделе Progress (4 или 5 недель в месяце).
/// </summary>
public partial class WeekCardViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekDisplayNumber))]
    private int _weekNumber;

    public string WeekDisplayNumber => $"Н{WeekNumber}";

    [ObservableProperty]
    private int _isoYear;

    [ObservableProperty]
    private string _dateRange = string.Empty;

    [ObservableProperty]
    private string _goalTitle = string.Empty;

    [ObservableProperty]
    private string? _milestoneTag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    private string _status = "InProgress"; // Completed, InProgress, Postponed, Cancelled, Missed

    public bool IsCompleted => string.Equals(Status, "Completed", System.StringComparison.OrdinalIgnoreCase);

    public string StatusBadgeText => Status switch
    {
        "Completed" => "Выполнено",
        "InProgress" => "В процессе",
        "Postponed" => "Перенесено",
        "Cancelled" => "Отменено",
        "Missed" => "Пропущено",
        _ => Status
    };
}
