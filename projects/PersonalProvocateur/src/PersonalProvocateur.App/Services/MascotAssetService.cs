using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Storage;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Manages mascot sprite graphics, dynamic file loading with non-locking streams,
/// and offline programmatic vector generation of 4 character states (ADR-003).
/// </summary>
public class MascotAssetService
{
    private const int SpriteSize = 320;
    private const double Dpi = 96.0;

    private readonly ConcurrentDictionary<MascotState, BitmapSource> _cache = new();

    /// <summary>
    /// Gets the absolute directory path where mascot image assets are stored and loaded from.
    /// </summary>
    public string AssetsDirectory { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MascotAssetService"/>.
    /// </summary>
    /// <param name="assetsDirectory">
    /// Optional directory path. If null, checks Assets/Mascot in BaseDirectory,
    /// falling back to AppPaths.AppDataDirectory/Assets/Mascot if BaseDirectory is not writable.
    /// </param>
    public MascotAssetService(string? assetsDirectory = null)
    {
        AssetsDirectory = ResolveAssetsDirectory(assetsDirectory);
    }

    private static string ResolveAssetsDirectory(string? customDirectory)
    {
        if (!string.IsNullOrWhiteSpace(customDirectory))
        {
            return customDirectory;
        }

        var baseDirAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Mascot");
        try
        {
            Directory.CreateDirectory(baseDirAssets);
            var testProbe = Path.Combine(baseDirAssets, $".probe_{Guid.NewGuid():N}");
            File.WriteAllText(testProbe, "probe");
            File.Delete(testProbe);
            return baseDirAssets;
        }
        catch
        {
            var appDataAssets = Path.Combine(AppPaths.AppDataDirectory, "Assets", "Mascot");
            try
            {
                Directory.CreateDirectory(appDataAssets);
            }
            catch
            {
                // Suppress if creation deferred
            }
            return appDataAssets;
        }
    }

    /// <summary>
    /// Checks for the 4 character sprite files (idle.png, peek.png, attention.png, clicked.png).
    /// If missing, generates high-quality 320x320 transparent PNG assets programmatically.
    /// </summary>
    public void EnsureAssetsExist()
    {
        RunOnSta(() =>
        {
            if (!Directory.Exists(AssetsDirectory))
            {
                Directory.CreateDirectory(AssetsDirectory);
            }

            var states = new[]
            {
                (MascotState.Idle, "idle.png"),
                (MascotState.Peek, "peek.png"),
                (MascotState.Attention, "attention.png"),
                (MascotState.Clicked, "clicked.png")
            };

            foreach (var (state, filename) in states)
            {
                var filePath = Path.Combine(AssetsDirectory, filename);
                if (!File.Exists(filePath))
                {
                    var bitmap = RenderSprite(state);
                    SavePng(bitmap, filePath);
                }
            }
        });
    }

    /// <summary>
    /// Gets the sprite bitmap for the requested mascot state.
    /// Loads from disk with <see cref="BitmapCacheOption.OnLoad"/> to prevent file locking,
    /// caching the result in memory. Falls back to a vector-generated bitmap on missing or corrupt files.
    /// </summary>
    /// <param name="state">The mascot state.</param>
    /// <returns>A thread-safe frozen <see cref="BitmapSource"/>.</returns>
    public BitmapSource GetSprite(MascotState state)
    {
        if (state == MascotState.Hidden)
        {
            return _cache.GetOrAdd(MascotState.Hidden, _ => CreateTransparentBitmap());
        }

        if (_cache.TryGetValue(state, out var cached))
        {
            return cached;
        }

        var filename = GetFileNameForState(state);
        var filePath = Path.Combine(AssetsDirectory, filename);

        if (File.Exists(filePath))
        {
            try
            {
                var diskBitmap = LoadFromFileWithoutLock(filePath);
                _cache[state] = diskBitmap;
                return diskBitmap;
            }
            catch
            {
                // Corrupted file or decode failure — fall back to vector generation
            }
        }

        var fallback = RenderSprite(state);
        _cache[state] = fallback;
        return fallback;
    }

    /// <summary>
    /// Clears the in-memory sprite cache, allowing updated disk files to be reloaded.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Gets the standard file name for a mascot state.
    /// </summary>
    public static string GetFileNameForState(MascotState state) => state switch
    {
        MascotState.Peek => "peek.png",
        MascotState.Attention => "attention.png",
        MascotState.Clicked => "clicked.png",
        _ => "idle.png"
    };

    private static BitmapSource LoadFromFileWithoutLock(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static void SavePng(BitmapSource bitmap, string filePath)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static BitmapSource CreateTransparentBitmap()
    {
        return RunOnSta(() =>
        {
            var rtb = new RenderTargetBitmap(1, 1, Dpi, Dpi, PixelFormats.Pbgra32);
            rtb.Freeze();
            return rtb;
        });
    }

    /// <summary>
    /// Renders a mascot state sprite in-memory using WPF DrawingVisual and RenderTargetBitmap.
    /// </summary>
    public BitmapSource RenderSprite(MascotState state)
    {
        return RunOnSta(() =>
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                DrawMascot(dc, state);
            }

            var rtb = new RenderTargetBitmap(SpriteSize, SpriteSize, Dpi, Dpi, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        });
    }

    private static void DrawMascot(DrawingContext dc, MascotState state)
    {
        // 1. Color Brushes and Pens
        var bodyBrush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(0x9D, 0x71, 0xF8), 0.0), // #9D71F8 Top highlight
                new(Color.FromRgb(0x8B, 0x5C, 0xF6), 0.35), // #8B5CF6 Primary purple
                new(Color.FromRgb(0x7C, 0x3A, 0xED), 0.70), // #7C3AED Vibrant violet
                new(Color.FromRgb(0x5B, 0x21, 0xB6), 1.0)  // #5B21B6 Deep base shadow
            },
            new Point(0.5, 0.0),
            new Point(0.5, 1.0));
        bodyBrush.Freeze();

        var hornBrush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(0xC4, 0xB5, 0xFD), 0.0), // #C4B5FD light tip
                new(Color.FromRgb(0x8B, 0x5C, 0xF6), 0.5),
                new(Color.FromRgb(0x5B, 0x21, 0xB6), 1.0)  // #5B21B6 base
            },
            new Point(0.3, 0.0),
            new Point(0.7, 1.0));
        hornBrush.Freeze();

        var scleraBrush = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)); // #F8FAFC
        scleraBrush.Freeze();

        var scleraPen = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0xDD, 0xD6, 0xFE)), 1.5);
        scleraPen.Freeze();

        var pupilBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1B, 0x4B)); // #1E1B4B
        pupilBrush.Freeze();

        var cyanGlowBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)); // #38BDF8
        cyanGlowBrush.Freeze();

        var highlightBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        highlightBrush.Freeze();

        var blushBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF4, 0x72, 0xB6)); // #F472B6 with 40% opacity
        blushBrush.Freeze();

        var mouthDarkBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x10, 0x3D));
        mouthDarkBrush.Freeze();

        var tongueBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0x71, 0x85)); // #FB7185
        tongueBrush.Freeze();

        var outlinePen = new Pen(new SolidColorBrush(Color.FromRgb(0x4C, 0x1D, 0x95)), 2.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        outlinePen.Freeze();

        // 2. Horns (rendered behind the body)
        DrawHorns(dc, hornBrush, outlinePen, state);

        // 3. Main Body
        DrawBody(dc, bodyBrush, outlinePen, state);

        // 4. Belly / Glossy Highlight
        DrawBellyAndGloss(dc, state);

        // 5. Paws / Hands
        DrawPaws(dc, bodyBrush, outlinePen, state);

        // 6. Blush Cheeks
        DrawCheeks(dc, blushBrush, state);

        // 7. Expressive Eyes & Eyebrows
        DrawEyes(dc, scleraBrush, scleraPen, pupilBrush, cyanGlowBrush, highlightBrush, outlinePen, state);

        // 8. Mouth and Smile Expressions
        DrawMouth(dc, mouthDarkBrush, tongueBrush, highlightBrush, outlinePen, state);

        // 9. Extra Accents (Stars, Sparks)
        DrawAccents(dc, state);
    }

    private static void DrawHorns(DrawingContext dc, Brush hornBrush, Pen pen, MascotState state)
    {
        // Left Horn
        var leftHorn = new StreamGeometry();
        using (var ctx = leftHorn.Open())
        {
            if (state == MascotState.Peek)
            {
                ctx.BeginFigure(new Point(128, 92), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(105, 68), new Point(90, 42), new Point(102, 30), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(116, 30), new Point(132, 58), new Point(148, 85), isStroked: true, isSmoothJoin: true);
            }
            else if (state == MascotState.Attention)
            {
                ctx.BeginFigure(new Point(115, 95), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(90, 65), new Point(72, 35), new Point(88, 22), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(104, 22), new Point(122, 55), new Point(138, 86), isStroked: true, isSmoothJoin: true);
            }
            else
            {
                ctx.BeginFigure(new Point(118, 102), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(98, 78), new Point(80, 50), new Point(94, 36), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(108, 36), new Point(124, 66), new Point(140, 92), isStroked: true, isSmoothJoin: true);
            }
        }
        leftHorn.Freeze();
        dc.DrawGeometry(hornBrush, pen, leftHorn);

        // Right Horn
        var rightHorn = new StreamGeometry();
        using (var ctx = rightHorn.Open())
        {
            if (state == MascotState.Peek)
            {
                ctx.BeginFigure(new Point(210, 95), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(232, 75), new Point(252, 48), new Point(242, 34), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(228, 34), new Point(210, 62), new Point(194, 88), isStroked: true, isSmoothJoin: true);
            }
            else if (state == MascotState.Attention)
            {
                ctx.BeginFigure(new Point(205, 95), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(230, 65), new Point(248, 35), new Point(232, 22), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(216, 22), new Point(198, 55), new Point(182, 86), isStroked: true, isSmoothJoin: true);
            }
            else
            {
                ctx.BeginFigure(new Point(202, 102), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(222, 78), new Point(240, 50), new Point(226, 36), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(212, 36), new Point(196, 66), new Point(180, 92), isStroked: true, isSmoothJoin: true);
            }
        }
        rightHorn.Freeze();
        dc.DrawGeometry(hornBrush, pen, rightHorn);
    }

    private static void DrawBody(DrawingContext dc, Brush bodyBrush, Pen pen, MascotState state)
    {
        var bodyGeom = new StreamGeometry();
        using (var ctx = bodyGeom.Open())
        {
            if (state == MascotState.Peek)
            {
                // Tilted body leaning sideways
                ctx.BeginFigure(new Point(175, 78), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(250, 96), new Point(275, 182), new Point(260, 246), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(246, 276), new Point(140, 276), new Point(116, 252), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(92, 208), new Point(108, 114), new Point(175, 78), isStroked: true, isSmoothJoin: true);
            }
            else if (state == MascotState.Attention)
            {
                // Stretched energetic body
                ctx.BeginFigure(new Point(160, 66), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(242, 80), new Point(258, 172), new Point(246, 244), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(232, 276), new Point(88, 276), new Point(74, 244), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(62, 172), new Point(78, 80), new Point(160, 66), isStroked: true, isSmoothJoin: true);
            }
            else if (state == MascotState.Clicked)
            {
                // Squashed joyful body
                ctx.BeginFigure(new Point(160, 96), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(248, 108), new Point(268, 186), new Point(256, 252), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(240, 282), new Point(80, 282), new Point(64, 252), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(52, 186), new Point(72, 108), new Point(160, 96), isStroked: true, isSmoothJoin: true);
            }
            else
            {
                // Classic droplet/pear body
                ctx.BeginFigure(new Point(160, 80), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(242, 95), new Point(258, 180), new Point(246, 248), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(234, 278), new Point(86, 278), new Point(74, 248), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(62, 180), new Point(78, 95), new Point(160, 80), isStroked: true, isSmoothJoin: true);
            }
        }
        bodyGeom.Freeze();
        dc.DrawGeometry(bodyBrush, pen, bodyGeom);
    }

    private static void DrawBellyAndGloss(DrawingContext dc, MascotState state)
    {
        // Soft lighter belly patch
        var bellyBrush = new SolidColorBrush(Color.FromArgb(0x35, 0xC4, 0xB5, 0xFD));
        bellyBrush.Freeze();

        var bellyCenter = state switch
        {
            MascotState.Peek => new Point(175, 212),
            MascotState.Attention => new Point(160, 206),
            MascotState.Clicked => new Point(160, 216),
            _ => new Point(160, 210)
        };
        dc.DrawEllipse(bellyBrush, null, bellyCenter, 52, 38);

        // Head gloss highlight
        var glossBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
        glossBrush.Freeze();

        var glossPoint = state switch
        {
            MascotState.Peek => new Point(168, 95),
            MascotState.Attention => new Point(155, 84),
            MascotState.Clicked => new Point(155, 110),
            _ => new Point(155, 96)
        };
        dc.DrawEllipse(glossBrush, null, glossPoint, 24, 10);
    }

    private static void DrawPaws(DrawingContext dc, Brush bodyBrush, Pen pen, MascotState state)
    {
        if (state == MascotState.Attention)
        {
            // Raised waving paws
            dc.DrawEllipse(bodyBrush, pen, new Point(68, 142), 16, 20);
            dc.DrawEllipse(bodyBrush, pen, new Point(252, 142), 16, 20);
        }
        else if (state == MascotState.Clicked)
        {
            // Victory celebration paws
            dc.DrawEllipse(bodyBrush, pen, new Point(64, 136), 18, 18);
            dc.DrawEllipse(bodyBrush, pen, new Point(256, 136), 18, 18);
        }
        else if (state == MascotState.Peek)
        {
            // Peeking paws (gripping edge)
            dc.DrawEllipse(bodyBrush, pen, new Point(94, 212), 16, 14);
            dc.DrawEllipse(bodyBrush, pen, new Point(246, 174), 16, 18);
        }
        else
        {
            // Relaxed casual paws
            dc.DrawEllipse(bodyBrush, pen, new Point(78, 202), 16, 16);
            dc.DrawEllipse(bodyBrush, pen, new Point(242, 202), 16, 16);
        }
    }

    private static void DrawCheeks(DrawingContext dc, Brush blushBrush, MascotState state)
    {
        var (leftCenter, rightCenter) = state switch
        {
            MascotState.Peek => (new Point(126, 172), new Point(222, 172)),
            MascotState.Attention => (new Point(106, 168), new Point(214, 168)),
            MascotState.Clicked => (new Point(102, 174), new Point(218, 174)),
            _ => (new Point(108, 170), new Point(212, 170))
        };

        var radiusX = state == MascotState.Clicked ? 16 : 14;
        var radiusY = state == MascotState.Clicked ? 10 : 8;

        dc.DrawEllipse(blushBrush, null, leftCenter, radiusX, radiusY);
        dc.DrawEllipse(blushBrush, null, rightCenter, radiusX, radiusY);
    }

    private static void DrawEyes(
        DrawingContext dc,
        Brush scleraBrush,
        Pen scleraPen,
        Brush pupilBrush,
        Brush glowBrush,
        Brush highlightBrush,
        Pen pen,
        MascotState state)
    {
        if (state == MascotState.Clicked)
        {
            // Left eye: playful cheeky wink (thick curve)
            var winkPen = new Pen(new SolidColorBrush(Color.FromRgb(0x1E, 0x1B, 0x4B)), 4.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            winkPen.Freeze();

            var winkGeom = new StreamGeometry();
            using (var ctx = winkGeom.Open())
            {
                ctx.BeginFigure(new Point(110, 148), isFilled: false, isClosed: false);
                ctx.BezierTo(new Point(122, 134), new Point(136, 134), new Point(148, 148), isStroked: true, isSmoothJoin: true);
            }
            winkGeom.Freeze();
            dc.DrawGeometry(null, winkPen, winkGeom);

            // Right eye: sparkling wide joyful eye
            var rightEyeCenter = new Point(202, 144);
            dc.DrawEllipse(scleraBrush, scleraPen, rightEyeCenter, 20, 22);
            dc.DrawEllipse(glowBrush, null, rightEyeCenter, 14, 15);
            dc.DrawEllipse(pupilBrush, null, rightEyeCenter, 10, 11);

            // 4-point star specular highlight
            DrawSparkleStar(dc, highlightBrush, new Point(198, 138), 7);
            dc.DrawEllipse(highlightBrush, null, new Point(206, 148), 3, 3);
            return;
        }

        if (state == MascotState.Attention)
        {
            // Widely opened glowing eyes with double highlight
            var leftCenter = new Point(120, 140);
            var rightCenter = new Point(200, 140);

            // Sclera
            dc.DrawEllipse(scleraBrush, scleraPen, leftCenter, 22, 24);
            dc.DrawEllipse(scleraBrush, scleraPen, rightCenter, 22, 24);

            // Glowing cyan/violet irises
            dc.DrawEllipse(glowBrush, null, leftCenter, 16, 17);
            dc.DrawEllipse(glowBrush, null, rightCenter, 16, 17);

            // Pupils
            dc.DrawEllipse(pupilBrush, null, leftCenter, 11, 12);
            dc.DrawEllipse(pupilBrush, null, rightCenter, 11, 12);

            // Specular highlights
            dc.DrawEllipse(highlightBrush, null, new Point(116, 134), 5, 5);
            dc.DrawEllipse(highlightBrush, null, new Point(124, 144), 3, 3);
            dc.DrawEllipse(highlightBrush, null, new Point(196, 134), 5, 5);
            dc.DrawEllipse(highlightBrush, null, new Point(204, 144), 3, 3);

            // Perked eyebrows
            var browPen = new Pen(new SolidColorBrush(Color.FromRgb(0x4C, 0x1D, 0x95)), 3.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            browPen.Freeze();

            var leftBrow = new StreamGeometry();
            using (var ctx = leftBrow.Open())
            {
                ctx.BeginFigure(new Point(106, 116), isFilled: false, isClosed: false);
                ctx.BezierTo(new Point(118, 106), new Point(128, 108), new Point(136, 118), isStroked: true, isSmoothJoin: true);
            }
            leftBrow.Freeze();
            dc.DrawGeometry(null, browPen, leftBrow);

            var rightBrow = new StreamGeometry();
            using (var ctx = rightBrow.Open())
            {
                ctx.BeginFigure(new Point(184, 118), isFilled: false, isClosed: false);
                ctx.BezierTo(new Point(192, 108), new Point(202, 106), new Point(214, 116), isStroked: true, isSmoothJoin: true);
            }
            rightBrow.Freeze();
            dc.DrawGeometry(null, browPen, rightBrow);
            return;
        }

        if (state == MascotState.Peek)
        {
            // Left eye: squinted curious eye
            var squintPen = new Pen(new SolidColorBrush(Color.FromRgb(0x1E, 0x1B, 0x4B)), 3.5)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            squintPen.Freeze();

            var squintGeom = new StreamGeometry();
            using (var ctx = squintGeom.Open())
            {
                ctx.BeginFigure(new Point(122, 146), isFilled: false, isClosed: false);
                ctx.BezierTo(new Point(132, 138), new Point(142, 138), new Point(152, 146), isStroked: true, isSmoothJoin: true);
            }
            squintGeom.Freeze();
            dc.DrawGeometry(null, squintPen, squintGeom);

            // Right eye: wide open, looking right towards user
            var rightCenter = new Point(208, 144);
            dc.DrawEllipse(scleraBrush, scleraPen, rightCenter, 20, 22);
            dc.DrawEllipse(glowBrush, null, new Point(212, 144), 14, 15);
            dc.DrawEllipse(pupilBrush, null, new Point(214, 144), 10, 11);
            dc.DrawEllipse(highlightBrush, null, new Point(211, 138), 5, 5);
            dc.DrawEllipse(highlightBrush, null, new Point(217, 147), 3, 3);
            return;
        }

        // Default: Idle (Relaxed, sly, provocative glance)
        var idleLeft = new Point(124, 144);
        var idleRight = new Point(196, 144);

        // Sclera
        dc.DrawEllipse(scleraBrush, scleraPen, idleLeft, 19, 21);
        dc.DrawEllipse(scleraBrush, scleraPen, idleRight, 19, 21);

        // Glowing purple/blue irises
        dc.DrawEllipse(glowBrush, null, new Point(126, 144), 13, 14);
        dc.DrawEllipse(glowBrush, null, new Point(198, 144), 13, 14);

        // Pupils slightly looking right/forward
        dc.DrawEllipse(pupilBrush, null, new Point(128, 144), 9, 10);
        dc.DrawEllipse(pupilBrush, null, new Point(200, 144), 9, 10);

        // Specular highlights
        dc.DrawEllipse(highlightBrush, null, new Point(126, 139), 4, 4);
        dc.DrawEllipse(highlightBrush, null, new Point(198, 139), 4, 4);
        dc.DrawEllipse(highlightBrush, null, new Point(131, 147), 2, 2);
        dc.DrawEllipse(highlightBrush, null, new Point(203, 147), 2, 2);

        // Sly half-lidded eyelids
        var eyelidBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6));
        eyelidBrush.Freeze();

        var leftLid = new StreamGeometry();
        using (var ctx = leftLid.Open())
        {
            ctx.BeginFigure(new Point(106, 140), isFilled: true, isClosed: true);
            ctx.BezierTo(new Point(116, 126), new Point(132, 126), new Point(142, 138), isStroked: false, isSmoothJoin: true);
            ctx.BezierTo(new Point(130, 142), new Point(118, 142), new Point(106, 140), isStroked: false, isSmoothJoin: true);
        }
        leftLid.Freeze();
        dc.DrawGeometry(eyelidBrush, null, leftLid);

        var rightLid = new StreamGeometry();
        using (var ctx = rightLid.Open())
        {
            ctx.BeginFigure(new Point(178, 138), isFilled: true, isClosed: true);
            ctx.BezierTo(new Point(188, 126), new Point(204, 126), new Point(214, 140), isStroked: false, isSmoothJoin: true);
            ctx.BezierTo(new Point(202, 142), new Point(190, 142), new Point(178, 138), isStroked: false, isSmoothJoin: true);
        }
        rightLid.Freeze();
        dc.DrawGeometry(eyelidBrush, null, rightLid);
    }

    private static void DrawMouth(
        DrawingContext dc,
        Brush mouthDarkBrush,
        Brush tongueBrush,
        Brush whiteBrush,
        Pen pen,
        MascotState state)
    {
        if (state == MascotState.Clicked)
        {
            // Triumphant open grin with tongue and teeth
            var grin = new StreamGeometry();
            using (var ctx = grin.Open())
            {
                ctx.BeginFigure(new Point(138, 182), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(148, 180), new Point(172, 180), new Point(182, 182), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(178, 212), new Point(142, 212), new Point(138, 182), isStroked: true, isSmoothJoin: true);
            }
            grin.Freeze();
            dc.DrawGeometry(mouthDarkBrush, pen, grin);

            // Tongue
            dc.DrawEllipse(tongueBrush, null, new Point(160, 203), 14, 7);

            // Upper teeth
            var teeth = new StreamGeometry();
            using (var ctx = teeth.Open())
            {
                ctx.BeginFigure(new Point(144, 182), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(176, 182), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(172, 188), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(148, 188), isStroked: false, isSmoothJoin: false);
            }
            teeth.Freeze();
            dc.DrawGeometry(whiteBrush, null, teeth);
            return;
        }

        if (state == MascotState.Attention)
        {
            // Open shouting mouth ("Hey, where are you?!")
            var shout = new StreamGeometry();
            using (var ctx = shout.Open())
            {
                ctx.BeginFigure(new Point(144, 184), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(152, 178), new Point(168, 178), new Point(176, 184), isStroked: true, isSmoothJoin: true);
                ctx.BezierTo(new Point(174, 206), new Point(146, 206), new Point(144, 184), isStroked: true, isSmoothJoin: true);
            }
            shout.Freeze();
            dc.DrawGeometry(mouthDarkBrush, pen, shout);

            // Tongue
            dc.DrawEllipse(tongueBrush, null, new Point(160, 200), 10, 5);

            // Cute little fang teeth
            var fangs = new StreamGeometry();
            using (var ctx = fangs.Open())
            {
                ctx.BeginFigure(new Point(150, 182), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(153, 188), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(156, 182), isStroked: false, isSmoothJoin: false);

                ctx.BeginFigure(new Point(164, 182), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(167, 188), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(170, 182), isStroked: false, isSmoothJoin: false);
            }
            fangs.Freeze();
            dc.DrawGeometry(whiteBrush, null, fangs);
            return;
        }

        if (state == MascotState.Peek)
        {
            // Quirky curious smile
            var peekSmile = new StreamGeometry();
            using (var ctx = peekSmile.Open())
            {
                ctx.BeginFigure(new Point(156, 188), isFilled: false, isClosed: false);
                ctx.BezierTo(new Point(166, 198), new Point(178, 198), new Point(188, 188), isStroked: true, isSmoothJoin: true);
            }
            peekSmile.Freeze();
            dc.DrawGeometry(null, pen, peekSmile);

            // Cute tongue tip sticking out
            dc.DrawEllipse(tongueBrush, null, new Point(172, 196), 4, 3);
            return;
        }

        // Default: Idle (Sly half-smile / smirk)
        var idleSmirk = new StreamGeometry();
        using (var ctx = idleSmirk.Open())
        {
            ctx.BeginFigure(new Point(146, 192), isFilled: false, isClosed: false);
            ctx.BezierTo(new Point(160, 198), new Point(176, 194), new Point(186, 184), isStroked: true, isSmoothJoin: true);
        }
        idleSmirk.Freeze();
        dc.DrawGeometry(null, pen, idleSmirk);
    }

    private static void DrawAccents(DrawingContext dc, MascotState state)
    {
        if (state == MascotState.Clicked)
        {
            // Golden celebration sparkles
            var starBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)); // #FBBF24
            starBrush.Freeze();

            DrawSparkleStar(dc, starBrush, new Point(72, 85), 9);
            DrawSparkleStar(dc, starBrush, new Point(254, 75), 11);
            DrawSparkleStar(dc, starBrush, new Point(268, 130), 6);
        }
        else if (state == MascotState.Attention)
        {
            // Energy attention sparks
            var sparkBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)); // #38BDF8
            sparkBrush.Freeze();

            DrawSparkleStar(dc, sparkBrush, new Point(62, 55), 8);
            DrawSparkleStar(dc, sparkBrush, new Point(258, 52), 8);
        }
    }

    private static void DrawSparkleStar(DrawingContext dc, Brush brush, Point center, double radius)
    {
        var star = new StreamGeometry();
        using (var ctx = star.Open())
        {
            ctx.BeginFigure(new Point(center.X, center.Y - radius), isFilled: true, isClosed: true);
            ctx.BezierTo(new Point(center.X, center.Y - radius * 0.3), new Point(center.X + radius * 0.3, center.Y), new Point(center.X + radius, center.Y), isStroked: false, isSmoothJoin: true);
            ctx.BezierTo(new Point(center.X + radius * 0.3, center.Y), new Point(center.X, center.Y + radius * 0.3), new Point(center.X, center.Y + radius), isStroked: false, isSmoothJoin: true);
            ctx.BezierTo(new Point(center.X, center.Y + radius * 0.3), new Point(center.X - radius * 0.3, center.Y), new Point(center.X - radius, center.Y), isStroked: false, isSmoothJoin: true);
            ctx.BezierTo(new Point(center.X - radius * 0.3, center.Y), new Point(center.X, center.Y - radius * 0.3), new Point(center.X, center.Y - radius), isStroked: false, isSmoothJoin: true);
        }
        star.Freeze();
        dc.DrawGeometry(brush, null, star);
    }

    private static T RunOnSta<T>(Func<T> func)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return func();
        }

        T result = default!;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw new InvalidOperationException("Failed executing STA graphics rendering operation.", exception);
        }

        return result;
    }

    private static void RunOnSta(Action action)
    {
        RunOnSta<object?>(() =>
        {
            action();
            return null;
        });
    }
}
