using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.UI;

namespace PersonalProvocateur.App.WindowsInterop;

/// <summary>
/// Manages the system notification area (system tray) icon and its interaction menu.
/// Uses native WinForms NotifyIcon without third-party dependencies (ADR-001).
/// </summary>
public sealed class TrayIconService : IDisposable
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly OverlayCoordinator _coordinator;
    private readonly RandomReminderScheduler _scheduler;
    private readonly SettingsStore _settingsStore;
    private readonly Action _openSettingsAction;
    private TrayPopupMenu? _activeMenu;
    private bool _isDisposed;

    /// <summary>
    /// Gets or sets the tooltip text displayed when hovering over the notification area icon.
    /// Clamped to 63 characters to conform to standard Win32 NOTIFYICONDATA limits.
    /// </summary>
    public string Text
    {
        get => _notifyIcon.Text;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                _notifyIcon.Text = string.Empty;
                return;
            }

            _notifyIcon.Text = value.Length > 63 ? value[..63] : value;
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TrayIconService"/>.
    /// </summary>
    /// <param name="coordinator">Overlay coordinator.</param>
    /// <param name="scheduler">Reminder scheduler.</param>
    /// <param name="settingsStore">Application settings store.</param>
    /// <param name="openSettingsAction">Callback to open settings window.</param>
    public TrayIconService(
        OverlayCoordinator coordinator,
        RandomReminderScheduler scheduler,
        SettingsStore settingsStore,
        Action openSettingsAction)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _openSettingsAction = openSettingsAction ?? throw new ArgumentNullException(nameof(openSettingsAction));

        _icon = CreateProgrammaticIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "Personal Provocateur",
            Visible = true
        };

        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
    }

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
    {
        var cursorPosition = Cursor.Position;
        ShowTrayMenu(cursorPosition.X, cursorPosition.Y);
    }

    /// <summary>
    /// Displays the dark theme popup menu near the given screen coordinates.
    /// </summary>
    public void ShowTrayMenu(int screenX, int screenY)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(() => ShowTrayMenu(screenX, screenY));
            return;
        }

        if (_activeMenu != null)
        {
            try
            {
                _activeMenu.Close();
            }
            catch
            {
                // Suppress close exceptions
            }
            _activeMenu = null;
        }

        _activeMenu = new TrayPopupMenu(_coordinator, _scheduler, _settingsStore, _openSettingsAction);
        _activeMenu.Closed += (s, e) => _activeMenu = null;
        _activeMenu.ShowNear(screenX, screenY);
    }

    /// <summary>
    /// Generates a crisp 32x32 icon in memory without external .ico dependencies.
    /// </summary>
    private static Icon CreateProgrammaticIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            // Purple circle background (#8B5CF6)
            using (var brush = new SolidBrush(Color.FromArgb(0x8B, 0x5C, 0xF6)))
            {
                graphics.FillEllipse(brush, 1, 1, 30, 30);
            }

            // Subtle bright border
            using (var borderPen = new System.Drawing.Pen(Color.FromArgb(0xC4, 0xB5, 0xFD), 1.5f))
            {
                graphics.DrawEllipse(borderPen, 1, 1, 30, 30);
            }

            // Bold "P" character
            using var font = new Font("Segoe UI", 16, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            graphics.DrawString("P", font, textBrush, new RectangleF(0, 0, 32, 32), format);
        }

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>
    /// Removes the tray icon and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            if (_activeMenu != null)
            {
                _activeMenu.Close();
                _activeMenu = null;
            }

            _notifyIcon.Visible = false;
            _notifyIcon.MouseClick -= OnNotifyIconMouseClick;
            _notifyIcon.Dispose();
            _icon.Dispose();
        }
        catch
        {
            // Suppress cleanup exceptions
        }

        GC.SuppressFinalize(this);
    }
}
