namespace SecondSelf.Core.Messages;

/// <summary>
/// Сообщение о подтверждении и зачёте ежедневного ритуала Daily Reset.
/// </summary>
public sealed record DailyRitualCountedMessage(
    string LogicalDate,
    string VideoId,
    string CompletedAtIso);
