namespace UniversalMediaPlayer.Core.Persistence;

using UniversalMediaPlayer.Core.Models;

public interface IWatchHistoryStore
{
    Task RecordPositionAsync(WatchHistoryItem item, CancellationToken ct = default);
    Task<WatchHistoryItem?> GetLatestByShowIdAsync(string showId, CancellationToken ct = default);
    Task<WatchHistoryItem?> GetByFilePathAsync(string filePath, CancellationToken ct = default);
    Task<IReadOnlyList<WatchHistoryItem>> GetContinueWatchingAsync(int limit = 10, CancellationToken ct = default);
    Task MarkCompletedAsync(string filePath, CancellationToken ct = default);
    Task ClearHistoryAsync(CancellationToken ct = default);
}
