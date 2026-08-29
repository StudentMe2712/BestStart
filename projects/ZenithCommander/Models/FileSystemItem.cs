using System;
using System.Globalization;
using System.IO;

namespace ZenithCommander.Models;

public class FileSystemItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool IsParentDirectory { get; set; }
    public long? Size { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public DateTime? DateModified { get; set; }
    public string FormattedDate { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "📄";
    public FileAttributes? Attributes { get; set; }
    public bool IsHidden { get; set; }
    public bool IsSystem { get; set; }

    public static FileSystemItem CreateParentDirectory(string parentPath)
    {
        return new FileSystemItem
        {
            Name = "[..]",
            FullPath = parentPath,
            IsDirectory = true,
            IsParentDirectory = true,
            Size = null,
            FormattedSize = "<UP-DIR>",
            DateModified = null,
            FormattedDate = string.Empty,
            Extension = string.Empty,
            IconGlyph = "⬆️"
        };
    }

    public static FileSystemItem FromDirectoryInfo(DirectoryInfo dir)
    {
        bool isHidden = (dir.Attributes & FileAttributes.Hidden) != 0;
        bool isSystem = (dir.Attributes & FileAttributes.System) != 0;

        return new FileSystemItem
        {
            Name = dir.Name,
            FullPath = dir.FullName,
            IsDirectory = true,
            IsParentDirectory = false,
            Size = null,
            FormattedSize = "<DIR>",
            DateModified = dir.LastWriteTime,
            FormattedDate = dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            Extension = string.Empty,
            IconGlyph = "📁",
            Attributes = dir.Attributes,
            IsHidden = isHidden,
            IsSystem = isSystem
        };
    }

    public static FileSystemItem FromFileInfo(FileInfo file)
    {
        string ext = file.Extension.TrimStart('.').ToUpperInvariant();
        long size = 0;
        try
        {
            size = file.Length;
        }
        catch
        {
            // In case file is locked or length inaccessible
        }

        bool isHidden = (file.Attributes & FileAttributes.Hidden) != 0;
        bool isSystem = (file.Attributes & FileAttributes.System) != 0;

        return new FileSystemItem
        {
            Name = file.Name,
            FullPath = file.FullName,
            IsDirectory = false,
            IsParentDirectory = false,
            Size = size,
            FormattedSize = FormatFileSize(size),
            DateModified = file.LastWriteTime,
            FormattedDate = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            Extension = ext,
            IconGlyph = ResolveIconGlyph(ext),
            Attributes = file.Attributes,
            IsHidden = isHidden,
            IsSystem = isSystem
        };
    }

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return string.Format(CultureInfo.InvariantCulture, "{0} B", bytes);
        if (bytes < 1024L * 1024)
            return string.Format(CultureInfo.InvariantCulture, "{0:F1} KB", bytes / 1024.0);
        if (bytes < 1024L * 1024 * 1024)
            return string.Format(CultureInfo.InvariantCulture, "{0:F1} MB", bytes / (1024.0 * 1024));
        if (bytes < 1024L * 1024 * 1024 * 1024)
            return string.Format(CultureInfo.InvariantCulture, "{0:F2} GB", bytes / (1024.0 * 1024 * 1024));

        return string.Format(CultureInfo.InvariantCulture, "{0:F2} TB", bytes / (1024.0 * 1024 * 1024 * 1024));
    }

    public static string ResolveIconGlyph(string extensionUpper)
    {
        return extensionUpper switch
        {
            "EXE" or "MSI" or "BAT" or "CMD" or "PS1" or "VBS" => "⚡",
            "DLL" or "SYS" or "INI" or "CFG" or "CONF" => "⚙️",
            "ZIP" or "RAR" or "7Z" or "TAR" or "GZ" or "BZ2" or "XZ" or "ISO" => "📦",
            "PNG" or "JPG" or "JPEG" or "GIF" or "BMP" or "WEBP" or "SVG" or "ICO" => "🖼️",
            "MP3" or "WAV" or "FLAC" or "AAC" or "OGG" or "M4A" or "WMA" => "🎵",
            "MP4" or "MKV" or "AVI" or "MOV" or "WMV" or "FLV" or "WEBM" => "🎬",
            "TXT" or "MD" or "LOG" or "NFO" or "RTF" => "📝",
            "PDF" or "DOC" or "DOCX" or "XLS" or "XLSX" or "PPT" or "PPTX" => "📄",
            "CS" or "JS" or "TS" or "JSON" or "XML" or "HTML" or "CSS" or "PY" or "CPP" or "C" or "H" or "SQL" => "💻",
            _ => "📄"
        };
    }
}