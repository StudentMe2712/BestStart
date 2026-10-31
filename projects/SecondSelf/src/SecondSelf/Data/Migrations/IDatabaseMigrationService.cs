using System.Threading;
using System.Threading.Tasks;

namespace SecondSelf.Data.Migrations;

/// <summary>
/// Сервис управления и автоматического наката миграций схемы базы данных.
/// </summary>
public interface IDatabaseMigrationService
{
    /// <summary>
    /// Выполняет поиск и последовательный накат непримененных миграций.
    /// </summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
