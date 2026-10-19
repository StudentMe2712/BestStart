using System.Threading;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Ensures only a single instance of PersonalProvocateur runs at a time.
/// Uses a system-wide Mutex and EventWaitHandle to detect existing instances
/// and signal them to wake up and present the user interface.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    public const string DefaultMutexName = @"Global\PersonalProvocateur_SingleInstance_Mutex";
    public const string DefaultWakeupEventName = @"Global\PersonalProvocateur_Wakeup_Event";

    private readonly string _mutexName;
    private readonly string _wakeupEventName;

    private Mutex? _mutex;
    private EventWaitHandle? _wakeupEvent;
    private readonly ManualResetEvent _shutdownEvent = new(false);
    private Thread? _listenerThread;
    private bool _hasAcquired;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of <see cref="SingleInstanceGuard"/>.
    /// </summary>
    /// <param name="mutexName">System-wide named mutex identifier.</param>
    /// <param name="wakeupEventName">System-wide named event handle identifier.</param>
    public SingleInstanceGuard(
        string mutexName = DefaultMutexName,
        string wakeupEventName = DefaultWakeupEventName)
    {
        _mutexName = mutexName;
        _wakeupEventName = wakeupEventName;
    }

    /// <summary>
    /// Attempts to acquire ownership of the single instance mutex.
    /// Returns true if this is the primary instance; false if another instance is already running.
    /// </summary>
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            if (!createdNew)
            {
                _hasAcquired = false;
                return false;
            }

            _hasAcquired = true;
            _wakeupEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _wakeupEventName);
            return true;
        }
        catch (Exception)
        {
            _hasAcquired = false;
            return false;
        }
    }

    /// <summary>
    /// Signals the already-running primary instance via the wakeup EventWaitHandle.
    /// </summary>
    public void SignalExistingInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(_wakeupEventName, out var handle))
            {
                using (handle)
                {
                    handle.Set();
                }
            }
        }
        catch
        {
            // Silently suppress if unable to signal
        }
    }

    /// <summary>
    /// Starts a background thread listening for wakeup signals from secondary instances.
    /// When signaled, invokes onWakeupRequested on the UI Dispatcher if available.
    /// </summary>
    /// <param name="onWakeupRequested">Action to execute when a wakeup signal arrives.</param>
    public void StartListening(Action onWakeupRequested)
    {
        ArgumentNullException.ThrowIfNull(onWakeupRequested);

        if (!_hasAcquired || _wakeupEvent == null || _isDisposed)
        {
            return;
        }

        if (_listenerThread != null && _listenerThread.IsAlive)
        {
            return;
        }

        _listenerThread = new Thread(() =>
        {
            var handles = new WaitHandle[] { _shutdownEvent, _wakeupEvent };

            while (!_isDisposed)
            {
                try
                {
                    int index = WaitHandle.WaitAny(handles);
                    if (index == 0)
                    {
                        // Shutdown event signaled
                        break;
                    }

                    if (index == 1)
                    {
                        // Wakeup event signaled
                        DispatchToUi(onWakeupRequested);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Continue listening unless disposed
                }
            }
        })
        {
            IsBackground = true,
            Name = "SingleInstanceWakeupListener"
        };

        _listenerThread.Start();
    }

    private static void DispatchToUi(Action action)
    {
        try
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher != null && app.Dispatcher.Thread != null && app.Dispatcher.Thread.IsAlive && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
        catch
        {
            action();
        }
    }

    /// <summary>
    /// Releases the mutex and terminates the background listening thread.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _shutdownEvent.Set();
            if (_listenerThread != null && _listenerThread.IsAlive)
            {
                _listenerThread.Join(500);
            }
        }
        catch
        {
            // Ignore shutdown errors
        }

        if (_hasAcquired && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // Ignore if not owned or already released
            }
        }

        _mutex?.Dispose();
        _wakeupEvent?.Dispose();
        _shutdownEvent.Dispose();
    }
}
