using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Hooks;

/// <summary>
/// Реализация глобального низкоуровневого хука мыши (WH_MOUSE_LL) в изолированном STA-потоке.
/// Обработка кликов не блокирует операционную систему благодаря немедленному делегированию событий в ThreadPool.
/// </summary>
public sealed class LowLevelMouseHookService : IMouseHookService
{
    private readonly object _syncLock = new();
    private readonly NativeMethods.HookProc _hookProc;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private nint _hookHandle = nint.Zero;
    private ManualResetEventSlim? _initEvent;
    private bool _isDisposed;

    public event EventHandler<MouseClickEvent>? MouseClicked;

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

            _initEvent = new ManualResetEventSlim(false);

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
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось установить глобальный хук мыши (WH_MOUSE_LL).");
            }
        }
    }

    public void Stop()
    {
        lock (_syncLock)
        {
            if (!IsRunning || _hookThreadId == 0)
            {
                return;
            }

            // Посылаем WM_QUIT в очередь сообщений выделенного потока для корректного выхода
            NativeMethods.PostThreadMessage(_hookThreadId, NativeMethods.WM_QUIT, nint.Zero, nint.Zero);

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

        using (var curProcess = Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            var moduleHandle = NativeMethods.GetModuleHandle(curModule?.ModuleName);
            _hookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL,
                _hookProc,
                moduleHandle,
                0
            );
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
            ActionType? action = msg switch
            {
                NativeMethods.WM_LBUTTONDOWN => ActionType.LeftClick,
                NativeMethods.WM_RBUTTONDOWN => ActionType.RightClick,
                NativeMethods.WM_MBUTTONDOWN => ActionType.MiddleClick,
                NativeMethods.WM_LBUTTONDBLCLK => ActionType.DoubleLeftClick,
                _ => null
            };

            if (action.HasValue)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var clickEvent = new MouseClickEvent(
                    hookStruct.Pt.X,
                    hookStruct.Pt.Y,
                    action.Value,
                    DateTime.UtcNow
                );

                // ВАЖНО: Асинхронно передаем событие в пул потоков, чтобы ни в коем случае
                // не задерживать поток хука и не вызывать лаг мыши в Windows!
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        MouseClicked?.Invoke(this, clickEvent);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка в обработчике MouseClicked: {ex.Message}");
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
