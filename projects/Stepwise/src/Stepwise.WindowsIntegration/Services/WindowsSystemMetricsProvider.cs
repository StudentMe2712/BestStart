using Stepwise.Core.Interfaces;
using Stepwise.WindowsIntegration.Native;

namespace Stepwise.WindowsIntegration.Services;

/// <summary>
/// Провайдер системных метрик Windows на основе нативных Win32 API функций (User32).
/// Использует реальные системные пороги времени и размеров зоны двойного клика,
/// настроенные пользователем в операционной системе Windows (через GetDoubleClickTime и GetSystemMetrics).
/// Предусматривает отказоустойчивые резервные значения (500 мс, 4x4 пикселя) при сбоях или возврате неположительных значений.
/// </summary>
public sealed class WindowsSystemMetricsProvider : ISystemMetricsProvider
{
    public const int FallbackDoubleClickTimeMs = 500;
    public const int FallbackDoubleClickWidth = 4;
    public const int FallbackDoubleClickHeight = 4;

    /// <summary>
    /// Максимальный интервал в миллисекундах между двумя кликами для регистрации двойного клика.
    /// Вызывает нативную функцию Windows <see cref="NativeMethods.GetDoubleClickTime"/>.
    /// Если результат API <= 0, возвращает резервное значение 500 мс.
    /// </summary>
    public int DoubleClickTimeMs
    {
        get
        {
            var time = (int)NativeMethods.GetDoubleClickTime();
            return time > 0 ? time : FallbackDoubleClickTimeMs;
        }
    }

    /// <summary>
    /// Максимальная ширина прямоугольника в пикселях, в пределах которого должен произойти второй клик.
    /// Вызывает нативную функцию Windows <see cref="NativeMethods.GetSystemMetrics"/> с параметром <see cref="NativeMethods.SM_CXDOUBLECLK"/>.
    /// Если результат API <= 0, возвращает резервное значение 4 пикселя.
    /// </summary>
    public int DoubleClickWidth
    {
        get
        {
            var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXDOUBLECLK);
            return width > 0 ? width : FallbackDoubleClickWidth;
        }
    }

    /// <summary>
    /// Максимальная высота прямоугольника в пикселях, в пределах которого должен произойти второй клик.
    /// Вызывает нативную функцию Windows <see cref="NativeMethods.GetSystemMetrics"/> с параметром <see cref="NativeMethods.SM_CYDOUBLECLK"/>.
    /// Если результат API <= 0, возвращает резервное значение 4 пикселя.
    /// </summary>
    public int DoubleClickHeight
    {
        get
        {
            var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYDOUBLECLK);
            return height > 0 ? height : FallbackDoubleClickHeight;
        }
    }
}
