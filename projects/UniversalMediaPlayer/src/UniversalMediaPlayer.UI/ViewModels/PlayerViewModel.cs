using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.UI.Helpers;

namespace UniversalMediaPlayer.UI.ViewModels;

public sealed partial class PlayerViewModel : ObservableObject
{
    [ObservableProperty]
    private double _currentPosition;

    [ObservableProperty]
    private double _duration;

    [ObservableProperty]
    private string _formattedTimecode = "00:00 / 00:00";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private int _volume = 100;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private string _osdMessage = string.Empty;

    [ObservableProperty]
    private bool _isOsdVisible;

    [ObservableProperty]
    private string _episodeTitle = string.Empty;

    [ObservableProperty]
    private string _packageSummary = string.Empty;

    [ObservableProperty]
    private bool _hasMedia;

    [ObservableProperty]
    private bool _areControlsVisible = true;

    [ObservableProperty]
    private TrackSelectorViewModel _trackSelector = new();

    // Resume prompt properties
    [ObservableProperty]
    private bool _isResumePromptVisible;

    [ObservableProperty]
    private string _resumePromptMessage = string.Empty;

    [ObservableProperty]
    private double _resumePositionSeconds;

    // Auto-next prompt properties
    [ObservableProperty]
    private bool _isAutoNextPromptVisible;

    [ObservableProperty]
    private string _autoNextMessage = string.Empty;

    [ObservableProperty]
    private int _autoNextCountdownSeconds = 5;

    [ObservableProperty]
    private bool _autoNextEnabled = true;

    // Continue watching properties
    [ObservableProperty]
    private bool _hasContinueWatching;

    [ObservableProperty]
    private string _continueWatchingTitle = string.Empty;

    [ObservableProperty]
    private string _continueWatchingDetails = string.Empty;

    [ObservableProperty]
    private string _continueWatchingFilePath = string.Empty;

    [ObservableProperty]
    private double _continueWatchingPosition;

    public void SetContinueWatching(string title, string details, string filePath, double position)
    {
        HasContinueWatching = true;
        ContinueWatchingTitle = title;
        ContinueWatchingDetails = details;
        ContinueWatchingFilePath = filePath;
        ContinueWatchingPosition = position;
    }

    public void ClearContinueWatching()
    {
        HasContinueWatching = false;
        ContinueWatchingTitle = string.Empty;
        ContinueWatchingDetails = string.Empty;
        ContinueWatchingFilePath = string.Empty;
        ContinueWatchingPosition = 0;
    }

    public void ShowResumePrompt(string message, double position)
    {
        IsResumePromptVisible = true;
        ResumePromptMessage = message;
        ResumePositionSeconds = position;
    }

    public void HideResumePrompt()
    {
        IsResumePromptVisible = false;
        ResumePromptMessage = string.Empty;
        ResumePositionSeconds = 0;
    }

    public void ShowAutoNextPrompt(string message, int countdownSeconds = 5)
    {
        IsAutoNextPromptVisible = true;
        AutoNextMessage = message;
        AutoNextCountdownSeconds = countdownSeconds;
    }

    public void HideAutoNextPrompt()
    {
        IsAutoNextPromptVisible = false;
        AutoNextMessage = string.Empty;
        AutoNextCountdownSeconds = 5;
    }

    public void UpdateTime(double position, double duration)
    {
        CurrentPosition = position;
        Duration = duration;
        FormattedTimecode = $"{FormatHelper.FormatTimecode(position)} / {FormatHelper.FormatTimecode(duration)}";
    }

    public void UpdateMediaPackage(MediaPackage package)
    {
        HasMedia = true;
        if (package.Episode != null)
        {
            var s = package.Episode.SeasonNumber.HasValue ? $"S{package.Episode.SeasonNumber:D2}" : "";
            EpisodeTitle = $"{package.Episode.ShowTitle} {s}E{package.Episode.EpisodeNumber:D2}".Trim();
            PackageSummary = $"· 🎬 1 video  🎧 {package.AudioTracks.Count} audio  💬 {package.SubtitleTracks.Count} subs  🔤 {package.Fonts?.Count ?? 0} fonts";
        }
        else
        {
            EpisodeTitle = package.PrimaryVideo.FileName;
            PackageSummary = $"· 🎧 {package.AudioTracks.Count} audio  💬 {package.SubtitleTracks.Count} subs";
        }
    }

    public void ShowOsd(string message)
    {
        OsdMessage = message;
        IsOsdVisible = true;
    }

    public void HideOsd()
    {
        IsOsdVisible = false;
    }
}
