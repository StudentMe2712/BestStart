using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.WindowsIntegration.Capture;

/// <summary>
/// Сервис захвата экрана для шагов инструкции на базе Win32 GDI с автоматической подсветкой активного элемента
/// и гарантированной генерацией валидного PNG даже в изолированных сессиях.
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

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(nint hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, nint hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(nint hwnd, nint hdcBlt, uint nFlags);

    private readonly object _syncLock = new();

    public string? Capture(string projectRootPath, int sequenceIndex, BoundingBox? targetRegion = null, long windowHandle = 0)
    {
        try
        {
            var screenshotsDir = Path.Combine(projectRootPath, "assets", "screenshots");
            Directory.CreateDirectory(screenshotsDir);

            var fileName = $"step_{sequenceIndex:D3}.png";
            var fullPath = Path.Combine(screenshotsDir, fileName);

            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            int originX = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int originY = GetSystemMetrics(SM_YVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
            {
                width = GetSystemMetrics(SM_CXSCREEN);
                height = GetSystemMetrics(SM_CYSCREEN);
            }

            if (width <= 0 || height <= 0)
            {
                width = 1920;
                height = 1080;
                originX = 0;
                originY = 0;
            }

            lock (_syncLock)
            {
                using var bitmap = CaptureDesktopOrWindowGdi(originX, originY, width, height, (nint)windowHandle);

                // Накладываем акцентную рамку подсветки вокруг целевого элемента
                if (targetRegion.HasValue && !targetRegion.Value.IsEmpty)
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

    private static Bitmap CaptureDesktopOrWindowGdi(int x, int y, int width, int height, nint windowHandle)
    {
        nint deskHwnd = windowHandle != nint.Zero ? windowHandle : GetDesktopWindow();
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
                                bool bltSuccess = BitBlt(memDc, 0, 0, width, height, deskDc, x, y, SRCCOPY);

                                SelectObject(memDc, oldBitmap);

                                if (bltSuccess)
                                {
                                    using var tempImage = Image.FromHbitmap(hBitmap);
                                    return new Bitmap(tempImage);
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

        // Отказоустойчивый синтетический холст, если десктоп заблокирован или сессия неинтерактивна
        return CreateFallbackCanvas(width, height);
    }

    private static void HighlightTargetRegion(Bitmap bitmap, BoundingBox rect, int originX, int originY)
    {
        using var graphics = Graphics.FromImage(bitmap);
        int targetX = (int)(rect.X - originX);
        int targetY = (int)(rect.Y - originY);
        int width = (int)rect.Width;
        int height = (int)rect.Height;

        if (width > 0 && height > 0)
        {
            using var pen = new Pen(Color.FromArgb(239, 68, 68), 3); // Modern Red (#EF4444)
            graphics.DrawRectangle(pen, targetX, targetY, width, height);

            using var fillBrush = new SolidBrush(Color.FromArgb(40, 239, 68, 68));
            graphics.FillRectangle(fillBrush, targetX, targetY, width, height);
        }
    }

    private static Bitmap CreateFallbackCanvas(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(30, 30, 36));

        using var pen = new Pen(Color.FromArgb(70, 70, 80), 1);
        graphics.DrawRectangle(pen, 10, 10, width - 20, height - 20);

        return bitmap;
    }
}
