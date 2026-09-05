using UniversalMediaPlayer.Discovery;
using Xunit;

namespace UniversalMediaPlayer.Tests;

public class LanguageDetectorTests
{
    [Theory]
    [InlineData("Movie.RU.mka", "ru")]
    [InlineData("Show.S01E01.rus.ass", "ru")]
    [InlineData("Show.S01E01.Russian.srt", "ru")]
    [InlineData("Show.S01E01.Русский.mka", "ru")]
    [InlineData("Show.S01E01.дубляж.mka", "ru")]
    [InlineData("Show.S01E01.en.srt", "en")]
    [InlineData("Show.S01E01.English.vtt", "en")]
    [InlineData("Show.S01E01.ja.mka", "ja")]
    [InlineData("Show.S01E01.Japanese.ass", "ja")]
    [InlineData("Show.S01E01.de.srt", "de")]
    [InlineData("Show.S01E01.fr.srt", "fr")]
    [InlineData("Show.S01E01.es.srt", "es")]
    [InlineData("Show.S01E01.unknown_tag.srt", "und")]
    public void DetectLanguage_ResolvesToCanonicalIsoCode(string fileName, string expectedLang)
    {
        var lang = LanguageDetector.DetectLanguage(fileName);
        Assert.Equal(expectedLang, lang);
    }

    [Theory]
    [InlineData("ru", "Russian", true)]
    [InlineData("RUS", "русский", true)]
    [InlineData("en", "English", true)]
    [InlineData("en", "Russian", false)]
    [InlineData("ja", "en", false)]
    public void AreLanguagesEqual_ComparesAliasesCorrectly(string lang1, string lang2, bool expectedEqual)
    {
        var isEqual = LanguageDetector.AreLanguagesEqual(lang1, lang2);
        Assert.Equal(expectedEqual, isEqual);
    }
}
