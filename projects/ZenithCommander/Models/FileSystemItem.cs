using System;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using NexusCommander.Helpers;

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
    public ImageSource? Icon { get; set; }
    public string IconGlyph { get; set; } = "📄";
    public string ItemType { get; set; } = "Файл";
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
            Icon = IconExtractor.GetFolderIcon(true),
            IconGlyph = "📁",
            ItemType = "Папка с файлами",
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
            Icon = IconExtractor.GetFileIcon(file.FullName, true),
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
            return string.Format(CultureInfo.InvariantCulture, "{0} Б", bytes);
        if (bytes < 1024L * 1024)
            return string.Format(CultureInfo.InvariantCulture, "{0:F1} КБ", bytes / 1024.0);
        if (bytes < 1024L * 1024 * 1024)
            return string.Format(CultureInfo.InvariantCulture, "{0:F1} МБ", bytes / (1024.0 * 1024));
        if (bytes < 1024L * 1024 * 1024 * 1024)
            return string.Format(CultureInfo.InvariantCulture, "{0:F2} ГБ", bytes / (1024.0 * 1024 * 1024));

        return string.Format(CultureInfo.InvariantCulture, "{0:F2} ТБ", bytes / (1024.0 * 1024 * 1024 * 1024));
    }

    public static string ResolveIconGlyph(string extUpper)
    {
        return extUpper switch
        {
            // Executable / App files: Electric blue ⚡
            "EXE" or "MSI" or "BAT" or "CMD" or "PS1" or "VBS" or "COM" or "APK" or "APP" or "JAR" or "WSF" => "⚡",
            // System / Config
            "DLL" or "SYS" or "INI" or "CFG" or "CONF" or "ENV" or "CONFIG" or "REG" or "INF" or "PROPERTIES" => "⚙️",
            // Archives: Gold amber 📦
            "ZIP" or "RAR" or "7Z" or "TAR" or "GZ" or "BZ2" or "XZ" or "ISO" or "CAB" or "TGZ" or "WAR" or "Z" => "📦",
            // Images: Magenta/Rose 🖼️
            "PNG" or "JPG" or "JPEG" or "GIF" or "BMP" or "WEBP" or "SVG" or "ICO" or "TIFF" or "PSD" or "AI" or "RAW" or "CR2" or "NEF" or "HEIC" or "AVIF" => "🖼️",
            // Audio: Purple/Pink 🎵
            "MP3" or "WAV" or "FLAC" or "AAC" or "OGG" or "M4A" or "WMA" or "AIFF" or "MID" or "MIDI" or "OPUS" or "M3U" => "🎵",
            // Video: Coral/Red 🎬
            "MP4" or "MKV" or "AVI" or "MOV" or "WMV" or "FLV" or "WEBM" or "M4V" or "3GP" or "TS" or "MPG" or "MPEG" or "VOB" => "🎬",
            // Documents & Notes: Warm amber / text 📝 / 📄 / 📕 / 📊 / 📽️
            "TXT" or "LOG" or "NFO" or "RTF" => "📝",
            "MD" or "MARKDOWN" => "📝",
            "PDF" => "📕",
            "DOC" or "DOCX" or "ODT" => "📄",
            "XLS" or "XLSX" or "CSV" or "TSV" or "ODS" => "📊",
            "PPT" or "PPTX" or "ODP" or "KEY" => "📽️",
            // Code files: Cyan/Green 💻
            "CS" or "JS" or "TS" or "JSON" or "XML" or "HTML" or "HTM" or "CSS" or "SCSS" or "SASS" or "LESS" or "PY" or "PYW" or "CPP" or "C" or "CC" or "CXX" or "H" or "HPP" or "SQL" or "JAVA" or "GO" or "RS" or "PHP" or "RB" or "SH" or "BASH" or "YAML" or "YML" or "XAML" or "VUE" or "JSX" or "TSX" or "DART" or "SWIFT" or "KT" or "LUA" or "R" or "PL" or "CSX" or "CSPROJ" or "SLN" or "TOML" => "💻",
            _ => "📄"
        };
    }

    public static string ResolveItemType(string extUpper)
    {
        return extUpper switch
        {
            "EXE" => "Приложение",
            "DLL" => "Библиотека приложений",
            "MSI" => "Пакет установки Windows",
            "BAT" or "CMD" => "Командный сценарий Windows",
            "PS1" => "Скрипт PowerShell",
            "SYS" => "Системный файл",
            "CS" => "Исходный код C#",
            "XAML" => "Разметка XAML",
            "CPP" or "CXX" or "C" or "CC" or "H" or "HPP" => "Исходный код C/C++",
            "PY" or "PYW" => "Скрипт Python",
            "JS" => "Файл JavaScript",
            "TS" => "Файл TypeScript",
            "HTML" or "HTM" => "Документ HTML",
            "CSS" or "SCSS" or "SASS" or "LESS" => "Каскадная таблица стилей",
            "JSON" => "Документ JSON",
            "XML" => "Документ XML",
            "YAML" or "YML" or "TOML" => "Файл конфигурации",
            "MD" or "MARKDOWN" => "Документ Markdown",
            "TXT" or "LOG" or "INI" or "CFG" or "CONF" or "CONFIG" or "NFO" => "Текстовый документ",
            "PNG" or "JPG" or "JPEG" or "GIF" or "BMP" or "WEBP" or "SVG" or "ICO" or "TIFF" or "PSD" or "HEIC" or "AVIF" => $"Изображение {extUpper}",
            "MP3" or "WAV" or "FLAC" or "AAC" or "OGG" or "M4A" or "WMA" or "OPUS" => $"Аудиофайл {extUpper}",
            "MP4" or "MKV" or "AVI" or "MOV" or "WMV" or "FLV" or "WEBM" or "M4V" => $"Видеофайл {extUpper}",
            "ZIP" or "RAR" or "7Z" or "TAR" or "GZ" or "TGZ" or "BZ2" or "XZ" or "CAB" => $"Архив {extUpper}",
            "ISO" => "Образ диска ISO",
            "PDF" => "Документ PDF",
            "DOC" or "DOCX" or "ODT" or "RTF" => "Документ Microsoft Word",
            "XLS" or "XLSX" or "CSV" or "TSV" or "ODS" => "Таблица Microsoft Excel",
            "PPT" or "PPTX" or "ODP" => "Презентация Microsoft PowerPoint",
            "SQL" => "Скрипт базы данных SQL",
            "CSPROJ" or "SLN" => "Проект Visual Studio",
            "JAVA" => "Исходный код Java",
            "GO" => "Исходный код Go",
            "RS" => "Исходный код Rust",
            "PHP" => "Скрипт PHP",
            "RB" => "Скрипт Ruby",
            "SH" or "BASH" => "Скрипт оболочки Unix",
            "" => "Файл",
            _ => $"Файл {extUpper}"
        };
    }
}