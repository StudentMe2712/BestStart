using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Hooks;

/// <summary>
/// Реализация глобального низкоуровневого хука мыши (WH_MOUSE_LL) в изолированном STA-потоке.
/// Обработка кликов и сырых событий мыши не блокирует операционную систему благодаря немедленному делегированию в ThreadPool.
/// Обеспечивает 100% обратную совместимость с IMouseHookService.
/// </summary>
public sealed class LowLevelMouseHookService : IMouseHookService
{
    private readonly object _syncLock = new();
    private readonly NativeMethods.HookProc _hookProc;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private nint _hookHandle = nint.Zero;
    private ManualResetEventSlim? _initEvent;
    private int _lastWin32Error;
    private bool _isDisposed;

    public event EventHandler<MouseClickEvent>? MouseClicked;
    public event EventHandler<RawMouseEvent>? RawMouseEventReceived;

    public bool IsRunning => _hookHandle != nint.Zero;

    public LowLevelMouseHookService()
    {
        // Предотвращаем сборку делегата сборщиком мусора во время работы хука
        _hookProc = HookCallback;
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
                    Name = "Stepwise.LowLevelMouseHookThread"
                };

                _hookThread.SetApartmentState(ApartmentState.STA);
                _hookThread.Start();

                // Ожидаем успешной установки хука в потоке
                if (!_initEvent.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Таймаут инициализации низкоуровневого хука мыши.");
                }

                if (_hookHandle == nint.Zero)
                {
                    throw new Win32Exception(_lastWin32Error, "Не удалось установить глобальный хук мыши (WH_MOUSE_LL).");
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
            if (_hookThread == null && _hookHandle == nint.Zero && _hookThreadId == 0)
            {
                return;
            }

            // Посылаем WM_QUIT в очередь сообщений выделенного потока для корректного выхода
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

    private void RunHookLoop()
    {
        _hookThreadId = NativeMethods.GetCurrentThreadId();

        // Принудительно создаем очередь сообщений потока перед сигнализацией готовности
        NativeMethods.PeekMessage(out _, nint.Zero, 0, 0, NativeMethods.PM_NOREMOVE);

        var moduleHandle = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _hookProc,
            moduleHandle,
            0
        );

        if (_hookHandle == nint.Zero)
        {
            _lastWin32Error = Marshal.GetLastWin32Error();
        }

        // Сигнализируем вызывающему потоку о завершении установки
        _initEvent?.Set();

        if (_hookHandle == nint.Zero)
        {
            return;
        }

        try
        {
            // Стандартный Win32 Message Loop, необходимый для работы WH_MOUSE_LL
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
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = nint.Zero;
            }
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;
            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            var now = DateTime.UtcNow;

            // Определяем логическое действие клика (для обратной совместимости с MouseClicked)
            ActionType? clickAction = msg switch
            {
                NativeMethods.WM_LBUTTONDOWN => ActionType.LeftClick,
                NativeMethods.WM_RBUTTONDOWN => ActionType.RightClick,
                NativeMethods.WM_MBUTTONDOWN => ActionType.MiddleClick,
                NativeMethods.WM_LBUTTONDBLCLK => ActionType.DoubleLeftClick,
                _ => null
            };

            // Определяем низкоуровневое сырое событие мыши (RawMouseEvent)
            RawMouseEvent? rawEvent = null;
            switch (msg)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                    rawEvent = new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, hookStruct.Pt.X, hookStruct.Pt.Y, 0, now);
                    break;
                case NativeMethods.WM_LBUTTONUP:
                    rawEvent = new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, hookStruct.Pt.X, hookStruct.Pt.Y, 0, now);
                    break;
                case NativeMethods.WM_RBUTTONDOWN:
                    rawEvent = new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Right, hookStruct.Pt.X, hookStruct.Pt.Y, 0, now);
                    break;
                case NativeMethods.WM_RBUTTONUP:
                    rawEvent = new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Right, hookStruct.Pt.X, hookStruct.Pt.Y, 0, now);
                    break;
                case NativeMethods.WM_MBUTTONDOWN:
                    rawEvent = new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Middle, hookStruct.Pt.X, hookStruct.Pt.Y, 0, now);
                    break;
                case NativeMethods.WM_MBUTTONUP:
                    rawEvent = new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Middle, hookStruct.Pt.X, hookStruct.Pt.Y, 0, now);
                    break;
                case NativeMethods.WM_MOUSEMOVE:
                    if (RawMouseEventReceived != null)
                    {
                        rawEvent = new RawMouseEvent(RawMouseEventType.Move, RawMouseButton.None, hookStruct.Pt.X, hookStruct.Pt.Y, 0, now);
                    }
                    break;
                case NativeMethods.WM_MOUSEWHEEL:
                case NativeMethods.WM_MOUSEHWHEEL:
                    short wheelDelta = unchecked((short)((hookStruct.MouseData >> 16) & 0xFFFF));
                    rawEvent = new RawMouseEvent(RawMouseEventType.Wheel, RawMouseButton.None, hookStruct.Pt.X, hookStruct.Pt.Y, wheelDelta, now);
                    break;
            }

            if (clickAction.HasValue || rawEvent.HasValue)
            {
                // ВАЖНО: Асинхронно передаем событие в пул потоков, чтобы ни в коем случае
                // не задерживать поток хука и не вызывать лаг мыши в Windows!
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        if (clickAction.HasValue)
                        {
                            var clickEvent = new MouseClickEvent(
                                hookStruct.Pt.X,
                                hookStruct.Pt.Y,
                                clickAction.Value,
                                now
                            );
                            MouseClicked?.Invoke(this, clickEvent);
                        }

                        if (rawEvent.HasValue)
                        {
                            RawMouseEventReceived?.Invoke(this, rawEvent.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LowLevelMouseHookService] Ошибка в обработчике событий мыши: {ex.Message}");
                    }
                });
            }
        }

        // Немедленно передаем управление следующему хуку в цепочке
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
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
