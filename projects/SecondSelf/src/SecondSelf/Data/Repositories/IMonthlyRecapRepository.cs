using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SecondSelf.Data.Entities;

namespace SecondSelf.Data.Repositories;

/// <summary>
/// Репозиторий ежемесячных отчётов и итоговой рефлексии (Monthly Recap).
/// </summary>
public interface IMonthlyRecapRepository
{
    /// <summary>
    /// Получает ежемесячный отчёт по ключу месяца ('YYYY-MM').
    /// </summary>
    Task<MonthlyRecap?> GetRecapAsync(string yearMonth, CancellationToken ct = default);

    /// <summary>
    /// Получает список всех сохранённых ежемесячных отчётов в обратном хронологическом порядке.
    /// </summary>
    Task<IReadOnlyList<MonthlyRecap>> GetAllRecapsAsync(CancellationToken ct = default);

    /// <summary>
    /// Сохраняет или обновляет ежемесячный отчёт.
    /// </summary>
    Task SaveRecapAsync(MonthlyRecap recap, CancellationToken ct = default);

    /// <summary>
    /// Финализирует ежемесячный отчёт (сохраняет текст рефлексии, ставит IsFinalized = true, фиксирует CompletedAt = UtcNow).
    /// </summary>
    Task FinalizeRecapAsync(string yearMonth, string reflectionText, CancellationToken ct = default);
}
