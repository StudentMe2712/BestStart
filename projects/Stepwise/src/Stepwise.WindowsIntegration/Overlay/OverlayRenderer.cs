using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Overlay;

/// <summary>
/// Интерфейс низкоуровневого рендерера графического содержимого оверлея.
/// Отвечает за отрисовку прозрачного выреза подсветки, затемнения фона, рамки, карточки подсказки и маркера клика.
/// </summary>
public interface IOverlayRenderer
{
    /// <summary>
    /// Отрисовывает визуальные элементы оверлея и передает буфер окну через UpdateLayeredWindow.
    /// </summary>
    void Render(
        nint hwnd,
        int virtualX,
        int virtualY,
        int virtualWidth,
        int virtualHeight,
        OverlayTargetInfo? target,
        CalloutPosition? callout,
        bool isMissingTarget
    );

    /// <summary>
    /// Очищает содержимое оверлея на указанном окне (делает его полностью прозрачным).
    /// </summary>
    void Clear(nint hwnd);
}

/// <summary>
/// Реализация рендерера оверлея на базе Win32 GDI+ и UpdateLayeredWindow.
/// Поддерживает попиксельный альфа-канал (32bpp ARGB), мультимониторные отрицательные координаты,
/// прозрачный вырез подсветки целевого элемента, рамку Fluent blue, маркер клика и карточку выноски.
/// </summary>
public sealed class OverlayRenderer : IOverlayRenderer, IDisposable
{
    // Цветовая палитра спецификации
    private static readonly Color DimBackgroundColor = Color.FromArgb(90, 0, 0, 0);       // 30-35% затемнение
    private static readonly Color TransparentCutoutColor = Color.FromArgb(0, 0, 0, 0);     // 100% прозрачный вырез
    private static readonly Color TargetBorderColor = Color.FromArgb(255, 0, 120, 215);    // Fluent blue акцент
    private static readonly Color CalloutBgColor = Color.FromArgb(240, 32, 32, 36);        // Темная карточка
    private static readonly Color CalloutBorderColor = Color.FromArgb(90, 255, 255, 255);  // Тонкая белая рамка
    private static readonly Color WarningBorderColor = Color.FromArgb(220, 239, 68, 68);   // Рамка предупреждения
    private static readonly Color TitleTextColor = Color.FromArgb(255, 255, 255, 255);     // Белый заголовок
    private static readonly Color DescTextColor = Color.FromArgb(255, 204, 204, 204);      // Светло-серый текст
    private static readonly Color ClickPinColor = Color.FromArgb(200, 0, 120, 215);        // Акцентный маркер клика

    private bool _isDisposed;

    /// <summary>
    /// Отрисовывает оверлей и передает буфер окну через Win32 UpdateLayeredWindow.
    /// </summary>
    public void Render(
        nint hwnd,
        int virtualX,
        int virtualY,
        int virtualWidth,
        int virtualHeight,
        OverlayTargetInfo? target,
        CalloutPosition? callout,
        bool isMissingTarget)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (virtualWidth <= 0 || virtualHeight <= 0)
        {
            return;
        }

