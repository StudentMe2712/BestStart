using Stepwise.Core.Models;

namespace Stepwise.Core.Interfaces;

/// <summary>
/// Детектор и генератор шагов инструкции на основе семантического действия, целевого элемента и решения политики.
/// </summary>
public interface IStepDetector
{
    /// <summary>
    /// Формирует шаг интерактивной инструкции.
    /// Возвращает <c>null</c>, если шаг не должен быть создан (например, при решении <see cref="RecordingPolicyDecision.Suppress"/>).
    /// </summary>
    /// <param name="action">Семантическое действие пользователя.</param>
    /// <param name="target">Целевой UI-элемент взаимодействия.</param>
    /// <param name="policyDecision">Решение политики записи.</param>
    /// <param name="sequenceIndex">Порядковый номер формируемого шага.</param>
    /// <returns>Экземпляр <see cref="Step"/> или <c>null</c>, если шаг подавлен.</returns>
    Step? DetectStep(SemanticAction action, ElementInfo target, RecordingPolicyDecision policyDecision, int sequenceIndex);
}
