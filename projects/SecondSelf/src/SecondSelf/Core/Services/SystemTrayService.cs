using System;
using System.Runtime.InteropServices;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация системного трея Windows на основе Win32 Shell_NotifyIconW API.
/// Создаёт скрытое окно сообщений для обработки сообщений мыши и команд меню.
/// </summary>
public sealed class SystemTrayService : ISystemTrayService
{
    public const uint CMD_OPEN_DASHBOARD = 1001;
    public const uint CMD_TOGGLE_WIDGET = 1002;
    public const uint CMD_QUICK_ADD_TASK = 1003;
    public const uint CMD_EXIT = 1004;

    public const int HOTKEY_ADD_TASK_ID = 2001;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_N = 0x4E;
    private const uint WM_HOTKEY = 0x0312;

    private const uint TRAY_ICON_ID = 1;
    private const uint WM_NULL = 0x0000;
    private const uint WM_USER = 0x0400;
    public const uint WM_TRAYICON = WM_USER + 101;
    private const uint WM_COMMAND = 0x0111;
    private const uint WM_DESTROY = 0x0002;

    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_LBUTTONDBLCLK = 0x0203;
    public const uint WM_RBUTTONUP = 0x0205;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint NIIF_INFO = 0x00000001;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;

    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;

    private const uint WS_POPUP = 0x80000000;

    public event Action? ShowDashboardRequested;
    public event Action? ToggleWidgetRequested;
    public event Action? QuickAddTaskRequested;
    public event Action? ExitRequested;

    private IntPtr _hwnd = IntPtr.Zero;
    private bool _createdOwnWindow;
    private bool _isTrayIconAdded;
    private bool _disposed;
    private WndProcDelegate? _wndProc;

    public bool IsInitialized { get; private set; }

