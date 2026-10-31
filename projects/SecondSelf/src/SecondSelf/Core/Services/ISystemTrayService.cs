using System;

namespace SecondSelf.Core.Services;

/// <summary>
/// Сервис системного трея Windows (Shell_NotifyIconW) с поддержкой контекстного меню и уведомлений.
/// </summary>
public interface ISystemTrayService : IDisposable
{
    /// <summary>
    /// Запрос открытия Personal Dashboard (действие по умолчанию / Bold).
    /// </summary>
    event Action? ShowDashboardRequested;

    /// <summary>
    /// Запрос показать / скрыть Desktop Widget.
    /// </summary>
    event Action? ToggleWidgetRequested;

    /// <summary>
    /// Запрос быстрого добавления задачи (Alt+Shift+N).
    /// </summary>
    event Action? QuickAddTaskRequested;

    /// <summary>
    /// Запрос выхода из приложения.
    /// </summary>
    event Action? ExitRequested;

    /// <summary>
    /// Флаг успешной инициализации системного трея.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Инициализация иконки в трее с указанием окна или созданием скрытого окна-обработчика.
    /// В безголовом/тестовом окружении вызов безопасен и не вызывает исключений.
    /// </summary>
    void Initialize(IntPtr? windowHandle = null, string? tooltip = null);

    /// <summary>
    /// Отображение всплывающего системного уведомления (balloon tip).
    /// </summary>
    void ShowNotification(string title, string message);

    /// <summary>
    /// Программное выполнение команды контекстного меню трея (для тестов и шорткатов).
    /// </summary>
    void TriggerCommand(uint commandId);

    /// <summary>
    /// Программное срабатывание глобального шортката (для тестов).
    /// </summary>
    void TriggerHotkey(int hotkeyId);
}
