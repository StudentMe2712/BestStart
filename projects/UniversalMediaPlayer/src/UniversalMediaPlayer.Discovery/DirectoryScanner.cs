using UniversalMediaPlayer.Core.Enums;
using UniversalMediaPlayer.Core.Models;

namespace UniversalMediaPlayer.Discovery;

public static class DirectoryScanner
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mka", "ac3", "eac3", "flac", "aac", "opus", "mp3", "wav", "ogg", "dts", "dtshd"
    };

    private static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ass", "ssa", "srt", "vtt", "sub", "idx"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mkv", "mp4", "avi", "mov", "webm", "wmv", "flv", "ts", "m2ts", "vob", "ogv", "rmvb"
    };

    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ttf", "otf", "woff2", "ttc"
    };

    public static MediaPackage Scan(string videoFilePath)
    {
        var primaryVideo = MediaItem.FromFilePath(videoFilePath);
        var videoDir = Path.GetDirectoryName(primaryVideo.FilePath) ?? Environment.CurrentDirectory;
        var episode = EpisodeParser.Parse(primaryVideo.FileName, new DirectoryInfo(videoDir).Name);

        var audioTracks = new List<AudioTrack>();
        var subtitleTracks = new List<SubtitleTrack>();
        var siblingVideos = new List<MediaItem>();
        FontPackage? fontPackage = null;

        if (!Directory.Exists(videoDir))
        {
            return new MediaPackage
            {
                PrimaryVideo = primaryVideo,
                Episode = episode
            };
        }

        // 1. Scan immediate parent directory
        var allFiles = Directory.GetFiles(videoDir);
        ScanFileList(allFiles, primaryVideo, episode, true, audioTracks, subtitleTracks, siblingVideos);

        // 2. Scan standard subfolders (Subs/, Subtitles/, Audio/, Fonts/)
        var subDirs = Directory.GetDirectories(videoDir);
        foreach (var dir in subDirs)
        {
            var dirName = Path.GetFileName(dir).ToLowerInvariant();
            if (dirName is "subs" or "subtitles" or "sub")
            {
                var subFiles = Directory.GetFiles(dir);
                ScanFileList(subFiles, primaryVideo, episode, false, audioTracks, subtitleTracks, siblingVideos);
            }
            else if (dirName is "audio" or "sound")
            {
                var audioFiles = Directory.GetFiles(dir);
                ScanFileList(audioFiles, primaryVideo, episode, false, audioTracks, subtitleTracks, siblingVideos);
            }
            else if (dirName is "fonts" or "font" or "attachments")
            {
                fontPackage = DiscoverFonts(dir);
            }
        }

        // Check if fonts are in parent directory fonts/ if not found in subfolder
        fontPackage ??= DiscoverFonts(Path.Combine(videoDir, "fonts"));

        return new MediaPackage
        {
            PrimaryVideo = primaryVideo,
            Episode = episode,
            AudioTracks = audioTracks,
            SubtitleTracks = subtitleTracks,
            Fonts = fontPackage,
            SiblingEpisodes = siblingVideos.OrderBy(v => v.FileName).ToList()
        };
    }

    private static void ScanFileList(
        string[] files,
        MediaItem primaryVideo,
        EpisodeInfo? videoEpisode,
        bool isSameDirectory,
        List<AudioTrack> audioTracks,
        List<SubtitleTrack> subtitleTracks,
        List<MediaItem> siblingVideos)
    {
        int audioIdCounter = 100;
        int subIdCounter = 100;

        foreach (var file in files)
        {
            // Skip the primary video itself
            if (string.Equals(file, primaryVideo.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();

            // Audio files
            if (AudioExtensions.Contains(ext))
            {
                var match = MatchEngine.Evaluate(primaryVideo, videoEpisode, file, isSameDirectory);
                if (match.IsAccepted)
                {
                    var lang = LanguageDetector.DetectLanguage(file);
                    audioTracks.Add(new AudioTrack
                    {
                        Id = audioIdCounter++,
                        Title = Path.GetFileNameWithoutExtension(file),
                        Language = lang,
                        Origin = TrackOrigin.External,
                        ExternalFilePath = file,
                        Codec = ext.ToUpperInvariant(),
                        Channels = 2 // Default estimation until demuxed by engine
                    });
                }
            }
            // Subtitle files
            else if (SubtitleExtensions.Contains(ext))
            {
                var match = MatchEngine.Evaluate(primaryVideo, videoEpisode, file, isSameDirectory);
                if (match.IsAccepted)
                {
                    var lang = LanguageDetector.DetectLanguage(file);
                    var format = ext switch
                    {
                        "ass" => SubtitleFormat.ASS,
                        "ssa" => SubtitleFormat.SSA,
                        "srt" => SubtitleFormat.SRT,
                        "vtt" => SubtitleFormat.VTT,
                        "sub" or "idx" => SubtitleFormat.VobSub,
                        _ => SubtitleFormat.Unknown
                    };

                    subtitleTracks.Add(new SubtitleTrack
                    {
                        Id = subIdCounter++,
                        Title = Path.GetFileNameWithoutExtension(file),
                        Language = lang,
                        Origin = TrackOrigin.External,
                        ExternalFilePath = file,
                        Codec = ext.ToUpperInvariant(),
                        Format = format
                    });
                }
            }
            // Sibling videos
            else if (VideoExtensions.Contains(ext) && isSameDirectory)
            {
                siblingVideos.Add(MediaItem.FromFilePath(file));
            }
        }
    }

    private static FontPackage? DiscoverFonts(string fontsDir)
    {
        if (!Directory.Exists(fontsDir)) return null;

        var fontFiles = Directory.GetFiles(fontsDir)
            .Where(f => FontExtensions.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()))
            .Select(Path.GetFileName)
            .Where(f => !string.IsNullOrEmpty(f))
            .ToList();

        return fontFiles.Count > 0
            ? new FontPackage(fontsDir, fontFiles!)
            : null;
    }
}
