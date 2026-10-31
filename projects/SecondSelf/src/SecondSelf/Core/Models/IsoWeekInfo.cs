using System;

namespace SecondSelf.Core.Models;

/// <summary>
/// Информация о календарной неделе по стандарту ISO-8601.
/// Принадлежность недели к месяцу определяется по четвергу.
/// </summary>
public sealed record IsoWeekInfo
{
    public required int IsoYear { get; init; }
    public required int IsoWeek { get; init; }
    public required DateOnly ThursdayDate { get; init; }
    public required DateOnly StartDateMonday { get; init; }
    public required DateOnly EndDateSunday { get; init; }
}
