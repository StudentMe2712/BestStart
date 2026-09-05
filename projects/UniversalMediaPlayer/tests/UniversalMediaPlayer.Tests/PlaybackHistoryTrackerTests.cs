namespace UniversalMediaPlayer.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Core.Services;
using UniversalMediaPlayer.Persistence;
using Xunit;

public sealed class PlaybackHistoryTrackerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public PlaybackHistoryTrackerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "UMP_HistoryTrackerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "history.db");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private (PlaybackHistoryTracker Tracker, SqliteWatchHistoryStore Store) CreateTracker()
    {
        var store = new SqliteWatchHistoryStore(_dbPath);
        var tracker = new PlaybackHistoryTracker(store);
        return (tracker, store);
    }

    [Fact]
    public async Task Throttling_OneHundredMicroUpdatesWithinTwoSeconds_ResultsInOnlyOneSave()
    {
        var (tracker, store) = CreateTracker();
        using (tracker)
        using (store)
        {
            var filePath = @"C:\Media\ThrottlingTest.mkv";
            await tracker.TrackMediaAsync(filePath, "ThrottlingShow", seasonNumber: 1, episodeNumber: 1, duration: 1000.0);

            // Send 100 micro-updates from 0.00s to 1.98s (step = 0.02s)
            for (int i = 0; i < 100; i++)
            {
                double position = i * 0.02;
                await tracker.UpdatePositionAsync(position, 1000.0);
            }

            // Exactly 1 save should have occurred (the initial save on the first update at 0.0s, while 0.02..1.98s are throttled)
            Assert.Equal(1, tracker.SaveCount);

            var recorded = await store.GetByFilePathAsync(filePath);
            Assert.NotNull(recorded);
            Assert.Equal(0.0, recorded.PositionSeconds, precision: 2);
            Assert.False(recorded.Completed);

            // Now advance past 5.0 seconds from last saved position (0.0 + 5.1s)
            await tracker.UpdatePositionAsync(5.1, 1000.0);

            // Now a second save occurs
            Assert.Equal(2, tracker.SaveCount);
            recorded = await store.GetByFilePathAsync(filePath);
            Assert.NotNull(recorded);
            Assert.Equal(5.1, recorded.PositionSeconds, precision: 2);
        }
    }

    [Fact]
    public async Task SignificantSeek_TriggersImmediateSave()
    {
        var (tracker, store) = CreateTracker();
        using (tracker)
        using (store)
        {
            var filePath = @"C:\Media\SeekTest.mkv";
            await tracker.TrackMediaAsync(filePath, "SeekShow", seasonNumber: 1, episodeNumber: 1, duration: 1000.0);

            // Initial update at 0s saves (SaveCount = 1)
            await tracker.UpdatePositionAsync(0.0, 1000.0);
            Assert.Equal(1, tracker.SaveCount);

            // Seek to 350.0s
            await tracker.OnSeekAsync(350.0);

            // Seek forces an immediate save
            Assert.Equal(2, tracker.SaveCount);
            Assert.Equal(350.0, tracker.CurrentPosition, precision: 2);

            var recorded = await store.GetByFilePathAsync(filePath);
            Assert.NotNull(recorded);
            Assert.Equal(350.0, recorded.PositionSeconds, precision: 2);
            Assert.False(recorded.Completed);
        }
    }

    [Fact]
    public async Task Pause_TriggersImmediateSave()
    {
        var (tracker, store) = CreateTracker();
        using (tracker)
        using (store)
        {
            var filePath = @"C:\Media\PauseTest.mkv";
            await tracker.TrackMediaAsync(filePath, "PauseShow", seasonNumber: 1, episodeNumber: 1, duration: 1000.0);

            // First update saves at 0.0 (SaveCount = 1)
            await tracker.UpdatePositionAsync(0.0, 1000.0);
            Assert.Equal(1, tracker.SaveCount);

            // Micro updates advance to 3.2s (delta 3.2 < 5.0s, so throttled)
            await tracker.UpdatePositionAsync(3.2, 1000.0);
            Assert.Equal(1, tracker.SaveCount);

            // User pauses playback
            await tracker.OnPauseAsync();

            // Pause forces immediate save of the uncommitted 3.2s position
            Assert.Equal(2, tracker.SaveCount);
            Assert.Equal(3.2, tracker.CurrentPosition, precision: 2);

            var recorded = await store.GetByFilePathAsync(filePath);
            Assert.NotNull(recorded);
            Assert.Equal(3.2, recorded.PositionSeconds, precision: 2);
            Assert.False(recorded.Completed);
        }
    }

    [Fact]
    public async Task NinetyPercentCompletion_TriggersCompletedFlagAndImmediateSave()
    {
        var (tracker, store) = CreateTracker();
        using (tracker)
        using (store)
        {
            var filePath = @"C:\Media\CompletionTest.mkv";
            double duration = 1000.0;
            await tracker.TrackMediaAsync(filePath, "CompletionShow", seasonNumber: 1, episodeNumber: 1, duration: duration);

            // 89% (890s) - not completed
            await tracker.UpdatePositionAsync(890.0, duration);
            Assert.False(tracker.IsCompleted);

            var recorded89 = await store.GetByFilePathAsync(filePath);
            Assert.NotNull(recorded89);
            Assert.False(recorded89.Completed);

            int currentSaves = tracker.SaveCount;

            // 90% (900s) - should trigger completed status and immediate save
            await tracker.UpdatePositionAsync(900.0, duration);

            Assert.True(tracker.IsCompleted);
            Assert.True(tracker.SaveCount > currentSaves);

            var recorded90 = await store.GetByFilePathAsync(filePath);
            Assert.NotNull(recorded90);
            Assert.True(recorded90.Completed);
            Assert.Equal(900.0, recorded90.PositionSeconds, precision: 2);
        }
    }

    [Fact]
    public async Task OpenA_ThenOpenB_RaceSafety_FileACannotOverwriteB()
    {
        var (tracker, store) = CreateTracker();
        using (tracker)
        using (store)
        {
            var fileA = @"C:\Media\EpisodeA.mkv";
            var fileB = @"C:\Media\EpisodeB.mkv";

            // 1. Open File A and progress to 50.0s
            await tracker.TrackMediaAsync(fileA, "ShowA", seasonNumber: 1, episodeNumber: 1, duration: 1000.0);
            var sessionAId = tracker.CurrentSessionId;
            await tracker.UpdatePositionAsync(50.0, 1000.0);

            Assert.Equal(fileA, tracker.CurrentFilePath);
            Assert.Equal(50.0, tracker.CurrentPosition, precision: 2);

            // 2. Open File B (media change: terminates session A and flushes state for File A)
            await tracker.TrackMediaAsync(fileB, "ShowB", seasonNumber: 1, episodeNumber: 2, duration: 1000.0);
            var sessionBId = tracker.CurrentSessionId;
            Assert.NotEqual(sessionAId, sessionBId);
            Assert.Equal(fileB, tracker.CurrentFilePath);

            // Progress File B to 10.0s
            await tracker.UpdatePositionAsync(10.0, 1000.0);
            Assert.Equal(10.0, tracker.CurrentPosition, precision: 2);

            // 3. Simulate delayed out-of-order events from File A trying to overwrite state
            // Case A: Event carrying File A's path
            await tracker.UpdatePositionAsync(99.0, 1000.0, filePath: fileA);

            // Case B: Event carrying obsolete session A ID
            await tracker.UpdatePositionAsync(88.0, 1000.0, sessionId: sessionAId);

            // Case C: Seek or pause on obsolete session / file
            await tracker.OnPauseAsync(filePath: fileA);
            await tracker.OnSeekAsync(500.0, sessionId: sessionAId);

            // In-memory verification: File B remains unchanged
            Assert.Equal(fileB, tracker.CurrentFilePath);
            Assert.Equal(10.0, tracker.CurrentPosition, precision: 2);

            // In-database verification: File B is still at 10.0s (never overwritten by 99s, 88s, 500s)
            var recordB = await store.GetByFilePathAsync(fileB);
            Assert.NotNull(recordB);
            Assert.Equal(10.0, recordB.PositionSeconds, precision: 2);

            // File A remains at 50.0s (flushed on transition, never corrupted by 99s or 88s)
            var recordA = await store.GetByFilePathAsync(fileA);
            Assert.NotNull(recordA);
            Assert.Equal(50.0, recordA.PositionSeconds, precision: 2);
        }
    }

    [Fact]
    public async Task CompletedItem_ResetsPositionToZero_UponStartOrReopen()
    {
        var (tracker, store) = CreateTracker();
        using (tracker)
        using (store)
        {
            var filePath = @"C:\Media\RewatchEpisode.mkv";

            // Pre-seed database with a completed episode watched at 960s of 1000s
            await store.RecordPositionAsync(new WatchHistoryItem
            {
                ShowId = "RewatchShow",
                SeasonNumber = 1,
                EpisodeNumber = 1,
                FilePath = filePath,
                PositionSeconds = 960.0,
                DurationSeconds = 1000.0,
                Completed = true,
                LastPlayedUtc = DateTime.UtcNow.AddDays(-1)
            });

            // User re-opens this episode
            await tracker.TrackMediaAsync(filePath, "RewatchShow", seasonNumber: 1, episodeNumber: 1, duration: 1000.0);

            // In tracker memory, position must reset to 0
            Assert.Equal(0.0, tracker.CurrentPosition);
            Assert.False(tracker.IsCompleted);

            // In persistent store, position must be reset to 0
            var reloaded = await store.GetByFilePathAsync(filePath);
            Assert.NotNull(reloaded);
            Assert.Equal(0.0, reloaded.PositionSeconds);
            Assert.False(reloaded.Completed);
        }
    }

    [Fact]
    public async Task OnStop_And_OnMediaChanging_FlushesUnsavedProgressImmediately()
    {
        var (tracker, store) = CreateTracker();
        using (tracker)
        using (store)
        {
            var filePath = @"C:\Media\StopTest.mkv";
            await tracker.TrackMediaAsync(filePath, "StopShow", seasonNumber: 1, episodeNumber: 1, duration: 1000.0);

            await tracker.UpdatePositionAsync(0.0, 1000.0);
            Assert.Equal(1, tracker.SaveCount);

            // Advance by 3.5s (throttled)
            await tracker.UpdatePositionAsync(3.5, 1000.0);
            Assert.Equal(1, tracker.SaveCount);

            // Stop playback
            await tracker.OnStopAsync();

            Assert.Equal(2, tracker.SaveCount);
            var record = await store.GetByFilePathAsync(filePath);
            Assert.NotNull(record);
            Assert.Equal(3.5, record.PositionSeconds, precision: 2);

            // Media changing
            await tracker.UpdatePositionAsync(4.5, 1000.0);
            await tracker.OnMediaChangingAsync();

            Assert.Equal(3, tracker.SaveCount);
            record = await store.GetByFilePathAsync(filePath);
            Assert.NotNull(record);
            Assert.Equal(4.5, record.PositionSeconds, precision: 2);
        }
    }
}
