using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация сервиса детекции полноэкранных игр и видео (AC-03).
/// Периодически (1000 мс) проверяет состояние через SHQueryUserNotificationState и GetForegroundWindow/MonitorFromWindow.
/// При запуске Direct3D игры или разворачивании видео виджет плавно скрывается за 0-300 мс.
/// </summary>
public sealed class SystemMonitorService : ISystemMonitorService
{
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    private readonly Timer? _timer;
    private readonly object _stateLock = new();
    private bool? _overrideFullscreenStateForTesting;
    private bool _lastReportedState;
    private bool _disposed;
    private int _pollingIntervalMs = 1000;

    public bool IsFullscreenRunning { get; private set; }

    public event Action<bool>? IsFullscreenRunningChanged;

    public int PollingIntervalMs
    {
        get => _pollingIntervalMs;
        set
        {
            _pollingIntervalMs = value;
            if (_timer != null && !_disposed)
            {
                try
                {
                    _timer.Change(0, _pollingIntervalMs);
                }
                catch
                {
                    // Ignore timer configuration errors
                }
            }
        }
    }

    public SystemMonitorService(bool autoStart = true, int pollingIntervalMs = 1000)
    {
        _pollingIntervalMs = pollingIntervalMs > 0 ? pollingIntervalMs : 1000;

        if (autoStart)
        {
            _timer = new Timer(OnTimerTick, null, 0, _pollingIntervalMs);
        }
    }

    public void Start()
    {
        if (_disposed) return;
        _timer?.Change(0, _pollingIntervalMs);
    }

    public void Stop()
    {
        if (_disposed) return;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void SetFullscreenStateForTesting(bool isFullscreen)
    {
        lock (_stateLock)
        {
            _overrideFullscreenStateForTesting = isFullscreen;
        }

        UpdateState(isFullscreen);
    }

    public bool CheckFullscreenState()
    {
        bool isFullscreen = DetermineCurrentFullscreenState();
        UpdateState(isFullscreen);
        return isFullscreen;
    }

    private void OnTimerTick(object? state)
    {
        if (_disposed) return;

        try
        {
            CheckFullscreenState();
        }
        catch
        {
            // Подавляем исключения периодического таймера
        }
    }

    private void UpdateState(bool newState)
    {
        bool stateChanged = false;

        lock (_stateLock)
        {
            if (_lastReportedState != newState)
            {
                _lastReportedState = newState;
                IsFullscreenRunning = newState;
                stateChanged = true;
            }
        }

        if (stateChanged)
        {
            IsFullscreenRunningChanged?.Invoke(newState);
        }
    }

    private bool DetermineCurrentFullscreenState()
    {
        lock (_stateLock)
        {
            if (_overrideFullscreenStateForTesting.HasValue)
            {
                return _overrideFullscreenStateForTesting.Value;
            }
        }

        try
        {
            // 1. Проверка через Shell32 SHQueryUserNotificationState
            int hr = SHQueryUserNotificationState(out var qState);
            if (hr == 0) // S_OK
            {
                if (qState == QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN ||
                    qState == QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY)
                {
                    return true;
                }
            }

            // 2. Fallback: Проверка перекрытия окна переднего плана с размерами текущего монитора
            var fgHwnd = GetForegroundWindow();
            if (fgHwnd == IntPtr.Zero)
            {
                return false;
            }

            var desktopHwnd = GetDesktopWindow();
            var shellHwnd = GetShellWindow();

            if (fgHwnd == desktopHwnd || fgHwnd == shellHwnd)
            {
                return false;
            }

            var sb = new StringBuilder(256);
            GetClassNameW(fgHwnd, sb, sb.Capacity);
            string className = sb.ToString();

            // Исключаем системные окна оболочки рабочего стола
            if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow")
            {
                return false;
            }

            if (GetWindowRect(fgHwnd, out var windowRect))
            {
                var hMonitor = MonitorFromWindow(fgHwnd, MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero)
                {
                    var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfoW(hMonitor, ref mi))
                    {
                        var rcMon = mi.rcMonitor;
                        if (windowRect.Left <= rcMon.Left &&
                            windowRect.Top <= rcMon.Top &&
                            windowRect.Right >= rcMon.Right &&
                            windowRect.Bottom >= rcMon.Bottom)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // В headless среде возвращаем false без сбоев
            return false;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer?.Dispose();
    }

    #region Win32 P/Invoke Definitions

    private enum QUERY_USER_NOTIFICATION_STATE
    {
        QUNS_NOT_PRESENT = 1,
        QUNS_BUSY = 2,
        QUNS_RUNNING_D3D_FULL_SCREEN = 3,
        QUNS_PRESENTATION_MODE = 4,
        QUNS_ACCEPTS_NOTIFICATIONS = 5,
        QUNS_QUIET_TIME = 6,
        QUNS_APP = 7
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("shell32.dll", SetLastError = false)]
    private static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    #endregion
}
