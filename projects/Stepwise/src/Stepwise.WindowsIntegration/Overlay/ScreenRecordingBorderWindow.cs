using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Overlay;

/// <summary>
/// Прозрачное полноэкранное окно-оверлей со стилем WS_EX_TRANSPARENT,
/// отображающее периметральную рамку активной записи экрана, информационный плавающий виджет
/// и мгновенную визуальную подсветку захваченного элемента (Live Spotlight Ripple / Toast).
/// Полностью пассивно: не перехватывает клики мыши (HTTRANSPARENT) и не активируется (WS_EX_NOACTIVATE).
/// </summary>
public sealed class ScreenRecordingBorderWindow : IScreenRecordingIndicator
{
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const uint SW_SHOWNOACTIVATE = 4;
    private const uint SW_HIDE = 0;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly nint HWND_TOPMOST = -1;

    private const uint ULW_ALPHA = 0x00000002;
    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        nint hWnd,
        nint hdcDst,
        ref NativeMethods.POINT pptDst,
        ref NativeMethods.SIZE psize,
        nint hdcSrc,
        ref NativeMethods.POINT pptSrc,
        uint crKey,
        ref BLENDFUNCTION pblend,
        uint dwFlags
    );

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint hgdiobj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClass(string lpClassName, nint hInstance);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam
    );

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, uint nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    private readonly object _syncLock = new();
    private readonly Thread _windowThread;
    private readonly nint _hInstance;
    private readonly string _className;
    private readonly NativeMethods.WndProc _wndProcDelegate;

    private nint _hwnd = nint.Zero;
    private uint _threadId = 0;
    private ManualResetEventSlim? _initEvent;
    private bool _isDisposed;
    private bool _isActive;

    // Состояние отображения
    private DateTime _recordingStartedAt;
    private int _recordedStepCount;
    private System.Threading.Timer? _refreshTimer;
    private System.Threading.Timer? _flashResetTimer;

    // Данные мгновенной подсветки (Flash capture)
    private BoundingBox? _flashedElementBounds;
    private string? _flashedElementName;
    private int _flashedStepIndex;
    private bool _isFlashing;

    public ScreenRecordingBorderWindow()
    {
        _className = $"StepwiseRecordingBorderClass_{Guid.NewGuid():N}";
        _hInstance = NativeMethods.GetModuleHandle(null);
        _wndProcDelegate = WndProc;
        _initEvent = new ManualResetEventSlim(false);

        _windowThread = new Thread(RunWindowLoop)
        {
            IsBackground = true,
            Name = "Stepwise.RecordingBorderWindowThread"
        };
        _windowThread.SetApartmentState(ApartmentState.STA);
        _windowThread.Start();

        if (!_initEvent.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("Таймаут инициализации окна индикатора записи.");
        }
    }

    public void StartIndicator()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isActive = true;
            _recordingStartedAt = DateTime.UtcNow;
            _recordedStepCount = 0;
            _isFlashing = false;
            _flashedElementBounds = null;

            UpdateBoundsAndShow();

            _refreshTimer?.Dispose();
            _refreshTimer = new System.Threading.Timer(_ =>
            {
                lock (_syncLock)
                {
                    if (_isActive && !_isDisposed)
                    {
                        RenderVisuals();
                    }
                }
            }, null, 500, 1000);
        }
    }

    public void StopIndicator()
    {
        lock (_syncLock)
        {
            _isActive = false;
            _refreshTimer?.Dispose();
            _refreshTimer = null;
            _flashResetTimer?.Dispose();
            _flashResetTimer = null;

            if (_hwnd != nint.Zero && NativeMethods.IsWindow(_hwnd))
            {
                ShowWindow(_hwnd, SW_HIDE);
            }
        }
    }

    public void UpdateStepCount(int stepCount)
    {
        lock (_syncLock)
        {
            _recordedStepCount = stepCount;
            if (_isActive && !_isDisposed)
            {
                RenderVisuals();
            }
        }
    }

    public void FlashCapture(BoundingBox elementBounds, string elementName, int sequenceIndex)
    {
        lock (_syncLock)
        {
            if (!_isActive || _isDisposed)
            {
                return;
            }

            _isFlashing = true;
            _flashedElementBounds = elementBounds;
            _flashedElementName = string.IsNullOrWhiteSpace(elementName) ? "Элемент" : elementName;
            _flashedStepIndex = sequenceIndex;
            _recordedStepCount = Math.Max(_recordedStepCount, sequenceIndex);

            RenderVisuals();

            // Сбрасываем вспышку через 1.8 секунды
            _flashResetTimer?.Dispose();
            _flashResetTimer = new System.Threading.Timer(_ =>
            {
                lock (_syncLock)
                {
                    _isFlashing = false;
                    _flashedElementBounds = null;
                    if (_isActive && !_isDisposed)
                    {
                        RenderVisuals();
                    }
                }
            }, null, 1800, Timeout.Infinite);
        }
    }

    private void UpdateBoundsAndShow()
    {
        if (_hwnd == nint.Zero || !NativeMethods.IsWindow(_hwnd))
        {
            return;
        }

        int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        SetWindowPos(_hwnd, HWND_TOPMOST, vx, vy, vw, vh, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
        RenderVisuals();
    }

    private void RenderVisuals()
    {
        if (_hwnd == nint.Zero || !NativeMethods.IsWindow(_hwnd))
        {
            return;
        }

        int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        if (vw <= 0 || vh <= 0)
        {
            return;
        }

        using var bitmap = new Bitmap(vw, vh, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            // 1. Периметральная акцентная рамка вокруг экрана (Modern Red #EF4444)
            using (var borderPen = new Pen(Color.FromArgb(230, 239, 68, 68), 4))
            {
                g.DrawRectangle(borderPen, 2, 2, vw - 4, vh - 4);
            }

            // 2. Вспышка зафиксированного элемента (Live Spotlight Ripple)
            if (_isFlashing && _flashedElementBounds.HasValue && !_flashedElementBounds.Value.IsEmpty)
            {
                var eb = _flashedElementBounds.Value;
                int targetX = (int)Math.Clamp(Math.Round(eb.X - vx), 0, vw);
                int targetY = (int)Math.Clamp(Math.Round(eb.Y - vy), 0, vh);
                int targetW = (int)Math.Clamp(Math.Round(eb.Width), 4, vw);
                int targetH = (int)Math.Clamp(Math.Round(eb.Height), 4, vh);

                using var flashBrush = new SolidBrush(Color.FromArgb(50, 16, 185, 129)); // Modern Emerald tint
                using var flashPen = new Pen(Color.FromArgb(255, 16, 185, 129), 3);
                g.FillRectangle(flashBrush, targetX, targetY, targetW, targetH);
                g.DrawRectangle(flashPen, targetX, targetY, targetW, targetH);
            }

            // 3. Плавающий информационный виджет по центру вверху экрана
            RenderTopWidget(g, vw, vh);
        }

        ApplyBitmapToLayeredWindow(_hwnd, bitmap, vx, vy, vw, vh);
    }

    private void RenderTopWidget(Graphics g, int vw, int vh)
    {
        var elapsed = DateTime.UtcNow - _recordingStartedAt;
        string timeStr = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";

        string badgeText;
        Color pillBorderColor;
        Color dotColor;

        if (_isFlashing)
        {
            badgeText = $"✓ Шаг {_flashedStepIndex}: {_flashedElementName} ({timeStr})";
            pillBorderColor = Color.FromArgb(255, 16, 185, 129); // Emerald
            dotColor = Color.FromArgb(255, 16, 185, 129);
        }
        else
        {
            badgeText = $"● ЗАПИСЬ  |  {timeStr}  |  Шагов: {_recordedStepCount}  (Ctrl+Shift+R / F9)";
            pillBorderColor = Color.FromArgb(240, 239, 68, 68); // Red
            dotColor = Color.FromArgb(255, 239, 68, 68);
        }

        using var font = new System.Drawing.Font("Segoe UI", 10.5f, FontStyle.Bold);
        var textSize = g.MeasureString(badgeText, font);

        int pillWidth = (int)Math.Ceiling(textSize.Width) + 36;
        int pillHeight = 36;
        int pillX = (vw - pillWidth) / 2;
        int pillY = 12;

        // Фон виджета (Dark glass)
        using var bgBrush = new SolidBrush(Color.FromArgb(240, 24, 24, 27));
        using var borderPen = new Pen(pillBorderColor, 1.5f);

        var rect = new Rectangle(pillX, pillY, pillWidth, pillHeight);
        using var path = CreateRoundedRectangle(rect, 10);
        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);

        // Индикаторный круг
        int dotSize = 10;
        int dotX = pillX + 14;
        int dotY = pillY + (pillHeight - dotSize) / 2;
        using var dotBrush = new SolidBrush(dotColor);
        g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);

        // Текст
        using var textBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
        g.DrawString(badgeText, font, textBrush, pillX + 30, pillY + 7);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new System.Drawing.Size(diameter, diameter));

        // Top-left
        path.AddArc(arc, 180, 90);
        // Top-right
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        // Bottom-right
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        // Bottom-left
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    private static void ApplyBitmapToLayeredWindow(nint hwnd, Bitmap bitmap, int x, int y, int width, int height)
    {
        nint screenDc = GetDC(nint.Zero);
        if (screenDc == nint.Zero)
        {
            return;
        }

        try
        {
            nint memDc = CreateCompatibleDC(screenDc);
            if (memDc == nint.Zero)
            {
                return;
            }

            try
            {
                nint hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                nint oldBitmap = SelectObject(memDc, hBitmap);

                try
                {
                    var ptDst = new NativeMethods.POINT { X = x, Y = y };
                    var size = new NativeMethods.SIZE { cx = width, cy = height };
                    var ptSrc = new NativeMethods.POINT { X = 0, Y = 0 };

                    var blend = new BLENDFUNCTION
                    {
                        BlendOp = AC_SRC_OVER,
                        BlendFlags = 0,
                        SourceConstantAlpha = 255,
                        AlphaFormat = AC_SRC_ALPHA
                    };

                    UpdateLayeredWindow(
                        hwnd,
                        screenDc,
                        ref ptDst,
                        ref size,
                        memDc,
                        ref ptSrc,
                        0,
                        ref blend,
                        ULW_ALPHA
                    );
                }
                finally
                {
                    SelectObject(memDc, oldBitmap);
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
            ReleaseDC(nint.Zero, screenDc);
        }
    }

    private void RunWindowLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        NativeMethods.PeekMessage(out _, nint.Zero, 0, 0, NativeMethods.PM_NOREMOVE);

        var wndClass = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = _hInstance,
            hIcon = nint.Zero,
            hCursor = nint.Zero,
            hbrBackground = nint.Zero,
            lpszMenuName = null,
            lpszClassName = _className,
            hIconSm = nint.Zero
        };

        if (RegisterClassEx(ref wndClass) == 0)
        {
            _initEvent?.Set();
            return;
        }

        int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        int exStyle = WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        _hwnd = CreateWindowEx(
            exStyle,
            _className,
            "Stepwise Screen Recording Indicator",
            WS_POPUP,
            vx, vy, vw, vh,
            nint.Zero,
            nint.Zero,
            _hInstance,
            nint.Zero
        );

        _initEvent?.Set();

        if (_hwnd == nint.Zero)
        {
            return;
        }

        while (NativeMethods.GetMessage(out var msg, nint.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        if (_hwnd != nint.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = nint.Zero;
        }

        UnregisterClass(_className, _hInstance);
    }

    private nint WndProc(nint hWnd, uint uMsg, nint wParam, nint lParam)
    {
        const uint WM_NCHITTEST = 0x0084;
        const nint HTTRANSPARENT = -1;

        if (uMsg == WM_NCHITTEST)
        {
            return HTTRANSPARENT; // 100% click-through
        }

        return DefWindowProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            StopIndicator();

            if (_threadId != 0)
            {
                NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, nint.Zero, nint.Zero);
            }

            if (_windowThread.IsAlive)
            {
                _windowThread.Join(TimeSpan.FromSeconds(2));
            }

            _initEvent?.Dispose();
            _initEvent = null;
        }
    }
}
