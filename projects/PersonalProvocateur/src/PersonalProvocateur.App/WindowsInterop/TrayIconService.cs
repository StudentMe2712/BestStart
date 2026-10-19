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
    /// Creates the application tray icon using the brand squircle logo (ADR-011)
    /// or high-quality programmatic obsidian-violet crystal squircle fallback.
    /// </summary>
    public static Icon CreateApplicationIcon()
    {
        string? logoPath = FindLogoFile();
        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
        {
            try
            {
                return CreateIconFromImage(logoPath);
            }
            catch
            {
                // Fall back to programmatic squircle on decode error
            }
        }

        return CreateProgrammaticSquircleIcon();
    }

    /// <summary>
    /// Backwards compatibility alias for CreateApplicationIcon.
    /// </summary>
    public static Icon CreateProgrammaticIcon() => CreateApplicationIcon();

    private static string? FindLogoFile()
    {
        string[] candidates =
        [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.png"),
            Path.Combine(AppPaths.AppDataDirectory, "Assets", "logo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png"),
            "Assets/logo.png",
            "logo.png"
        ];

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static Icon CreateIconFromImage(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var original = Image.FromStream(stream);
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.Clear(Color.Transparent);

            // macOS Squircle clip (32x32 with 7px corner radius)
            using var path = CreateRoundedRectanglePath(new RectangleF(0.5f, 0.5f, 31f, 31f), 7f);

            // Subtle gradient background behind logo
            using (var bgBrush = new LinearGradientBrush(
                new PointF(0, 0),
                new PointF(32, 32),
                Color.FromArgb(0x1E, 0x1B, 0x4B),
                Color.FromArgb(0x6D, 0x28, 0xD9)))
            {
                g.FillPath(bgBrush, path);
            }

            g.SetClip(path);
            g.DrawImage(original, new RectangleF(1f, 1f, 30f, 30f));
            g.ResetClip();

            // Subtle border: #40FFFFFF
            using var borderPen = new System.Drawing.Pen(Color.FromArgb(64, 255, 255, 255), 1.0f);
            g.DrawPath(borderPen, path);
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

    private static Icon CreateProgrammaticSquircleIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            using var path = CreateRoundedRectanglePath(new RectangleF(0.5f, 0.5f, 31f, 31f), 7f);

            // Deep obsidian-purple squircle background (#1E1B4B -> #6D28D9)
            using (var bgBrush = new LinearGradientBrush(
                new PointF(0, 0),
                new PointF(32, 32),
                Color.FromArgb(0x1E, 0x1B, 0x4B),
                Color.FromArgb(0x6D, 0x28, 0xD9)))
            {
                g.FillPath(bgBrush, path);
            }

            // Crisp neon crystal inside (IconBrandPrism)
            PointF[] outerPoints =
            [
                new(16f, 6f),
                new(6f, 12.5f),
                new(16f, 26f),
                new(26f, 12.5f)
            ];
            PointF[] innerPoints =
            [
                new(16f, 9.2f),
                new(22.5f, 13.5f),
                new(16f, 22.2f),
                new(9.5f, 13.5f)
            ];

            using (var crystalFill = new SolidBrush(Color.FromArgb(180, 0xC4, 0xB5, 0xFD)))
            {
                g.FillPolygon(crystalFill, innerPoints);
            }

            using (var outerPen = new System.Drawing.Pen(Color.FromArgb(0x8B, 0x5C, 0xF6), 1.5f))
            {
                g.DrawPolygon(outerPen, outerPoints);
            }

            using (var innerPen = new System.Drawing.Pen(Color.FromArgb(0xEE, 0xF2, 0xFF), 1.2f))
            {
                g.DrawPolygon(innerPen, innerPoints);
                g.DrawLine(innerPen, outerPoints[0], innerPoints[0]);
                g.DrawLine(innerPen, outerPoints[1], innerPoints[3]);
                g.DrawLine(innerPen, outerPoints[2], innerPoints[2]);
                g.DrawLine(innerPen, outerPoints[3], innerPoints[1]);
            }

            // Subtle border: #40FFFFFF
            using var borderPen = new System.Drawing.Pen(Color.FromArgb(64, 255, 255, 255), 1.0f);
            g.DrawPath(borderPen, path);
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

    private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2f;
        var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
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
