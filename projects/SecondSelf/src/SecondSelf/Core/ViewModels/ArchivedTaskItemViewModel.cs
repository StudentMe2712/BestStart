using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SecondSelf.Data.Entities;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel строки выполненной задачи в ленте закрытых дел (раздел Archive).
/// </summary>
public partial class ArchivedTaskItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string? _originalTaskId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _taskSource = "Today";

    [ObservableProperty]
    private string _createdAt = string.Empty;

    [ObservableProperty]
    private string _completedAt = string.Empty;

    [ObservableProperty]
    private string _logicalDate = string.Empty;

    public string FormattedCompletedTime
    {
        get
        {
            if (DateTime.TryParse(CompletedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
            {
                return dt.ToLocalTime().ToString("HH:mm");
            }
            return string.Empty;
        }
    }

    public ArchivedTaskItemViewModel()
    {
    }

    public ArchivedTaskItemViewModel(ArchivedTask entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Id = entity.Id;
        OriginalTaskId = entity.OriginalTaskId;
        Title = entity.Title;
        TaskSource = entity.TaskSource;
        CreatedAt = entity.CreatedAt;
        CompletedAt = entity.CompletedAt;
        LogicalDate = entity.LogicalDate;
    }
}
