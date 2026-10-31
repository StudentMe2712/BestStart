using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SecondSelf.Data;

/// <summary>
/// Фабрика соединений с локальной базой данных SQLite в режиме WAL.
/// </summary>
public interface ISqliteConnectionFactory
{
    /// <summary>
    /// Путь к файлу базы данных SQLite.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Синхронно создаёт и открывает подключение к SQLite с применением обязательных PRAGMA.
    /// </summary>
    SqliteConnection OpenConnection();

    /// <summary>
    /// Асинхронно создаёт и открывает подключение к SQLite с применением обязательных PRAGMA.
    /// </summary>
    Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
