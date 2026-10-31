using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SecondSelf.Core.ViewModels;
using SecondSelf.Data;
using SecondSelf.Data.Entities;
using SecondSelf.Data.Migrations;
using SecondSelf.Data.Repositories;
using Xunit;

namespace SecondSelf.Tests;

/// <summary>
/// BDD-тесты приёмочного критерия AC-10: Блокировка Weekly Goal (Commitment Lock).
/// 
/// Given: началась календарная неделя (понедельник после 04:00). Пользователь создал цель недели «Завершить архитектуру БД».
/// When: прошло 2 часа с момента создания или пользователь нажал «Зафиксировать на неделю».
/// Then:
///   1. Текстовое поле цели переходит в режим чтения (прямое редактирование текста заблокировано).
///   2. Доступны только две явные операции: «Перенести на след. неделю» или «Отменить цель» с обязательным выбором причины.
///   3. При отмене неделя получает статус Cancelled (0 в числитель месяца) с фиксацией в истории.
///   4. При переносе текущая неделя получает статус Postponed, создаётся цель на следующую неделю.
/// </summary>
public sealed class AC10_WeeklyGoalCommitmentLockTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly WeeklyGoalRepository _weeklyGoalRepository;
    private readonly BigGoalRepository _bigGoalRepository;
    private readonly WeeklyGoalModalViewModel _viewModel;

    public AC10_WeeklyGoalCommitmentLockTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"secondself_ac10_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_testDbPath);
        new DatabaseMigrationService(_connectionFactory).MigrateAsync().GetAwaiter().GetResult();

        _weeklyGoalRepository = new WeeklyGoalRepository(_connectionFactory);
        _bigGoalRepository = new BigGoalRepository(_connectionFactory);

        _viewModel = new WeeklyGoalModalViewModel(_weeklyGoalRepository, _bigGoalRepository);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task AC10_NewGoal_IsInitiallyEditable_BeforeLock()
    {
        // Given: понедельник недели 44 года 2026
        int isoYear = 2026;
        int isoWeek = 44;
        var baseTime = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
        _viewModel.CurrentTimeForTesting = baseTime;

        // When: пользователь открывает окно и задает формулировку цели
        await _viewModel.LoadForWeekAsync(isoYear, isoWeek);
        _viewModel.Title = "Завершить архитектуру БД";

        // Then:
        // Цель доступна для редактирования
        Assert.False(_viewModel.IsLocked);
        Assert.True(_viewModel.CanEdit);
        Assert.Equal("Active", _viewModel.Status);

        // Сохранение черновика
        await _viewModel.SaveDraftAsync();

        var saved = await _weeklyGoalRepository.GetGoalForWeekAsync(isoYear, isoWeek);
        Assert.NotNull(saved);
        Assert.Equal("Завершить архитектуру БД", saved.Title);
        Assert.Equal("Active", saved.Status);
    }

    [Fact]
    public async Task AC10_AfterTwoHours_EditingIsLocked_AndDirectEditThrows()
    {
        // Given: создана цель в 09:00
        int isoYear = 2026;
        int isoWeek = 44;
        var creationTime = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
        _viewModel.CurrentTimeForTesting = creationTime;

        await _viewModel.LoadForWeekAsync(isoYear, isoWeek);
        _viewModel.Title = "Первоначальная сложная цель";
        await _viewModel.SaveDraftAsync();

        // When: прошло 2 часа 5 минут (время 11:05)
        var afterTwoHours = creationTime.AddHours(2).AddMinutes(5);
        _viewModel.CurrentTimeForTesting = afterTwoHours;

        // Повторная загрузка / проверка блокировки обязательства (Commitment Lock)
        await _viewModel.LoadForWeekAsync(isoYear, isoWeek);

        // Then:
        // Текстовое поле переходит в режим чтения, прямое редактирование заблокировано
        Assert.True(_viewModel.IsLocked);
        Assert.False(_viewModel.CanEdit);

        // Попытка тихой подмены формулировки выбрасывает исключение
        _viewModel.Title = "Тихая подмена на простую задачу";
        await Assert.ThrowsAsync<InvalidOperationException>(() => _viewModel.SaveDraftAsync());
    }

    [Fact]
    public async Task AC10_ExplicitSaveAndLock_LocksFormulationImmediately()
    {
        // Given: пользователь создает формулировку цели
        int isoYear = 2026;
        int isoWeek = 44;
        await _viewModel.LoadForWeekAsync(isoYear, isoWeek);
        _viewModel.Title = "Запустить бета-тестирование";

        // When: пользователь нажимает «Зафиксировать на неделю» (Commitment Lock)
        await _viewModel.SaveAndLockAsync();

        // Then:
        // Статус цели = 'Locked', IsLocked = true, CanEdit = false
        Assert.True(_viewModel.IsLocked);
        Assert.False(_viewModel.CanEdit);
        Assert.Equal("Locked", _viewModel.Status);
        Assert.NotNull(_viewModel.LockedAt);

        // Проверяем фиксацию в SQLite
        var inDb = await _weeklyGoalRepository.GetGoalForWeekAsync(isoYear, isoWeek);
        Assert.NotNull(inDb);
        Assert.Equal("Locked", inDb.Status);
        Assert.NotNull(inDb.LockedAt);
    }

    [Fact]
    public async Task AC10_CancelGoal_SetsStatusCancelled_WithReason()
    {
        // Given: зафиксированная цель на неделю
        int isoYear = 2026;
        int isoWeek = 44;
        await _viewModel.LoadForWeekAsync(isoYear, isoWeek);
        _viewModel.Title = "Интеграция с внешним сервисом";
        await _viewModel.SaveAndLockAsync();
        Assert.True(_viewModel.IsLocked);

        // When: пользователь выбирает явное действие «Отменить цель» с указанием причины
        const string reason = "Внешний API временно недоступен";
        await _viewModel.CancelAsync(reason);

        // Then:
        Assert.Equal("Cancelled", _viewModel.Status);
        Assert.Equal(reason, _viewModel.CancellationReason);

        var inDb = await _weeklyGoalRepository.GetGoalForWeekAsync(isoYear, isoWeek);
        Assert.NotNull(inDb);
        Assert.Equal("Cancelled", inDb.Status);
        Assert.Equal(reason, inDb.CancellationReason);
    }

    [Fact]
    public async Task AC10_PostponeGoal_CreatesGoalForNextWeek_WithReason()
    {
        // Given: зафиксированная цель недели 44
        int isoYear = 2026;
        int isoWeek = 44;
        await _viewModel.LoadForWeekAsync(isoYear, isoWeek);
        _viewModel.Title = "Оптимизация производительности рендеринга";
        await _viewModel.SaveAndLockAsync();

        // When: пользователь выбирает действие «Перенести на след. неделю» с указанием причины
        const string reason = "Срочный багфикс релиза сдвинул приоритеты";
        await _viewModel.PostponeAsync(reason);

        // Then:
        // Текущая цель недели 44 переведена в Postponed
        var week44Goal = await _weeklyGoalRepository.GetGoalForWeekAsync(2026, 44);
        Assert.NotNull(week44Goal);
        Assert.Equal("Postponed", week44Goal.Status);
        Assert.Equal(reason, week44Goal.CancellationReason);

        // Автоматически создана цель на неделю 45 со статусом Active
        var week45Goal = await _weeklyGoalRepository.GetGoalForWeekAsync(2026, 45);
        Assert.NotNull(week45Goal);
        Assert.Equal("Оптимизация производительности рендеринга", week45Goal.Title);
        Assert.Equal("Active", week45Goal.Status);
    }
}
