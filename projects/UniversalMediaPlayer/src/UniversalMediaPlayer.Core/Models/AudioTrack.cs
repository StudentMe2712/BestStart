using UniversalMediaPlayer.Core.Enums;

namespace UniversalMediaPlayer.Core.Models;

public record AudioTrack : MediaTrack
{
    public int Channels { get; init; } = 2;
    public int SampleRate { get; init; } = 48000;

    public string ChannelLayoutDescription => Channels switch
    {
        1 => "1.0 Mono",
        2 => "2.0 Stereo",
        6 => "5.1 Surround",
        8 => "7.1 Surround",
        _ => $"{Channels}.0"
    };

    public string DisplaySummary => 
        $"{Language.ToUpperInvariant()} · {Codec.ToUpperInvariant()} · {ChannelLayoutDescription} · {(IsExternal ? "External" : "Embedded")}";
}