    public void Initialize(IntPtr? windowHandle = null, string? tooltip = null)
    {
        if (_disposed || IsInitialized)
        {
            return;
        }

        try
        {
            if (windowHandle.HasValue && windowHandle.Value != IntPtr.Zero)
            {
                _hwnd = windowHandle.Value;
                _createdOwnWindow = false;
            }
            else
            {
                _hwnd = CreateHiddenHelperWindow();
                _createdOwnWindow = true;
            }

            if (_hwnd != IntPtr.Zero)
            {
                IntPtr hIcon = LoadDefaultIcon();

                var nid = new NOTIFYICONDATAW
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                    hWnd = _hwnd,
                    uID = TRAY_ICON_ID,
                    uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                    uCallbackMessage = WM_TRAYICON,
                    hIcon = hIcon,
                    szTip = tooltip ?? "Second Self"
                };

                _isTrayIconAdded = Shell_NotifyIconW(NIM_ADD, ref nid);

                // Регистрация глобального шортката Alt+Shift+N
                RegisterHotKey(_hwnd, HOTKEY_ADD_TASK_ID, MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VK_N);
            }

            IsInitialized = true;
        }
        catch
        {
            // В headless/тестовом режиме подавляем сбои P/Invoke
            IsInitialized = false;
        }
    }

    public void ShowNotification(string title, string message)
    {
        if (_disposed || !IsInitialized || _hwnd == IntPtr.Zero || !_isTrayIconAdded)
        {
            return;
        }

        try
        {
            var nid = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _hwnd,
                uID = TRAY_ICON_ID,
                uFlags = NIF_INFO,
                szInfo = message ?? string.Empty,
                szInfoTitle = title ?? string.Empty,
                dwInfoFlags = NIIF_INFO
            };

            Shell_NotifyIconW(NIM_MODIFY, ref nid);
        }
        catch
        {
            // Безголовое окружение
        }
    }

    public void TriggerCommand(uint commandId)
    {
        switch (commandId)
        {
            case CMD_OPEN_DASHBOARD:
                ShowDashboardRequested?.Invoke();
                break;
            case CMD_TOGGLE_WIDGET:
                ToggleWidgetRequested?.Invoke();
                break;
            case CMD_QUICK_ADD_TASK:
                QuickAddTaskRequested?.Invoke();
                break;
            case CMD_EXIT:
                ExitRequested?.Invoke();
                break;
        }
    }

    /// <summary>
    /// Имитирует срабатывание глобального шортката (для безголового тестирования).
    /// </summary>
    public void TriggerHotkey(int hotkeyId)
    {
        if (hotkeyId == HOTKEY_ADD_TASK_ID)
        {
            TriggerCommand(CMD_QUICK_ADD_TASK);
        }
    }

    private IntPtr CreateHiddenHelperWindow()
    {
        try
        {
            _wndProc = TrayWindowProc;
            string className = $"SecondSelf_Tray_MsgWnd_{Guid.NewGuid():N}";
            var hInstance = GetModuleHandleW(null);

            var wndClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = hInstance,
                lpszClassName = className
            };

            ushort atom = RegisterClassExW(ref wndClass);
            if (atom == 0)
            {
                return IntPtr.Zero;
            }

            return CreateWindowExW(
                0,
                className,
                "SecondSelf_TrayWindow",
                WS_POPUP,
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private IntPtr TrayWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            uint eventMsg = (uint)(lParam.ToInt64() & 0xFFFF);
            HandleTrayMessage(eventMsg);
            return IntPtr.Zero;
        }
        else if (msg == WM_COMMAND)
        {
            uint cmdId = (uint)(wParam.ToInt64() & 0xFFFF);
            TriggerCommand(cmdId);
            return IntPtr.Zero;
        }
        else if (msg == WM_HOTKEY)
        {
            int hotkeyId = (int)(wParam.ToInt64() & 0xFFFF);
            TriggerHotkey(hotkeyId);
            return IntPtr.Zero;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void HandleTrayMessage(uint eventMsg)
    {
        switch (eventMsg)
        {
            case WM_RBUTTONUP:
                ShowContextMenu();
                break;

            case WM_LBUTTONDBLCLK:
                ShowDashboardRequested?.Invoke();
                break;

            case WM_LBUTTONUP:
                ToggleWidgetRequested?.Invoke();
                break;
        }
    }

    private void ShowContextMenu()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            SetForegroundWindow(_hwnd);

            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // 1. «Открыть Dashboard» (По умолчанию / Bold)
                AppendMenuW(hMenu, MF_STRING, (nuint)CMD_OPEN_DASHBOARD, "Открыть Dashboard");
                SetMenuDefaultItem(hMenu, CMD_OPEN_DASHBOARD, 0);

                // 2. «Показать / Скрыть виджет»
                AppendMenuW(hMenu, MF_STRING, (nuint)CMD_TOGGLE_WIDGET, "Показать / Скрыть виджет");

                // 3. «Быстрое добавление задачи (Alt+Shift+N)»
                AppendMenuW(hMenu, MF_STRING, (nuint)CMD_QUICK_ADD_TASK, "Быстрое добавление задачи (Alt+Shift+N)");

                // Разделитель
                AppendMenuW(hMenu, MF_SEPARATOR, 0, string.Empty);

                // 4. «Выход»
                AppendMenuW(hMenu, MF_STRING, (nuint)CMD_EXIT, "Выход");

                GetCursorPos(out POINT pt);
                uint selectedCmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, IntPtr.Zero);
                PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

                if (selectedCmd > 0)
                {
                    TriggerCommand(selectedCmd);
                }
            }
            finally
            {
                DestroyMenu(hMenu);
            }
        }
        catch
        {
            // Безголовый запуск
        }
    }

    private static IntPtr LoadDefaultIcon()
    {
        try
        {
            // 32512 = IDI_APPLICATION
            return LoadIconW(IntPtr.Zero, (IntPtr)32512);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_isTrayIconAdded && _hwnd != IntPtr.Zero)
        {
            try
            {
                var nid = new NOTIFYICONDATAW
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                    hWnd = _hwnd,
                    uID = TRAY_ICON_ID
                };
                Shell_NotifyIconW(NIM_DELETE, ref nid);
            }
            catch
            {
                // Игнорируем ошибки удаления при закрытии
            }
            _isTrayIconAdded = false;
        }

        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                UnregisterHotKey(_hwnd, HOTKEY_ADD_TASK_ID);
            }
            catch
            {
                // Игнорируем
            }
        }

        if (_createdOwnWindow && _hwnd != IntPtr.Zero)
        {
            try
            {
                DestroyWindow(_hwnd);
            }
            catch
            {
                // Игнорируем
            }
            _hwnd = IntPtr.Zero;
        }

        IsInitialized = false;
    }

    #region P/Invoke Definitions

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetMenuDefaultItem(IntPtr hMenu, uint uItem, uint fByPos);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMParams);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    #endregion
}
