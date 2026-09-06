using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Overlay;

/// <summary>
/// Аргументы события обновления визуальных данных оверлея.
/// </summary>
public sealed class OverlayVisualsEventArgs : EventArgs
{
    public OverlayTargetInfo? Target { get; }
    public CalloutPosition? Callout { get; }
    public bool IsMissingTarget { get; }

    public OverlayVisualsEventArgs(OverlayTargetInfo? target, CalloutPosition? callout, bool isMissingTarget)
    {
        Target = target;
        Callout = callout;
        IsMissingTarget = isMissingTarget;
    }
}

/// <summary>
/// Нативное Win32-окно оверлея рабочего стола на выделенном STA-потоке с собственным циклом сообщений.
/// Реализует платформенный контракт <see cref="IOverlayWindow"/> и <see cref="IDisposable"/>.
/// Обеспечивает клик-сквозной прозрачный полноэкранный режим поверх всех окон (WS_EX_TRANSPARENT, WM_NCHITTEST -> HTTRANSPARENT).
/// </summary>
public class NativeOverlayWindow : IOverlayWindow, IDisposable
{
    public const string OverlayClassName = "StepwiseDesktopOverlayClass";

    private static readonly object s_classLock = new();
    private static int s_activeOverlayCount;
    private static bool s_classRegistered;
    private static readonly NativeMethods.WndProc s_wndProc = StaticWndProc;
    private static readonly ConcurrentDictionary<nint, NativeOverlayWindow> s_windows = new();
    [ThreadStatic] private static NativeOverlayWindow? t_pendingInstance;

    private readonly object _syncLock = new();
    private readonly nint _hInstance;
    private readonly IOverlayRenderer _renderer;

    private Thread? _windowThread;
    private uint _threadId;
    private nint _hwnd = nint.Zero;
    private ManualResetEventSlim? _initEvent;
    private Exception? _initException;
    private int _lastWin32Error;
    private bool _isDisposed;

    /// <summary>
    /// Рендерер графического содержимого оверлея.
    /// </summary>
    public IOverlayRenderer Renderer => _renderer;

    /// <summary>
    /// HWND дескриптор нативного окна оверлея.
    /// </summary>
    public nint Handle => _hwnd;

    /// <summary>
    /// Координата X левого края окна в виртуальном экранном пространстве.
    /// </summary>
    public int VirtualX { get; private set; }

    /// <summary>
    /// Координата Y верхнего края окна в виртуальном экранном пространстве.
    /// </summary>
    public int VirtualY { get; private set; }

    /// <summary>
    /// Ширина окна в виртуальном экранном пространстве.
    /// </summary>
    public int VirtualWidth { get; private set; }

    /// <summary>
    /// Высота окна в виртуальном экранном пространстве.
    /// </summary>
    public int VirtualHeight { get; private set; }

    /// <summary>
    /// Признак видимости окна на экране.
    /// </summary>
    public bool IsWindowVisible
    {
        get
        {
            if (_isDisposed)
            {
                return false;
            }

            var hwnd = _hwnd;
            return hwnd != nint.Zero && NativeMethods.IsWindow(hwnd) && NativeMethods.IsWindowVisible(hwnd);
        }
    }

    /// <summary>
    /// Последняя переданная информация о целевом элементе.
    /// </summary>
    public OverlayTargetInfo? LastTarget { get; private set; }

    /// <summary>
    /// Последняя рассчитанная позиция выноски.
    /// </summary>
    public CalloutPosition? LastCallout { get; private set; }

    /// <summary>
    /// Признак отсутствия целевого элемента на экране при последнем обновлении.
    /// </summary>
    public bool LastIsMissingTarget { get; private set; }

    /// <summary>
    /// Событие обновления визуального состояния оверлея (для связывания с рендерером).
    /// </summary>
    public event EventHandler<OverlayVisualsEventArgs>? VisualsUpdated;

