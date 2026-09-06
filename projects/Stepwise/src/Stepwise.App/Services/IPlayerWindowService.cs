namespace Stepwise.App.Services;

/// <summary>
/// Сервис управления жизненным циклом автономного окна плеера (PlayerWindow).
/// </summary>
public interface IPlayerWindowService
{
    /// <summary>
    /// Открывает и активирует окно плеера с загрузкой указанного или текущего проекта.
    /// </summary>
    /// <param name="projectPath">Путь к каталогу проекта или null для текущего/по умолчанию.</param>
    void ShowPlayerWindow(string? projectPath = null);

    /// <summary>
    /// Закрывает активное окно плеера, если оно открыто.
    /// </summary>
    void ClosePlayerWindow();
}
