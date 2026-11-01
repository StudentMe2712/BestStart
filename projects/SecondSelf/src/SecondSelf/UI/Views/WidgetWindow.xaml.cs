using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecondSelf.Core.Messages;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;
using DisplayArea = Microsoft.UI.Windowing.DisplayArea;

namespace SecondSelf.UI.Views;

/// <summary>
/// Безрамочное плавающее окно виджета рабочего стола с поддержкой Warm Glassmorphism,
/// Win32 P/Invoke стилей, IVirtualDesktopManager и аппаратных анимаций Composition.
/// </summary>
public sealed partial class WidgetWindow : Window
{
    #region Win32 Constants & P/Invoke Definitions

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const long WS_MINIMIZEBOX = 0x00020000L;
    private const long WS_MAXIMIZEBOX = 0x00010000L;
    private const long WS_POPUP = 0x80000000L;

    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_TOPMOST = 0x00000008L;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;

        public MARGINS(int margin)
        {
            cxLeftWidth = margin;
            cxRightWidth = margin;
            cyTopHeight = margin;
            cyBottomHeight = margin;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_BLURBEHIND
    {
        public const uint DWM_BB_ENABLE = 0x00000001;
        public const uint DWM_BB_BLURREGION = 0x00000002;
        public const uint DWM_BB_TRANSITIONONMAXIMIZED = 0x00000004;

        public uint dwFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fEnable;
        public IntPtr hRgnBlur;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fTransitionOnMaximized;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    private const uint WM_DISPLAYCHANGE = 0x007E;

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("4ce8158b-a8f1-4c5e-a427-ea7a04f39ebb")]
    private interface IVirtualDesktopPinnedStore
    {
        [PreserveSig] int IsViewPinned(IntPtr applicationView, out bool isPinned);
        [PreserveSig] int PinView(IntPtr applicationView);
        [PreserveSig] int UnpinView(IntPtr applicationView);
        [PreserveSig] int IsWindowPinned(IntPtr topLevelWindow, out bool isPinned);
        [PreserveSig] int PinWindow(IntPtr topLevelWindow);
        [PreserveSig] int UnpinWindow(IntPtr topLevelWindow);
    }

    #endregion

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly IWindowStateService _windowStateService;
    private readonly ISystemMonitorService? _systemMonitorService;
    private readonly SubclassProc _subclassProcDelegate;
    private Windows.Media.Playback.MediaPlayer? _mediaPlayer;
    private bool _isTransitioning;

    public WidgetViewModel ViewModel { get; }

    public WidgetWindow(
        WidgetViewModel viewModel,
        IWindowStateService windowStateService,
        ISystemMonitorService? systemMonitorService = null)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
        _systemMonitorService = systemMonitorService;

        this.InitializeComponent();

        // 1. Прозрачный фон
        this.SystemBackdrop = null;

        // 2. Получение дескриптора HWND и AppWindow
        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // 3. Настройка безрамочных P/Invoke стилей
        ConfigureWindowStyles();

        // 4. Попытка закрепления на всех виртуальных рабочих столах (PinWindow)
        TryPinToAllVirtualDesktops();

        // 4.1. Подписка на системные оконные сообщения (WM_DISPLAYCHANGE)
        _subclassProcDelegate = SubclassWindowProc;
        SetWindowSubclass(_hwnd, _subclassProcDelegate, UIntPtr.Zero, IntPtr.Zero);

        // 5. Подписка на изменение режима раскрытия
        ViewModel.ExpandedStateChanged += OnExpandedStateChanged;

        // 6. Подписка на события фокус-плеера Daily Reset
        ViewModel.PlayerStateChanged += OnPlayerStateChanged;
        ViewModel.Player.RequestStartMedia += OnRequestStartMedia;
        ViewModel.Player.RequestPauseMedia += OnRequestPauseMedia;
        ViewModel.Player.RequestResumeMedia += OnRequestResumeMedia;
        ViewModel.Player.PropertyChanged += OnPlayerPropertyChanged;

        // 7. Подписка на деактивацию окна (автосворачивание при потере фокуса)
        this.Activated += OnWindowActivated;

        // 8. Подписка на горячие клавиши (Space = Play/Pause, Esc = Close)
        RootGrid.KeyDown += OnRootGridKeyDown;

        // 8.1. Подписка на автоматическое сворачивание при открытии Personal Space (DEFECT 4)
        WeakReferenceMessenger.Default.Register<CollapseWidgetMessage>(this, (r, m) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.IsExpanded)
                {
                    ViewModel.Collapse();
                }
            });
        });

        // 9. Подписка на изменение свойств ViewModel
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(WidgetViewModel.IsAddingTask) && ViewModel.IsAddingTask)
            {
                FocusInlineInput();
            }
            else if (e.PropertyName == nameof(WidgetViewModel.IsShowingTasks))
            {
                OnShowingTasksChanged(ViewModel.IsShowingTasks);
            }
        };

        // 9.1. Инициализация аппаратных теней WinUI Composition DropShadow (DEFECT 1)
        if (this.Content is FrameworkElement fe)
        {
            fe.Loaded += (s, e) => ApplyAllShadows();
        }

        // 10. Подписка на полноэкранные приложения (игры, видео на весь экран)
        if (_systemMonitorService != null)
        {
            _systemMonitorService.IsFullscreenRunningChanged += OnFullscreenRunningChanged;
        }

        // 11. Начальное позиционирование и загрузка данных
        this.DispatcherQueue.TryEnqueue(async () =>
        {
            await InitializeWindowStateAsync();
            await ViewModel.LoadDataAsync();
        });
    }

    #region Window Initialization & P/Invoke Styling

    private void ConfigureWindowStyles()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
        }

        // Удаление рамок, кнопок сворачивания/разворачивания, установка WS_POPUP
        long style = GetWindowLongPtr(_hwnd, GWL_STYLE).ToInt64();
        style &= ~WS_CAPTION;
        style &= ~WS_THICKFRAME;
        style &= ~WS_MINIMIZEBOX;
        style &= ~WS_MAXIMIZEBOX;
        style |= WS_POPUP;
        SetWindowLongPtr(_hwnd, GWL_STYLE, new IntPtr(style));

        // Добавление WS_EX_TOOLWINDOW (исключение из Alt+Tab) и WS_EX_TOPMOST
        long exStyle = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle |= WS_EX_TOPMOST;
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(exStyle));

        // Применение изменений стилей к неклиентской области
        SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

        // Включение прозрачности DWM для всего окна
        var blurBehind = new DWM_BLURBEHIND
        {
            dwFlags = 0x1,
            fEnable = true,
            hRgnBlur = IntPtr.Zero,
            fTransitionOnMaximized = false
        };
        DwmEnableBlurBehindWindow(_hwnd, ref blurBehind);

        var margins = new MARGINS(-1);
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        try
        {
            var backdropHolder = this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>();
            if (backdropHolder != null)
            {
                var compositor = new Windows.UI.Composition.Compositor();
                backdropHolder.SystemBackdrop = compositor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            }
        }
        catch
        {
            // Подавляем в headless/mock средах
        }
    }

    private void TryPinToAllVirtualDesktops()
    {
        try
        {
            var pinnedStoreType = Type.GetTypeFromCLSID(new Guid("b5a50a17-29be-4734-a170-b7ed5e884263"));
            if (pinnedStoreType != null)
            {
                var store = (IVirtualDesktopPinnedStore?)Activator.CreateInstance(pinnedStoreType);
                if (store != null)
                {
                    store.PinWindow(_hwnd);
                }
            }
        }
        catch
        {
            // Игнорируем в средах без поддержки COM или в тестах
        }
    }

    private async Task InitializeWindowStateAsync()
    {
        var state = await _windowStateService.LoadStateAsync();
        var dpiScale = GetDpiScale();

        int targetWidthDip = state.IsExpanded ? 400 : 180;
        int targetHeightDip = state.IsExpanded ? 260 : 36;

        int physicalWidth = (int)Math.Round(targetWidthDip * dpiScale);
        int physicalHeight = (int)Math.Round(targetHeightDip * dpiScale);

        // Получаем рабочую область активного монитора (Top-Center Island)
        var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        int targetX = workArea.X + (workArea.Width - physicalWidth) / 2;
        int targetY = workArea.Y;

        try
        {
            var monitors = DisplayArea.FindAll();
            if (monitors != null && monitors.Count > 0)
            {
                var safePos = _windowStateService.EnsureSafePosition(
                    new WindowPosition(targetX, targetY, physicalWidth, physicalHeight),
                    monitors);
                targetX = safePos.X;
                targetY = workArea.Y;
            }
        }
        catch
        {
            // Безопасный фоллбэк при недоступности API мониторов
        }

        _appWindow.MoveAndResize(new RectInt32(targetX, targetY, physicalWidth, physicalHeight));

        if (state.IsExpanded)
        {
            WidgetCanvas.Width = 400;
            WidgetCanvas.Height = 260;
            CompactTrigger.Visibility = Visibility.Collapsed;
            CompactTriggerShadowHost.Visibility = Visibility.Collapsed;
            WidgetCanvas.Visibility = Visibility.Visible;
            CompactView.Opacity = 1.0;
            CompactView.Visibility = Visibility.Visible;
            ExpandedRoot.Visibility = Visibility.Collapsed;
            PlayerRoot.Visibility = Visibility.Collapsed;
        }
        else
        {
            CompactTrigger.Visibility = Visibility.Visible;
            CompactTriggerShadowHost.Visibility = Visibility.Visible;
            WidgetCanvas.Visibility = Visibility.Collapsed;
            CompactView.Opacity = 1.0;
            CompactView.Visibility = Visibility.Visible;
            ExpandedRoot.Visibility = Visibility.Collapsed;
            PlayerRoot.Visibility = Visibility.Collapsed;
        }
    }

    private double GetDpiScale()
    {
        uint dpi = GetDpiForWindow(_hwnd);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    private IntPtr SubclassWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_DISPLAYCHANGE)
        {
            HandleDisplayChange();
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    /// <summary>
    /// Обрабатывает изменение конфигурации дисплеев (WM_DISPLAYCHANGE или отключение монитора).
    /// Гарантирует, что виджет не останется за пределами экрана на отключенном дисплее.
    /// </summary>
    public void HandleDisplayChange()
    {
        try
        {
            var monitors = DisplayArea.FindAll();
            if (monitors != null && monitors.Count > 0)
            {
                var curPos = _appWindow.Position;
                var curSize = _appWindow.Size;
                var currentPos = new WindowPosition(curPos.X, curPos.Y, curSize.Width, curSize.Height);
                var safePos = _windowStateService.EnsureSafePosition(currentPos, monitors);
                if (safePos.X != curPos.X || safePos.Y != curPos.Y)
                {
                    _appWindow.Move(new PointInt32(safePos.X, safePos.Y));
                    SaveCurrentWindowPosition();
                }
            }
        }
        catch
        {
            // Безопасная обработка в тестовых и безголовых средах
        }
    }

    #endregion

    #region Mouse Dragging & Focus Loss Handling

    public void DragWindow()
    {
        ReleaseCapture();
        SendMessage(_hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        SaveCurrentWindowPosition();
    }

    private void OnWindowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootGrid);
        if (point.Properties.IsLeftButtonPressed)
        {
            DragWindow();
            e.Handled = true;
        }
    }

    private void OnCardPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as UIElement);
        if (point.Properties.IsLeftButtonPressed)
        {
            ReleaseCapture();
            SendMessage(_hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            SaveCurrentWindowPosition();
            e.Handled = true;
        }
    }

    private void OnInteractivePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnCompactCapsulePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ViewModel.OpenTasks();
        e.Handled = true;
    }

    private void OnCompactExpandButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenTasks();
    }

    private void OnBackToHomeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.BackToHome();
    }

    private void OnTriggerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as UIElement);
        if (point.Properties.IsLeftButtonPressed)
        {
            ViewModel.Expand();
            e.Handled = true;
        }
    }

    private void OnTriggerPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (CompactTrigger != null)
        {
            CompactTrigger.Opacity = 0.85;
        }
    }

    private void OnTriggerPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (CompactTrigger != null)
        {
            CompactTrigger.Opacity = 1.0;
        }
    }

    private void ApplyCardShadow(FrameworkElement? hostElement)
    {
        if (hostElement == null) return;
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(hostElement);
            var compositor = visual.Compositor;

            var dropShadow = compositor.CreateDropShadow();
            dropShadow.BlurRadius = 40.0f;
            dropShadow.Offset = new System.Numerics.Vector3(0f, 16f, 0f);
            dropShadow.Color = Windows.UI.Color.FromArgb(128, 0, 0, 0); // rgba(0,0,0,0.5)

            var spriteVisual = compositor.CreateSpriteVisual();
            spriteVisual.Shadow = dropShadow;

            var bindSize = compositor.CreateExpressionAnimation("visual.Size");
            bindSize.SetReferenceParameter("visual", visual);
            spriteVisual.StartAnimation("Size", bindSize);

            ElementCompositionPreview.SetElementChildVisual(hostElement, spriteVisual);
        }
        catch
        {
            // Подавляем в headless/тестовой среде
        }
    }

    private void ApplyAllShadows()
    {
        ApplyCardShadow(TodaysFocusShadowHost);
        ApplyCardShadow(DailyResetShadowHost);
        ApplyCardShadow(QuickHubShadowHost);
        ApplyCardShadow(ExpandedShadowHost);
        ApplyCardShadow(PlayerShadowHost);
        ApplyCardShadow(CompactTriggerShadowHost);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (ViewModel.IsExpanded)
            {
                ViewModel.Collapse();
            }
        }
    }

    private void SaveCurrentWindowPosition()
    {
        var pos = _appWindow.Position;
        var size = _appWindow.Size;
        var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var (normX, normY) = _windowStateService.CalculateNormalizedCoordinates(
            pos.X,
            pos.Y,
            size.Width,
            size.Height,
            workArea.X,
            workArea.Y,
            workArea.Width,
            workArea.Height);

        _ = Task.Run(async () =>
        {
            try
            {
                var currentState = await _windowStateService.LoadStateAsync();
                var updated = currentState with
                {
                    NormalizedX = normX,
                    NormalizedY = normY,
                    DpiScale = GetDpiScale(),
                    IsExpanded = ViewModel.IsExpanded
                };
                await _windowStateService.SaveStateAsync(updated);
            }
            catch
            {
                // Игнорируем ошибки фонового сохранения позиции
            }
        });
    }

    #endregion

    #region Composition Transition Animations (Spring Out 280ms / 240ms)

    private void OnExpandedStateChanged(bool isExpanded)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        var dpiScale = GetDpiScale();
        var currentPos = _appWindow.Position;

        if (isExpanded)
        {
            // ПРАВИЛО ЧИСТОГО СТАРТА (Home Screen Always):
            // ВСЕГДА гарантируем, что пользователь видит Главный экран с 3 карточками
            ViewModel.IsShowingTasks = false;
            ViewModel.IsPlayerOpen = false;
            ViewModel.IsAddingTask = false;

            int triggerWidthPhysical = (int)Math.Round(180.0 * dpiScale);
            int newPhysicalWidth = (int)Math.Round(400.0 * dpiScale);
            int newPhysicalHeight = (int)Math.Round(260.0 * dpiScale);

            int deltaX = (newPhysicalWidth - triggerWidthPhysical) / 2;
            int newX = currentPos.X - deltaX;

            var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
            int topY = displayArea?.WorkArea.Y ?? 0;
            if (displayArea != null)
            {
                if (newX < displayArea.WorkArea.X)
                {
                    newX = displayArea.WorkArea.X;
                }
                else if (newX + newPhysicalWidth > displayArea.WorkArea.X + displayArea.WorkArea.Width)
                {
                    newX = displayArea.WorkArea.X + displayArea.WorkArea.Width - newPhysicalWidth;
                }
            }

            _appWindow.MoveAndResize(new RectInt32(newX, topY, newPhysicalWidth, newPhysicalHeight));

            CompactTrigger.Visibility = Visibility.Collapsed;
            CompactTriggerShadowHost.Visibility = Visibility.Collapsed;

            WidgetCanvas.Width = 400;
            WidgetCanvas.Height = 260;
            CompactView.Opacity = 1.0;
            CompactView.Visibility = Visibility.Visible;
            ExpandedRoot.Visibility = Visibility.Collapsed;
            PlayerRoot.Visibility = Visibility.Collapsed;
            WidgetCanvas.Visibility = Visibility.Visible;

            ExpandStoryboard.Begin();
        }
        else
        {
            StopAndCleanupMediaPlayer();
            CollapseStoryboard.Begin();
        }
    }

    private void OnExpandStoryboardCompleted(object? sender, object e)
    {
        _isTransitioning = false;
        SaveCurrentWindowPosition();
        App.TrimWorkingSet();
    }

    private void OnCollapseStoryboardCompleted(object? sender, object e)
    {
        WidgetCanvas.Visibility = Visibility.Collapsed;
        CompactTrigger.Visibility = Visibility.Visible;
        CompactTriggerShadowHost.Visibility = Visibility.Visible;

        CompactView.Visibility = Visibility.Visible;
        CompactView.Opacity = 1.0;
        ExpandedRoot.Visibility = Visibility.Collapsed;
        ExpandedRoot.Opacity = 0.0;
        PlayerRoot.Visibility = Visibility.Collapsed;
        PlayerRoot.Opacity = 0.0;

        var dpiScale = GetDpiScale();
        var currentPos = _appWindow.Position;
        int currentPhysicalWidth = _appWindow.Size.Width;
        int triggerWidthPhysical = (int)Math.Round(180.0 * dpiScale);
        int triggerHeight = (int)Math.Round(36.0 * dpiScale);

        int deltaX = (currentPhysicalWidth - triggerWidthPhysical) / 2;
        int newX = currentPos.X + deltaX;

        var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
        int topY = displayArea?.WorkArea.Y ?? 0;

        _appWindow.MoveAndResize(new RectInt32(newX, topY, triggerWidthPhysical, triggerHeight));

        _isTransitioning = false;
        SaveCurrentWindowPosition();
        App.TrimWorkingSet();
    }

    private void OnShowingTasksChanged(bool isShowing)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        var dpiScale = GetDpiScale();
        var currentPos = _appWindow.Position;

        if (isShowing)
        {
            int deltaWidthPhysical = (int)Math.Round(10.0 * dpiScale);
            int newPhysicalWidth = (int)Math.Round(380.0 * dpiScale);
            int newPhysicalHeight = (int)Math.Round(680.0 * dpiScale);
            int newX = currentPos.X + deltaWidthPhysical;

            var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
            int topY = displayArea?.WorkArea.Y ?? 0;

            _appWindow.MoveAndResize(new RectInt32(newX, topY, newPhysicalWidth, newPhysicalHeight));

            WidgetCanvas.Width = 380;
            WidgetCanvas.Height = 680;
            ExpandedRoot.Visibility = Visibility.Visible;
            CompactView.Visibility = Visibility.Visible;

            OpenTasksStoryboard.Begin();
        }
        else
        {
            CompactView.Visibility = Visibility.Visible;
            CloseTasksStoryboard.Begin();
        }
    }

    private void OnOpenTasksStoryboardCompleted(object? sender, object e)
    {
        CompactView.Visibility = Visibility.Collapsed;
        _isTransitioning = false;
        SaveCurrentWindowPosition();
        App.TrimWorkingSet();
    }

    private void OnCloseTasksStoryboardCompleted(object? sender, object e)
    {
        ExpandedRoot.Visibility = Visibility.Collapsed;
        var dpiScale = GetDpiScale();
        var currentPos = _appWindow.Position;

        int deltaWidthPhysical = (int)Math.Round(10.0 * dpiScale);
        int newPhysicalWidth = (int)Math.Round(400.0 * dpiScale);
        int newPhysicalHeight = (int)Math.Round(260.0 * dpiScale);
        int newX = currentPos.X - deltaWidthPhysical;

        var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
        int topY = displayArea?.WorkArea.Y ?? 0;

        _appWindow.MoveAndResize(new RectInt32(newX, topY, newPhysicalWidth, newPhysicalHeight));

        WidgetCanvas.Width = 400;
        WidgetCanvas.Height = 260;

        _isTransitioning = false;
        SaveCurrentWindowPosition();
        App.TrimWorkingSet();
    }

    #endregion

    #region UI Event Handlers & Interactions

    private void OnCardClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.Expand();
    }

    private void OnCompactAddButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Expand();
        ViewModel.StartAddingTask();
        FocusInlineInput();
    }

    private void OnOpenDashboardClick(object sender, RoutedEventArgs e)
    {
        App.OpenDashboard();
    }

    private void OnExpandedAddButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.StartAddingTask();
        FocusInlineInput();
    }

    private void FocusInlineInput()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            InlineTaskInputBox?.Focus(FocusState.Programmatic);
        });
    }

    private async void OnSubmitNewTaskClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.AddTaskAsync();
    }

    private void OnCancelNewTaskClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelAddingTask();
    }

    private async void OnInlineTaskTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.AddTaskAsync();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            ViewModel.CancelAddingTask();
            e.Handled = true;
        }
    }

    private async void OnTaskCheckboxClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            await ViewModel.CompleteTaskAsync(item);
        }
    }

    private void OnTaskRowTextPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            ViewModel.ToggleTaskActionPanel(item);
            e.Handled = true;
        }
    }

    private void OnTaskMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            ViewModel.ToggleTaskActionPanel(item);
        }
    }

    private async void OnTaskActionCheckClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            await ViewModel.CompleteTaskAsync(item);
        }
    }

    private void OnTaskActionEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            ViewModel.StartEditTask(item);
        }
    }

    private async void OnTaskActionTrashClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            await ViewModel.DeleteTaskAsync(item);
        }
    }

    private async void OnTaskInlineEditKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await ViewModel.SaveEditTaskAsync(item);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ViewModel.CancelEditTask(item);
                e.Handled = true;
            }
        }
    }

    private void OnTrashPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0xF8, 0x71, 0x71));
        }
    }

    private void OnTrashPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF));
        }
    }

    private void OnUndoToastClick(object sender, RoutedEventArgs e)
    {
        ViewModel.UndoLastTaskCompletion();
    }

    private void OnFullscreenRunningChanged(bool isFullscreen)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isFullscreen)
            {
                // Скрываем виджет при переходе в Fullscreen
                WidgetCanvas.Opacity = 0.0;
                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = false;
                }
            }
            else
            {
                // Плавно восстанавливаем виджет за 300 мс при выходе из Fullscreen
                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                }
                FullscreenRestoreStoryboard.Begin();
            }
        });
    }

    private void OnCollapseChevronClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Collapse();
    }

    private void ShowFallbackAtmosphereView()
    {
        if (DailyResetMediaPlayerElement != null)
        {
            DailyResetMediaPlayerElement.Visibility = Visibility.Collapsed;
        }
        if (FallbackAtmosphereBorder != null)
        {
            FallbackAtmosphereBorder.Visibility = Visibility.Visible;
        }
    }

    private void ShowPlaybackErrorState()
    {
        ShowFallbackAtmosphereView();
    }

    private void OnRequestStartMedia(string filePath, int startPositionMs)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            string? targetFilePath = null;
            if (!string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath))
            {
                targetFilePath = filePath;
            }
            else
            {
                string defaultPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Videos", "default.mp4");
                if (System.IO.File.Exists(defaultPath))
                {
                    targetFilePath = defaultPath;
                }
            }

            if (targetFilePath == null)
            {
                // Если ни одного файла нет: НИКОГДА не инициализируй плеер с битым URI! Мгновенно вызови ShowFallbackAtmosphereView()
                ShowFallbackAtmosphereView();
                _ = ViewModel.Player.OnMediaFailedAsync();
                return;
            }

            try
            {
                if (_mediaPlayer == null)
                {
                    _mediaPlayer = new Windows.Media.Playback.MediaPlayer
                    {
                        AudioCategory = Windows.Media.Playback.MediaPlayerAudioCategory.Media,
                        Volume = ViewModel.Player.Volume,
                        IsMuted = ViewModel.Player.IsMuted
                    };

                    _mediaPlayer.MediaEnded += (s, e) =>
                    {
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            await ViewModel.Player.OnMediaEndedAsync();
                        });
                    };

                    _mediaPlayer.MediaFailed += (s, e) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            ShowPlaybackErrorState();
                            _ = ViewModel.Player.OnMediaFailedAsync();
                        });
                    };

                    _mediaPlayer.PlaybackSession.PositionChanged += (session, e) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            int pos = (int)session.Position.TotalMilliseconds;
                            int dur = (int)session.NaturalDuration.TotalMilliseconds;
                            ViewModel.Player.UpdatePosition(pos, dur);
                        });
                    };
                }

                var uri = new Uri(System.IO.Path.GetFullPath(targetFilePath));
                var mediaSource = Windows.Media.Core.MediaSource.CreateFromUri(uri);
                _mediaPlayer.Source = mediaSource;

                if (startPositionMs > 0)
                {
                    _mediaPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(startPositionMs);
                }

                DailyResetMediaPlayerElement.SetMediaPlayer(_mediaPlayer);
                DailyResetMediaPlayerElement.Visibility = Visibility.Visible;
                FallbackAtmosphereBorder.Visibility = Visibility.Collapsed;

                _mediaPlayer.Play();
            }
            catch
            {
                ShowPlaybackErrorState();
                _ = ViewModel.Player.OnMediaFailedAsync();
            }
        });
    }

    private void StopAndCleanupMediaPlayer()
    {
        if (_mediaPlayer != null)
        {
            try
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Source = null;
                DailyResetMediaPlayerElement.SetMediaPlayer(null);
                _mediaPlayer.Dispose();
            }
            catch { }
            finally
            {
                _mediaPlayer = null;
            }
        }
        DailyResetMediaPlayerElement.Visibility = Visibility.Collapsed;
        FallbackAtmosphereBorder.Visibility = Visibility.Visible;
    }

    private void OnRequestPauseMedia()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _mediaPlayer?.Pause();
        });
    }

    private void OnRequestResumeMedia()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _mediaPlayer?.Play();
        });
    }

    private void OnRootGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (ViewModel.IsExpanded)
            {
                ViewModel.Collapse();
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Space && ViewModel.IsPlayerOpen)
        {
            _ = ViewModel.Player.PlayPauseAsync();
            e.Handled = true;
        }
    }

    private void OnPlayerStateChanged(bool isOpen)
    {
        if (isOpen)
        {
            var dpiScale = GetDpiScale();
            var currentPos = _appWindow.Position;

            // Если виджет был в компактном режиме (400x260), центрируем и расширяем окно до 380x680
            if (WidgetCanvas.Width != 380 || WidgetCanvas.Height != 680)
            {
                int deltaWidthPhysical = (int)Math.Round(10.0 * dpiScale);
                int newPhysicalWidth = (int)Math.Round(380.0 * dpiScale);
                int newPhysicalHeight = (int)Math.Round(680.0 * dpiScale);
                int newX = currentPos.X + deltaWidthPhysical;

                var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
                int topY = displayArea?.WorkArea.Y ?? 0;

                _appWindow.MoveAndResize(new RectInt32(newX, topY, newPhysicalWidth, newPhysicalHeight));
                WidgetCanvas.Width = 380;
                WidgetCanvas.Height = 680;
            }

            PlayerRoot.Visibility = Visibility.Visible;
            OpenPlayerStoryboard.Begin();
        }
        else
        {
            StopAndCleanupMediaPlayer();
            CompactView.Visibility = Visibility.Visible;
            ClosePlayerStoryboard.Begin();
        }
    }

    private void OnPlayerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoPlayerViewModel.IsCountButtonVisible) && ViewModel.Player.IsCountButtonVisible)
        {
            AnimateCountButtonReveal();
        }
    }

    private void AnimateCountButtonReveal()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            CountButtonTransform.Y = 12;
            CountRitualButton.Opacity = 0.0;

            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();

            var translateYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(translateYAnim, CountButtonTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(translateYAnim, "Y");
            translateYAnim.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.SplineDoubleKeyFrame
            {
                KeyTime = Microsoft.UI.Xaml.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)),
                Value = 0,
                KeySpline = new Microsoft.UI.Xaml.Media.Animation.KeySpline { ControlPoint1 = new Windows.Foundation.Point(0.16, 1.0), ControlPoint2 = new Windows.Foundation.Point(0.3, 1.0) }
            });

            var opacityAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(opacityAnim, CountRitualButton);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            opacityAnim.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.SplineDoubleKeyFrame
            {
                KeyTime = Microsoft.UI.Xaml.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)),
                Value = 1.0,
                KeySpline = new Microsoft.UI.Xaml.Media.Animation.KeySpline { ControlPoint1 = new Windows.Foundation.Point(0.16, 1.0), ControlPoint2 = new Windows.Foundation.Point(0.3, 1.0) }
            });

            sb.Children.Add(translateYAnim);
            sb.Children.Add(opacityAnim);
            sb.Begin();
        });
    }

    private async void OnDailyResetCardPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as UIElement);
        if (point.Properties.IsLeftButtonPressed)
        {
            await ViewModel.OpenPlayerAsync();
            e.Handled = true;
        }
    }

    private async void OnOpenPlayerClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.OpenPlayerAsync();
    }

    private void OnPlayerCloseClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ClosePlayer();
    }

    private async void OnVideoViewportPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as UIElement);
        if (point.Properties.IsLeftButtonPressed)
        {
            await ViewModel.Player.PlayPauseAsync();
            e.Handled = true;
        }
    }

    private void OnPlayerMuteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Player.ToggleMute();
        if (_mediaPlayer != null)
        {
            _mediaPlayer.IsMuted = ViewModel.Player.IsMuted;
        }
    }

    private void OnPlayerVolumeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ViewModel.Player.SetVolume(e.NewValue);
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Volume = e.NewValue;
        }
    }

    private async void OnCountRitualButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.Player.CountRitualAsync();
    }

    private void OnOpenPlayerStoryboardCompleted(object? sender, object e)
    {
        ExpandedRoot.Visibility = Visibility.Collapsed;
        CompactView.Visibility = Visibility.Collapsed;
        _isTransitioning = false;
    }

    private void OnClosePlayerStoryboardCompleted(object? sender, object e)
    {
        PlayerRoot.Visibility = Visibility.Collapsed;
        StopAndCleanupMediaPlayer();

        var dpiScale = GetDpiScale();
        var currentPos = _appWindow.Position;

        if (!ViewModel.IsShowingTasks)
        {
            int deltaWidthPhysical = (int)Math.Round(10.0 * dpiScale);
            int newPhysicalWidth = (int)Math.Round(400.0 * dpiScale);
            int newPhysicalHeight = (int)Math.Round(260.0 * dpiScale);
            int newX = currentPos.X - deltaWidthPhysical;

            var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
            int topY = displayArea?.WorkArea.Y ?? 0;

            _appWindow.MoveAndResize(new RectInt32(newX, topY, newPhysicalWidth, newPhysicalHeight));
            WidgetCanvas.Width = 400;
            WidgetCanvas.Height = 260;
            CompactView.Visibility = Visibility.Visible;
            CompactView.Opacity = 1.0;
        }

        _isTransitioning = false;
        App.TrimWorkingSet();
    }

    private void OnPlayButtonClick(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.OpenPlayerAsync();
    }

    private void OnCapsulePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        }
    }

    private void OnCapsulePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
        }
    }

    private void OnTaskRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
        }
    }

    private void OnTaskRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
        }
    }

    public string FormatPercent(int score) => $"{score}%";

    public Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InvertBoolToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public Windows.UI.Text.TextDecorations StrikethroughToDecoration(bool isStrikethrough) =>
        isStrikethrough ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None;

    public Visibility CheckboxCheckedToVisibility(bool isChecked) => isChecked ? Visibility.Visible : Visibility.Collapsed;

    public SolidColorBrush GetStatusBackground(string status) =>
        status == "Paused"
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x2E, 0xF5, 0x9E, 0x0B))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0x2E, 0x26, 0xA6, 0x5B));

    public SolidColorBrush GetStatusForeground(string status) =>
        status == "Paused"
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x36, 0xD3, 0x99));

    public string GetMuteGlyph(bool isMuted) =>
        isMuted ? "\uE74F" : "\uE767";

    #endregion
}
