using System;
using SecondSelf.Core.Services;
using Xunit;

namespace SecondSelf.Tests;

public class SystemTrayServiceTests
{
    [Fact]
    public void Initialize_HeadlessMode_DoesNotThrowAndDisposesCleanly()
    {
        using var trayService = new SystemTrayService();

        // В безголовом тестовом режиме инициализация не должна выбрасывать исключений
        var exception = Record.Exception(() => trayService.Initialize(IntPtr.Zero, "Test SecondSelf"));
        Assert.Null(exception);

        // Показ уведомлений также не должен падать
        var notifyEx = Record.Exception(() => trayService.ShowNotification("Hello", "World"));
        Assert.Null(notifyEx);
    }

    [Fact]
    public void TriggerCommand_FiresCorrespondingEvents()
    {
        using var trayService = new SystemTrayService();

        bool dashboardFired = false;
        bool widgetFired = false;
        bool quickAddFired = false;
        bool exitFired = false;

        trayService.ShowDashboardRequested += () => dashboardFired = true;
        trayService.ToggleWidgetRequested += () => widgetFired = true;
        trayService.QuickAddTaskRequested += () => quickAddFired = true;
        trayService.ExitRequested += () => exitFired = true;

        trayService.TriggerCommand(SystemTrayService.CMD_OPEN_DASHBOARD);
        Assert.True(dashboardFired);

        trayService.TriggerCommand(SystemTrayService.CMD_TOGGLE_WIDGET);
        Assert.True(widgetFired);

        trayService.TriggerCommand(SystemTrayService.CMD_QUICK_ADD_TASK);
        Assert.True(quickAddFired);

        trayService.TriggerCommand(SystemTrayService.CMD_EXIT);
        Assert.True(exitFired);
    }
}
