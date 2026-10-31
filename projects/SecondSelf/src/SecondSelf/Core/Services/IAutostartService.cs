namespace SecondSelf.Core.Services;

/// <summary>
/// Сервис управления автозапуском приложения при старте Windows через реестр.
/// </summary>
public interface IAutostartService
{
    /// <summary>
    /// Проверяет, включён ли автозапуск в ветке HKCU Run.
    /// </summary>
    bool IsAutostartEnabled();

    /// <summary>
    /// Включает или отключает автозапуск приложения в реестре Windows.
    /// </summary>
    void SetAutostart(bool enable);

    /// <summary>
    /// Возвращает сформированную командную строку запуска с флагом --startup-background.
    /// </summary>
    string GetAutostartCommandLine();
}
