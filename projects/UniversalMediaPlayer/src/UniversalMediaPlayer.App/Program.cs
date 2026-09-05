using System;
using System.IO;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace UniversalMediaPlayer.App;

public static class Program
{
    private static readonly string LogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalMediaPlayer", "startup.log");

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Program.Main entered. Args: {string.Join(" ", args)}\n");

            WinRT.ComWrappersSupport.InitializeComWrappers();
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] ComWrappersSupport.InitializeComWrappers completed\n");

            Application.Start((p) =>
            {
                try
                {
                    File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Application.Start callback invoked\n");
                    var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                    File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] DispatcherQueue: {(dispatcherQueue != null ? "Found" : "NULL")}\n");

                    var context = new DispatcherQueueSynchronizationContext(dispatcherQueue!);
                    SynchronizationContext.SetSynchronizationContext(context);
                    File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] SynchronizationContext configured\n");

                    new App();
                    File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] App constructed inside callback\n");
                }
                catch (Exception ex)
                {
                    File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] EXCEPTION inside Application.Start callback: {ex}\n");
                    throw;
                }
            });

            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Application.Start finished\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] FATAL EXCEPTION in Program.Main: {ex}\n");
        }
    }
}
