namespace Stepwise.Core.Interfaces;

using Stepwise.Core.Models;

/// <summary>
/// Движок воспроизведения и навигации по шагам интерактивной инструкции (Player).
/// </summary>
public interface IPlayerEngine
{
    /// <summary>
    /// Текущее состояние конечного автомата воспроизведения.
    /// </summary>
    PlayerState State { get; }

    /// <summary>
    /// Индекс текущего отображаемого шага (0-based, либо -1, если инструкция не загружена/пуста).
    /// </summary>
    int CurrentIndex { get; }

    /// <summary>
    /// Общее количество шагов в текущей инструкции.
    /// </summary>
    int TotalSteps { get; }

    /// <summary>
    /// Текущий активный шаг или <c>null</c>, если шаги отсутствуют или не выбраны.
    /// </summary>
    Step? CurrentStep { get; }

    /// <summary>
    /// Полный неизменяемый список шагов загруженной инструкции.
    /// </summary>
    IReadOnlyList<Step> Steps { get; }

    /// <summary>
    /// Идентификатор текущей загруженной инструкции или <c>null</c>, если инструкция не загружена.
    /// </summary>
    Guid? CurrentGuideId { get; }

    /// <summary>
    /// Признак возможности перехода к следующему шагу или завершения инструкции.
    /// </summary>
    bool CanNext { get; }

    /// <summary>
    /// Признак возможности возврата к предыдущему шагу.
    /// </summary>
    bool CanPrevious { get; }

    /// <summary>
    /// Признак возможности перезапуска воспроизведения с первого шага.
    /// </summary>
    bool CanRestart { get; }

    /// <summary>
    /// Признак возможности запуска или возобновления воспроизведения.
    /// </summary>
    bool CanPlay { get; }

    /// <summary>
    /// Признак возможности приостановки воспроизведения.
    /// </summary>
    bool CanPause { get; }

    /// <summary>
    /// Признак возможности перехода к первому шагу инструкции.
    /// </summary>
    bool CanFirst { get; }

    /// <summary>
    /// Признак возможности перехода к последнему шагу инструкции.
    /// </summary>
    bool CanLast { get; }

    /// <summary>
    /// Загружает инструкцию для воспроизведения.
    /// </summary>
    /// <param name="guideId">Идентификатор инструкции.</param>
    /// <param name="steps">Список шагов инструкции.</param>
    /// <returns><c>true</c>, если загружена валидная непустая инструкция; иначе <c>false</c>.</returns>
    bool LoadGuide(Guid guideId, IReadOnlyList<Step>? steps);

    /// <summary>
    /// Выгружает текущую инструкцию и переводит движок в начальное состояние Idle.
    /// </summary>
    void Unload();

    /// <summary>
    /// Запускает или возобновляет воспроизведение инструкции.
    /// </summary>
    /// <returns><c>true</c>, если переход в Playing выполнен успешно; иначе <c>false</c>.</returns>
    bool Play();

    /// <summary>
    /// Приостанавливает воспроизведение инструкции.
    /// </summary>
    /// <returns><c>true</c>, если переход в Paused выполнен успешно; иначе <c>false</c>.</returns>
    bool Pause();

    /// <summary>
    /// Возобновляет воспроизведение инструкции после паузы.
    /// </summary>
    /// <returns><c>true</c>, если переход из Paused в Playing выполнен успешно; иначе <c>false</c>.</returns>
    bool Resume();

    /// <summary>
    /// Переходит к следующему шагу или завершает воспроизведение на последнем шаге.
    /// </summary>
    /// <returns><c>true</c>, если переход или завершение выполнены успешно; иначе <c>false</c>.</returns>
    bool Next();

    /// <summary>
    /// Возвращается к предыдущему шагу инструкции.
    /// </summary>
    /// <returns><c>true</c>, если переход выполнен успешно; иначе <c>false</c>.</returns>
    bool Previous();

    /// <summary>
    /// Переходит к первому шагу инструкции.
    /// </summary>
    /// <returns><c>true</c>, если переход выполнен успешно; иначе <c>false</c>.</returns>
    bool First();

    /// <summary>
    /// Переходит к последнему шагу инструкции.
    /// </summary>
    /// <returns><c>true</c>, если переход выполнен успешно; иначе <c>false</c>.</returns>
    bool Last();

    /// <summary>
    /// Перезапускает воспроизведение с первого шага (CurrentIndex = 0, State = Playing).
    /// </summary>
    /// <returns><c>true</c>, если перезапуск выполнен успешно; иначе <c>false</c>.</returns>
    bool Restart();

    /// <summary>
    /// Переводит движок в состояние ошибки с указанием причины сбоя.
    /// </summary>
    /// <param name="reason">Диагностическое описание ошибки.</param>
    void Fail(string reason);

    /// <summary>
    /// Событие изменения состояния конечного автомата воспроизведения.
    /// </summary>
    event PlayerStateChangedHandler? StateChanged;

    /// <summary>
    /// Событие смены текущего шага инструкции (новый индекс и новый шаг).
    /// </summary>
    event Action<int, Step?>? StepChanged;
}