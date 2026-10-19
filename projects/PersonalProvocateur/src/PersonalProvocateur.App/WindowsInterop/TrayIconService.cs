using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
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

        _icon = CreateApplicationIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "Provocateur",
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
    /// Creates the application tray icon on a 100% transparent background with smooth IconBrandPrism crystal (ADR-017).
    /// </summary>
    public static Icon CreateApplicationIcon()
    {
        using var bitmap = CreateCrystalBitmap(32);
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
    /// Creates a high-resolution WPF ImageSource on 100% transparent background for SettingsWindow.Icon and Windows Taskbar (ADR-017).
    /// </summary>
    public static System.Windows.Media.ImageSource CreateTaskbarIconSource()
    {
        using var bitmap = CreateCrystalBitmap(48);
        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>
    /// Backwards compatibility alias for CreateApplicationIcon.
    /// </summary>
    public static Icon CreateProgrammaticIcon() => CreateApplicationIcon();

    private static Bitmap CreateCrystalBitmap(int size)
    {
        var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float scale = size / 24.0f;

            // Coordinates matching IconBrandPrism in 24x24 viewBox:
            // Outer: (12, 2) -> (2, 8.5) -> (12, 22) -> (22, 8.5)
            // Inner: (12, 5.2) -> (18.5, 9.5) -> (12, 18.2) -> (5.5, 9.5)
            PointF[] outerPoints =
            [
                new PointF(12f * scale, 2f * scale),
                new PointF(2f * scale, 8.5f * scale),
                new PointF(12f * scale, 22f * scale),
                new PointF(22f * scale, 8.5f * scale)
            ];

            PointF[] innerPoints =
            [
                new PointF(12f * scale, 5.2f * scale),
                new PointF(18.5f * scale, 9.5f * scale),
                new PointF(12f * scale, 18.2f * scale),
                new PointF(5.5f * scale, 9.5f * scale)
            ];

            // Neon-purple crystal facet fills
            using (var innerBrush = new SolidBrush(Color.FromArgb(200, 0x8B, 0x5C, 0xF6)))
            {
                g.FillPolygon(innerBrush, innerPoints);
            }

            // Top facet highlight
            PointF[] topFacet = [outerPoints[0], innerPoints[0], innerPoints[1], outerPoints[3]];
            using (var topBrush = new SolidBrush(Color.FromArgb(90, 0xC4, 0xB5, 0xFD)))
            {
                g.FillPolygon(topBrush, topFacet);
            }

            // Left facet
            PointF[] leftFacet = [outerPoints[0], outerPoints[1], innerPoints[3], innerPoints[0]];
            using (var leftBrush = new SolidBrush(Color.FromArgb(60, 0x7C, 0x3A, 0xED)))
            {
                g.FillPolygon(leftBrush, leftFacet);
            }

            // Facet connector lines
            using (var facetPen = new System.Drawing.Pen(Color.FromArgb(220, 0xC4, 0xB5, 0xFD), 1.2f * scale))
            {
                g.DrawLine(facetPen, outerPoints[0], innerPoints[0]);
                g.DrawLine(facetPen, outerPoints[1], innerPoints[3]);
                g.DrawLine(facetPen, outerPoints[2], innerPoints[2]);
                g.DrawLine(facetPen, outerPoints[3], innerPoints[1]);
            }

            // Crisp neon violet outer contour
            using (var outerPen = new System.Drawing.Pen(Color.FromArgb(0x8B, 0x5C, 0xF6), 1.8f * scale))
            {
                g.DrawPolygon(outerPen, outerPoints);
            }

            // Inner contour
            using (var innerPen = new System.Drawing.Pen(Color.FromArgb(0xEE, 0xF2, 0xFF), 1.2f * scale))
            {
                g.DrawPolygon(innerPen, innerPoints);
            }
        }

        return bitmap;
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
