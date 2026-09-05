namespace UniversalMediaPlayer.Core.Persistence;

using UniversalMediaPlayer.Core.Models;

public interface IShowPreferencesStore
{
    Task<ShowPreferences?> GetPreferencesAsync(string showId, CancellationToken ct = default);
    Task SavePreferencesAsync(ShowPreferences preferences, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, ShowPreferences>> GetAllPreferencesAsync(CancellationToken ct = default);
}
