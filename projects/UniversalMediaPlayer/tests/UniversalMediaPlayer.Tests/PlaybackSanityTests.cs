using UniversalMediaPlayer.Playback;
using Xunit;

namespace UniversalMediaPlayer.Tests;

public class PlaybackSanityTests
{
    [Fact]
    public async Task MpvPlaybackEngine_Lifecycle_InitializesAndDisposesCleanly()
    {
        var engine = new MpvPlaybackEngine();
        Assert.False(engine.IsInitialized);

        // 1. Initialize (headless null-vo mode)
        await engine.InitializeAsync(windowHandle: 0);
        Assert.True(engine.IsInitialized);

        // 2. Set and Get property
        await engine.SetPropertyAsync("volume", "75");
        var volume = await engine.GetPropertyAsync("volume");
        Assert.Equal("75.000000", volume);

        // 3. Check pause property
        await engine.PauseAsync();
        var paused = await engine.GetPropertyAsync("pause");
        Assert.Equal("yes", paused);

        await engine.PlayAsync();
        var playing = await engine.GetPropertyAsync("pause");
        Assert.Equal("no", playing);

        // 4. Check fullscreen toggle
        await engine.SetFullscreenAsync(true);
        var fs = await engine.GetPropertyAsync("fullscreen");
        Assert.Equal("yes", fs);

        await engine.ToggleFullscreenAsync();
        fs = await engine.GetPropertyAsync("fullscreen");
        Assert.Equal("no", fs);

        // 5. Dispose cleanly
        await engine.DisposeAsync();
        Assert.False(engine.IsInitialized);
    }
}
