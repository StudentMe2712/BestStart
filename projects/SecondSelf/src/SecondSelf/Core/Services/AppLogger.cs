using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SecondSelf.Core.Services;

/// <summary>
/// Потокобезопасный файловый логер с авто-флашем (Zero Blind Coding).
/// </summary>
public static class AppLogger
{
    private static readonly object _syncLock = new();
    private static StreamWriter? _writer;

    /// <summary>
    /// Путь к директории хранения файлов логов.
    /// </summary>
    public static string LogDirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SecondSelf",
        "logs");

    /// <summary>
    /// Полный путь к актуальному файлу логов приложения.
    /// </summary>
    public static string LogFilePath { get; } = Path.Combine(LogDirectoryPath, "secondself_latest.log");

    public static void Info(string message) => WriteLog("INFO", message);

    public static void Warn(string message) => WriteLog("WARN", message);

    public static void Error(Exception? ex, string message) => WriteLog("ERROR", message, ex);

    public static void Error(string message) => WriteLog("ERROR", message, null);

    public static void Fatal(Exception? ex, string message) => WriteLog("FATAL", message, ex);

    public static void Fatal(string message) => WriteLog("FATAL", message, null);

    private static void WriteLog(string level, string message, Exception? ex = null)
    {
        try
        {
            string formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            if (ex != null)
            {
                formattedMessage += $"{Environment.NewLine}{ex}";
            }

            Debug.WriteLine(formattedMessage);

            lock (_syncLock)
            {
                EnsureWriterInitialized();
                _writer?.WriteLine(formattedMessage);
            }
        }
        catch
        {
            // Логирование никогда не должно приводить к падению приложения
        }
    }

    private static void EnsureWriterInitialized()
    {
        if (_writer == null)
        {
            if (!Directory.Exists(LogDirectoryPath))
            {
                Directory.CreateDirectory(LogDirectoryPath);
            }

            var fileStream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(fileStream, Encoding.UTF8)
            {
                AutoFlush = true
            };
        }
    }

    /// <summary>
    /// Сброс буферов и закрытие потока записи (при выходе из приложения или в тестах).
    /// </summary>
    public static void Close()
    {
        lock (_syncLock)
        {
            if (_writer != null)
            {
                try
                {
                    _writer.Flush();
                    _writer.Dispose();
                }
                catch
                {
                    // Подавление ошибок закрытия
                }
                finally
                {
                    _writer = null;
                }
            }
        }
    }
}
