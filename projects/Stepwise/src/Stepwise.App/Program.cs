using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace Stepwise.App;

/// <summary>
/// Точка входа WinUI 3 Desktop Unpackaged приложения.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        Application.Start((p) =>
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            var context = new DispatcherQueueSynchronizationContext(dispatcherQueue);
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
