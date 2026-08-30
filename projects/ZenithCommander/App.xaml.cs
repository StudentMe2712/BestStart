using System;
using System.IO;
using System.Windows;

namespace NexusCommander;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var msg = $"[AppDomain Unhandled] {ex?.ToString() ?? args.ExceptionObject?.ToString()}";
            Console.WriteLine(msg);
            File.WriteAllText("startup_error.log", msg);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            var msg = $"[Dispatcher Unhandled] {args.Exception}";
            Console.WriteLine(msg);
            File.WriteAllText("startup_error.log", msg);
            args.Handled = false;
        };

        base.OnStartup(e);
    }
}