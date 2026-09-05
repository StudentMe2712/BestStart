using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Services;

/// <summary>
/// Сервис отслеживания активности окон Windows на базе WinEventHook (EVENT_SYSTEM_FOREGROUND).
/// Полностью управляется событиями операционной системы без циклов активного ожидания (busy polling).
/// Запускается в выделенном STA-потоке со стандартным Win32 Message Loop.
/// </summary>
public sealed class ActiveWindowTracker : IActiveWindowTracker
{
    private readonly object _syncLock = new();
    private readonly object _stateLock = new();
    private readonly NativeMethods.WinEventProc _winEventProc;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private nint _hookHandle = nint.Zero;
    private ManualResetEventSlim? _initEvent;
    private int _lastWin32Error;
    private long _lastWindowHandle;
    private string? _lastWindowTitle;
    private bool _isDisposed;

    public event EventHandler<ActiveWindowInfo>? ActiveWindowChanged;

    public bool IsRunning => _hookHandle != nint.Zero;

    public ActiveWindowTracker()
    {
        // Предотвращаем сборку делегата сборщиком мусора
        _winEventProc = WinEventCallback;
    }

    public void Start()
    {
        lock (_syncLock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (IsRunning)
            {
                return;
            }

            _lastWin32Error = 0;
            _initEvent = new ManualResetEventSlim(false);

            try
            {
                _hookThread = new Thread(RunHookLoop)
                {
                    IsBackground = true,
                    Name = "Stepwise.ActiveWindowTrackerThread"
                };

                _hookThread.SetApartmentState(ApartmentState.STA);
                _hookThread.Start();

                if (!_initEvent.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Таймаут инициализации хука отслеживания активных окон.");
                }

                if (_hookHandle == nint.Zero)
                {
                    throw new Win32Exception(_lastWin32Error, "Не удалось установить WinEventHook (EVENT_SYSTEM_FOREGROUND).");
                }

                // Сразу публикуем информацию о текущем активном окне при запуске
                var currentWindow = GetActiveWindow();
                if (currentWindow != null)
                {
                    lock (_stateLock)
                    {
                        _lastWindowHandle = currentWindow.WindowHandle;
                        _lastWindowTitle = currentWindow.WindowTitle;
                    }

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            ActiveWindowChanged?.Invoke(this, currentWindow);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ActiveWindowTracker] Ошибка при уведомлении о начальном окне: {ex.Message}");
                        }
                    });
                }
            }
            catch
            {
                Stop();
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_syncLock)
        {
            lock (_stateLock)
            {
                _lastWindowHandle = 0;
                _lastWindowTitle = null;
            }

            if (_hookThread == null && _hookHandle == nint.Zero && _hookThreadId == 0)
            {
                return;
            }

            // Посылаем WM_QUIT для штатного выхода из GetMessage
            if (_hookThreadId != 0)
            {
                NativeMethods.PostThreadMessage(_hookThreadId, NativeMethods.WM_QUIT, nint.Zero, nint.Zero);
            }

            if (_hookThread != null && _hookThread.IsAlive)
            {
                _hookThread.Join(TimeSpan.FromSeconds(2));
            }

            _hookThread = null;
            _hookThreadId = 0;
            _hookHandle = nint.Zero;

            _initEvent?.Dispose();
            _initEvent = null;
        }
    }

    public ActiveWindowInfo? GetActiveWindow()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return null;
        }

        return CaptureWindowInfo(hwnd);
    }

    private void RunHookLoop()
    {
        _hookThreadId = NativeMethods.GetCurrentThreadId();

        // Принудительно создаем очередь сообщений потока перед сигнализацией готовности
        NativeMethods.PeekMessage(out _, nint.Zero, 0, 0, NativeMethods.PM_NOREMOVE);

        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            nint.Zero,
            _winEventProc,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT
        );

        if (_hookHandle == nint.Zero)
        {
            _lastWin32Error = Marshal.GetLastWin32Error();
        }

        _initEvent?.Set();

        if (_hookHandle == nint.Zero)
        {
            return;
        }

        try
        {
            while (NativeMethods.GetMessage(out var msg, nint.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        finally
        {
            if (_hookHandle != nint.Zero)
            {
                NativeMethods.UnhookWinEvent(_hookHandle);
                _hookHandle = nint.Zero;
            }
        }
    }

    internal bool TryProcessActiveWindow(ActiveWindowInfo windowInfo)
    {
        lock (_stateLock)
        {
            if (windowInfo.WindowHandle == _lastWindowHandle &&
                string.Equals(windowInfo.WindowTitle, _lastWindowTitle, StringComparison.Ordinal))
            {
                return false;
            }

            _lastWindowHandle = windowInfo.WindowHandle;
            _lastWindowTitle = windowInfo.WindowTitle;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                ActiveWindowChanged?.Invoke(this, windowInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActiveWindowTracker] Ошибка в обработчике ActiveWindowChanged: {ex.Message}");
            }
        });

        return true;
    }

    internal void WinEventCallback(
        nint hWinEventHook,
        uint @event,
        nint hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint dwmsEventTime)
    {
        if (@event == NativeMethods.EVENT_SYSTEM_FOREGROUND && hwnd != nint.Zero)
        {
            var windowInfo = CaptureWindowInfo(hwnd);
            if (windowInfo != null)
            {
                TryProcessActiveWindow(windowInfo);
            }
        }
    }

    internal static ActiveWindowInfo? CaptureWindowInfo(nint hwnd)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return null;
        }

        // Если передан дочерний/вложенный элемент, определяем корневое окно верхнего уровня
        var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        if (root != nint.Zero && NativeMethods.IsWindow(root))
        {
            hwnd = root;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        int processId = (int)pid;
        string processName = "Unknown";

        if (processId > 0)
        {
            try
            {
                using var proc = Process.GetProcessById(processId);
                processName = proc.ProcessName;
            }
            catch
            {
                processName = "Unknown";
            }
        }

        // Заголовок окна
        string title = string.Empty;
        int length = NativeMethods.GetWindowTextLength(hwnd);
        if (length > 0)
        {
            var sb = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            title = sb.ToString();
        }

        // Границы окна
        BoundingBox bounds = BoundingBox.Empty;
        if (NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width > 0 && height > 0)
            {
                bounds = new BoundingBox(rect.Left, rect.Top, width, height);
            }
        }

        return new ActiveWindowInfo(
            WindowHandle: (long)hwnd,
            ProcessId: processId,
            ProcessName: processName,
            WindowTitle: title,
            Bounds: bounds,
            Timestamp: DateTime.UtcNow
        );
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Stop();
    }
}
