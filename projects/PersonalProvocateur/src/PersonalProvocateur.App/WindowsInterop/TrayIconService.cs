using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
        var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(icoPath))
        {
            try
            {
                using var fullIcon = new Icon(icoPath, 32, 32);
                return (Icon)fullIcon.Clone();
            }
            catch
            {
                // Fall back to procedural generation
            }
        }

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
        var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(icoPath))
        {
            try
            {
                var uri = new Uri(icoPath, UriKind.Absolute);
                var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                    uri,
                    System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                frame.Freeze();
                return frame;
            }
            catch
            {
                // Fall back to procedural generation
            }
        }

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

    /// <summary>
    /// Creates the crystal icon bitmap in 32-bit ARGB format with transparent background (ADR-017).
    /// </summary>
    public static Bitmap CreateCrystalBitmap(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
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
    /// Generates multi-resolution .ico binary data for the crystal icon in standard Windows resolutions.
    /// </summary>
    /// <param name="sizes">Optional resolution list. Defaults to 16, 24, 32, 48, 64, 128, 256.</param>
    /// <returns>Valid .ico byte array.</returns>
    public static byte[] CreateIcoData(int[]? sizes = null)
    {
        sizes ??= [16, 24, 32, 48, 64, 128, 256];
        var entries = new List<(int width, int height, byte[] data)>();

        foreach (var size in sizes)
        {
            using var bmp = CreateCrystalBitmap(size);
            byte[] data;
            if (size >= 256)
            {
                using var pngMs = new MemoryStream();
                bmp.Save(pngMs, ImageFormat.Png);
                data = pngMs.ToArray();
            }
            else
            {
                data = BitmapToDibData(bmp);
            }
            entries.Add((size, size, data));
        }

        using var outMs = new MemoryStream();
        using var bw = new BinaryWriter(outMs);

        // ICONDIR
        bw.Write((ushort)0); // idReserved
        bw.Write((ushort)1); // idType (1 = Icon)
        bw.Write((ushort)entries.Count); // idCount

        int currentOffset = 6 + (16 * entries.Count);

        foreach (var entry in entries)
        {
            byte w = entry.width >= 256 ? (byte)0 : (byte)entry.width;
            byte h = entry.height >= 256 ? (byte)0 : (byte)entry.height;

            bw.Write(w);
            bw.Write(h);
            bw.Write((byte)0); // bColorCount
            bw.Write((byte)0); // bReserved
            bw.Write((ushort)1); // wPlanes
            bw.Write((ushort)32); // wBitCount
            bw.Write((uint)entry.data.Length);
            bw.Write((uint)currentOffset);

            currentOffset += entry.data.Length;
        }

        foreach (var entry in entries)
        {
            bw.Write(entry.data);
        }

        bw.Flush();
        return outMs.ToArray();
    }

    /// <summary>
    /// Ensures that the specified or default app.ico file exists on disk.
    /// If missing, generates it from the crystal vector icon definition.
    /// </summary>
    public static void EnsureAppIconFile(string? icoPath = null)
    {
        icoPath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(icoPath))
        {
            return;
        }

        var dir = Path.GetDirectoryName(icoPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var data = CreateIcoData();
        File.WriteAllBytes(icoPath, data);
    }

    private static byte[] BitmapToDibData(Bitmap bmp)
    {
        int width = bmp.Width;
        int height = bmp.Height;
        int andMaskRowSize = ((width + 31) / 32) * 4;
        int andMaskSize = andMaskRowSize * height;
        int xorSize = width * height * 4;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // BITMAPINFOHEADER (40 bytes)
        bw.Write((uint)40); // biSize
        bw.Write(width);    // biWidth
        bw.Write(height * 2); // biHeight (doubled for XOR + AND mask)
        bw.Write((ushort)1);  // biPlanes
        bw.Write((ushort)32); // biBitCount
        bw.Write((uint)0);    // biCompression (BI_RGB)
        bw.Write((uint)(xorSize + andMaskSize)); // biSizeImage
        bw.Write(0);        // biXPelsPerMeter
        bw.Write(0);        // biYPelsPerMeter
        bw.Write((uint)0);    // biClrUsed
        bw.Write((uint)0);    // biClrImportant

        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        byte[] rawBytes = new byte[bmpData.Stride * height];
        Marshal.Copy(bmpData.Scan0, rawBytes, 0, rawBytes.Length);
        bmp.UnlockBits(bmpData);

        byte[] xorData = new byte[xorSize];
        byte[] andData = new byte[andMaskSize];

        for (int y = 0; y < height; y++)
        {
            int dibY = height - 1 - y;
            int srcRowStart = y * bmpData.Stride;
            int dstRowStart = dibY * width * 4;

            for (int x = 0; x < width; x++)
            {
                int srcIdx = srcRowStart + (x * 4);
                int dstIdx = dstRowStart + (x * 4);

                byte b = rawBytes[srcIdx + 0];
                byte g = rawBytes[srcIdx + 1];
                byte r = rawBytes[srcIdx + 2];
                byte a = rawBytes[srcIdx + 3];

                xorData[dstIdx + 0] = b;
                xorData[dstIdx + 1] = g;
                xorData[dstIdx + 2] = r;
                xorData[dstIdx + 3] = a;

                if (a == 0)
                {
                    int andByteIdx = dibY * andMaskRowSize + (x / 8);
                    int andBitIdx = 7 - (x % 8);
                    andData[andByteIdx] |= (byte)(1 << andBitIdx);
                }
            }
        }

        bw.Write(xorData);
        bw.Write(andData);
        bw.Flush();
        return ms.ToArray();
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
