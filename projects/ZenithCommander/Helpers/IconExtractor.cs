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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHSTOCKICONINFO
    {
        public uint cbSize;
        public IntPtr hIcon;
        public int iSysImageIndex;
        public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPath;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint SHGFI_OPENICON = 0x000000002;

    private const uint SHGSI_ICON = 0x000000100;
    private const uint SHGSI_SMALLICON = 0x000000001;
    private const uint SHGSI_LARGEICON = 0x000000000;

    public const uint SIID_FOLDER = 3;
    public const uint SIID_DRIVE525 = 5;
    public const uint SIID_MYNETWORK = 17;
    public const uint SIID_DESKTOPPC = 35;
    public const uint SIID_WORLD = 49;

    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int ExtractIconEx(
        string lpszFile,
        int nIconIndex,
        out IntPtr phiconLarge,
        out IntPtr phiconSmall,
        int nIcons);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("shell32.dll", SetLastError = false)]
    private static extern int SHGetStockIconInfo(
        uint siid,
        uint uFlags,
        ref SHSTOCKICONINFO psii);

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

    /// <summary>
    /// Extracts an icon from %windir%\system32\imageres.dll by icon resource ID (negative index, e.g. -1024) or zero-based index.
    /// </summary>
    public static ImageSource? ExtractFromImageres(int iconIndex, bool isSmall = true)
    {
        string cacheKey = $"imageres_{iconIndex}_{(isSmall ? "s" : "l")}";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        string systemDir = Environment.SystemDirectory;
        string imageresPath = Path.Combine(systemDir, "imageres.dll");
        if (!File.Exists(imageresPath))
        {
            imageresPath = Environment.ExpandEnvironmentVariables(@"%windir%\system32\imageres.dll");
        }

        if (!File.Exists(imageresPath))
        {
            return null;
        }

        IntPtr hIconLarge = IntPtr.Zero;
        IntPtr hIconSmall = IntPtr.Zero;

        try
        {
            int extracted = ExtractIconEx(imageresPath, iconIndex, out hIconLarge, out hIconSmall, 1);
            if (extracted > 0)
            {
                IntPtr targetHIcon = isSmall
                    ? (hIconSmall != IntPtr.Zero ? hIconSmall : hIconLarge)
                    : (hIconLarge != IntPtr.Zero ? hIconLarge : hIconSmall);

                if (targetHIcon != IntPtr.Zero)
                {
                    var source = ConvertHIconToImageSource(targetHIcon);
                    if (source != null)
                    {
                        IconCache[cacheKey] = source;
                        return source;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract icon {iconIndex} from imageres.dll: {ex.Message}");
        }
        finally
        {
            if (hIconLarge != IntPtr.Zero)
            {
                DestroyIcon(hIconLarge);
            }
            if (hIconSmall != IntPtr.Zero)
            {
                DestroyIcon(hIconSmall);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the authentic Windows 11 "Home" (Blue House) icon from imageres.dll with fallbacks.
    /// </summary>
    public static ImageSource? GetHomeIcon(bool isSmall = true)
    {
        string cacheKey = $"__home_{(isSmall ? "s" : "l")}";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        ImageSource? icon = ExtractFromImageres(-1024, isSmall)
            ?? ExtractFromImageres(123, isSmall)
            ?? ExtractFromImageres(1024, isSmall)
            ?? GetStockIcon(SIID_FOLDER, isSmall)
            ?? GetSpecialFolderIcon(Environment.SpecialFolder.UserProfile, isSmall)
            ?? GetFolderIcon(isSmall);

        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }

        return icon;
    }

    /// <summary>
    /// Gets the authentic Windows 11 "Gallery" (Photos/Pictures) icon from imageres.dll with fallbacks.
    /// </summary>
    public static ImageSource? GetGalleryIcon(bool isSmall = true)
    {
        string cacheKey = $"__gallery_{(isSmall ? "s" : "l")}";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        ImageSource? icon = ExtractFromImageres(-113, isSmall)
            ?? ExtractFromImageres(113, isSmall)
            ?? GetSpecialFolderIcon(Environment.SpecialFolder.MyPictures, isSmall)
            ?? GetFolderIcon(isSmall);

        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }

        return icon;
    }

    /// <summary>
    /// Gets the authentic Windows 11 "This PC" (Computer tower & monitor blue icon).
    /// Extracts from imageres.dll, virtual shell folder GUID, and falls back to SIID_DESKTOPPC / SIID_DRIVE525.
    /// </summary>
    public static ImageSource? GetThisPcIcon(bool isSmall = true)
    {
        string cacheKey = $"__this_pc_{(isSmall ? "s" : "l")}";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // 1. Try imageres.dll resource -109 / index 109
        ImageSource? icon = ExtractFromImageres(-109, isSmall)
            ?? ExtractFromImageres(109, isSmall);

        // 2. Query shell virtual folder GUID for This PC
        if (icon == null)
        {
            try
            {
                icon = ExtractIconFromPath(@"shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", isSmall)
                    ?? ExtractIconFromPath(@"::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", isSmall);
            }
            catch { }
        }

        // 3. Fallback to Shell Stock Icon SIID_DESKTOPPC (35) or SIID_DRIVE525 (5)
        if (icon == null)
        {
            icon = GetStockIcon(SIID_DESKTOPPC, isSmall) ?? GetStockIcon(SIID_DRIVE525, isSmall);
        }

        // 4. Fallback to drive C:
        if (icon == null)
        {
            icon = GetDriveIcon("C:\\", isSmall);
        }

        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }

        return icon;
    }

    /// <summary>
    /// Gets the authentic Windows 11 "Network" icon (Network monitor with globe, never yellow folder).
    /// Extracts from imageres.dll, virtual shell folder GUID, and falls back to SIID_MYNETWORK / SIID_WORLD.
    /// </summary>
    public static ImageSource? GetNetworkIcon(bool isSmall = true)
    {
        string cacheKey = $"__network_{(isSmall ? "s" : "l")}";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // 1. Try imageres.dll resource -25 / index 25
        ImageSource? icon = ExtractFromImageres(-25, isSmall)
            ?? ExtractFromImageres(25, isSmall);

        // 2. Query shell virtual folder GUID for Network Places
        if (icon == null)
        {
            try
            {
                icon = ExtractIconFromPath(@"shell:::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", isSmall)
                    ?? ExtractIconFromPath(@"::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", isSmall);
            }
            catch { }
        }

        // 3. Fallback to Shell Stock Icon SIID_MYNETWORK (17) or SIID_WORLD (49)
        if (icon == null)
        {
            icon = GetStockIcon(SIID_MYNETWORK, isSmall) ?? GetStockIcon(SIID_WORLD, isSmall);
        }

        if (icon != null)
        {
            IconCache[cacheKey] = icon;
        }

        return icon;
    }

    /// <summary>
    /// Extracts a Windows stock shell icon by Stock Icon ID (SHSTOCKICONID).
    /// </summary>
    public static ImageSource? GetStockIcon(uint siid, bool isSmall = true)
    {
        string cacheKey = $"stock_{siid}_{(isSmall ? "s" : "l")}";
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var psii = new SHSTOCKICONINFO();
        psii.cbSize = (uint)Marshal.SizeOf(typeof(SHSTOCKICONINFO));
        uint flags = SHGSI_ICON | (isSmall ? SHGSI_SMALLICON : SHGSI_LARGEICON);

        try
        {
            int hr = SHGetStockIconInfo(siid, flags, ref psii);
            if (hr == 0 && psii.hIcon != IntPtr.Zero)
            {
                var icon = ConvertHIconToImageSource(psii.hIcon);
                if (icon != null)
                {
                    IconCache[cacheKey] = icon;
                }
                return icon;
            }
        }
        catch { }
        finally
        {
            if (psii.hIcon != IntPtr.Zero)
            {
                DestroyIcon(psii.hIcon);
            }
        }

        return null;
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
