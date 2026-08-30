using System.Collections.ObjectModel;
using System.Windows.Media;
using NexusCommander.Helpers;

namespace NexusCommander.Models;

public class SidebarItem : ViewModelBase
{
    private bool _isActive;
    private bool _isExpanded;
    private bool _isPinned;
    private string? _subtitle;
    private double _usagePercent;

    public string Title { get; set; } = string.Empty;
    public string? Path { get; set; }
    public ImageSource? Icon { get; set; }
    public string IconGlyph { get; set; } = "📁";
    public bool IsDrive { get; set; }
    public bool IsSeparator { get; set; }
    public double FreeSpaceBytes { get; set; }
    public double TotalSizeBytes { get; set; }

    public ObservableCollection<SidebarItem> Children { get; } = new();
    public bool HasChildren => Children.Count > 0;

    public SidebarItem()
    {
        Children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasChildren));
    }

    public double UsagePercent
    {
        get => _usagePercent;
        set => SetField(ref _usagePercent, value);
    }

    public string? Subtitle
    {
        get => _subtitle;
        set => SetField(ref _subtitle, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set => SetField(ref _isPinned, value);
    }
}
