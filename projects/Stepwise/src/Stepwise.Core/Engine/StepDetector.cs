using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.Core.Engine;

/// <summary>
/// Детектор и генератор шагов интерактивной инструкции на основе скоррелированных семантических действий,
/// информации об элементах UI Automation и решений политики записи.
/// Чистая доменная логика (.NET 9).
/// </summary>
public sealed class StepDetector : IStepDetector
{
    /// <inheritdoc />
    public Step? DetectStep(
        SemanticAction action,
        ElementInfo target,
        RecordingPolicyDecision policyDecision,
        int sequenceIndex)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(target);

        // Если политика записи подавляет действие, шаг инструкции не создается
        if (policyDecision == RecordingPolicyDecision.Suppress)
        {
            return null;
        }

        var actionType = action.ToStepActionType();
        var (title, description) = GenerateTitleAndDescription(action, target, policyDecision);

        var metadata = new Dictionary<string, string>
        {
            ["ProcessName"] = target.ProcessName ?? string.Empty,
            ["ProcessId"] = target.ProcessId.ToString(),
            ["WindowTitle"] = target.WindowTitle ?? string.Empty,
            ["ControlType"] = target.ControlType ?? string.Empty,
            ["AutomationId"] = target.AutomationId ?? string.Empty,
            ["ClassName"] = target.ClassName ?? string.Empty
        };

        if (action.ActionType == SemanticActionType.TextInput)
        {
            metadata["StartedAt"] = action.StartedAt.ToString("o");
            metadata["CompletedAt"] = action.CompletedAt.ToString("o");
            metadata["CharacterCount"] = action.CharacterCount.ToString();
        }

        if (policyDecision == RecordingPolicyDecision.Mask)
        {
            metadata["IsMasked"] = "true";
        }

        double clickX = action.X.HasValue
            ? action.X.Value
            : (target.BoundingRectangle.X + target.BoundingRectangle.Width / 2.0);

        double clickY = action.Y.HasValue
            ? action.Y.Value
            : (target.BoundingRectangle.Y + target.BoundingRectangle.Height / 2.0);

        return new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: sequenceIndex,
            Timestamp: action.Timestamp,
            Action: actionType,
            ClickX: clickX,
            ClickY: clickY,
            TargetElement: target,
            ScreenshotPath: null,
            Title: title,
            Description: description,
            Metadata: metadata
        );
    }

    private static (string Title, string Description) GenerateTitleAndDescription(
        SemanticAction action,
        ElementInfo target,
        RecordingPolicyDecision policyDecision)
    {
        bool hasTargetName = !string.IsNullOrWhiteSpace(target.Name);

        switch (action.ActionType)
        {
            case SemanticActionType.LeftClick:
            {
                string title = hasTargetName ? $"Click \"{target.Name}\"" : $"Click {target.ControlType}";
                string description = hasTargetName
                    ? $"Click the {target.Name} ({target.ControlType}) in {target.ProcessName}."
                    : $"Click the {target.ControlType} in {target.ProcessName}.";
                return (title, description);
            }

            case SemanticActionType.DoubleLeftClick:
            {
                string title = hasTargetName ? $"Double-click \"{target.Name}\"" : $"Double-click {target.ControlType}";
                string description = hasTargetName
                    ? $"Double-click the {target.Name} ({target.ControlType}) in {target.ProcessName}."
                    : $"Double-click the {target.ControlType} in {target.ProcessName}.";
                return (title, description);
            }

            case SemanticActionType.RightClick:
            {
                string title = hasTargetName ? $"Right-click \"{target.Name}\"" : $"Right-click {target.ControlType}";
                string description = hasTargetName
                    ? $"Right-click the {target.Name} ({target.ControlType}) in {target.ProcessName}."
                    : $"Right-click the {target.ControlType} in {target.ProcessName}.";
                return (title, description);
            }

            case SemanticActionType.MiddleClick:
            {
                string title = hasTargetName ? $"Middle-click \"{target.Name}\"" : $"Middle-click {target.ControlType}";
                string description = hasTargetName
                    ? $"Middle-click the {target.Name} ({target.ControlType}) in {target.ProcessName}."
                    : $"Middle-click the {target.ControlType} in {target.ProcessName}.";
                return (title, description);
            }

            case SemanticActionType.TextInput:
            {
                if (policyDecision == RecordingPolicyDecision.Mask)
                {
                    string title = hasTargetName ? $"Type text into \"{target.Name}\"" : $"Type text into {target.ControlType}";
                    string description = hasTargetName
                        ? $"Type sensitive text into {target.Name}."
                        : $"Type sensitive text into {target.ControlType}.";
                    return (title, description);
                }
                else
                {
                    string title = hasTargetName
                        ? $"Type \"{action.Text}\" into \"{target.Name}\""
                        : $"Type \"{action.Text}\"";
                    string description = hasTargetName
                        ? $"Type \"{action.Text}\" into {target.Name} in {target.ProcessName}."
                        : $"Type \"{action.Text}\" into {target.ControlType} in {target.ProcessName}.";
                    return (title, description);
                }
            }

            case SemanticActionType.Shortcut:
            {
                string shortcut = (action.Modifiers.HasValue && action.Modifiers.Value != KeyboardModifiers.None)
                    ? $"{action.Modifiers.Value}+{action.KeyName}"
                    : action.KeyName ?? string.Empty;
                string title = $"Press {shortcut}";
                string description = $"Press keyboard shortcut {shortcut} in {target.ProcessName}.";
                return (title, description);
            }

            case SemanticActionType.KeyPress:
            {
                string title = $"Press {action.KeyName}";
                string description = $"Press {action.KeyName} key in {target.ProcessName}.";
                return (title, description);
            }

            default:
            {
                string title = hasTargetName ? $"Interact with \"{target.Name}\"" : $"Interact with {target.ControlType}";
                string description = hasTargetName
                    ? $"Interact with {target.Name} ({target.ControlType}) in {target.ProcessName}."
                    : $"Interact with {target.ControlType} in {target.ProcessName}.";
                return (title, description);
            }
        }
    }
}
