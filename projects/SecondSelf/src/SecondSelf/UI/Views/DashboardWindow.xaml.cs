using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.Mvvm.Messaging;
using SecondSelf.Core.Messages;
using SecondSelf.Core.Services;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data.Entities;
using Windows.Graphics;
using WinRT.Interop;
using DisplayArea = Microsoft.UI.Windowing.DisplayArea;

namespace SecondSelf.UI.Views;

/// <summary>
/// Главное окно Personal Dashboard (1120x780px) с поддержкой Snap Layouts,
/// скрытием в трей при закрытии [X], минимальным размером 960x640px,
/// 5 разделами навигации и модальными оверлеями.
/// </summary>
public sealed partial class DashboardWindow : Window
{
    #region Win32 Constants & P/Invoke Definitions for MinTrackSize

    private const int WM_GETMINMAXINFO = 0x0024;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    #endregion

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly SubclassProc _subclassProcDelegate;

    private static readonly SolidColorBrush SelectedTabBgBrush = new(Windows.UI.Color.FromArgb(0x29, 0xCE, 0x82, 0x4C));
    private static readonly SolidColorBrush TransparentBrush = new(Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00));
    private static readonly SolidColorBrush SelectedTabFgBrush = new(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush UnselectedTabFgBrush = new(Windows.UI.Color.FromArgb(0xFF, 0xA1, 0xA1, 0xAA));

    public DashboardViewModel ViewModel { get; }

    public Visibility HasHistoricalRecapsVisibility(int count) =>
        count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public DashboardWindow(DashboardViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        this.InitializeComponent();

        // 1. Получение HWND и AppWindow
        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // 2. Настройка геометрии окна (1120x780px) и центрирование
        ConfigureWindowGeometry(windowId);

        // 3. Подключение хука ограничения минимального размера (MinWidth 960, MinHeight 640)
        _subclassProcDelegate = SubclassWindowProc;
        SetWindowSubclass(_hwnd, _subclassProcDelegate, UIntPtr.Zero, IntPtr.Zero);

        // 4. Настройка тёмной темы заголовка и Snap Layouts
        ConfigureTitleBar();

        // 4.1. Установка брендовой иконки окна
        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (System.IO.File.Exists(iconPath))
        {
            _appWindow.SetIcon(iconPath);
        }

        // 5. Обработка закрытия: отмена уничтожения и скрытие в трей (Rule 6, возврат свёрнутого виджета 260x42)
        _appWindow.Closing += (sender, args) =>
        {
            args.Cancel = true;
            _appWindow.Hide();
            App.WidgetWindow?.AppWindow.Show();
            App.CollapseWidget();
        };

        // 5.1. Автосворачивание и скрытие виджета при открытии или активации Personal Space
        this.Activated += OnWindowActivated;
        App.WidgetWindow?.AppWindow.Hide();
        WeakReferenceMessenger.Default.Send(new CollapseWidgetMessage());

        // 6. Инициализация данных при первом открытии
        this.DispatcherQueue.TryEnqueue(async () =>
        {
            await ViewModel.InitializeAsync();
        });
    }

    private void ConfigureWindowGeometry(WindowId windowId)
    {
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        if (displayArea != null)
        {
            var workArea = displayArea.WorkArea;
            int width = Math.Max(1440, (int)Math.Round(workArea.Width * 0.85));
            int height = Math.Max(900, (int)Math.Round(workArea.Height * 0.85));
            width = Math.Min(width, (int)(workArea.Width * 0.95));
            height = Math.Min(height, (int)(workArea.Height * 0.95));
            _appWindow.Resize(new SizeInt32(width, height));

            int posX = Math.Max(0, workArea.X + (workArea.Width - width) / 2);
            int posY = Math.Max(0, workArea.Y + (workArea.Height - height) / 2);
            _appWindow.Move(new PointInt32(posX, posY));
        }
        else
        {
            _appWindow.Resize(new SizeInt32(1440, 900));
        }
    }

    private void ConfigureTitleBar()
    {
        this.Title = "Second Self — Личное пространство";

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = false;

            var darkBg = Windows.UI.Color.FromArgb(255, 9, 9, 11);
            var fg = Windows.UI.Color.FromArgb(255, 244, 244, 245);
            var inactiveFg = Windows.UI.Color.FromArgb(255, 161, 161, 170);
            var hoverBg = Windows.UI.Color.FromArgb(255, 28, 28, 32);
            var pressedBg = Windows.UI.Color.FromArgb(255, 45, 45, 50);

            titleBar.BackgroundColor = darkBg;
            titleBar.InactiveBackgroundColor = darkBg;
            titleBar.ForegroundColor = fg;
            titleBar.InactiveForegroundColor = inactiveFg;

            titleBar.ButtonBackgroundColor = darkBg;
            titleBar.ButtonInactiveBackgroundColor = darkBg;
            titleBar.ButtonForegroundColor = fg;
            titleBar.ButtonInactiveForegroundColor = inactiveFg;

            titleBar.ButtonHoverBackgroundColor = hoverBg;
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = pressedBg;
            titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            App.WidgetWindow?.AppWindow.Hide();
            WeakReferenceMessenger.Default.Send(new CollapseWidgetMessage());
        }
    }

    private IntPtr SubclassWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            uint dpi = GetDpiForWindow(hWnd);
            double scale = (dpi > 0) ? dpi / 96.0 : 1.0;

            mmi.ptMinTrackSize.x = (int)(960 * scale);
            mmi.ptMinTrackSize.y = (int)(640 * scale);

            Marshal.StructureToPtr(mmi, lParam, false);
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    #region Tab Styling & Visibility Helpers

    public SolidColorBrush GetTabBackground(string currentTab, string targetTab) =>
        string.Equals(currentTab, targetTab, StringComparison.OrdinalIgnoreCase) ? SelectedTabBgBrush : TransparentBrush;

    public SolidColorBrush GetTabForeground(string currentTab, string targetTab) =>
        string.Equals(currentTab, targetTab, StringComparison.OrdinalIgnoreCase) ? SelectedTabFgBrush : UnselectedTabFgBrush;

    public Visibility GetTabMarkerVisibility(string currentTab, string targetTab) =>
        string.Equals(currentTab, targetTab, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    public Windows.UI.Text.FontWeight GetTabFontWeight(string currentTab, string targetTab) =>
        string.Equals(currentTab, targetTab, StringComparison.OrdinalIgnoreCase)
            ? new Windows.UI.Text.FontWeight { Weight = 600 }
            : new Windows.UI.Text.FontWeight { Weight = 400 };

    public Visibility TabToVisibility(string currentTab, string targetTab) =>
        string.Equals(currentTab, targetTab, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region Event Handlers: Navigation & Sidebar

    private void OnNavTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tabName)
        {
            ViewModel.NavigateToTab(tabName);
        }
    }

    private void OnToggleWidgetClick(object sender, RoutedEventArgs e)
    {
        App.ToggleWidget();
    }

    #endregion

    #region Event Handlers: Today Tab

    private void OnEditWeeklyGoalClick(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenWeeklyGoalModal();
    }

    private async void OnAddTodayTaskClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.TodayViewModel.AddTaskAsync();
    }

    private async void OnTodayInlineTaskKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.TodayViewModel.AddTaskAsync();
        }
    }

    private async void OnTaskCheckboxClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is TaskItemViewModel task)
        {
            await ViewModel.TodayViewModel.CompleteTaskAsync(task);
        }
    }

    private async void OnTaskDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TaskItemViewModel task)
        {
            await ViewModel.TodayViewModel.DeleteTaskAsync(task);
        }
    }

    private void OnUndoToastClick(object sender, RoutedEventArgs e)
    {
        ViewModel.TodayViewModel.UndoCompleteTask();
    }

    private void OnPlayDailyResetClick(object sender, RoutedEventArgs e)
    {
        ViewModel.TodayViewModel.PlayDailyVideo();
    }

    #endregion

    #region Event Handlers: Goals Tab

    private void OnGoalCardPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is BigGoalViewModel goal)
        {
            ViewModel.SelectGoalDetail(goal);
        }
    }

    private void OnAddBigGoalClick(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenAddGoalModal();
    }

    private void OnBackToGoalsClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseGoalDetail();
    }

    private async void OnArchiveSelectedGoalClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.GoalsViewModel.SelectedGoal != null)
        {
            await ViewModel.GoalsViewModel.ArchiveGoalAsync(ViewModel.GoalsViewModel.SelectedGoal.Id);
        }
    }

    private async void OnCompleteMilestoneClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string milestoneId)
        {
            await ViewModel.GoalsViewModel.CompleteMilestoneAsync(milestoneId);
        }
    }

    #endregion

    #region Event Handlers: Progress Tab

    private void OnRecapCardPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is MonthlyRecap recap)
        {
            ViewModel.ProgressViewModel.OpenRecap(recap);
        }
    }

    #endregion

    #region Event Handlers: Settings Tab

    private async void OnRescanLibraryClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SettingsViewModel.RescanLibraryAsync();
    }

    private async void OnShuffleQueueClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SettingsViewModel.ShuffleQueueAsync();
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
                var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".webm", ".mov" };
                var destinationFolder = Path.Combine(AppContext.BaseDirectory, "Videos");
                Directory.CreateDirectory(destinationFolder);

                var files = Directory.GetFiles(folder.Path, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => supported.Contains(Path.GetExtension(f)));

                foreach (var file in files)
                {
                    var destPath = Path.Combine(destinationFolder, Path.GetFileName(file));
                    if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(file, destPath, overwrite: true);
                    }
                }

                await ViewModel.SettingsViewModel.RescanLibraryAsync();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error picking video folder in Dashboard");
        }
    }

    private async void OnAddVideoFilesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            InitializeWithWindow.Initialize(picker, _hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mkv");
            picker.FileTypeFilter.Add(".webm");
            picker.FileTypeFilter.Add(".mov");

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    await ViewModel.SettingsViewModel.AddVideoFileAsync(file.Path);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error picking video files in Dashboard");
        }
    }

    private async void OnAddVideoFileClick(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        InitializeWithWindow.Initialize(picker, _hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
        picker.FileTypeFilter.Add(".mp4");
        picker.FileTypeFilter.Add(".mkv");
        picker.FileTypeFilter.Add(".webm");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await ViewModel.SettingsViewModel.AddVideoFileAsync(file.Path);
        }
    }

    private async void OnSaveWeightsClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SettingsViewModel.UpdateDailyWeightAsync(ViewModel.SettingsViewModel.DailyWeight);
    }

    private async void OnSaveWidgetSettingsClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SettingsViewModel.SaveWidgetSettingsAsync();
    }

    private void OnAutostartToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            ViewModel.SettingsViewModel.ToggleAutostart(toggleSwitch.IsOn);
        }
    }

    private async void OnCreateBackupClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SettingsViewModel.CreateBackupNowAsync();
    }

    private async void OnRestoreBackupClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string backupPath)
        {
            await ViewModel.SettingsViewModel.RestoreBackupAsync(backupPath);
        }
    }

    #endregion

    #region Event Handlers: Modals & Overlays

    private void OnCloseWeeklyGoalModalClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseWeeklyGoalModal();
    }

    private async void OnWeeklyGoalLockClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.WeeklyGoalModalViewModel.SaveAndLockAsync();
        ViewModel.CloseWeeklyGoalModal();
    }

    private async void OnWeeklyGoalSaveDraftClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.WeeklyGoalModalViewModel.SaveDraftAsync();
        ViewModel.CloseWeeklyGoalModal();
    }

    private async void OnWeeklyGoalPostponeClick(object sender, RoutedEventArgs e)
    {
        var reason = ViewModel.WeeklyGoalModalViewModel.ActionReason;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            await ViewModel.WeeklyGoalModalViewModel.PostponeAsync(reason);
            ViewModel.CloseWeeklyGoalModal();
        }
    }

    private async void OnWeeklyGoalCancelActionClick(object sender, RoutedEventArgs e)
    {
        var reason = ViewModel.WeeklyGoalModalViewModel.ActionReason;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            await ViewModel.WeeklyGoalModalViewModel.CancelAsync(reason);
            ViewModel.CloseWeeklyGoalModal();
        }
    }

    private void OnCloseAddBigGoalModalClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseAddGoalModal();
    }

    private void OnGoalIconSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item && item.Content is string iconKey)
        {
            ViewModel.GoalsViewModel.NewGoalIconKey = iconKey;
        }
    }

    private void OnAddMilestoneToNewGoalClick(object sender, RoutedEventArgs e)
    {
        ViewModel.GoalsViewModel.AddMilestoneToNewGoal();
    }

    private void OnRemoveMilestoneFromNewGoalClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string title)
        {
            ViewModel.GoalsViewModel.RemoveMilestoneFromNewGoal(title);
        }
    }

    private async void OnSaveNewBigGoalClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.GoalsViewModel.SaveNewGoalAsync();
    }

    private void OnCloseMonthlyRecapClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseMonthlyRecap();
    }

    private async void OnFinalizeMonthClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.MonthlyRecapViewModel.FinalizeMonthAsync();
    }

    #endregion
}
