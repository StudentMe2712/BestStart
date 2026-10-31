using System;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Core.Services;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-03: Поведение поверх полноэкранных приложений (Fullscreen Detection).
/// Given: SystemMonitorService с периодическим таймером 1000 мс (SLA <= 1500 мс).
/// When: пользователь запускает полноэкранную игру или Direct3D-приложение (QUNS_RUNNING_D3D_FULL_SCREEN / QUNS_BUSY).
/// Then: виджет скрывается с экрана (Opacity -> 0, IsFullscreenRunning = true).
/// When: игра закрывается / происходит выход из Fullscreen.
/// Then: виджет плавно восстанавливается на экране за 300 мс.
/// </summary>
public sealed class AC03_FullscreenDetectionTests : IDisposable
{
    private readonly SystemMonitorService _service;

    public AC03_FullscreenDetectionTests()
    {
        // Отключаем автозапуск периодического таймера для детерминированного тестирования
        _service = new SystemMonitorService(autoStart: false, pollingIntervalMs: 1000);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void AC03_DefaultState_IsNotFullscreen()
    {
        // Given & Then: в обычном режиме полноэкранный режим не активен
        Assert.False(_service.IsFullscreenRunning);
    }

    [Fact]
    public void AC03_FullscreenDetected_FiresIsFullscreenRunningChanged_True()
    {
        // Given: сервис мониторинга активен
        bool eventFired = false;
        bool lastState = false;
        _service.IsFullscreenRunningChanged += state =>
        {
            eventFired = true;
            lastState = state;
        };

        // When: запущена полноэкранная игра (эмуляция перехода в D3D Fullscreen)
        _service.SetFullscreenStateForTesting(true);

        // Then: фиксируется полноэкранный режим, событие отправлено
        Assert.True(_service.IsFullscreenRunning);
        Assert.True(eventFired);
        Assert.True(lastState);
    }

    [Fact]
    public void AC03_ExitFullscreen_RestoresState_FiresEvent_False()
    {
        // Given: активна полноэкранная игра
        _service.SetFullscreenStateForTesting(true);
        Assert.True(_service.IsFullscreenRunning);

        bool exitFired = false;
        bool stateAfterExit = true;
        _service.IsFullscreenRunningChanged += state =>
        {
            exitFired = true;
            stateAfterExit = state;
        };

        // When: выход из полноэкранного режима на рабочий стол
        _service.SetFullscreenStateForTesting(false);

        // Then: сервис сообщает о выходе из fullscreen
        Assert.False(_service.IsFullscreenRunning);
        Assert.True(exitFired);
        Assert.False(stateAfterExit);
    }

    [Fact]
    public void AC03_PollingInterval_MeetsSpecificationSLA()
    {
        // Given: требование спецификации: детекция в течение 1500 мс
        // Then: интервал опроса сервиса составляет 1000 мс, что строго укладывается в норматив <= 1500 мс
        Assert.Equal(1000, _service.PollingIntervalMs);
        Assert.True(_service.PollingIntervalMs <= 1500);
    }

    [Fact]
    public void AC03_HeadlessCheck_DoesNotThrow()
    {
        // Given: безголовая среда CI/CD без активных окон
        // When: вызывается прямая проверка системных API
        var exception = Record.Exception(() => _service.CheckFullscreenState());

        // Then: P/Invoke не вызывает падений
        Assert.Null(exception);
    }
}
