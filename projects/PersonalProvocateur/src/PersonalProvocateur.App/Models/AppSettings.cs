namespace PersonalProvocateur.App.Models;

/// <summary>
/// Application settings model for Personal Provocateur.
/// </summary>
public class AppSettings
{
    private int _minIntervalMinutes = 5;
    private int _maxIntervalMinutes = 20;
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
    /// Visual mascot skin ("DynamicOrb", "CyberVisor", "CustomImage"). Default: "DynamicOrb".
    /// </summary>
    public string MascotSkin { get; set; } = "DynamicOrb";

    /// <summary>
    /// File path to custom mascot image if MascotSkin is "CustomImage".
    /// </summary>
    public string CustomMascotPath { get; set; } = string.Empty;

    /// <summary>
    /// <summary>
    /// Target monitor index for presenting interactive provocations (default: 0).
    /// </summary>
    public int TargetMonitorIndex { get; set; } = 0;

    /// <summary>
    /// Trigger mode for provocations ("SmartHybrid", "SmartOnly", "RandomTimer"). Default: "SmartHybrid".
    /// </summary>
    public string TriggerMode { get; set; } = "SmartHybrid";

    /// <summary>
    /// Whether to trigger provocation when entertainment applications or sites are detected.
    /// </summary>
    public bool TriggerOnEntertainment { get; set; } = true;

    /// <summary>
    /// Dwell time in seconds on entertainment window before triggering provocation (clamped between 5 and 3600).
    /// </summary>
    public int EntertainmentGraceSeconds { get; set; } = 60;

    /// <summary>
    /// Keywords for detecting entertainment applications or web browser tabs.
    /// </summary>
    public List<string> EntertainmentKeywords { get; set; } = new()
    {
        "YouTube", "TikTok", "Twitch", "Reddit", "VK", "Shorts", "Reels", "Netflix", "Steam"
    };

    /// <summary>
    /// Whether to trigger provocation when the system has been idle.
    /// </summary>
    public bool TriggerOnIdle { get; set; } = true;

    /// <summary>
    /// Threshold of system inactivity/idle in minutes before triggering provocation (clamped between 1 and 120).
    /// </summary>
    public int IdleThresholdMinutes { get; set; } = 12;

    /// <summary>
    /// Minimum cooldown in minutes between consecutive smart triggers (clamped between 1 and 120).
    /// </summary>
    public int SmartCooldownMinutes { get; set; } = 10;

    /// <summary>
    /// User's video library items.
    /// </summary>
    public List<VideoItem> Videos { get; set; } = new();

    /// <summary>
    /// Validates and normalizes all settings values to remain within valid bounds.
    /// </summary>
    public void Sanitize()
    {
        if (TargetMonitorIndex < 0)
        {
            TargetMonitorIndex = 0;
        }

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

        if (MascotSkin != "CyberVisor" && MascotSkin != "CustomImage")
        {
            MascotSkin = "DynamicOrb";
        }

        CustomMascotPath ??= string.Empty;

        if (TriggerMode != "SmartHybrid" && TriggerMode != "SmartOnly" && TriggerMode != "RandomTimer")
        {
            TriggerMode = "SmartHybrid";
        }

        EntertainmentGraceSeconds = Math.Clamp(EntertainmentGraceSeconds, 5, 3600);
        EntertainmentKeywords ??= new() { "YouTube", "TikTok", "Twitch", "Reddit", "VK", "Shorts", "Reels", "Netflix", "Steam" };
        IdleThresholdMinutes = Math.Clamp(IdleThresholdMinutes, 1, 120);
        SmartCooldownMinutes = Math.Clamp(SmartCooldownMinutes, 1, 120);

        Videos ??= new List<VideoItem>();
    }
}
