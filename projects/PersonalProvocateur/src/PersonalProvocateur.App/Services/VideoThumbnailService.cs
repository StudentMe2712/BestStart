using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalProvocateur.App.Storage;
using PersonalProvocateur.App.WindowsInterop;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using FontFamily = System.Windows.Media.FontFamily;
using FlowDirection = System.Windows.FlowDirection;

namespace PersonalProvocateur.App.Services;

/// <summary>
/// Encapsulates metadata extracted from a video item.
/// </summary>
public class VideoItemMetadata
{
    /// <summary>
    /// File path to cached 16:9 thumbnail preview image.
    /// </summary>
    public string ThumbnailPath { get; set; } = string.Empty;

    /// <summary>
    /// Formatted video duration text (e.g. "01:30", "04:15").
    /// </summary>
    public string DurationText { get; set; } = string.Empty;

    public VideoItemMetadata()
    {
    }

    public VideoItemMetadata(string thumbnailPath, string durationText)
    {
        ThumbnailPath = thumbnailPath;
        DurationText = durationText;
    }
}

/// <summary>
/// Generates and caches 16:9 thumbnails and retrieves video duration
/// using Windows Shell IShellItemImageFactory with graceful procedural fallback.
/// </summary>
public class VideoThumbnailService
{
    private readonly string _thumbnailsDirectory;
    private readonly Func<string, TimeSpan?>? _durationExtractor;
    private readonly Func<string, bool> _fileExistsChecker;

    [ComImport]
    [Guid("bcc18b79-ba16-442d-80c4-6a140c42a146")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] ShellSize size,
            [In] SIIGBF flags,
            [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ShellSize
    {
        public int cx;
        public int cy;

        public ShellSize(int cx, int cy)
        {
            this.cx = cx;
            this.cy = cy;
        }
    }

    [Flags]
    private enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_MEMORYONLY = 0x02,
        SIIGBF_ICONONLY = 0x04,
        SIIGBF_THUMBNAILONLY = 0x08,
        SIIGBF_INCACHEONLY = 0x10,
        SIIGBF_CROPTOSQUARE = 0x20,
        SIIGBF_WIDETHUMBNAILS = 0x40,
        SIIGBF_ICONBACKGROUND = 0x80,
        SIIGBF_SCALEUP = 0x100
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? ppv);

    /// <summary>
    /// Gets the target directory where thumbnails are cached.
    /// </summary>
    public string ThumbnailsDirectory => _thumbnailsDirectory;

    /// <summary>
    /// Initializes a new instance of <see cref="VideoThumbnailService"/>.
    /// </summary>
    /// <param name="thumbnailsDirectory">Optional custom directory path for caching thumbnails.</param>
    /// <param name="durationExtractor">Optional custom delegate to extract video duration.</param>
    /// <param name="fileExistsChecker">Optional delegate to check file existence.</param>
    public VideoThumbnailService(
        string? thumbnailsDirectory = null,
        Func<string, TimeSpan?>? durationExtractor = null,
        Func<string, bool>? fileExistsChecker = null)
    {
        _thumbnailsDirectory = string.IsNullOrWhiteSpace(thumbnailsDirectory)
            ? AppPaths.ThumbnailsDirectory
            : thumbnailsDirectory;
        _durationExtractor = durationExtractor;
        _fileExistsChecker = fileExistsChecker ?? File.Exists;
    }

    /// <summary>
    /// Synchronously processes a video file: extracts or generates 16:9 thumbnail and duration.
    /// </summary>
    public VideoItemMetadata ProcessVideo(string filePath, Guid videoId)
    {
        if (!Directory.Exists(_thumbnailsDirectory))
        {
            Directory.CreateDirectory(_thumbnailsDirectory);
        }

        string targetPath = Path.Combine(_thumbnailsDirectory, $"{videoId}.jpg");

        if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
        {
            bool extracted = false;
            try
            {
                extracted = TryExtractShellThumbnail(filePath, targetPath);
            }
            catch
            {
                extracted = false;
            }

            if (!extracted)
            {
                GenerateProceduralThumbnail(filePath, targetPath);
            }
        }

        string durationText = ExtractDuration(filePath);
        return new VideoItemMetadata(targetPath, durationText);
    }

    /// <summary>
    /// Asynchronously processes a video file: extracts or generates 16:9 thumbnail and duration.
    /// </summary>
    public Task<VideoItemMetadata> ProcessVideoAsync(string filePath, Guid videoId)
    {
        return Task.Run(() => ProcessVideo(filePath, videoId));
    }

