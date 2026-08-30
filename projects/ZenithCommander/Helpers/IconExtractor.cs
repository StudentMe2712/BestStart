using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NexusCommander.Helpers;

public static class IconExtractor
{
    #region Win32 Native Interop

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint SHGFI_OPENICON = 0x000000002;

    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    #endregion

    private static readonly ConcurrentDictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the native Windows yellow folder icon.
    /// </summary>
    public static ImageSource? GetFolderIcon(bool isSmall = true)
    {
        string cacheKey = isSmall ? "__folder_small__" : "__folder_large__";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var icon = ExtractIconFromAttributes("folder", FILE_ATTRIBUTE_DIRECTORY, isSmall);
        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }
        return icon;
    }

    /// <summary>
    /// Gets the native Windows file icon based on file path or extension.
    /// </summary>
    public static ImageSource? GetFileIcon(string filePathOrExt, bool isSmall = true)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExt))
        {
            return GetGenericFileIcon(isSmall);
        }

        string ext = Path.GetExtension(filePathOrExt).ToLowerInvariant();

        // For executable, shortcut, and icon files, extract specific icon per file path if it exists
        bool isSpecificFile = (ext is ".exe" or ".ico" or ".lnk" or ".cur") && File.Exists(filePathOrExt);

        string cacheKey = isSpecificFile
            ? $"path_{filePathOrExt}_{(isSmall ? "s" : "l")}"
            : $"ext_{ext}_{(isSmall ? "s" : "l")}";

        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        ImageSource? icon = null;

        if (isSpecificFile)
        {
            icon = ExtractIconFromPath(filePathOrExt, isSmall);
        }

        // Fallback or generic extension extraction via shell attributes (no disk IO)
        if (icon == null)
        {
            string lookup = string.IsNullOrEmpty(ext) ? "dummy" : ext;
            icon = ExtractIconFromAttributes(lookup, FILE_ATTRIBUTE_NORMAL, isSmall);
        }

        if (icon == null)
        {
            icon = GetGenericFileIcon(isSmall);
        }

        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }

        return icon;
    }

    /// <summary>
    /// Gets the native Windows drive icon (e.g. C:\).
    /// </summary>
    public static ImageSource? GetDriveIcon(string drivePath, bool isSmall = true)
    {
        if (string.IsNullOrWhiteSpace(drivePath))
        {
            drivePath = "C:\\";
        }

        string normalized = drivePath.TrimEnd('\\') + "\\";
        string cacheKey = $"drive_{normalized.ToUpperInvariant()}_{(isSmall ? "s" : "l")}";

        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var icon = ExtractIconFromPath(normalized, isSmall);
        if (icon == null)
        {
            icon = GetFolderIcon(isSmall);
        }

        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }

        return icon;
    }

    /// <summary>
    /// Gets native colorful shell icon for Windows Special Folders (Desktop, Downloads, Documents, Pictures, Music, Videos).
    /// </summary>
    public static ImageSource? GetSpecialFolderIcon(Environment.SpecialFolder folder, bool isSmall = true)
    {
        string cacheKey = $"special_{(int)folder}_{(isSmall ? "s" : "l")}";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        ImageSource? icon = null;
        try
        {
            string path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                icon = ExtractIconFromPath(path, isSmall);
            }
        }
        catch { }

        if (icon == null)
        {
            icon = GetFolderIcon(isSmall);
        }

        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }

        return icon;
    }

    /// <summary>
    /// Gets native shell icon for any custom directory path (e.g. Downloads folder in User Profile).
    /// </summary>
    public static ImageSource? GetCustomFolderIcon(string path, bool isSmall = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return GetFolderIcon(isSmall);
        }

        string cacheKey = $"dir_{path.ToUpperInvariant()}_{(isSmall ? "s" : "l")}";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var icon = ExtractIconFromPath(path, isSmall);
        if (icon == null)
        {
            icon = GetFolderIcon(isSmall);
        }

        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }

        return icon;
    }

    private static ImageSource? GetGenericFileIcon(bool isSmall)
    {
        string cacheKey = isSmall ? "__file_generic_small__" : "__file_generic_large__";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var icon = ExtractIconFromAttributes(".txt", FILE_ATTRIBUTE_NORMAL, isSmall);
        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }
        return icon;
    }

    private static ImageSource? ExtractIconFromAttributes(string fakeName, uint fileAttributes, bool isSmall)
    {
        var shfi = new SHFILEINFO();
        uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (isSmall ? SHGFI_SMALLICON : SHGFI_LARGEICON);

        try
        {
            IntPtr res = SHGetFileInfo(fakeName, fileAttributes, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
            if (res != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
            {
                return ConvertHIconToImageSource(shfi.hIcon);
            }
        }
        catch { }
        finally
        {
            if (shfi.hIcon != IntPtr.Zero)
            {
                DestroyIcon(shfi.hIcon);
            }
        }

        return null;
    }

    private static ImageSource? ExtractIconFromPath(string path, bool isSmall)
    {
        var shfi = new SHFILEINFO();
        uint flags = SHGFI_ICON | (isSmall ? SHGFI_SMALLICON : SHGFI_LARGEICON);

        try
        {
            IntPtr res = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
            if (res != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
            {
                return ConvertHIconToImageSource(shfi.hIcon);
            }
        }
        catch { }
        finally
        {
            if (shfi.hIcon != IntPtr.Zero)
            {
                DestroyIcon(shfi.hIcon);
            }
        }

        return null;
    }

    private static ImageSource? ConvertHIconToImageSource(IntPtr hIcon)
    {
        try
        {
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            // Freeze the BitmapSource so it is thread-safe and can be used on any thread / passed across threads
            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            return null;
        }
    }
}
