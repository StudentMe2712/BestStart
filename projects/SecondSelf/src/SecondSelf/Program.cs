using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SecondSelf.Core.Services;
using WinRT;

namespace SecondSelf;

/// <summary>
/// Точка входа WinUI 3 Desktop Unpackaged приложения Second Self.
/// </summary>
public static class Program
{
    public static bool IsStartupBackground { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            AppLogger.Fatal(ex, $"AppDomain UnhandledException (IsTerminating={e.IsTerminating})");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            AppLogger.Error(e.Exception, "TaskScheduler UnobservedTaskException");
        };

        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            AppLogger.Info("SecondSelf application stopping (ProcessExit)...");
            AppLogger.Close();
        };

        AppLogger.Info("SecondSelf application starting...");

        try
        {
            IsStartupBackground = args.Any(a => string.Equals(a, "--startup-background", StringComparison.OrdinalIgnoreCase));

            ComWrappersSupport.InitializeComWrappers();
            Application.Start((p) =>
            {
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                var context = new DispatcherQueueSynchronizationContext(dispatcherQueue);
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App(IsStartupBackground);
            });
        }
        catch (Exception ex)
        {
            AppLogger.Fatal(ex, "Unhandled exception in Program.Main");
            throw;
        }
        finally
        {
            AppLogger.Info("SecondSelf application stopping...");
            AppLogger.Close();
        }
    }
}