    public NativeOverlayWindow(IOverlayRenderer? renderer = null)
    {
        _renderer = renderer ?? new OverlayRenderer();
        _hInstance = NativeMethods.GetModuleHandle(null);
        _initEvent = new ManualResetEventSlim(false);

        _windowThread = new Thread(RunWindowLoop)
        {
            IsBackground = true,
            Name = "Stepwise.NativeOverlayWindowThread"
        };
        _windowThread.SetApartmentState(ApartmentState.STA);
        _windowThread.Start();

        if (!_initEvent.Wait(TimeSpan.FromSeconds(5)))
        {
            Dispose();
            throw new TimeoutException("Таймаут инициализации нативного окна оверлея.");
        }

        if (_initException != null)
        {
            Dispose();
            throw new Win32Exception(_lastWin32Error, $"Ошибка при инициализации окна оверлея: {_initException.Message}");
        }

        if (_hwnd == nint.Zero)
        {
            Dispose();
            throw new Win32Exception(_lastWin32Error, "Не удалось создать нативное окно оверлея.");
        }
    }

    private void RunWindowLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();

        // Принудительно создаем очередь сообщений потока
        NativeMethods.PeekMessage(out _, nint.Zero, 0, 0, NativeMethods.PM_NOREMOVE);

        try
        {
            EnsureWindowClassRegistered();

            // Инициализация границ по виртуальному экрану (SM_XVIRTUALSCREEN и др.)
            var x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            var y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

            if (width <= 0) width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            if (height <= 0) height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;

            VirtualX = x;
            VirtualY = y;
            VirtualWidth = width;
            VirtualHeight = height;

            // Стили: WS_POPUP, WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
            uint dwExStyle = NativeMethods.WS_EX_LAYERED
                           | NativeMethods.WS_EX_TRANSPARENT
                           | NativeMethods.WS_EX_NOACTIVATE
                           | NativeMethods.WS_EX_TOOLWINDOW
                           | NativeMethods.WS_EX_TOPMOST;

            uint dwStyle = NativeMethods.WS_POPUP;

            t_pendingInstance = this;
            _hwnd = NativeMethods.CreateWindowEx(
                dwExStyle,
                OverlayClassName,
                "Stepwise Desktop Overlay",
                dwStyle,
                x,
                y,
                width,
                height,
                nint.Zero,
                nint.Zero,
                _hInstance,
                nint.Zero
            );
            t_pendingInstance = null;

            if (_hwnd == nint.Zero)
            {
                _lastWin32Error = Marshal.GetLastWin32Error();
            }
            else
            {
                s_windows[_hwnd] = this;
                Interlocked.Increment(ref s_activeOverlayCount);
            }
        }
        catch (Exception ex)
        {
            _initException = ex;
        }
        finally
        {
            _initEvent?.Set();
        }

        if (_hwnd == nint.Zero)
        {
            return;
        }

