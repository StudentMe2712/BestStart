using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.Core.Policy;

/// <summary>
/// Политика записи по умолчанию:
/// 1. Исключение нежелательных процессов (черный список процессов без учета регистра).
/// 2. Защита конфиденциальных данных и паролей (подавление текстового ввода в поля паролей и чувствительные контролы).
/// 3. Разрешение записи остальных стандартных взаимодействий.
/// </summary>
public sealed class DefaultRecordingPolicy : IRecordingPolicy
{
    private readonly HashSet<string> _excludedProcesses;
    private readonly object _syncLock = new();

    /// <summary>
    /// Флаг разрешения маскирования вместо полного подавления для чувствительного ввода (по умолчанию false -> Suppress).
    /// </summary>
    public bool MaskSensitiveInputs { get; set; }

    /// <summary>
    /// Создает экземпляр <see cref="DefaultRecordingPolicy"/>.
    /// </summary>
    /// <param name="excludedProcesses">Опциональный начальный набор исключаемых процессов (регистронезависимый).</param>
    /// <param name="maskSensitiveInputs">Флаг использования маскирования вместо подавления для чувствительного ввода.</param>
    public DefaultRecordingPolicy(
        IEnumerable<string>? excludedProcesses = null,
        bool maskSensitiveInputs = false)
    {
        _excludedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MaskSensitiveInputs = maskSensitiveInputs;

        if (excludedProcesses != null)
        {
            foreach (var process in excludedProcesses)
            {
                if (!string.IsNullOrWhiteSpace(process))
                {
                    _excludedProcesses.Add(process.Trim());
                }
            }
        }
    }

    /// <summary>
    /// Неизменяемый снимок текущего набора исключенных процессов.
    /// </summary>
    public IReadOnlySet<string> ExcludedProcesses
    {
        get
        {
            lock (_syncLock)
            {
                return new HashSet<string>(_excludedProcesses, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Добавляет процесс в список исключений.
    /// </summary>
    /// <param name="processName">Имя исполняемого файла или процесса (например, "devenv" или "devenv.exe").</param>
    public void AddExcludedProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        lock (_syncLock)
        {
            _excludedProcesses.Add(processName.Trim());
        }
    }

    /// <summary>
    /// Удаляет процесс из списка исключений.
    /// </summary>
    /// <param name="processName">Имя процесса.</param>
    /// <returns><c>true</c>, если процесс присутствовал и был успешно удален; иначе <c>false</c>.</returns>
    public bool RemoveExcludedProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        lock (_syncLock)
        {
            return _excludedProcesses.Remove(processName.Trim());
        }
    }

    /// <inheritdoc />
    public RecordingPolicyDecision Evaluate(SemanticAction action, ElementInfo target)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(target);

        // 1. Проверка исключения процессов
        if (IsProcessExcluded(target.ProcessName) || IsProcessExcluded(action.Context?.ProcessName))
        {
            return RecordingPolicyDecision.Suppress;
        }

        // 2. Проверка паролей и чувствительных данных
        if (target.IsPassword || action.IsSensitive)
        {
            if (action.ActionType == SemanticActionType.TextInput)
            {
                // По требованию 22: по умолчанию предпочтителен Suppress для TextInput
                return MaskSensitiveInputs ? RecordingPolicyDecision.Mask : RecordingPolicyDecision.Suppress;
            }

            // Для остальных действий над полями паролей (например, клик/фокусировка) - маскирование
            return RecordingPolicyDecision.Mask;
        }

        // 3. Стандартное разрешенное действие
        return RecordingPolicyDecision.Allow;
    }

    private bool IsProcessExcluded(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        string trimmed = processName.Trim();

        lock (_syncLock)
        {
            if (_excludedProcesses.Contains(trimmed))
            {
                return true;
            }

            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var withoutExt = trimmed[..^4];
                if (_excludedProcesses.Contains(withoutExt))
                {
                    return true;
                }
            }
            else
            {
                var withExt = trimmed + ".exe";
                if (_excludedProcesses.Contains(withExt))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
