using System.Text.RegularExpressions;

namespace UniversalMediaPlayer.Discovery;

public static class LanguageDetector
{
    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Russian
        { "ru", "ru" }, { "rus", "ru" }, { "russian", "ru" }, { "рус", "ru" }, { "русский", "ru" },
        { "дубляж", "ru" }, { "мво", "ru" }, { "звук", "ru" }, { "russe", "ru" },
        // English
        { "en", "en" }, { "eng", "en" }, { "english", "en" }, { "en-us", "en" }, { "en-gb", "en" },
        // Japanese
        { "ja", "ja" }, { "jp", "ja" }, { "jpn", "ja" }, { "japanese", "ja" }, { "японский", "ja" },
        // German
        { "de", "de" }, { "ger", "deu" }, { "deu", "de" }, { "deutsch", "de" },
        // French
        { "fr", "fr" }, { "fre", "fr" }, { "fra", "fr" }, { "french", "fr" }, { "francais", "fr" },
        // Spanish
        { "es", "es" }, { "spa", "es" }, { "spanish", "es" }, { "espanol", "es" },
        // Italian
        { "it", "it" }, { "ita", "it" }, { "italian", "it" },
        // Chinese
        { "zh", "zh" }, { "chi", "zh" }, { "zho", "zh" }, { "chinese", "zh" },
        // Korean
        { "ko", "ko" }, { "kor", "ko" }, { "korean", "ko" },
        // Ukrainian
        { "uk", "uk" }, { "ukr", "uk" }, { "ukrainian", "uk" }, { "укр", "uk" }
    };

    private static readonly char[] Separators = ['.', '_', '-', ' ', '[', ']', '(', ')'];

    public static string DetectLanguage(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var tokens = nameWithoutExt.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        // Search tokens in reverse order (suffixes usually contain language, e.g. S01E01.RU.ass)
        for (int i = tokens.Length - 1; i >= 0; i--)
        {
            if (LanguageAliases.TryGetValue(tokens[i], out var canonicalLang))
            {
                return canonicalLang;
            }
        }

        return "und";
    }

    public static bool AreLanguagesEqual(string lang1, string lang2)
    {
        var c1 = LanguageAliases.GetValueOrDefault(lang1, lang1).ToLowerInvariant();
        var c2 = LanguageAliases.GetValueOrDefault(lang2, lang2).ToLowerInvariant();
        return c1 == c2 && c1 != "und";
    }
}
