using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.WindowsIntegration.Capture;

/// <summary>
/// Сервис захвата экрана для шагов инструкции на базе Win32 GDI.
/// Сохраняет чистые (unannotated) скриншоты для векторного наложения в UI
/// и гарантирует генерацию валидного PNG даже в изолированных сессиях.
/// </summary>
public sealed class ScreenCaptureService : IScreenCaptureService
{
    private const int SRCCOPY = 0x00CC0020;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const uint PW_RENDERFULLCONTENT = 2;

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleBitmap(nint hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint hgdiobj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(nint hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, nint hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(nint hwnd, nint hdcBlt, uint nFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private readonly object _syncLock = new();

    /// <summary>
    /// Захватывает чистый (unannotated) снимок экрана или окна и сохраняет его на диск.
    /// Векторная подсветка накладывается на уровне UI и не впекается в базовый файл.
    /// </summary>
    public string? Capture(string projectRootPath, int sequenceIndex, BoundingBox? targetRegion = null, long windowHandle = 0)
    {
        return CaptureInternal(projectRootPath, sequenceIndex, targetRegion, windowHandle, annotate: false);
    }

    /// <summary>
    /// Захватывает снимок экрана с впеканием акцентной рамки подсветки (для экспорта).
    /// </summary>
    public string? CaptureAnnotated(string projectRootPath, int sequenceIndex, BoundingBox targetRegion, long windowHandle = 0)
    {
        return CaptureInternal(projectRootPath, sequenceIndex, targetRegion, windowHandle, annotate: true);
    }

    private string? CaptureInternal(string projectRootPath, int sequenceIndex, BoundingBox? targetRegion, long windowHandle, bool annotate)
    {
        try
        {
            var screenshotsDir = Path.Combine(projectRootPath, "assets", "screenshots");
            Directory.CreateDirectory(screenshotsDir);

            var fileName = $"step_{sequenceIndex:D3}.png";
            var fullPath = Path.Combine(screenshotsDir, fileName);

            lock (_syncLock)
            {
                using var bitmap = CaptureBitmap(windowHandle, out int originX, out int originY);

                if (annotate && targetRegion.HasValue && !targetRegion.Value.IsEmpty)
                {
                    HighlightTargetRegion(bitmap, targetRegion.Value, originX, originY);
                }

                bitmap.Save(fullPath, ImageFormat.Png);
            }

            return $"assets/screenshots/{fileName}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScreenCaptureService] Ошибка захвата экрана: {ex.Message}");
            return null;
        }
    }

    private static Bitmap CaptureBitmap(long windowHandle, out int originX, out int originY)
    {
        // 1. Если передан дескриптор окна, пробуем захватить конкретное окно
        if (windowHandle != 0 && IsWindow((nint)windowHandle))
        {
            nint hWnd = (nint)windowHandle;
            if (GetWindowRect(hWnd, out var winRect))
            {
                int winWidth = winRect.Right - winRect.Left;
                int winHeight = winRect.Bottom - winRect.Top;

                if (winWidth > 0 && winHeight > 0)
                {
                    // Для окна клиентский/оконный DC имеет начало координат (0, 0).
                    // Не смешиваем начало виртуального рабочего стола с контекстом окна!
                    originX = winRect.Left;
                    originY = winRect.Top;

                    var winBmp = CaptureWindowGdi(hWnd, winWidth, winHeight);
                    if (winBmp != null)
                    {
                        return winBmp;
                    }
                }
            }
        }

        // 2. Стандартизированный захват полного виртуального экрана
        return CaptureFullDesktopGdi(out originX, out originY);
    }

    private static Bitmap? CaptureWindowGdi(nint hWnd, int width, int height)
    {
        nint winDc = GetDC(hWnd);
        if (winDc == nint.Zero)
        {
            return null;
        }

        try
        {
            nint memDc = CreateCompatibleDC(winDc);
            if (memDc == nint.Zero)
            {
                return null;
            }

            try
            {
                nint hBitmap = CreateCompatibleBitmap(winDc, width, height);
                if (hBitmap == nint.Zero)
                {
                    return null;
                }

                try
                {
                    nint oldBitmap = SelectObject(memDc, hBitmap);
                    try
                    {
                        // Для контекста окна источник начинается строго в (0, 0)
                        bool bltSuccess = BitBlt(memDc, 0, 0, width, height, winDc, 0, 0, SRCCOPY);
                        if (!bltSuccess)
                        {
                            bltSuccess = PrintWindow(hWnd, memDc, PW_RENDERFULLCONTENT);
                        }

                        if (bltSuccess)
                        {
                            using var tempImage = Image.FromHbitmap(hBitmap);
                            return new Bitmap(tempImage);
                        }
                    }
                    finally
                    {
                        if (oldBitmap != nint.Zero && oldBitmap != (nint)(-1))
                        {
                            SelectObject(memDc, oldBitmap);
                        }
                    }
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            finally
            {
                DeleteDC(memDc);
            }
        }
        finally
        {
            ReleaseDC(hWnd, winDc);
        }

        return null;
    }

    private static Bitmap CaptureFullDesktopGdi(out int originX, out int originY)
    {
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        originX = GetSystemMetrics(SM_XVIRTUALSCREEN);
        originY = GetSystemMetrics(SM_YVIRTUALSCREEN);

        if (width <= 0 || height <= 0)
        {
            width = GetSystemMetrics(SM_CXSCREEN);
            height = GetSystemMetrics(SM_CYSCREEN);
            originX = 0;
            originY = 0;
        }

        if (width <= 0 || height <= 0)
        {
            width = 1920;
            height = 1080;
            originX = 0;
            originY = 0;
        }

        // Всегда получаем DC рабочего стола GetDesktopWindow() с виртуальными экранными габаритами
        nint deskHwnd = GetDesktopWindow();
        nint deskDc = GetDC(deskHwnd);

        if (deskDc != nint.Zero)
        {
            try
            {
                nint memDc = CreateCompatibleDC(deskDc);
                if (memDc != nint.Zero)
                {
                    try
                    {
                        nint hBitmap = CreateCompatibleBitmap(deskDc, width, height);
                        if (hBitmap != nint.Zero)
                        {
                            try
                            {
                                nint oldBitmap = SelectObject(memDc, hBitmap);
                                try
                                {
                                    bool bltSuccess = BitBlt(memDc, 0, 0, width, height, deskDc, originX, originY, SRCCOPY);
                                    if (bltSuccess)
                                    {
                                        using var tempImage = Image.FromHbitmap(hBitmap);
                                        return new Bitmap(tempImage);
                                    }
                                }
                                finally
                                {
                                    if (oldBitmap != nint.Zero && oldBitmap != (nint)(-1))
                                    {
                                        SelectObject(memDc, oldBitmap);
                                    }
                                }
                            }
                            finally
                            {
                                DeleteObject(hBitmap);
                            }
                        }
                    }
                    finally
                    {
                        DeleteDC(memDc);
                    }
                }
            }
            finally
            {
                ReleaseDC(deskHwnd, deskDc);
            }
        }

        return CreateFallbackCanvas(width, height);
    }

    /// <summary>
    /// Транслирует абсолютные экранные координаты в относительные координаты битмапа с учетом смещения начала захвата (originX, originY).
    /// Гарантирует корректную обработку отрицательных координат виртуального экрана (мультимониторные конфигурации) без переполнения.
    /// </summary>
    /// <param name="absoluteBounds">Абсолютные координаты элемента на виртуальном экране или внутри окна.</param>
    /// <param name="originX">Смещение X начала области захвата (например, SM_XVIRTUALSCREEN или Rect.Left окна).</param>
    /// <param name="originY">Смещение Y начала области захвата (например, SM_YVIRTUALSCREEN или Rect.Top окна).</param>
    /// <returns>Относительные координаты BoundingBox для отображения на битмапе.</returns>
    public static BoundingBox TranslateToBitmapCoordinates(BoundingBox absoluteBounds, int originX, int originY)
    {
        if (absoluteBounds.IsEmpty)
        {
            return BoundingBox.Empty;
        }

        double relX = absoluteBounds.X - originX;
        double relY = absoluteBounds.Y - originY;

        if (!double.IsFinite(relX) || !double.IsFinite(relY) ||
            !double.IsFinite(absoluteBounds.Width) || !double.IsFinite(absoluteBounds.Height))
        {
            return BoundingBox.Empty;
        }

        return new BoundingBox(relX, relY, absoluteBounds.Width, absoluteBounds.Height);
    }

    private static void HighlightTargetRegion(Bitmap bitmap, BoundingBox rect, int originX, int originY)
    {
        var rel = TranslateToBitmapCoordinates(rect, originX, originY);
        if (rel.IsEmpty)
        {
            return;
        }

        // Защита от переполнений при приведении к целым координатам GDI+
        int targetX = (int)Math.Clamp(Math.Round(rel.X), -100_000, 100_000);
        int targetY = (int)Math.Clamp(Math.Round(rel.Y), -100_000, 100_000);
        int width = (int)Math.Clamp(Math.Round(rel.Width), 0, 100_000);
        int height = (int)Math.Clamp(Math.Round(rel.Height), 0, 100_000);

        if (width > 0 && height > 0)
        {
            using var graphics = Graphics.FromImage(bitmap);
            using var pen = new Pen(Color.FromArgb(239, 68, 68), 3); // Modern Red (#EF4444)
            using var fillBrush = new SolidBrush(Color.FromArgb(40, 239, 68, 68));
            graphics.DrawRectangle(pen, targetX, targetY, width, height);
            graphics.FillRectangle(fillBrush, targetX, targetY, width, height);
        }
    }

    private static Bitmap CreateFallbackCanvas(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.FromArgb(30, 30, 36));

            if (width > 20 && height > 20)
            {
                using var pen = new Pen(Color.FromArgb(70, 70, 80), 1);
                graphics.DrawRectangle(pen, 10, 10, width - 20, height - 20);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
