using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Движок координации записи действий пользователя и формирования шагов.
/// </summary>
public interface IRecordingEngine : IDisposable
{
    /// <summary>
    /// Событие формирования нового шага инструкции.
    /// </summary>
    event EventHandler<Step>? StepRecorded;

    /// <summary>
    /// Начинает запись действий.
    /// </summary>
    void StartRecording();

    /// <summary>
    /// Останавливает запись действий.
    /// </summary>
    void StopRecording();

    /// <summary>
    /// Признак активного процесса записи.
    /// </summary>
    bool IsRecording { get; }
}