        using var bitmap = RenderToBitmap(virtualX, virtualY, virtualWidth, virtualHeight, target, callout, isMissingTarget);

        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return;
        }

        UpdateLayeredWindowFromBitmap(hwnd, bitmap, virtualX, virtualY, virtualWidth, virtualHeight);
    }

    /// <summary>
    /// Очищает окно оверлея, делая его полностью прозрачным.
    /// </summary>
    public void Clear(nint hwnd)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return;
        }

        try
        {
            nint screenDc = NativeMethods.GetDC(nint.Zero);
            if (screenDc == nint.Zero)
            {
                return;
            }

            try
            {
                nint memDc = NativeMethods.CreateCompatibleDC(screenDc);
                if (memDc == nint.Zero)
                {
                    return;
                }

                try
                {
                    var bmi = new NativeMethods.BITMAPINFO
                    {
                        bmiHeader = new NativeMethods.BITMAPINFOHEADER
                        {
                            biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                            biWidth = 1,
                            biHeight = -1,
                            biPlanes = 1,
                            biBitCount = 32,
                            biCompression = NativeMethods.DIB_RGB_COLORS
                        }
                    };

                    nint hBitmap = NativeMethods.CreateDIBSection(screenDc, ref bmi, NativeMethods.DIB_RGB_COLORS, out _, nint.Zero, 0);
                    if (hBitmap != nint.Zero)
                    {
                        try
                        {
                            nint oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);
                            try
                            {
                                var ptDst = new NativeMethods.POINT { X = 0, Y = 0 };
                                var size = new NativeMethods.SIZE { cx = 1, cy = 1 };
                                var ptSrc = new NativeMethods.POINT { X = 0, Y = 0 };
                                var blend = new NativeMethods.BLENDFUNCTION
                                {
                                    BlendOp = NativeMethods.AC_SRC_OVER,
                                    BlendFlags = 0,
                                    SourceConstantAlpha = 0,
                                    AlphaFormat = NativeMethods.AC_SRC_ALPHA
                                };

                                NativeMethods.UpdateLayeredWindow(
                                    hwnd,
                                    screenDc,
                                    ref ptDst,
                                    ref size,
                                    memDc,
                                    ref ptSrc,
                                    0,
                                    ref blend,
                                    NativeMethods.ULW_ALPHA
                                );
                            }
                            finally
                            {
                                NativeMethods.SelectObject(memDc, oldBitmap);
                            }
                        }
                        finally
                        {
                            NativeMethods.DeleteObject(hBitmap);
                        }
                    }
                }
                finally
                {
                    NativeMethods.DeleteDC(memDc);
                }
            }
            finally
            {
                NativeMethods.ReleaseDC(nint.Zero, screenDc);
            }
        }
        catch
        {
            // Устойчивость к отказам при выходе/закрытии окна
        }
    }

    /// <summary>
    /// Генерирует 32-битный ARGB Bitmap с визуальным состоянием оверлея в координатах виртуального экрана.
    /// Позволяет автономно тестировать рендеринг без наличия HWND.
    /// </summary>
    public Bitmap RenderToBitmap(
        int virtualX,
        int virtualY,
        int virtualWidth,
        int virtualHeight,
        OverlayTargetInfo? target,
        CalloutPosition? callout,
        bool isMissingTarget)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        int width = Math.Max(1, virtualWidth);
        int height = Math.Max(1, virtualHeight);

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // 1. Базовое затемнение всего виртуального экрана (Alpha = 90)
            g.Clear(Color.FromArgb(0, 0, 0, 0));
            using (var dimBrush = new SolidBrush(DimBackgroundColor))
            {
                g.FillRectangle(dimBrush, 0, 0, width, height);
            }

            bool hasValidTarget = !isMissingTarget && target != null && target.IsTargetValid && !target.BoundingRectangle.IsEmpty;

            if (hasValidTarget)
            {
                // Смещение координат целевого элемента относительно начала виртуального экрана (VirtualX, VirtualY)
                float targetX = (float)(target!.BoundingRectangle.X - virtualX);
                float targetY = (float)(target.BoundingRectangle.Y - virtualY);
                float targetW = (float)target.BoundingRectangle.Width;
                float targetH = (float)target.BoundingRectangle.Height;
                var targetRect = new RectangleF(targetX, targetY, targetW, targetH);

                float cornerRadius = Math.Min(6f, Math.Min(targetW, targetH) / 2f);

                // 2. Spotlight cutout: 100% прозрачность над целевым элементом (Alpha = 0, SourceCopy)
                g.CompositingMode = CompositingMode.SourceCopy;
                using (var transparentBrush = new SolidBrush(TransparentCutoutColor))
                {
                    if (cornerRadius > 1f)
                    {
                        using var cutoutPath = CreateRoundedRectanglePath(targetRect, cornerRadius);
                        g.FillPath(transparentBrush, cutoutPath);
                    }
                    else
                    {
                        g.FillRectangle(transparentBrush, targetRect);
                    }
                }

                // 3. Target highlight: акцентная рамка (толщина 3, Fluent blue, SourceOver)
                g.CompositingMode = CompositingMode.SourceOver;
                using (var borderPen = new Pen(TargetBorderColor, 3f))
                {
                    borderPen.Alignment = PenAlignment.Center;
                    if (cornerRadius > 1f)
                    {
                        using var borderPath = CreateRoundedRectanglePath(targetRect, cornerRadius);
                        g.DrawPath(borderPen, borderPath);
                    }
                    else
                    {
                        g.DrawRectangle(borderPen, targetRect.X, targetRect.Y, targetRect.Width, targetRect.Height);
                    }
                }

                // 4. Click marker: круглый пин с точкой клика
                if (target.ClickX != 0 || target.ClickY != 0)
                {
                    float pinX = (float)(target.ClickX - virtualX);
                    float pinY = (float)(target.ClickY - virtualY);
                    DrawClickMarker(g, pinX, pinY);
                }

                // 5. Callout card: подсказка рядом с целевым элементом
                if (callout != null)
                {
                    float cardX = (float)(callout.X - virtualX);
                    float cardY = (float)(callout.Y - virtualY);
                    float cardW = (float)callout.Width;
                    float cardH = (float)callout.Height;
                    string title = !string.IsNullOrWhiteSpace(target.Title) ? target.Title : "Инструкция";
                    string desc = target.Description ?? string.Empty;
                    DrawCalloutCard(g, cardX, cardY, cardW, cardH, title, desc, isWarning: false);
                }
            }
            else
            {
                // Режим Missing Target: предупреждающая плашка и отсутствие выреза
                float cardW;
                float cardH;
                float cardX;
                float cardY;

                if (callout != null)
                {
                    cardX = (float)(callout.X - virtualX);
                    cardY = (float)(callout.Y - virtualY);
                    cardW = (float)callout.Width;
                    cardH = (float)callout.Height;
                }
                else
                {
                    // Центрируем предупреждение на экране
                    cardW = Math.Min(340f, width - 40f);
                    cardH = 90f;
                    cardX = Math.Max(0f, (width - cardW) / 2f);
                    cardY = Math.Max(0f, (height - cardH) / 2f);
                }

                const string warningTitle = "Целевой элемент недоступен";
                string warningDesc = !string.IsNullOrWhiteSpace(target?.Description)
                    ? target.Description
                    : "Элемент не найден на текущем экране.";

                DrawCalloutCard(g, cardX, cardY, cardW, cardH, warningTitle, warningDesc, isWarning: true);
            }
        }

        return bitmap;
    }

    private static void DrawCalloutCard(
        Graphics g,
        float x,
        float y,
        float width,
        float height,
        string title,
        string description,
        bool isWarning)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var cardRect = new RectangleF(x, y, width, height);
        using var cardPath = CreateRoundedRectanglePath(cardRect, 8f);

        // Фоновая темная карточка (Alpha = 240, SourceCopy для точного сохранения Alpha 240)
        g.CompositingMode = CompositingMode.SourceCopy;
        using (var bgBrush = new SolidBrush(CalloutBgColor))
        {
            g.FillPath(bgBrush, cardPath);
        }

        g.CompositingMode = CompositingMode.SourceOver;

        // Тонкая рамка карточки
        Color strokeColor = isWarning ? WarningBorderColor : CalloutBorderColor;
        using (var borderPen = new Pen(strokeColor, 1.5f))
        {
            g.DrawPath(borderPen, cardPath);
        }

        // Внутренние отступы и разметка текста
        float padX = 14f;
        float padY = 10f;
        float textWidth = Math.Max(0, width - (padX * 2));
        float titleHeight = 22f;

        var titleRect = new RectangleF(x + padX, y + padY, textWidth, titleHeight);
        var descRect = new RectangleF(x + padX, y + padY + titleHeight + 2f, textWidth, Math.Max(0, height - padY * 2 - titleHeight - 2f));

        using var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var descFont = new Font("Segoe UI", 9f, FontStyle.Regular);

        using var titleBrush = new SolidBrush(TitleTextColor);
        using var descBrush = new SolidBrush(DescTextColor);

        using var titleFormat = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        using var descFormat = new StringFormat
        {
            Trimming = StringTrimming.EllipsisWord
        };

        g.DrawString(title, titleFont, titleBrush, titleRect, titleFormat);

        if (!string.IsNullOrWhiteSpace(description))
        {
            g.DrawString(description, descFont, descBrush, descRect, descFormat);
        }
    }

    private static void DrawClickMarker(Graphics g, float cx, float cy)
    {
        float outerRadius = 9f;
        float innerRadius = 3.5f;

        // Внешний полупрозрачный акцентный круг
        using (var fillBrush = new SolidBrush(ClickPinColor))
        {
            g.FillEllipse(fillBrush, cx - outerRadius, cy - outerRadius, outerRadius * 2f, outerRadius * 2f);
        }

        // Белая обводка маркера
        using (var strokePen = new Pen(Color.FromArgb(255, 255, 255, 255), 2f))
        {
            g.DrawEllipse(strokePen, cx - outerRadius, cy - outerRadius, outerRadius * 2f, outerRadius * 2f);
        }

        // Центральная белая точка
        using (var dotBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
        {
            g.FillEllipse(dotBrush, cx - innerRadius, cy - innerRadius, innerRadius * 2f, innerRadius * 2f);
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return path;
        }

        float maxRadius = Math.Min(rect.Width, rect.Height) / 2f;
        radius = Math.Min(radius, maxRadius);

        if (radius <= 0.5f)
        {
            path.AddRectangle(rect);
            return path;
        }

        float diameter = radius * 2f;
        var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

        // Top-left
        path.AddArc(arc, 180, 90);

        // Top-right
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);

        // Bottom-right
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // Bottom-left
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    private static void UpdateLayeredWindowFromBitmap(nint hwnd, Bitmap bitmap, int virtualX, int virtualY, int width, int height)
    {
        nint screenDc = NativeMethods.GetDC(nint.Zero);
        if (screenDc == nint.Zero)
        {
            return;
        }

        try
        {
            nint memDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memDc == nint.Zero)
            {
                return;
            }

            try
            {
                var bmi = new NativeMethods.BITMAPINFO
                {
                    bmiHeader = new NativeMethods.BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                        biWidth = width,
                        biHeight = -height, // Top-down DIB
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = NativeMethods.DIB_RGB_COLORS
                    }
                };

                nint hBitmap = NativeMethods.CreateDIBSection(screenDc, ref bmi, NativeMethods.DIB_RGB_COLORS, out nint ppvBits, nint.Zero, 0);
                if (hBitmap == nint.Zero || ppvBits == nint.Zero)
                {
                    return;
                }

                try
                {
                    // Блокируем биты исходного GDI+ битмапа и переносим данные в память DIBSection с премультипликацией альфы
                    var bmpData = bitmap.LockBits(
                        new Rectangle(0, 0, width, height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb
                    );

                    try
                    {
                        unsafe
                        {
                            byte* src = (byte*)bmpData.Scan0;
                            byte* dst = (byte*)ppvBits;
                            int pixelCount = width * height;

                            for (int i = 0; i < pixelCount; i++)
                            {
                                byte b = src[0];
                                byte g = src[1];
                                byte r = src[2];
                                byte a = src[3];

                                if (a == 0)
                                {
                                    dst[0] = 0;
                                    dst[1] = 0;
                                    dst[2] = 0;
                                    dst[3] = 0;
                                }
                                else if (a == 255)
                                {
                                    dst[0] = b;
                                    dst[1] = g;
                                    dst[2] = r;
                                    dst[3] = 255;
                                }
                                else
                                {
                                    dst[0] = (byte)((b * a + 127) / 255);
                                    dst[1] = (byte)((g * a + 127) / 255);
                                    dst[2] = (byte)((r * a + 127) / 255);
                                    dst[3] = a;
                                }

                                src += 4;
                                dst += 4;
                            }
                        }
                    }
                    finally
                    {
                        bitmap.UnlockBits(bmpData);
                    }

                    nint oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);
                    try
                    {
                        var ptDst = new NativeMethods.POINT { X = virtualX, Y = virtualY };
                        var size = new NativeMethods.SIZE { cx = width, cy = height };
                        var ptSrc = new NativeMethods.POINT { X = 0, Y = 0 };
                        var blend = new NativeMethods.BLENDFUNCTION
                        {
                            BlendOp = NativeMethods.AC_SRC_OVER,
                            BlendFlags = 0,
                            SourceConstantAlpha = 255,
                            AlphaFormat = NativeMethods.AC_SRC_ALPHA
                        };

                        NativeMethods.UpdateLayeredWindow(
                            hwnd,
                            screenDc,
                            ref ptDst,
                            ref size,
                            memDc,
                            ref ptSrc,
                            0,
                            ref blend,
                            NativeMethods.ULW_ALPHA
                        );
                    }
                    finally
                    {
                        NativeMethods.SelectObject(memDc, oldBitmap);
                    }
                }
                finally
                {
                    NativeMethods.DeleteObject(hBitmap);
                }
            }
            finally
            {
                NativeMethods.DeleteDC(memDc);
            }
        }
        finally
        {
            NativeMethods.ReleaseDC(nint.Zero, screenDc);
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
    }
}