        try
        {
            // Win32 Message Loop в выделенном STA-потоке
            while (NativeMethods.GetMessage(out var msg, nint.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        finally
        {
            CleanupWindowOnThread();
        }
    }

    private void EnsureWindowClassRegistered()
    {
        lock (s_classLock)
        {
            if (s_classRegistered)
            {
                return;
            }

            var wc = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                style = NativeMethods.CS_HREDRAW | NativeMethods.CS_VREDRAW,
                lpfnWndProc = s_wndProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = _hInstance,
                hIcon = nint.Zero,
                hCursor = nint.Zero,
                hbrBackground = nint.Zero,
                lpszMenuName = null,
                lpszClassName = OverlayClassName,
                hIconSm = nint.Zero
            };

            var atom = NativeMethods.RegisterClassEx(ref wc);
            if (atom == 0)
            {
                var error = Marshal.GetLastWin32Error();
                const int ERROR_CLASS_ALREADY_EXISTS = 1410;
                if (error != ERROR_CLASS_ALREADY_EXISTS)
                {
                    throw new Win32Exception(error, $"Не удалось зарегистрировать класс окна '{OverlayClassName}'.");
                }
            }

            s_classRegistered = true;
        }
    }

    private void CleanupWindowOnThread()
    {
        if (_hwnd != nint.Zero)
        {
            s_windows.TryRemove(_hwnd, out _);

            if (NativeMethods.IsWindow(_hwnd))
            {
                NativeMethods.DestroyWindow(_hwnd);
            }
            _hwnd = nint.Zero;
        }

        lock (s_classLock)
        {
            if (Interlocked.Decrement(ref s_activeOverlayCount) <= 0)
            {
                s_activeOverlayCount = 0;
                if (s_classRegistered)
                {
                    NativeMethods.UnregisterClass(OverlayClassName, _hInstance);
                    s_classRegistered = false;
                }
            }
        }
    }

    private static nint StaticWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (!s_windows.TryGetValue(hWnd, out var window))
        {
            if (t_pendingInstance != null)
            {
                window = t_pendingInstance;
                s_windows[hWnd] = window;
            }
        }

        if (window != null)
        {
            return window.InstanceWndProc(hWnd, msg, wParam, lParam);
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    protected virtual nint InstanceWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_NCHITTEST:
                // Native click-through: возврат HTTRANSPARENT (-1) перенаправляет клики окну под оверлеем
                return NativeMethods.HTTRANSPARENT;

            case NativeMethods.WM_ERASEBKGND:
                // Предотвращаем мерцание при перерисовке
                return (nint)1;

            case NativeMethods.WM_CLOSE:
                NativeMethods.DestroyWindow(hWnd);
                return nint.Zero;

            case NativeMethods.WM_DESTROY:
                s_windows.TryRemove(hWnd, out _);
                NativeMethods.PostQuitMessage(0);
                return nint.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Устанавливает границы окна оверлея в координатах виртуального экрана.
    /// </summary>
    public void SetBounds(int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        VirtualX = x;
        VirtualY = y;
        VirtualWidth = width;
        VirtualHeight = height;

        var hwnd = _hwnd;
        if (hwnd != nint.Zero && NativeMethods.IsWindow(hwnd))
        {
            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                x,
                y,
                width,
                height,
                NativeMethods.SWP_NOACTIVATE
            );
        }
    }

    /// <summary>
    /// Отображает окно оверлея поверх всех окон без передачи фокуса (SW_SHOWNOACTIVATE).
    /// </summary>
    public void ShowWindow()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var hwnd = _hwnd;
        if (hwnd == nint.Zero)
        {
            return;
        }

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW
        );
    }

    /// <summary>
    /// Скрывает окно оверлея (SW_HIDE). Дескриптор окна сохраняется.
    /// </summary>
    public void HideWindow()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var hwnd = _hwnd;
        if (hwnd == nint.Zero)
        {
            return;
        }

        _renderer.Clear(hwnd);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
    }

    /// <summary>
    /// Закрывает окно оверлея и освобождает занятые графические и оконные ресурсы.
    /// </summary>
    public void CloseWindow()
    {
        Dispose();
    }

    /// <summary>
    /// Обновляет визуальное состояние оверлея и отрисовывает его через IOverlayRenderer.
    /// </summary>
    public virtual void UpdateVisuals(OverlayTargetInfo? target, CalloutPosition? callout, bool isMissingTarget)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        LastTarget = target;
        LastCallout = callout;
        LastIsMissingTarget = isMissingTarget;

        var hwnd = _hwnd;
        if (hwnd != nint.Zero && NativeMethods.IsWindow(hwnd))
        {
            _renderer.Render(hwnd, VirtualX, VirtualY, VirtualWidth, VirtualHeight, target, callout, isMissingTarget);
        }

        VisualsUpdated?.Invoke(this, new OverlayVisualsEventArgs(target, callout, isMissingTarget));
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_renderer is IDisposable disposableRenderer)
            {
                disposableRenderer.Dispose();
            }

            var hwnd = _hwnd;
            if (hwnd != nint.Zero && NativeMethods.IsWindow(hwnd))
            {
                NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, nint.Zero, nint.Zero);
            }
            else if (_threadId != 0)
            {
                NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, nint.Zero, nint.Zero);
            }

            if (_windowThread != null && _windowThread.IsAlive)
            {
                if (!_windowThread.Join(TimeSpan.FromSeconds(3)))
                {
                    if (_threadId != 0)
                    {
                        NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, nint.Zero, nint.Zero);
                        _windowThread.Join(TimeSpan.FromSeconds(1));
                    }
                }
            }

            _hwnd = nint.Zero;
            _windowThread = null;
            _threadId = 0;

            _initEvent?.Dispose();
            _initEvent = null;
        }

        GC.SuppressFinalize(this);
    }

    ~NativeOverlayWindow()
    {
        Dispose();
    }
}
