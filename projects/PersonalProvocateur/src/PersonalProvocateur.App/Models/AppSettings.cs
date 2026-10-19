namespace PersonalProvocateur.App.Models;

/// <summary>
/// Application settings model for Personal Provocateur.
/// </summary>
public class AppSettings
{
    private int _minIntervalMinutes = 15;
    private int _maxIntervalMinutes = 45;
    private double _dimmingOpacity = 0.60;
    private double _videoVolume = 0.85;

    /// <summary>
    /// Minimum interval in minutes before random provocation (>= 1 and <= MaxIntervalMinutes).
    /// </summary>
    public int MinIntervalMinutes
    {
        get => _minIntervalMinutes;
        set => _minIntervalMinutes = value;
    }

    /// <summary>
    /// Maximum interval in minutes before random provocation (>= MinIntervalMinutes and <= 480).
    /// </summary>
    public int MaxIntervalMinutes
    {
        get => _maxIntervalMinutes;
        set => _maxIntervalMinutes = value;
    }

    /// <summary>
    /// Overlay dimming opacity (clamped between 0.10 and 0.95).
    /// </summary>
    public double DimmingOpacity
    {
        get => _dimmingOpacity;
        set => _dimmingOpacity = double.IsNaN(value) ? 0.60 : Math.Clamp(value, 0.10, 0.95);
    }

    /// <summary>
    /// Global hotkey string for manual trigger (default: Ctrl+Alt+P).
    /// </summary>
    public string TriggerHotkey { get; set; } = "Ctrl+Alt+P";

    /// <summary>
    /// Global hotkey string for emergency panic close (default: Ctrl+Alt+Shift+P).
    /// </summary>
    public string PanicHotkey { get; set; } = "Ctrl+Alt+Shift+P";

    /// <summary>
    /// Whether application should run on Windows startup.
    /// </summary>
    public bool RunOnStartup { get; set; } = false;

    /// <summary>
    /// Whether random provocation timer is currently paused.
    /// </summary>
    public bool IsPaused { get; set; } = false;

    /// <summary>
    /// Video playback volume (clamped between 0.0 and 1.0).
    /// </summary>
    public double VideoVolume
    {
        get => _videoVolume;
        set => _videoVolume = double.IsNaN(value) ? 0.85 : Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>
    /// User's video library items.
    /// </summary>
    public List<VideoItem> Videos { get; set; } = new();

    /// <summary>
    /// Validates and normalizes all settings values to remain within valid bounds.
    /// </summary>
    public void Sanitize()
    {
        if (MinIntervalMinutes < 1)
        {
            MinIntervalMinutes = 1;
        }

        if (MaxIntervalMinutes > 480)
        {
            MaxIntervalMinutes = 480;
        }

        if (MaxIntervalMinutes < 1)
        {
            MaxIntervalMinutes = 1;
        }

        if (MinIntervalMinutes > MaxIntervalMinutes)
        {
            MinIntervalMinutes = MaxIntervalMinutes;
        }

        DimmingOpacity = double.IsNaN(DimmingOpacity) ? 0.60 : Math.Clamp(DimmingOpacity, 0.10, 0.95);
        VideoVolume = double.IsNaN(VideoVolume) ? 0.85 : Math.Clamp(VideoVolume, 0.0, 1.0);

        if (string.IsNullOrWhiteSpace(TriggerHotkey))
        {
            TriggerHotkey = "Ctrl+Alt+P";
        }

        if (string.IsNullOrWhiteSpace(PanicHotkey))
        {
            PanicHotkey = "Ctrl+Alt+Shift+P";
        }

        Videos ??= new List<VideoItem>();
    }
}
