using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;

namespace PersonalProvocateur.App.UI;

/// <summary>
/// Dark-themed borderless popup menu displayed when the user clicks the system tray icon.
/// </summary>
public partial class TrayPopupMenu : Window
{
    private readonly OverlayCoordinator? _coordinator;
    private readonly RandomReminderScheduler? _scheduler;
    private readonly SettingsStore? _settingsStore;
    private readonly Action? _openSettingsAction;

    /// <summary>
    /// Initializes a new instance of <see cref="TrayPopupMenu"/>.
    /// </summary>
    /// <param name="coordinator">Overlay coordinator.</param>
    /// <param name="scheduler">Reminder scheduler.</param>
    /// <param name="settingsStore">Settings store.</param>
    /// <param name="openSettingsAction">Callback to open settings window.</param>
    public TrayPopupMenu(
        OverlayCoordinator coordinator,
        RandomReminderScheduler scheduler,
        SettingsStore settingsStore,
        Action openSettingsAction)
    {
        InitializeComponent();

        _coordinator = coordinator;
        _scheduler = scheduler;
        _settingsStore = settingsStore;
        _openSettingsAction = openSettingsAction;

        Loaded += OnLoaded;
        Deactivated += OnDeactivated;
    }

    /// <summary>
    /// Parameterless constructor for designer and testing support.
    /// </summary>
    public TrayPopupMenu() : this(null!, null!, null!, null!)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        try
        {
            Close();
        }
        catch
        {
            // Suppress close exceptions if already closing
        }
    }

    /// <summary>
    /// Positions and displays the menu near the given screen coordinates, clamped to the primary work area.
    /// </summary>
    /// <param name="screenX">Cursor X coordinate in screen pixels.</param>
    /// <param name="screenY">Cursor Y coordinate in screen pixels.</param>
    public void ShowNear(int screenX, int screenY)
    {
        RefreshStatus();

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double targetWidth = DesiredSize.Width > 0 ? DesiredSize.Width : 260;
        double targetHeight = DesiredSize.Height > 0 ? DesiredSize.Height : 240;

        var workArea = SystemParameters.WorkArea;

        double left = screenX - targetWidth / 2;
        double top = screenY - targetHeight - 8; // Appear above cursor/tray by default

        if (left + targetWidth > workArea.Right)
        {
            left = workArea.Right - targetWidth - 8;
        }

        if (left < workArea.Left)
        {
            left = workArea.Left + 8;
        }

        if (top < workArea.Top)
        {
            top = screenY + 8; // If not enough room above, appear below
        }

        if (top + targetHeight > workArea.Bottom)
        {
            top = workArea.Bottom - targetHeight - 8;
        }

        Left = left;
        Top = top;

        Show();
        Activate();
    }

    private void RefreshStatus()
    {
        if (_coordinator == null || _scheduler == null)
        {
            NextTriggerTextBlock.Text = "Следующий: --:--";
            return;
        }

        if (_coordinator.CurrentState is SessionState.MascotOverlayActive or SessionState.VideoPlaying)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6));
            NextTriggerTextBlock.Text = "Статус: Оверлей активен";
            PauseResumeButton.Content = "⏸ Приостановить";
        }
        else if (_scheduler.IsPaused)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            NextTriggerTextBlock.Text = "Статус: На паузе";
            PauseResumeButton.Content = "▶ Возобновить";
        }
        else if (_scheduler.NextTriggerTimeLocal.HasValue)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            NextTriggerTextBlock.Text = $"Следующий: {_scheduler.NextTriggerTimeLocal.Value:HH:mm:ss}";
            PauseResumeButton.Content = "⏸ Приостановить";
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            NextTriggerTextBlock.Text = "Статус: Ожидание...";
            PauseResumeButton.Content = "⏸ Приостановить";
        }
    }

    private void TriggerNowButton_Click(object sender, RoutedEventArgs e)
    {
        _coordinator?.TriggerProvocation(true);
        Close();
    }

    private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator != null && _scheduler != null)
        {
            bool newPaused = !_scheduler.IsPaused;
            _coordinator.SetPaused(newPaused);
            RefreshStatus();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _openSettingsAction?.Invoke();
        Close();
    }

    private void DismissOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        _coordinator?.EmergencyDismiss();
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current?.Shutdown();
    }
}
