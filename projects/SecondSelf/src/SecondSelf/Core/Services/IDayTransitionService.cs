using System;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Data.Entities;

namespace SecondSelf.Core.Services;

/// <summary>
/// Сервис управления суточным циклом приложения и переходом суток в 04:00 утра.
/// </summary>
public interface IDayTransitionService : IDisposable
{
    /// <summary>
    /// Текущая логическая дата в формате 'yyyy-MM-dd' (смена происходит в 04:00 утра).
    /// </summary>
    string CurrentLogicalDate { get; }

    /// <summary>
    /// Возвращает назначенный на логическую дату ритуал или назначает следующий из очереди воспроизведения.
    /// Гарантирует сохранение одного и того же видео при любых перезапусках в рамках одних логических суток.
    /// В случае физического удаления назначенного файла производит бесшовную замену на следующий из очереди.
    /// </summary>
    Task<DailyRitualLog?> GetOrAssignDailyRitualAsync(DateTime? currentTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Вычисляет интервал времени до ближайших 04:00:00.
    /// </summary>
    TimeSpan CalculateDelayUntilNextTransition(DateTime currentTime);

    /// <summary>
    /// Запускает или перезапускает фоновый таймер перехода суток.
    /// </summary>
    void StartTimer();

    /// <summary>
    /// Останавливает фоновый таймер перехода суток.
    /// </summary>
    void StopTimer();

    /// <summary>
    /// Принудительно инициирует событие перехода суток на указанное время (используется для тестирования).
    /// </summary>
    Task TriggerDayTransitionAsync(DateTime transitionTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Помечает текущий файл как поврежденный/отсутствующий и производит тихую замену на следующий валидный ролик из очереди.
    /// </summary>
    Task<DailyRitualLog?> ReplaceCorruptedVideoAsync(string videoId, DateTime? currentTime = null, CancellationToken cancellationToken = default);
}
