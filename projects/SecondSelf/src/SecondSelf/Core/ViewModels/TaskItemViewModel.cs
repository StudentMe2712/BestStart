using CommunityToolkit.Mvvm.ComponentModel;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel отдельной задачи дня в списке Today's Focus.
/// Поддерживает инлайн-редактирование, панель действий (Check, Edit, Trash)
/// и двухфазную анимацию закрытия (зачёркивание 350 мс -> схлопывание высоты 350 мс).
/// </summary>
public partial class TaskItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private int _orderIndex;

    [ObservableProperty]
    private int _postponedDays;

    [ObservableProperty]
    private bool _isActionPanelOpen;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editText = string.Empty;

    [ObservableProperty]
    private bool _isCompleting;

    [ObservableProperty]
    private bool _isStrikethrough;

    [ObservableProperty]
    private bool _isCheckboxChecked;

    [ObservableProperty]
    private double _rowOpacity = 1.0;

    [ObservableProperty]
    private double _rowHeight = 44.0;

    public TaskItemViewModel()
    {
    }

    public TaskItemViewModel(string id, string title, bool isCompleted = false, int orderIndex = 0, int postponedDays = 0)
    {
        Id = id;
        Title = title;
        IsCompleted = isCompleted;
        OrderIndex = orderIndex;
        PostponedDays = postponedDays;
        EditText = title;
        RowOpacity = 1.0;
        RowHeight = 44.0;
    }

    public void StartEdit()
    {
        EditText = Title;
        IsEditing = true;
        IsActionPanelOpen = false;
    }

    public void CancelEdit()
    {
        EditText = Title;
        IsEditing = false;
    }

    public void ResetAnimationState()
    {
        IsCompleting = false;
        IsCompleted = false;
        IsCheckboxChecked = false;
        IsStrikethrough = false;
        RowOpacity = 1.0;
        RowHeight = 44.0;
        IsActionPanelOpen = false;
        IsEditing = false;
    }
}
