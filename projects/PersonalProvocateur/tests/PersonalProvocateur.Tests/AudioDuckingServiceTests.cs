using System;
using System.Collections.Generic;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.UI;
using Xunit;

namespace PersonalProvocateur.Tests;

public class MockAudioSessionItem : IAudioSessionItem
{
    public uint ProcessId { get; set; }
    public float Volume { get; set; } = 1.0f;
    public bool Muted { get; set; } = false;
    public bool Disposed { get; private set; }

    public float GetMasterVolume() => Volume;
    public void SetMasterVolume(float level) => Volume = level;
    public bool GetMute() => Muted;
    public void SetMute(bool isMuted) => Muted = isMuted;

    public void Dispose()
    {
        Disposed = true;
    }
}

public class AudioDuckingServiceTests
{
    private const uint MyPid = 1000;
    private const uint OtherPid1 = 2000;
    private const uint OtherPid2 = 3000;

    [Fact]
    public void AudioDuckingService_InitialState_IsNotDucked()
    {
        using var service = new AudioDuckingService(currentProcessId: MyPid);
        Assert.False(service.IsDucked);
    }

    [Fact]
    public void DuckOtherApplications_MuteMode_MutesOtherSessionsExcludingSelf()
    {
        var mySession = new MockAudioSessionItem { ProcessId = MyPid, Volume = 0.9f, Muted = false };
        var otherSession1 = new MockAudioSessionItem { ProcessId = OtherPid1, Volume = 0.8f, Muted = false };
        var otherSession2 = new MockAudioSessionItem { ProcessId = OtherPid2, Volume = 0.5f, Muted = true };
        var systemSounds = new MockAudioSessionItem { ProcessId = 0, Volume = 1.0f, Muted = false };

        var sessions = new List<IAudioSessionItem> { mySession, otherSession1, otherSession2, systemSounds };

        using var service = new AudioDuckingService(
            sessionProvider: () => sessions,
            currentProcessId: MyPid);

        service.DuckOtherApplications("Mute");

        Assert.True(service.IsDucked);
        // Our session must remain unmuted
        Assert.False(mySession.Muted);
        // Other sessions must be muted
        Assert.True(otherSession1.Muted);
        Assert.True(otherSession2.Muted);
        Assert.True(systemSounds.Muted);

        // Now restore
        service.RestoreOtherApplications();

        Assert.False(service.IsDucked);
        Assert.False(mySession.Muted);
        Assert.False(otherSession1.Muted); // restored to original false
        Assert.True(otherSession2.Muted);  // restored to original true
        Assert.False(systemSounds.Muted);  // restored to original false
    }

    [Fact]
    public void DuckOtherApplications_DuckMode_ReducesVolumeAndRestores()
    {
        var mySession = new MockAudioSessionItem { ProcessId = MyPid, Volume = 0.9f };
        var otherSession = new MockAudioSessionItem { ProcessId = OtherPid1, Volume = 0.8f };

        var sessions = new List<IAudioSessionItem> { mySession, otherSession };

        using var service = new AudioDuckingService(
            sessionProvider: () => sessions,
            currentProcessId: MyPid);

        service.DuckOtherApplications("Duck", duckLevel: 0.15f);

        Assert.True(service.IsDucked);
        Assert.Equal(0.9f, mySession.Volume, 2); // our app untouched
        Assert.Equal(0.15f, otherSession.Volume, 2); // ducked to 15%

        service.RestoreOtherApplications();

        Assert.False(service.IsDucked);
        Assert.Equal(0.8f, otherSession.Volume, 2); // restored to original 80%
    }

    [Fact]
    public void DuckOtherApplications_PauseMediaMode_TriggersMediaKeyAndMutes()
    {
        int mediaKeyInvocations = 0;
        var otherSession = new MockAudioSessionItem { ProcessId = OtherPid1, Volume = 1.0f, Muted = false };

        using var service = new AudioDuckingService(
            sessionProvider: () => new[] { otherSession },
            mediaPauseSender: () => mediaKeyInvocations++,
            currentProcessId: MyPid);

        service.DuckOtherApplications("PauseMedia");

        Assert.True(service.IsDucked);
        Assert.True(otherSession.Muted);
        Assert.Equal(1, mediaKeyInvocations); // paused

        service.RestoreOtherApplications();

        Assert.False(service.IsDucked);
        Assert.False(otherSession.Muted);
        Assert.Equal(2, mediaKeyInvocations); // resumed
    }

    [Fact]
    public void DuckOtherApplications_DisabledMode_DoesNothing()
    {
        var otherSession = new MockAudioSessionItem { ProcessId = OtherPid1, Volume = 1.0f, Muted = false };

        using var service = new AudioDuckingService(
            sessionProvider: () => new[] { otherSession },
            currentProcessId: MyPid);

        service.DuckOtherApplications("Disabled");

        Assert.False(service.IsDucked);
        Assert.False(otherSession.Muted);
        Assert.Equal(1.0f, otherSession.Volume, 2);
    }

    [Fact]
    public void DuckOtherApplications_Idempotency_DoesNotOverwriteOriginalState()
    {
        var otherSession = new MockAudioSessionItem { ProcessId = OtherPid1, Volume = 0.8f };

        using var service = new AudioDuckingService(
            sessionProvider: () => new[] { otherSession },
            currentProcessId: MyPid);

        service.DuckOtherApplications("Duck", duckLevel: 0.10f);
        Assert.Equal(0.10f, otherSession.Volume, 2);

        // Calling duck again with different level should be ignored to preserve original 0.8
        service.DuckOtherApplications("Duck", duckLevel: 0.50f);
        Assert.Equal(0.10f, otherSession.Volume, 2);

        service.RestoreOtherApplications();
        Assert.Equal(0.8f, otherSession.Volume, 2);
    }

    [Fact]
    public void RestoreOtherApplications_Idempotent_WhenNotDucked()
    {
        using var service = new AudioDuckingService(currentProcessId: MyPid);
        // Calling restore when not ducked should not throw or change state
        service.RestoreOtherApplications();
        Assert.False(service.IsDucked);
    }

    [Fact]
    public void Dispose_RestoresAudioSessions()
    {
        var otherSession = new MockAudioSessionItem { ProcessId = OtherPid1, Muted = false };
        var service = new AudioDuckingService(
            sessionProvider: () => new[] { otherSession },
            currentProcessId: MyPid);

        service.DuckOtherApplications("Mute");
        Assert.True(otherSession.Muted);

        service.Dispose();

        Assert.False(service.IsDucked);
        Assert.False(otherSession.Muted);
        Assert.True(otherSession.Disposed);
    }
}
