using System;
using System.Globalization;
using System.IO;

namespace NexusCommander.Models;

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
    public string ItemType { get; set; } = "File";
    public FileAttributes? Attributes { get; set; }
    public bool IsHidden { get; set; }
    public bool IsSystem { get; set; }

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
            FormattedSize = string.Empty,
            DateModified = dir.LastWriteTime,
            FormattedDate = dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            Extension = string.Empty,
            IconGlyph = "📁",
            ItemType = "File folder",
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
            Extension = string.IsNullOrEmpty(ext) ? string.Empty : "." + ext.ToLowerInvariant(),
            IconGlyph = ResolveIconGlyph(ext),
            ItemType = ResolveItemType(ext),
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

    public static string ResolveIconGlyph(string extUpper)
    {
        return extUpper switch
        {
            "EXE" or "MSI" or "BAT" or "CMD" or "PS1" or "VBS" or "COM" => "⚡",
            "DLL" or "SYS" or "INI" or "CFG" or "CONF" or "ENV" or "CONFIG" => "⚙️",
            "ZIP" or "RAR" or "7Z" or "TAR" or "GZ" or "BZ2" or "XZ" or "ISO" or "CAB" or "TGZ" => "📦",
            "PNG" or "JPG" or "JPEG" or "GIF" or "BMP" or "WEBP" or "SVG" or "ICO" or "TIFF" or "PSD" => "🖼️",
            "MP3" or "WAV" or "FLAC" or "AAC" or "OGG" or "M4A" or "WMA" or "AIFF" or "MID" => "🎵",
            "MP4" or "MKV" or "AVI" or "MOV" or "WMV" or "FLV" or "WEBM" or "M4V" or "3GP" => "🎬",
            "TXT" or "MD" or "LOG" or "NFO" or "RTF" or "DOC" or "DOCX" or "ODT" => "📝",
            "PDF" => "📕",
            "XLS" or "XLSX" or "CSV" or "TSV" => "📊",
            "PPT" or "PPTX" => "📽️",
            "CS" or "JS" or "TS" or "JSON" or "XML" or "HTML" or "CSS" or "SCSS" or "PY" or "CPP" or "C" or "H" or "HPP" or "SQL" or "JAVA" or "GO" or "RS" or "PHP" or "RB" or "SH" or "YAML" or "YML" or "XAML" => "💻",
            _ => "📄"
        };
    }

    public static string ResolveItemType(string extUpper)
    {
        return extUpper switch
        {
            "EXE" => "Application",
            "MSI" => "Windows Installer Package",
            "BAT" or "CMD" => "Windows Command Script",
            "PS1" => "PowerShell Script",
            "DLL" => "Application Extension (DLL)",
            "SYS" => "System File",
            "INI" or "CFG" or "CONF" or "CONFIG" => "Configuration File",
            "ZIP" => "ZIP Archive",
            "RAR" => "WinRAR Archive",
            "7Z" => "7-Zip Archive",
            "TAR" or "GZ" or "TGZ" => "Compressed Archive",
            "ISO" => "Disc Image File",
            "PNG" => "PNG Image",
            "JPG" or "JPEG" => "JPEG Image",
            "GIF" => "GIF Image",
            "SVG" => "SVG Vector Image",
            "ICO" => "Icon File",
            "WEBP" => "WEBP Image",
            "MP3" => "MP3 Audio",
            "WAV" => "WAV Audio",
            "FLAC" => "FLAC Audio",
            "MP4" => "MP4 Video",
            "MKV" => "MKV Video",
            "AVI" => "AVI Video",
            "TXT" => "Text Document",
            "MD" => "Markdown Document",
            "LOG" => "Log File",
            "PDF" => "PDF Document",
            "DOC" or "DOCX" => "Microsoft Word Document",
            "XLS" or "XLSX" => "Microsoft Excel Worksheet",
            "CSV" => "CSV Comma Delimited File",
            "PPT" or "PPTX" => "Microsoft PowerPoint Presentation",
            "CS" => "C# Source File",
            "JS" => "JavaScript File",
            "TS" => "TypeScript File",
            "JSON" => "JSON File",
            "XML" => "XML Document",
            "HTML" or "HTM" => "HTML Document",
            "CSS" => "Cascading Style Sheet",
            "PY" => "Python File",
            "CPP" or "CXX" => "C++ Source File",
            "C" => "C Source File",
            "H" or "HPP" => "C/C++ Header File",
            "SQL" => "SQL Query / Database Script",
            "XAML" => "XAML UI File",
            "CSPROJ" or "SLN" => "Visual Studio Project / Solution",
            "" => "File",
            _ => $"{extUpper} File"
        };
    }
}