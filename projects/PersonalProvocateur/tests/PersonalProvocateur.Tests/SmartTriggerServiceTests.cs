using System.IO;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using Xunit;

namespace PersonalProvocateur.Tests;

public class SmartTriggerServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public SmartTriggerServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pp_smart_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Suppress cleanup exceptions
        }
    }

    private (SettingsStore Store, OverlayCoordinator Coordinator) CreateDependencies(
        string triggerMode = "SmartHybrid",
        bool isPaused = false,
        int graceSeconds = 60,
        int idleMinutes = 12,
        int cooldownMinutes = 10)
    {
        var store = new SettingsStore(_settingsPath);
        store.Update(s =>
        {
            s.TriggerMode = triggerMode;
            s.IsPaused = isPaused;
            s.EntertainmentGraceSeconds = graceSeconds;
            s.IdleThresholdMinutes = idleMinutes;
            s.SmartCooldownMinutes = cooldownMinutes;
            s.TriggerOnEntertainment = true;
            s.TriggerOnIdle = true;
        });

        var scheduler = new RandomReminderScheduler();
        var library = new VideoLibraryService(store, fileExistsChecker: _ => true);
        var coordinator = new OverlayCoordinator(
            scheduler,
            library,
            store,
            overlayWindowFactory: _ => null,
            videoWindowFactory: _ => null,
            dispatcherInvoker: a => a(),
            fileExistsChecker: _ => true,
            screensProvider: () => Array.Empty<System.Windows.Forms.Screen>());

        return (store, coordinator);
    }

    [Theory]
    [InlineData("Смотреть YouTube видео")]
    [InlineData("VKontakte - Моя Страница")]
    [InlineData("Reddit - The front page of the internet - Google Chrome")]
    [InlineData("Twitch - live gaming stream")]
    [InlineData("TikTok - trends")]
    [InlineData("Netflix - Stranger Things")]
    [InlineData("Steam - Community Hub")]
    public void MatchesEntertainmentKeywords_RecognizesEntertainmentTitles(string title)
    {
        var (store, coordinator) = CreateDependencies();
        using var service = new SmartTriggerService(store, coordinator, startTimer: false);

        bool matches = service.MatchesEntertainmentKeywords(title, store.CurrentSettings.EntertainmentKeywords);

        Assert.True(matches, $"Title '{title}' should match entertainment keywords.");
    }

    [Theory]
    [InlineData("Provocateur.sln - Visual Studio")]
    [InlineData("Code - BestStart")]
    [InlineData("Administrator: Windows PowerShell")]
    [InlineData("Inbox - Outlook")]
    [InlineData("Document1 - Word")]
    [InlineData("")]
    [InlineData(null)]
    public void MatchesEntertainmentKeywords_IgnoresWorkTitles(string? title)
    {
        var (store, coordinator) = CreateDependencies();
        using var service = new SmartTriggerService(store, coordinator, startTimer: false);

        bool matches = service.MatchesEntertainmentKeywords(title!, store.CurrentSettings.EntertainmentKeywords);

        Assert.False(matches, $"Title '{title}' should NOT match entertainment keywords.");
    }

    [Fact]
    public void CheckTriggers_AccumulatesSeconds_AndTriggersAfterGraceSeconds()
    {
        var (store, coordinator) = CreateDependencies(graceSeconds: 60);
        string currentTitle = "YouTube - Cute cats";
        var idleTime = TimeSpan.Zero;
        var currentTime = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        int triggerEventCount = 0;
        using var service = new SmartTriggerService(
            store,
            coordinator,
            activeWindowTitleProvider: () => currentTitle,
            idleTimeProvider: () => idleTime,
            utcNowProvider: () => currentTime,
            startTimer: false);

        service.SmartProvocationRequested += (s, e) => triggerEventCount++;

        // 1. Tick 30 seconds
        currentTime = currentTime.AddSeconds(30);
        service.CheckTriggers(30);

        Assert.Equal(30, service.EntertainmentActiveSeconds);
        Assert.Equal(0, triggerEventCount);
        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);

        // 2. Tick another 30 seconds -> total 60s -> should trigger!
        currentTime = currentTime.AddSeconds(30);
        service.CheckTriggers(30);

        Assert.Equal(1, triggerEventCount);
        Assert.Equal(0, service.EntertainmentActiveSeconds);
        Assert.Equal(currentTime, service.LastSmartTriggerUtc);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);
    }

    [Fact]
    public void CheckTriggers_ResetsAccumulatedSeconds_WhenSwitchingToWorkWindow()
    {
        var (store, coordinator) = CreateDependencies(graceSeconds: 60);
        string currentTitle = "YouTube - Video";
        var idleTime = TimeSpan.Zero;
        var currentTime = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        int triggerEventCount = 0;
        using var service = new SmartTriggerService(
            store,
            coordinator,
            activeWindowTitleProvider: () => currentTitle,
            idleTimeProvider: () => idleTime,
            utcNowProvider: () => currentTime,
            startTimer: false);

        service.SmartProvocationRequested += (s, e) => triggerEventCount++;

        // Accumulate 45 seconds on YouTube
        currentTime = currentTime.AddSeconds(45);
        service.CheckTriggers(45);
        Assert.Equal(45, service.EntertainmentActiveSeconds);

        // User switches back to Visual Studio
        currentTitle = "Provocateur.sln - Visual Studio";
        currentTime = currentTime.AddSeconds(5);
        service.CheckTriggers(5);

        // Counter must be reset to 0
        Assert.Equal(0, service.EntertainmentActiveSeconds);
        Assert.Equal(0, triggerEventCount);

        // Switch back to YouTube and spend 40 seconds (less than 60 threshold)
        currentTitle = "YouTube - Another video";
        currentTime = currentTime.AddSeconds(40);
        service.CheckTriggers(40);

        Assert.Equal(40, service.EntertainmentActiveSeconds);
        Assert.Equal(0, triggerEventCount);
    }

    [Fact]
    public void CheckTriggers_TriggersOnIdleThreshold()
    {
        var (store, coordinator) = CreateDependencies(idleMinutes: 10);
        string currentTitle = "Provocateur.sln - Visual Studio"; // Work window
        var idleTime = TimeSpan.FromMinutes(5); // Not yet idle threshold
        var currentTime = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        int triggerEventCount = 0;
        using var service = new SmartTriggerService(
            store,
            coordinator,
            activeWindowTitleProvider: () => currentTitle,
            idleTimeProvider: () => idleTime,
            utcNowProvider: () => currentTime,
            startTimer: false);

        service.SmartProvocationRequested += (s, e) => triggerEventCount++;

        service.CheckTriggers(2);
        Assert.Equal(0, triggerEventCount);

        // Advance idle time to 11 minutes (exceeds 10m threshold)
        idleTime = TimeSpan.FromMinutes(11);
        currentTime = currentTime.AddSeconds(2);
        service.CheckTriggers(2);

        Assert.Equal(1, triggerEventCount);
        Assert.Equal(SessionState.MascotOverlayActive, coordinator.CurrentState);
    }

    [Fact]
    public void CheckTriggers_EnforcesCooldownBetweenTriggers()
    {
        var (store, coordinator) = CreateDependencies(graceSeconds: 30, cooldownMinutes: 10);
        string currentTitle = "YouTube - Stream";
        var idleTime = TimeSpan.Zero;
        var currentTime = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        int triggerEventCount = 0;
        using var service = new SmartTriggerService(
            store,
            coordinator,
            activeWindowTitleProvider: () => currentTitle,
            idleTimeProvider: () => idleTime,
            utcNowProvider: () => currentTime,
            startTimer: false);

        service.SmartProvocationRequested += (s, e) => triggerEventCount++;

        // Trigger #1
        currentTime = currentTime.AddSeconds(30);
        service.CheckTriggers(30);
        Assert.Equal(1, triggerEventCount);

        // Reset coordinator state back to BackgroundIdle so active session check passes
        coordinator.EmergencyDismiss();
        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);

        // 5 minutes later (< 10 minutes cooldown), user spends another 35 seconds on YouTube
        currentTime = currentTime.AddMinutes(5);
        service.CheckTriggers(35);

        // Cooldown suppresses trigger
        Assert.Equal(1, triggerEventCount);

        // 11 minutes after the first trigger (> 10 minutes cooldown)
        currentTime = currentTime.AddMinutes(6);
        service.CheckTriggers(35);

        // Trigger #2 fires!
        Assert.Equal(2, triggerEventCount);
    }

    [Fact]
    public void CheckTriggers_SuppressedWhenRandomTimerMode()
    {
        var (store, coordinator) = CreateDependencies(triggerMode: "RandomTimer", graceSeconds: 10, idleMinutes: 5);
        string currentTitle = "YouTube - Music";
        var idleTime = TimeSpan.FromMinutes(15);
        var currentTime = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        int triggerEventCount = 0;
        using var service = new SmartTriggerService(
            store,
            coordinator,
            activeWindowTitleProvider: () => currentTitle,
            idleTimeProvider: () => idleTime,
            utcNowProvider: () => currentTime,
            startTimer: false);

        service.SmartProvocationRequested += (s, e) => triggerEventCount++;

        service.CheckTriggers(100);

        Assert.Equal(0, triggerEventCount);
        Assert.Equal(SessionState.BackgroundIdle, coordinator.CurrentState);
    }

    [Fact]
    public void CheckTriggers_SuppressedWhenPaused()
    {
        var (store, coordinator) = CreateDependencies(isPaused: true, graceSeconds: 10);
        string currentTitle = "YouTube - Music";
        var idleTime = TimeSpan.FromMinutes(20);
        var currentTime = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        int triggerEventCount = 0;
        using var service = new SmartTriggerService(
            store,
            coordinator,
            activeWindowTitleProvider: () => currentTitle,
            idleTimeProvider: () => idleTime,
            utcNowProvider: () => currentTime,
            startTimer: false);

        service.SmartProvocationRequested += (s, e) => triggerEventCount++;

        service.CheckTriggers(100);

        Assert.Equal(0, triggerEventCount);
    }
}
