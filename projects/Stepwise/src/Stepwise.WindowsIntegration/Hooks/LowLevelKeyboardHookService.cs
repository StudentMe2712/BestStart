using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Hooks;

/// <summary>
/// Реализация глобального низкоуровневого хука клавиатуры (WH_KEYBOARD_LL) в изолированном STA-потоке.
/// Обработка событий не блокирует операционную систему благодаря немедленному делегированию событий в ThreadPool.
/// Корректно определяет раскладку активного окна, клавиши Shift, Ctrl, Alt, AltGr, CapsLock и дедкеи (dead keys).
/// </summary>
public sealed class LowLevelKeyboardHookService : IKeyboardHookService
{
    private const uint DONT_CHANGE_KEY_STATE = 0x04;

    private readonly object _syncLock = new();
    private readonly NativeMethods.HookProc _hookProc;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private nint _hookHandle = nint.Zero;
    private ManualResetEventSlim? _initEvent;
    private int _lastWin32Error;
    private bool _isDisposed;

    public event EventHandler<RawKeyboardEvent>? KeyboardEventReceived;

    public bool IsRunning => _hookHandle != nint.Zero;

    public LowLevelKeyboardHookService()
    {
        // Сохраняем делегат в поле для защиты от сборки мусора во время работы хука
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
                    Name = "Stepwise.LowLevelKeyboardHookThread"
                };

                _hookThread.SetApartmentState(ApartmentState.STA);
                _hookThread.Start();

                // Ожидаем завершения установки хука в выделенном потоке
                if (!_initEvent.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Таймаут инициализации низкоуровневого хука клавиатуры.");
                }

                if (_hookHandle == nint.Zero)
                {
                    throw new Win32Exception(_lastWin32Error, "Не удалось установить глобальный хук клавиатуры (WH_KEYBOARD_LL).");
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

            // Посылаем WM_QUIT в очередь сообщений выделенного потока для штатного выхода из цикла GetMessage
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
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            moduleHandle,
            0
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
            // Стандартный Win32 Message Loop, необходимый для работы WH_KEYBOARD_LL
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
            RawKeyboardEventType? eventType = msg switch
            {
                NativeMethods.WM_KEYDOWN => RawKeyboardEventType.KeyDown,
                NativeMethods.WM_SYSKEYDOWN => RawKeyboardEventType.KeyDown,
                NativeMethods.WM_KEYUP => RawKeyboardEventType.KeyUp,
                NativeMethods.WM_SYSKEYUP => RawKeyboardEventType.KeyUp,
                _ => null
            };

            if (eventType.HasValue)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                var rawEvent = ProcessKeyboardHookData(eventType.Value, hookStruct);

                // Асинхронно передаем событие в пул потоков, чтобы ни в коем случае не задерживать хук
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        KeyboardEventReceived?.Invoke(this, rawEvent);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LowLevelKeyboardHookService] Ошибка в обработчике KeyboardEventReceived: {ex.Message}");
                    }
                });
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    internal static RawKeyboardEvent ProcessKeyboardHookData(
        RawKeyboardEventType eventType,
        NativeMethods.KBDLLHOOKSTRUCT hookStruct
    )
    {
        bool isExtended = (hookStruct.Flags & NativeMethods.LLKHF_EXTENDED) != 0;
        bool isAltDown = (hookStruct.Flags & NativeMethods.LLKHF_ALTDOWN) != 0;

        // Определяем состояние модификаторов по физическому состоянию клавиш
        bool shiftPressed = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0 ||
                            (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LSHIFT) & 0x8000) != 0 ||
                            (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RSHIFT) & 0x8000) != 0;

        bool ctrlPressed = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0 ||
                           (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LCONTROL) & 0x8000) != 0 ||
                           (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RCONTROL) & 0x8000) != 0;

        bool altPressed = isAltDown ||
                          (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0 ||
                          (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LMENU) & 0x8000) != 0 ||
                          (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RMENU) & 0x8000) != 0;

        bool winPressed = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 ||
                          (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

        var modifiers = KeyboardModifiers.None;
        if (shiftPressed) modifiers |= KeyboardModifiers.Shift;
        if (ctrlPressed) modifiers |= KeyboardModifiers.Control;
        if (altPressed) modifiers |= KeyboardModifiers.Alt;
        if (winPressed) modifiers |= KeyboardModifiers.Windows;

        // Получаем раскладку клавиатуры активного окна
        nint fgWindow = NativeMethods.GetForegroundWindow();
        uint threadId = fgWindow != nint.Zero ? NativeMethods.GetWindowThreadProcessId(fgWindow, out _) : 0;
        nint keyboardLayout = NativeMethods.GetKeyboardLayout(threadId);
        if (keyboardLayout == nint.Zero)
        {
            keyboardLayout = NativeMethods.GetKeyboardLayout(0);
        }

        // Подготавливаем 256-байтовое состояние клавиатуры для ToUnicodeEx
        var keyState = new byte[256];
        NativeMethods.GetKeyboardState(keyState);

        // Синхронизируем модификаторы с актуальными аппаратными флагами
        keyState[NativeMethods.VK_SHIFT] = (byte)(shiftPressed ? 0x80 : 0);
        keyState[NativeMethods.VK_CONTROL] = (byte)(ctrlPressed ? 0x80 : 0);
        keyState[NativeMethods.VK_MENU] = (byte)(altPressed ? 0x80 : 0);

        // Правый Alt (AltGr) на европейских клавиатурах генерирует Ctrl+Alt
        bool rightAlt = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RMENU) & 0x8000) != 0;
        if (rightAlt)
        {
            keyState[NativeMethods.VK_CONTROL] = 0x80;
            keyState[NativeMethods.VK_MENU] = 0x80;
            modifiers |= KeyboardModifiers.AltGr;
        }

        // Учитываем CapsLock (младший бит указывает на включенное состояние)
        if ((NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 0x0001) != 0)
        {
            keyState[NativeMethods.VK_CAPITAL] = 0x01;
        }
        else
        {
            keyState[NativeMethods.VK_CAPITAL] = 0x00;
        }

        var (character, isDeadKey) = eventType == RawKeyboardEventType.KeyDown
            ? TranslateKey(hookStruct.VkCode, hookStruct.ScanCode, keyState, keyboardLayout)
            : (null, false);

        return new RawKeyboardEvent(
            EventType: eventType,
            VirtualKey: (int)hookStruct.VkCode,
            ScanCode: (int)hookStruct.ScanCode,
            Modifiers: modifiers,
            Character: character,
            IsDeadKey: isDeadKey,
            IsExtendedKey: isExtended,
            Timestamp: DateTime.UtcNow
        );
    }

    /// <summary>
    /// Преобразует виртуальный код клавиши и скан-код в Unicode-символ с использованием указанной раскладки и таблицы состояний клавиш.
    /// Метод изолирован и доступен для модульного тестирования без использования аппаратных прерываний.
    /// </summary>
    public static (string? Character, bool IsDeadKey) TranslateKey(
        uint vkCode,
        uint scanCode,
        byte[] keyState,
        nint keyboardLayout
    )
    {
        var sb = new StringBuilder(16);
        // Флаг DONT_CHANGE_KEY_STATE (0x04) предотвращает сброс буфера дедкеев в системе при проверке
        int result = NativeMethods.ToUnicodeEx(
            vkCode,
            scanCode,
            keyState,
            sb,
            sb.Capacity,
            DONT_CHANGE_KEY_STATE,
            keyboardLayout
        );

        if (result < 0)
        {
            return (null, true);
        }

        if (result > 0)
        {
            return (sb.ToString(0, result), false);
        }

        return (null, false);
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