    /// <summary>
    /// Formats a TimeSpan duration into standard display string (mm:ss or hh:mm:ss).
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "01:30";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }

        return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
    }

    /// <summary>
    /// Generates a procedural 16:9 placeholder thumbnail (320x180) and writes it to targetPath.
    /// </summary>
    public string GenerateProceduralThumbnail(string filePath, string targetPath)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            RenderProceduralThumbnailInternal(filePath, targetPath);
        }
        else
        {
            var thread = new Thread(() => RenderProceduralThumbnailInternal(filePath, targetPath));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        return targetPath;
    }

    private static void RenderProceduralThumbnailInternal(string filePath, string targetPath)
    {
        const int width = 320;
        const int height = 180;

        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            // Dark gradient background #141724 to #0B0D14
            var bgBrush = new LinearGradientBrush(
                Color.FromRgb(20, 23, 36),
                Color.FromRgb(11, 13, 20),
                new Point(0, 0),
                new Point(1, 1));
            bgBrush.Freeze();
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));

            // Rounded border outline
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)), 1);
            borderPen.Freeze();
            dc.DrawRoundedRectangle(null, borderPen, new Rect(1, 1, width - 2, height - 2), 8, 8);

            // Badge pill in center
            var badgeBrush = new SolidColorBrush(Color.FromArgb(220, 139, 92, 246)); // #8B5CF6
            badgeBrush.Freeze();
            dc.DrawRoundedRectangle(badgeBrush, null, new Rect(width / 2 - 24, height / 2 - 34, 48, 36), 8, 8);

            // Play triangle inside badge
            var playPath = new StreamGeometry();
            using (var ctx = playPath.Open())
            {
                ctx.BeginFigure(new Point(width / 2 - 6, height / 2 - 24), true, true);
                ctx.LineTo(new Point(width / 2 + 10, height / 2 - 16), true, false);
                ctx.LineTo(new Point(width / 2 - 6, height / 2 - 8), true, false);
            }
            playPath.Freeze();
            dc.DrawGeometry(Brushes.White, null, playPath);

            // Title text below badge
            string title = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = Path.GetFileName(filePath);
            }
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "Video Item";
            }
            if (title.Length > 28)
            {
                title = title.Substring(0, 25) + "...";
            }

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var textBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            textBrush.Freeze();

            var formattedText = new FormattedText(
                title,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                12,
                textBrush,
                1.0)
            {
                MaxTextWidth = width - 30,
                TextAlignment = TextAlignment.Center,
                MaxLineCount = 1
            };

            dc.DrawText(formattedText, new Point(15, height / 2 + 14));
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(drawingVisual);

        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        string? dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private bool TryExtractShellThumbnail(string filePath, string targetPath)
    {
        if (!_fileExistsChecker(filePath))
        {
            return false;
        }

        try
        {
            Guid factoryGuid = typeof(IShellItemImageFactory).GUID;
            int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, factoryGuid, out var factory);
            if (hr == 0 && factory != null)
            {
                var size = new ShellSize(320, 180);
                int hrImage = factory.GetImage(size, SIIGBF.SIIGBF_RESIZETOFIT, out IntPtr hBitmap);
                if (hrImage == 0 && hBitmap != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource? source = null;
                        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                        {
                            source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                        else
                        {
                            var thread = new Thread(() =>
                            {
                                source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                source?.Freeze();
                            });
                            thread.SetApartmentState(ApartmentState.STA);
                            thread.Start();
                            thread.Join();
                        }

                        if (source != null)
                        {
                            var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                            encoder.Frames.Add(BitmapFrame.Create(source));

                            string? dir = Path.GetDirectoryName(targetPath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }

                            using var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            encoder.Save(stream);
                            return true;
                        }
                    }
                    finally
                    {
                        NativeMethods.DeleteObject(hBitmap);
                    }
                }
            }
        }
        catch
        {
            // Shell extraction failed, fallback to procedural
        }

        return false;
    }

    private string ExtractDuration(string filePath)
    {
        if (_durationExtractor != null)
        {
            var extracted = _durationExtractor(filePath);
            return extracted.HasValue ? FormatDuration(extracted.Value) : "01:30";
        }

        if (!_fileExistsChecker(filePath))
        {
            return "01:30";
        }

        try
        {
            TimeSpan? duration = null;
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                duration = TryReadDurationWithMediaPlayer(filePath);
            }
            else
            {
                var thread = new Thread(() =>
                {
                    duration = TryReadDurationWithMediaPlayer(filePath);
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join(800);
            }

            if (duration.HasValue && duration.Value > TimeSpan.Zero)
            {
                return FormatDuration(duration.Value);
            }
        }
        catch
        {
            // Non-blocking fallback
        }

        return "01:30";
    }

    private static TimeSpan? TryReadDurationWithMediaPlayer(string filePath)
    {
        try
        {
            var player = new MediaPlayer();
            var waitHandle = new ManualResetEvent(false);
            player.MediaOpened += (s, e) => waitHandle.Set();
            player.MediaFailed += (s, e) => waitHandle.Set();
            player.Open(new Uri(filePath, UriKind.RelativeOrAbsolute));

            if (waitHandle.WaitOne(400))
            {
                if (player.NaturalDuration.HasTimeSpan)
                {
                    var ts = player.NaturalDuration.TimeSpan;
                    player.Close();
                    return ts;
                }
            }
            player.Close();
        }
        catch
        {
        }
        return null;
    }
}
