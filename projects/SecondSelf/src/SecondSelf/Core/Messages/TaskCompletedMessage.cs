namespace SecondSelf.Core.Messages;

/// <summary>
/// Сообщение о выполнении задачи дня или недельной задачи.
/// </summary>
public sealed record TaskCompletedMessage(
    string TaskId,
    string Title,
    string CompletedAtIso,
    string LogicalDate,
    string TaskSource = "Today");
