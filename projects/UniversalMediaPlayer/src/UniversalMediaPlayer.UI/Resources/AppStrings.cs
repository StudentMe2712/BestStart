namespace UniversalMediaPlayer.UI.Resources;

/// <summary>
/// Centralized Russian localization resource dictionary for Universal Media Player.
/// </summary>
public static class AppStrings
{
    // Application
    public const string AppTitle = "Universal Media Player";
    public const string DragDropTitle = "Universal Media Player";
    public const string DragDropSubtitle = "Перетащите видеофайл сюда или нажмите «Открыть»";

    // Playback Actions & Tooltips
    public const string Play = "Воспроизведение";
    public const string Pause = "Пауза";
    public const string Stop = "Остановить";
    public const string NextEpisode = "Следующая серия";
    public const string PrevEpisode = "Предыдущая серия";
    public const string NextEpisodeShortcut = "Следующая серия (PageDown)";
    public const string PrevEpisodeShortcut = "Предыдущая серия (PageUp)";
    public const string PlayPauseShortcut = "Воспроизведение / Пауза (Space)";
    public const string Fullscreen = "Полный экран";
    public const string FullscreenShortcut = "Полный экран (F / Alt+Enter)";
    public const string Windowed = "Оконный режим";
    public const string Volume = "Громкость";
    public const string VolumeShortcut = "Громкость (M - без звука)";
    public const string Muted = "Без звука";
    public const string Unmuted = "Звук включен";

    // Tracks & Flyout
    public const string Tracks = "Треки";
    public const string Audio = "Аудио";
    public const string Subtitles = "Субтитры";
    public const string SubtitlesOff = "Без субтитров";
    public const string NoAudioTracks = "Аудиодорожки не обнаружены";
    public const string NoSubtitleTracks = "Субтитры не обнаружены";
    public const string External = "Внешняя";
    public const string Embedded = "Встроенная";
    public const string Preferred = "Предпочтительно";
    public const string SavedAsPreference = "сохранено в предпочтениях";

    // Open File & Notifications
    public const string Open = "Открыть";
    public const string OpenShortcut = "Открыть файл (Ctrl+O)";
    public const string FileNotFound = "Файл не найден";
    public const string UnableToPlay = "Не удалось воспроизвести файл";

    // Continue Watching / Resume
    public const string ContinueWatchingHeader = "ПРОДОЛЖИТЬ ПРОСМОТР";
    public const string ContinueWatchingButton = "Продолжить просмотр";
    public const string ResumePromptQuestion = "Продолжить просмотр с {0}?";
    public const string ResumeButton = "Продолжить";
    public const string StartFromBeginningButton = "Начать сначала";
    public const string PausedAt = "Остановлено на {0} / {1}";

    // Auto Next
    public const string NextEpisodePrompt = "Следующая серия: {0}";
    public const string PlayingInSeconds = "Воспроизведение через {0} сек...";
    public const string PlayNow = "Смотреть сейчас";
    public const string Cancel = "Отмена";

    /// <summary>
    /// Returns Russian display name for a language code.
    /// </summary>
    public static string GetLanguageNameRu(string langCode)
    {
        return langCode.ToLowerInvariant() switch
        {
            "ru" or "rus" => "Русский",
            "en" or "eng" => "Английский",
            "ja" or "jpn" => "Японский",
            "de" or "ger" or "deu" => "Немецкий",
            "fr" or "fra" or "fre" => "Французский",
            "es" or "spa" => "Испанский",
            "it" or "ita" => "Итальянский",
            "zh" or "chi" or "zho" => "Китайский",
            "ko" or "kor" => "Корейский",
            "uk" or "ukr" => "Украинский",
            "orig" or "original" => "Оригинал",
            _ => string.IsNullOrWhiteSpace(langCode) || langCode == "und" ? "Оригинал" : langCode.ToUpperInvariant()
        };
    }
}