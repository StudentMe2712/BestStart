using PersonalProvocateur.App.Services;
using Xunit;

namespace PersonalProvocateur.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_FirstInstance_ReturnsTrue()
    {
        string uniqueName = $"Local\\Test_Mutex_{Guid.NewGuid():N}";
        string uniqueEvent = $"Local\\Test_Event_{Guid.NewGuid():N}";

        using var guard = new SingleInstanceGuard(uniqueName, uniqueEvent);
        bool acquired = guard.TryAcquire();

        Assert.True(acquired);
    }

    [Fact]
    public void TryAcquire_SecondInstanceWithSameMutex_ReturnsFalse()
    {
        string uniqueName = $"Local\\Test_Mutex_{Guid.NewGuid():N}";
        string uniqueEvent = $"Local\\Test_Event_{Guid.NewGuid():N}";

        using var guard1 = new SingleInstanceGuard(uniqueName, uniqueEvent);
        bool firstAcquired = guard1.TryAcquire();
        Assert.True(firstAcquired);

        using var guard2 = new SingleInstanceGuard(uniqueName, uniqueEvent);
        bool secondAcquired = guard2.TryAcquire();
        Assert.False(secondAcquired);
    }

    [Fact]
    public void Dispose_ReleasesMutex_AllowingSubsequentAcquisition()
    {
        string uniqueName = $"Local\\Test_Mutex_{Guid.NewGuid():N}";
        string uniqueEvent = $"Local\\Test_Event_{Guid.NewGuid():N}";

        var guard1 = new SingleInstanceGuard(uniqueName, uniqueEvent);
        Assert.True(guard1.TryAcquire());

        // Dispose guard1 to release mutex
        guard1.Dispose();

        using var guard2 = new SingleInstanceGuard(uniqueName, uniqueEvent);
        Assert.True(guard2.TryAcquire());
    }

    [Fact]
    public void SignalExistingInstance_WakesUpListeningFirstInstance()
    {
        string uniqueName = $"Local\\Test_Mutex_{Guid.NewGuid():N}";
        string uniqueEvent = $"Local\\Test_Event_{Guid.NewGuid():N}";

        using var guard1 = new SingleInstanceGuard(uniqueName, uniqueEvent);
        Assert.True(guard1.TryAcquire());

        using var wakeUpReceived = new ManualResetEventSlim(false);
        guard1.StartListening(() =>
        {
            wakeUpReceived.Set();
        });

        using var guard2 = new SingleInstanceGuard(uniqueName, uniqueEvent);
        Assert.False(guard2.TryAcquire());

        // Signal from guard2
        guard2.SignalExistingInstance();

        // Should receive signal
        bool received = wakeUpReceived.Wait(TimeSpan.FromSeconds(2));
        Assert.True(received);
    }
}
