using System;

namespace SecondSelf.Core.Services;

/// <summary>
/// Сервис детекции полноэкранных Direct3D-приложений, игр и полноэкранных медиаплееров.
/// Использует Win32 SHQueryUserNotificationState и GetWindowRect/MonitorFromWindow.
/// </summary>
public interface ISystemMonitorService : IDisposable
{
    /// <summary>
    /// Флаг активности полноэкранного приложения на мониторе.
    /// </summary>
    bool IsFullscreenRunning { get; }

    /// <summary>
    /// Событие изменения статуса полноэкранного режима.
    /// </summary>
    event Action<bool>? IsFullscreenRunningChanged;

    /// <summary>
    /// Интервал периодического опроса (по умолчанию 1000 мс).
    /// </summary>
    int PollingIntervalMs { get; set; }

    /// <summary>
    /// Запуск мониторинга.
    /// </summary>
    void Start();

    /// <summary>
    /// Остановка мониторинга.
    /// </summary>
    void Stop();

    /// <summary>
    /// Принудительная проверка текущего состояния (для тестов или вызова по требованию).
    /// </summary>
    bool CheckFullscreenState();

    /// <summary>
    /// Ручная установка состояния для безголового/юнит-тестирования.
    /// </summary>
    void SetFullscreenStateForTesting(bool isFullscreen);
}
