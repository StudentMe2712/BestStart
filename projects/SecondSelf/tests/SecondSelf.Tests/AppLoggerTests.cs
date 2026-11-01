using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SecondSelf.Core.Services;
using Xunit;

namespace SecondSelf.Tests;

public class AppLoggerTests
{
    [Fact]
    public void Paths_AreConfiguredCorrectly()
    {
        string expectedDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SecondSelf", "logs");
        string expectedFile = Path.Combine(expectedDir, "secondself_latest.log");

        Assert.Equal(expectedDir, AppLogger.LogDirectoryPath);
        Assert.Equal(expectedFile, AppLogger.LogFilePath);
    }

    [Fact]
    public void LogMethods_WriteFormattedLinesToFileAndHandleExceptions()
    {
        string testMessageInfo = $"Test_Info_{Guid.NewGuid():N}";
        string testMessageWarn = $"Test_Warn_{Guid.NewGuid():N}";
        string testMessageError = $"Test_Error_{Guid.NewGuid():N}";
        string testMessageFatal = $"Test_Fatal_{Guid.NewGuid():N}";

        var testEx = new InvalidOperationException("Simulation of an unhandled error");

        AppLogger.Info(testMessageInfo);
        AppLogger.Warn(testMessageWarn);
        AppLogger.Error(testEx, testMessageError);
        AppLogger.Fatal(testEx, testMessageFatal);

        // Убедимся, что директория и файл логов созданы
        Assert.True(Directory.Exists(AppLogger.LogDirectoryPath));
        Assert.True(File.Exists(AppLogger.LogFilePath));

        // Считываем файл с флагом FileShare.ReadWrite, чтобы не конфликтовать с открытым логером
        using var fs = new FileStream(AppLogger.LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        string content = reader.ReadToEnd();

        Assert.Contains("[INFO] " + testMessageInfo, content);
        Assert.Contains("[WARN] " + testMessageWarn, content);
        Assert.Contains("[ERROR] " + testMessageError, content);
        Assert.Contains("[FATAL] " + testMessageFatal, content);
        Assert.Contains("Simulation of an unhandled error", content);
    }

    [Fact]
    public void ConcurrentLogging_IsThreadSafe()
    {
        const int threadCount = 10;
        const int iterationsPerThread = 50;

        Parallel.For(0, threadCount, t =>
        {
            for (int i = 0; i < iterationsPerThread; i++)
            {
                AppLogger.Info($"Thread-{t}-iter-{i}");
            }
        });

        Assert.True(File.Exists(AppLogger.LogFilePath));
    }
}
