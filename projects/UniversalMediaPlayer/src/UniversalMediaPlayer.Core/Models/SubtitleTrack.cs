using UniversalMediaPlayer.Core.Enums;

namespace UniversalMediaPlayer.Core.Models;

public record SubtitleTrack : MediaTrack
{
    public SubtitleFormat Format { get; init; } = SubtitleFormat.Unknown;
    public bool RequiresFonts => Format is SubtitleFormat.ASS or SubtitleFormat.SSA;

    public string DisplaySummary =>
        $"{Language.ToUpperInvariant()} · {Format} · {(IsExternal ? "External" : "Embedded")}";
}
