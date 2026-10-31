namespace SecondSelf.Core.Messages;

/// <summary>
/// Сообщение о переходе суточного рубежа (04:00 утра) и смене логической даты.
/// </summary>
public sealed record DayChangedMessage(
    string PreviousLogicalDate,
    string NewLogicalDate);
