using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Решение политики записи относительно фиксации действия и элемента.
/// </summary>
public enum RecordingPolicyDecision
{
    /// <summary>
    /// Действие разрешено к обычной записи.
    /// </summary>
    Allow,

    /// <summary>
    /// Действие разрешено, но чувствительные данные должны быть маскированы (например, пароли или конфиденциальные поля).
    /// </summary>
    Mask,

    /// <summary>
    /// Действие подавлено (игнорируется, шаг инструкции не создается).
    /// </summary>
    Suppress
}

/// <summary>
/// Политика фильтрации, приватности и безопасности при записи действий пользователя.
/// </summary>
public interface IRecordingPolicy
{
    /// <summary>
    /// Оценивает действие и целевой элемент, возвращая решение о записи (<see cref="RecordingPolicyDecision"/>).
    /// </summary>
    /// <param name="action">Семантическое действие пользователя.</param>
    /// <param name="target">Целевой UI-элемент взаимодействия.</param>
    /// <returns>Решение политики записи.</returns>
    RecordingPolicyDecision Evaluate(SemanticAction action, ElementInfo target);
}
