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
/// Win32 P/Invoke стилей, фиксации по верхней кромке экрана (Rainmeter Top-Center),
/// плавной анимации TranslateTransform (0 <-> -244) и инлайн-управления задачами дня.
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
    private bool _isNavigating;

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

        // 1. Полная прозрачность окна (отключение системного фона и DWM расширение фрейма)
        this.SystemBackdrop = null;
        _hwnd = WindowNative.GetWindowHandle(this);
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        // 2. Получение дескриптора AppWindow
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // 2.1. Установка брендовой иконки окна
        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (System.IO.File.Exists(iconPath))
        {
            _appWindow.SetIcon(iconPath);
        }

        // 3. Настройка безрамочных P/Invoke стилей и DwmExtendFrameIntoClientArea
        ConfigureWindowStyles();

        // 4. Закрепление на всех виртуальных рабочих столах (PinWindow)
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

        // 7. Деактивация окна — автосворачивание УДАЛЕНО: окно не закрывается при клике мимо
        this.Activated += OnWindowActivated;

        // 8. Подписка на горячие клавиши (Space = Play/Pause, Esc = Close/Collapse)
        RootGrid.KeyDown += OnRootGridKeyDown;

        // 8.1. Подписка на автоматическое сворачивание при открытии Personal Space
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
                FocusCompactInlineInput();
            }
            else if (e.PropertyName == nameof(WidgetViewModel.IsShowingTasks))
            {
                OnShowingTasksChanged(ViewModel.IsShowingTasks);
            }
        };

        // 9.1. Инициализация аппаратных теней WinUI Composition DropShadow
        if (this.Content is FrameworkElement fe)
        {
            fe.Loaded += (s, e) => ApplyAllShadows();
        }

        // 10. Подписка на полноэкранные приложения (игры, видео на весь экран)
        if (_systemMonitorService != null)
        {
            _systemMonitorService.IsFullscreenRunningChanged += OnFullscreenRunningChanged;
        }

        // 11. Начальное позиционирование (Top-Center, Width=420, Height=280) и загрузка данных
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

        // Win32 DWM Glassmorphism: полная прозрачность клиентской области (DwmExtendFrameIntoClientArea с margins -1)
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);
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

        if (state.IsExpanded)
        {
            ViewModel.IsExpanded = true;
            ApplyExpandedHomeState();
        }
        else
        {
            ViewModel.IsExpanded = false;
            ApplyCollapsedState();
        }
    }

    private double GetDpiScale()
    {
        uint dpi = GetDpiForWindow(_hwnd);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    private void ResizeWidgetWindow(int widthDip, int heightDip, bool centerScreen = false)
    {
        try
        {
            var dpiScale = GetDpiScale();
            int physW = (int)Math.Round(widthDip * dpiScale);
            int physH = (int)Math.Round(heightDip * dpiScale);

            var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
            var workArea = displayArea != null ? displayArea.WorkArea : new RectInt32(0, 0, 1920, 1080);

            int targetX = centerScreen
                ? workArea.X + (workArea.Width - physW) / 2
                : workArea.X + (workArea.Width - physW) / 2;
            int targetY = centerScreen
                ? workArea.Y + (workArea.Height - physH) / 2
                : workArea.Y;

            _appWindow.MoveAndResize(new RectInt32(targetX, targetY, physW, physH));
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Error resizing widget window to {widthDip}x{heightDip}");
        }
    }

    public void ApplyCollapsedState()
    {
        if (CardsContainer != null) CardsContainer.Visibility = Visibility.Collapsed;
        if (BottomTabTrigger != null)
        {
            BottomTabTrigger.Visibility = Visibility.Visible;
            BottomTabTrigger.Width = 260;
            BottomTabTrigger.Height = 42;
        }
        ResizeWidgetWindow(260, 42, centerScreen: false);
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        CanvasTranslate.Y = 0;
        SaveCurrentWindowState();
        App.TrimWorkingSet();
    }

    public void ApplyExpandedHomeState()
    {
        if (CardsContainer != null) CardsContainer.Visibility = Visibility.Visible;
        if (CompactView != null)
        {
            CompactView.Visibility = Visibility.Visible;
            CompactView.Opacity = 1.0;
        }
        if (PlayerRoot != null)
        {
            PlayerRoot.Visibility = Visibility.Collapsed;
            PlayerRoot.Opacity = 0.0;
        }
        if (ExpandedRoot != null)
        {
            ExpandedRoot.Visibility = Visibility.Collapsed;
            ExpandedRoot.Opacity = 0.0;
        }
        if (SettingsRoot != null)
        {
            SettingsRoot.Visibility = Visibility.Collapsed;
            SettingsRoot.Opacity = 0.0;
        }
        if (BottomTabTrigger != null)
        {
            BottomTabTrigger.Visibility = Visibility.Visible;
            BottomTabTrigger.Width = 260;
            BottomTabTrigger.Height = 42;
        }
        ResizeWidgetWindow(440, 300, centerScreen: false);
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        CanvasTranslate.Y = 0;
        SaveCurrentWindowState();
        App.TrimWorkingSet();
    }

    public void ApplyPlayerState()
    {
        if (CardsContainer != null) CardsContainer.Visibility = Visibility.Visible;
        if (CompactView != null) CompactView.Visibility = Visibility.Collapsed;
        if (ExpandedRoot != null) ExpandedRoot.Visibility = Visibility.Collapsed;
        if (SettingsRoot != null) SettingsRoot.Visibility = Visibility.Collapsed;
        if (BottomTabTrigger != null) BottomTabTrigger.Visibility = Visibility.Collapsed;
        if (PlayerRoot != null)
        {
            PlayerRoot.Visibility = Visibility.Visible;
            PlayerRoot.Opacity = 1.0;
        }
        ResizeWidgetWindow(800, 600, centerScreen: true);
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        CanvasTranslate.Y = 0;
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
    /// Гарантирует центрирование виджета по верхней кромке активного монитора.
    /// </summary>
    public void HandleDisplayChange()
    {
        try
        {
            if (ViewModel.IsPlayerOpen)
            {
                ResizeWidgetWindow(800, 600, centerScreen: true);
            }
            else if (ViewModel.IsSettingsOpen)
            {
                ResizeWidgetWindow(840, 620, centerScreen: true);
            }
            else if (ViewModel.IsExpanded)
            {
                ResizeWidgetWindow(440, 300, centerScreen: false);
            }
            else
            {
                ResizeWidgetWindow(260, 42, centerScreen: false);
            }
            SaveCurrentWindowState();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error handling display change");
        }
    }

    #endregion

    #region Window State & Drag Removal

    // Drag & Drop полностью удален: окно зафиксировано по верхней кромке экрана
    private void OnWindowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
    }

    private void OnCardPointerPressed(object sender, PointerRoutedEventArgs e)
    {
    }

    private void OnInteractivePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        // Автоматическое сворачивание на Deactivated УДАЛЕНО!
        // Окно НЕ закрывается при клике мимо, сворачивание только по шеврону, Esc, Win+Ctrl+S.
    }

    private void SaveCurrentWindowState()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var currentState = await _windowStateService.LoadStateAsync();
                var updated = currentState with
                {
                    NormalizedX = 0.5,
                    NormalizedY = 0.0,
                    DpiScale = GetDpiScale(),
                    IsExpanded = ViewModel.IsExpanded
                };
                await _windowStateService.SaveStateAsync(updated);
            }
            catch
            {
                // Игнорируем фоновые ошибки сохранения
            }
        });
    }

    private void ApplyCardShadow(FrameworkElement? hostElement)
    {
        if (hostElement == null) return;
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(hostElement);
            var compositor = visual.Compositor;

            var dropShadow = compositor.CreateDropShadow();
            dropShadow.BlurRadius = 32.0f;
            dropShadow.Offset = new System.Numerics.Vector3(0f, 12f, 0f);
            dropShadow.Color = Windows.UI.Color.FromArgb(115, 0, 0, 0); // Opacity 0.45, Color #000000

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
        ApplyCardShadow(SettingsShadowHost);
    }

    #endregion

    #region Window State Transitions & Navigation

    private void OnExpandedStateChanged(bool isExpanded)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        try
        {
            if (isExpanded)
            {
                // Чистый старт: гарантируем возврат на экран 3 карточек
                ViewModel.IsShowingTasks = false;
                ViewModel.IsPlayerOpen = false;
                ViewModel.IsAddingTask = false;
                ViewModel.IsSettingsOpen = false;

                ApplyExpandedHomeState();
            }
            else
            {
                ExitPlayer();
                ApplyCollapsedState();
            }
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private void OnExpandStoryboardCompleted(object? sender, object e)
    {
        _isTransitioning = false;
        CanvasTranslate.Y = 0;
        SaveCurrentWindowState();
        App.TrimWorkingSet();
    }

    private void OnCollapseStoryboardCompleted(object? sender, object e)
    {
        _isTransitioning = false;
        CanvasTranslate.Y = 0;
        SaveCurrentWindowState();
        App.TrimWorkingSet();
    }

    private void OnShowingTasksChanged(bool isShowing)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        try
        {
            if (isShowing)
            {
                if (CompactView != null) CompactView.Visibility = Visibility.Collapsed;
                if (ExpandedRoot != null)
                {
                    ExpandedRoot.Visibility = Visibility.Visible;
                    ExpandedRoot.Opacity = 1.0;
                }
            }
            else
            {
                if (ExpandedRoot != null) ExpandedRoot.Visibility = Visibility.Collapsed;
                if (CompactView != null)
                {
                    CompactView.Visibility = Visibility.Visible;
                    CompactView.Opacity = 1.0;
                }
            }
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private void OnOpenTasksStoryboardCompleted(object? sender, object e)
    {
        if (CompactView != null) CompactView.Visibility = Visibility.Collapsed;
        _isTransitioning = false;
    }

    private void OnCloseTasksStoryboardCompleted(object? sender, object e)
    {
        if (ExpandedRoot != null) ExpandedRoot.Visibility = Visibility.Collapsed;
        _isTransitioning = false;
    }

    #endregion

    #region Today's Focus Compact View Task Interactions

    private void OnCompactCapsulePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Клик по строке задачи НЕ проваливает во вложенный экран Today's Focus Detail!
        e.Handled = true;
    }

    private void OnCompactAddButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsAddingTask)
        {
            ViewModel.CancelAddingTask();
        }
        else
        {
            ViewModel.IsAddingTask = true;
            FocusCompactInlineInput();
        }
    }

    private void FocusCompactInlineInput()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            CompactInlineTaskBox?.Focus(FocusState.Programmatic);
        });
    }

    private async void OnCompactInlineTaskKeyDown(object sender, KeyRoutedEventArgs e)
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

    private async void OnCompactSubmitTaskClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.AddTaskAsync();
    }

    private void OnCompactCancelTaskClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelAddingTask();
    }

    private async void OnCompactTaskCheckboxClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            await ViewModel.CompleteTaskAsync(item);
        }
    }

    private void OnCompactTaskTextPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            ViewModel.StartEditTask(item);
            e.Handled = true;
        }
    }

    private void OnCompactTaskRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TaskItemViewModel item)
        {
            item.IsHovered = true;
        }
    }

    private void OnCompactTaskRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TaskItemViewModel item)
        {
            item.IsHovered = false;
        }
    }

    private void OnCompactTaskEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            ViewModel.StartEditTask(item);
        }
    }

    private async void OnCompactTaskDeleteClick(object sender, RoutedEventArgs e)
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

    private void OnUndoToastClick(object sender, RoutedEventArgs e)
    {
        ViewModel.UndoLastTaskCompletion();
    }

    private void OnBackToHomeClick(object sender, RoutedEventArgs e)
    {
        NavigateToHome();
    }

    private void OnOpenDashboardClick(object sender, RoutedEventArgs e)
    {
        App.OpenDashboard();
    }

    private async void OnTaskCheckboxClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TaskItemViewModel item)
        {
            await ViewModel.CompleteTaskAsync(item);
        }
    }

    #endregion

    #region Daily Reset Player Lifecycle & Safe Exit

    /// <summary>
    /// Безопасный метод остановки и очистки медиаплеера с гарантированным возвратом на экран 3 карточек.
    /// </summary>
    public void ExitPlayer()
    {
        try
        {
            if (DailyResetMediaPlayerElement?.MediaPlayer != null)
            {
                DailyResetMediaPlayerElement.MediaPlayer.Pause();
                DailyResetMediaPlayerElement.MediaPlayer.Source = null;
                DailyResetMediaPlayerElement.SetMediaPlayer(null);
            }
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Source = null;
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }
            AppLogger.Info("Media player cleanly stopped and disposed.");
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error stopping media playback");
        }
        finally
        {
            NavigateToHome(); // Всегда гарантированный возврат на экран 3 карточек!
        }
    }

    /// <summary>
    /// Гарантированно возвращает на главный экран 3 карточек (440x300) или свёрнутую плашку (260x42).
    /// </summary>
    public void NavigateToHome()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                ViewModel.IsPlayerOpen = false;
                ViewModel.IsShowingTasks = false;
                ViewModel.IsSettingsOpen = false;

                if (ViewModel.IsExpanded)
                {
                    ApplyExpandedHomeState();
                }
                else
                {
                    ApplyCollapsedState();
                }
                AppLogger.Info("Navigated to Home (3 cards view).");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Error navigating to Home");
            }
        });
    }

    private async void OnOpenPlayerClicked(object sender, RoutedEventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        try
        {
            AppLogger.Info("Opening Daily Reset player...");
            await ViewModel.OpenPlayerAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error opening Daily Reset player");
            ExitPlayer();
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async void OnDailyResetCardPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as UIElement);
        if (point.Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            if (_isNavigating) return;
            _isNavigating = true;

            try
            {
                AppLogger.Info("Daily Reset card clicked, opening player...");
                await ViewModel.OpenPlayerAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Error opening Daily Reset player from card click");
                ExitPlayer();
            }
            finally
            {
                _isNavigating = false;
            }
        }
    }

    private void OnPlayerCloseClick(object sender, RoutedEventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        try
        {
            AppLogger.Info("Player close button clicked.");
            ExitPlayer();
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private void OnPlayerStateChanged(bool isOpen)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isOpen)
            {
                AppLogger.Info("Opening player view (580x440)...");
                ApplyPlayerState();
            }
            else
            {
                AppLogger.Info("Closing player view...");
                ExitPlayer();
            }
        });
    }

    private void OnOpenPlayerStoryboardCompleted(object? sender, object e)
    {
        if (CompactView != null) CompactView.Visibility = Visibility.Collapsed;
        if (ExpandedRoot != null) ExpandedRoot.Visibility = Visibility.Collapsed;
        _isTransitioning = false;
    }

    private void OnClosePlayerStoryboardCompleted(object? sender, object e)
    {
        if (PlayerRoot != null) PlayerRoot.Visibility = Visibility.Collapsed;
        StopAndCleanupMediaPlayer();
        if (CompactView != null)
        {
            CompactView.Visibility = Visibility.Visible;
            CompactView.Opacity = 1.0;
        }
        _isTransitioning = false;
        App.TrimWorkingSet();
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
            AppLogger.Info($"OnRequestStartMedia invoked with file: '{filePath}', pos: {startPositionMs}ms");
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
                AppLogger.Warn("No valid media file found, falling back to atmosphere view.");
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
                            AppLogger.Info("Media playback ended.");
                            await ViewModel.Player.OnMediaEndedAsync();
                        });
                    };

                    _mediaPlayer.MediaFailed += (s, e) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            AppLogger.Error("Media playback failed in MediaPlayer.");
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
                AppLogger.Info($"Media playback started successfully: {targetFilePath}");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Exception while starting media playback");
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
                DailyResetMediaPlayerElement?.SetMediaPlayer(null);
                _mediaPlayer.Dispose();
            }
            catch { }
            finally
            {
                _mediaPlayer = null;
            }
        }
        if (DailyResetMediaPlayerElement != null) DailyResetMediaPlayerElement.Visibility = Visibility.Collapsed;
        if (FallbackAtmosphereBorder != null) FallbackAtmosphereBorder.Visibility = Visibility.Visible;
    }

    private void OnRequestPauseMedia()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AppLogger.Info("Pausing media playback.");
            _mediaPlayer?.Pause();
        });
    }

    private void OnRequestResumeMedia()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AppLogger.Info("Resuming media playback.");
            _mediaPlayer?.Play();
        });
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
        try
        {
            // 1. Асинхронно фиксируем ритуал в БД
            await ViewModel.Player.CountRitualAsync();

            // 2. Глушим плеер безопасно
            try
            {
                _mediaPlayer?.Pause();
            }
            catch { }

            // 3. Показываем плавающее Toast-уведомление поверх экрана
            if (RitualCompletedToast != null)
            {
                RitualCompletedToast.Visibility = Visibility.Visible;
            }

            // 4. Через 1.2 секунды плавно возвращаемся на экран 3 карточек (440x300)
            await Task.Delay(1200);

            if (RitualCompletedToast != null)
            {
                RitualCompletedToast.Visibility = Visibility.Collapsed;
            }

            ExitPlayer();

            // 5. Обновляем процент месяца
            await ViewModel.RefreshMonthlyProgressAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error counting daily ritual");
            ExitPlayer();
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
            if (CountButtonTransform == null || CountRitualButton == null) return;
            CountButtonTransform.Y = 8;
            CountRitualButton.Opacity = 0.0;

            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();

            var translateYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(translateYAnim, CountButtonTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(translateYAnim, "Y");
            translateYAnim.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.SplineDoubleKeyFrame
            {
                KeyTime = Microsoft.UI.Xaml.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)),
                Value = 0,
                KeySpline = new Microsoft.UI.Xaml.Media.Animation.KeySpline { ControlPoint1 = new Windows.Foundation.Point(0.16, 1.0), ControlPoint2 = new Windows.Foundation.Point(0.3, 1.0) }
            });

            var opacityAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(opacityAnim, CountRitualButton);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            opacityAnim.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.SplineDoubleKeyFrame
            {
                KeyTime = Microsoft.UI.Xaml.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)),
                Value = 1.0,
                KeySpline = new Microsoft.UI.Xaml.Media.Animation.KeySpline { ControlPoint1 = new Windows.Foundation.Point(0.16, 1.0), ControlPoint2 = new Windows.Foundation.Point(0.3, 1.0) }
            });

            sb.Children.Add(translateYAnim);
            sb.Children.Add(opacityAnim);
            sb.Begin();
        });
    }

    #endregion

    #region Bottom Tab Chevron Trigger & Keyboard Shortcuts

    public string GetChevronGlyph(bool isExpanded) => isExpanded ? "\uE70E" : "\uE70D";

    public string GetChevronToolTip(bool isExpanded) => isExpanded ? "Свернуть виджет (Esc)" : "Развернуть виджет (Win+Ctrl+S)";

    private void OnBottomTabPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as UIElement);
        if (point.Properties.IsLeftButtonPressed)
        {
            ViewModel.ToggleExpanded();
            e.Handled = true;
        }
    }

    private void OnBottomTabPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (BottomTabTrigger != null)
        {
            BottomTabTrigger.Opacity = 0.85;
        }
    }

    private void OnBottomTabPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (BottomTabTrigger != null)
        {
            BottomTabTrigger.Opacity = 1.0;
        }
    }

    private void OnRootGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (ViewModel.IsPlayerOpen)
            {
                ExitPlayer();
                e.Handled = true;
            }
            else if (ViewModel.IsSettingsOpen)
            {
                NavigateToHome();
                e.Handled = true;
            }
            else if (ViewModel.IsExpanded)
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

    #region Settings Layer & Video Library

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (CompactView != null) CompactView.Visibility = Visibility.Collapsed;
            if (PlayerRoot != null) PlayerRoot.Visibility = Visibility.Collapsed;
            if (ExpandedRoot != null) ExpandedRoot.Visibility = Visibility.Collapsed;
            if (BottomTabTrigger != null) BottomTabTrigger.Visibility = Visibility.Collapsed;
            if (SettingsRoot != null)
            {
                SettingsRoot.Visibility = Visibility.Visible;
                SettingsRoot.Opacity = 1.0;
            }
            ResizeWidgetWindow(840, 620, centerScreen: true);
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(_hwnd, ref margins);
            ViewModel.OpenSettings();
        });
    }

    private void OnSettingsBackClick(object sender, RoutedEventArgs e)
    {
        ResizeWidgetWindow(440, 300, centerScreen: false);
        NavigateToHome();
    }

    private async void OnAddVideoFilesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mkv");
            picker.FileTypeFilter.Add(".webm");
            picker.FileTypeFilter.Add(".mov");

            InitializeWithWindow.Initialize(picker, _hwnd);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    await ViewModel.AddVideoFileAsync(file.Path);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error picking video files");
        }
    }

    private async void OnSelectVideoFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            folderPicker.FileTypeFilter.Add("*");

            InitializeWithWindow.Initialize(folderPicker, _hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                await ViewModel.AddVideosFromFolderAsync(folder.Path);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error picking video folder");
        }
    }

    private async void OnDeleteVideoItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is VideoLibraryItemViewModel item)
        {
            await ViewModel.DeleteVideoAsync(item);
        }
    }

    public Visibility GetEmptyVideosVisibility(int count) =>
        count == 0 ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    private void OnFullscreenRunningChanged(bool isFullscreen)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isFullscreen)
            {
                WidgetCanvas.Opacity = 0.0;
                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = false;
                }
            }
            else
            {
                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                }
                FullscreenRestoreStoryboard.Begin();
            }
        });
    }

    #endregion

    #region XAML Binding Helpers & Converters

    public Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InvertBoolToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public SolidColorBrush GetStatusBackground(string status) =>
        status == "Пауза" || status == "Paused"
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x2E, 0xF5, 0x9E, 0x0B))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0x2E, 0x26, 0xA6, 0x5B));

    public SolidColorBrush GetStatusForeground(string status) =>
        status == "Пауза" || status == "Paused"
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x36, 0xD3, 0x99));

    public string GetMuteGlyph(bool isMuted) =>
        isMuted ? "\uE74F" : "\uE767";

    #endregion
}
