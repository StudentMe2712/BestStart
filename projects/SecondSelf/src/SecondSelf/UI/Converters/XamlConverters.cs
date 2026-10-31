using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Text;

namespace SecondSelf.UI.Converters;

/// <summary>
/// Статические методы-конвертеры для выражений {x:Bind} в окнах WinUI 3 Window.
/// Не требуют FrameworkElement converter lookup root.
/// </summary>
public static class XamlConverters
{
    private static readonly SolidColorBrush EmeraldBgBrush = new(Color.FromArgb(0x2E, 0x26, 0xA6, 0x5B));
    private static readonly SolidColorBrush EmeraldFgBrush = new(Color.FromArgb(0xFF, 0x36, 0xD3, 0x99));
    private static readonly SolidColorBrush AmberBgBrush = new(Color.FromArgb(0x2E, 0xF5, 0x9E, 0x0B));
    private static readonly SolidColorBrush AmberFgBrush = new(Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
    private static readonly SolidColorBrush GrayBgBrush = new(Color.FromArgb(0x1F, 0x71, 0x71, 0x7A));
    private static readonly SolidColorBrush GrayFgBrush = new(Color.FromArgb(0xFF, 0x71, 0x71, 0x7A));
    private static readonly SolidColorBrush DarkCalendarBgBrush = new(Color.FromArgb(0xFF, 0x14, 0x14, 0x16));
    private static readonly SolidColorBrush SubtleBorderBrush = new(Color.FromArgb(0xFF, 0x27, 0x27, 0x2A));

    public static Visibility ToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToInverseVisibility(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility NotNullToVisibility(object? value) =>
        value != null ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility NullToVisibility(object? value) =>
        value == null ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility StringNotEmptyToVisibility(string? value) =>
        !string.IsNullOrWhiteSpace(value) ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility StringEmptyToVisibility(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Visibility.Visible : Visibility.Collapsed;

    public static TextDecorations ToStrikethrough(bool value) =>
        value ? TextDecorations.Strikethrough : TextDecorations.None;

    public static SolidColorBrush ToStatusBackgroundBrush(string status) =>
        status == "Paused" ? AmberBgBrush : EmeraldBgBrush;

    public static SolidColorBrush ToStatusForegroundBrush(string status) =>
        status == "Paused" ? AmberFgBrush : EmeraldFgBrush;

    public static string ToPlayPauseGlyph(bool isPlaying) =>
        isPlaying ? "\uE769" : "\uE768";

    public static string ToMuteGlyph(bool isMuted) =>
        isMuted ? "\uE74F" : "\uE767";

    public static string ToPercentString(int value) => $"{value}%";

    public static string ToPercentStringFromDouble(double value) => $"{(int)Math.Round(value)}%";

    public static string ToFileSizeString(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    public static string ToGoalIconGlyph(string? iconKey) =>
        iconKey switch
        {
            "Wallet" => "\uE8C7",
            "Briefcase" => "\uE802",
            "Brain" => "\uE8AC",
            "Compass" => "\uE707",
            _ => "\uE707"
        };

    public static string ToMilestoneGlyph(string status) =>
        status switch
        {
            "Completed" => "\uE73E",
            "InProgress" => "\uE768",
            _ => "\uEA3A"
        };

    public static SolidColorBrush ToMilestoneFgBrush(string status) =>
        status switch
        {
            "Completed" => EmeraldFgBrush,
            "InProgress" => AmberFgBrush,
            _ => GrayFgBrush
        };

    public static SolidColorBrush ToMilestoneBgBrush(string status) =>
        status switch
        {
            "Completed" => EmeraldBgBrush,
            "InProgress" => AmberBgBrush,
            _ => GrayBgBrush
        };

    public static SolidColorBrush ToCalendarDayBgBrush(bool isCompleted) =>
        isCompleted ? EmeraldBgBrush : DarkCalendarBgBrush;

    public static SolidColorBrush ToCalendarDayBorderBrush(bool isToday, bool isCompleted) =>
        isToday ? AmberFgBrush : (isCompleted ? EmeraldFgBrush : SubtleBorderBrush);

    public static SolidColorBrush ToCalendarDayFgBrush(bool isCompleted, bool isToday) =>
        (isCompleted || isToday) ? AmberFgBrush : GrayFgBrush;

    public static string FormatDayNumber(int day) => day.ToString();
}
