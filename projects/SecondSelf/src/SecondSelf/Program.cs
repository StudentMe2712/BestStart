using System;
using System.Linq;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
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
}
