using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.UI.Helpers;

namespace UniversalMediaPlayer.UI.ViewModels;

public sealed partial class TrackItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _badgeText = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExternal;

    [ObservableProperty]
    private string _language = "und";

    public MediaTrack DomainTrack { get; }

    public TrackItemViewModel(MediaTrack domainTrack)
    {
        DomainTrack = domainTrack;
        Id = domainTrack.Id;
        IsSelected = domainTrack.IsSelected;
        IsExternal = domainTrack.Origin == TrackOrigin.External;
        Language = domainTrack.Language;

        if (domainTrack is AudioTrack audio)
        {
            DisplayText = FormatHelper.FormatAudioTrackLabel(audio);
            BadgeText = audio.Origin == TrackOrigin.External ? "External" : "Embedded";
        }
        else if (domainTrack is SubtitleTrack sub)
        {
            DisplayText = FormatHelper.FormatSubtitleTrackLabel(sub);
            BadgeText = sub.Format != SubtitleFormat.Unknown ? sub.Format.ToString() : "SUB";
        }
    }
}

public sealed partial class TrackSelectorViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<TrackItemViewModel> _audioTracks = [];

    [ObservableProperty]
    private IReadOnlyList<TrackItemViewModel> _subtitleTracks = [];

    [ObservableProperty]
    private bool _isSubtitlesEnabled = true;

    public event Action<int>? AudioTrackSelected;
    public event Action<int>? SubtitleTrackSelected;
    public event Action<bool>? SubtitleVisibilityChanged;

    public void UpdateTracks(IReadOnlyList<MediaTrack> tracks)
    {
        var audios = tracks.OfType<AudioTrack>()
            .Select(a => new TrackItemViewModel(a))
            .ToList();

        var subs = tracks.OfType<SubtitleTrack>()
            .Select(s => new TrackItemViewModel(s))
            .ToList();

        AudioTracks = audios;
        SubtitleTracks = subs;
        IsSubtitlesEnabled = subs.Any(s => s.IsSelected);
    }

    [RelayCommand]
    public void SelectAudio(int trackId)
    {
        foreach (var a in AudioTracks)
        {
            a.IsSelected = a.Id == trackId;
        }
        AudioTrackSelected?.Invoke(trackId);
    }

    [RelayCommand]
    public void SelectSubtitle(int trackId)
    {
        foreach (var s in SubtitleTracks)
        {
            s.IsSelected = s.Id == trackId;
        }
        IsSubtitlesEnabled = true;
        SubtitleTrackSelected?.Invoke(trackId);
        SubtitleVisibilityChanged?.Invoke(true);
    }

    [RelayCommand]
    public void DisableSubtitles()
    {
        foreach (var s in SubtitleTracks)
        {
            s.IsSelected = false;
        }
        IsSubtitlesEnabled = false;
        SubtitleVisibilityChanged?.Invoke(false);
    }
}
