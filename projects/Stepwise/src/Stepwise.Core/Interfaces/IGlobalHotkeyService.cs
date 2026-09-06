using System;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Сервис регистрации глобальных системных горячих клавиш (Win32 RegisterHotKey)
/// для запуска и остановки записи из любого активного приложения (1С, браузер, рабочий стол).
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>
    /// Событие срабатывания глобальной горячей клавиши записи (Ctrl+Shift+R или F9).
    /// </summary>
    event EventHandler? RecordingHotkeyPressed;

    /// <summary>
    /// Регистрирует глобальные сочетания клавиш, привязанные к дескриптору окна.
    /// </summary>
    /// <param name="windowHandle">Дескриптор окна (HWND).</param>
    void Register(nint windowHandle);

    /// <summary>
    /// Отменяет регистрацию горячих клавиш.
    /// </summary>
    void Unregister();
}
