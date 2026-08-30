using System.Windows.Media;
using NexusCommander.Helpers;

namespace NexusCommander.Models;

public class SidebarItem : ViewModelBase
{
    private bool _isActive;
    private string _subtitle = string.Empty;
    private double _usagePercent;

    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ImageSource? Icon { get; set; }
    public string IconGlyph { get; set; } = "📁";
    public bool IsDrive { get; set; }
    public string Section { get; set; } = "Быстрый доступ"; // "Быстрый доступ" or "Диски"
    public double FreeSpaceBytes { get; set; }
    public double TotalSizeBytes { get; set; }

    public double UsagePercent
    {
        get => _usagePercent;
        set => SetField(ref _usagePercent, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetField(ref _subtitle, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }
}
