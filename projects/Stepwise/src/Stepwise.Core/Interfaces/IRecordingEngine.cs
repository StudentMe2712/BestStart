using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Движок координации записи действий пользователя и формирования шагов.
/// </summary>
public interface IRecordingEngine : IDisposable
{
    /// <summary>
    /// Текущее состояние жизненного цикла сессии записи.
    /// </summary>
    RecordingSessionState State { get; }

    /// <summary>
    /// Событие изменения состояния сессии записи.
    /// </summary>
    event EventHandler<RecordingSessionState>? StateChanged;

    /// <summary>
    /// Событие формирования нового шага инструкции.
    /// </summary>
    event EventHandler<Step>? StepRecorded;

    /// <summary>
    /// Начинает запись действий.
    /// </summary>
    void StartRecording();

    /// <summary>
    /// Приостанавливает запись действий.
    /// </summary>
    void PauseRecording();

    /// <summary>
    /// Возобновляет запись действий после паузы.
    /// </summary>
    void ResumeRecording();

    /// <summary>
    /// Синхронно останавливает запись действий.
    /// </summary>
    void StopRecording();

    /// <summary>
    /// Асинхронно останавливает запись действий с поддержкой токена отмены.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    Task StopRecordingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Признак активного процесса записи (эквивалентно <c>State == RecordingSessionState.Recording</c>).
    /// </summary>
    bool IsRecording { get; }
}
