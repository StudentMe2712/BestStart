using System.Threading;
using System.Threading.Tasks;

namespace SecondSelf.Data;

/// <summary>
/// Результат проверки целостности и самовосстановления базы данных SQLite.
/// </summary>
public sealed record DatabaseRecoveryResult
{
    /// <summary>
    /// Была ли обнаружена поврежденность базы данных SQLite.
    /// </summary>
    public bool WasCorrupted { get; init; }

    /// <summary>
    /// Была ли база данных успешно восстановлена из резервной копии.
    /// </summary>
    public bool RestoredSuccessfully { get; init; }

    /// <summary>
    /// Информационное сообщение о результате проверки/восстановления.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    public static DatabaseRecoveryResult Healthy(string message = "База данных целостна") =>
        new() { WasCorrupted = false, RestoredSuccessfully = false, Message = message };

    public static DatabaseRecoveryResult Restored(string message = "Данные восстановлены из резервной копии") =>
        new() { WasCorrupted = true, RestoredSuccessfully = true, Message = message };

    public static DatabaseRecoveryResult CorruptedNotRestored(string message) =>
        new() { WasCorrupted = true, RestoredSuccessfully = false, Message = message };
}

/// <summary>
/// Сервис проверки целостности SQLite (PRAGMA quick_check) и автоматического самовосстановления из бэкапа.
/// </summary>
public interface IDatabaseRecoveryService
{
    /// <summary>
    /// Проверяет базу данных на наличие повреждений и при необходимости восстанавливает её из последней резервной копии.
    /// </summary>
    Task<DatabaseRecoveryResult> CheckAndRecoverAsync(CancellationToken cancellationToken = default);
}
