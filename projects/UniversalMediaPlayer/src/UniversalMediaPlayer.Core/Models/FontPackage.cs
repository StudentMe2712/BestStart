namespace UniversalMediaPlayer.Core.Models;

public record FontPackage(string FontsDirectory, IReadOnlyList<string> FontFileNames)
{
    public int Count => FontFileNames.Count;
    public bool HasFonts => FontFileNames.Count > 0;
}
