using System.Globalization;
using System.Text.RegularExpressions;
using SelectCast.Core.Conversion;

namespace SelectCast.Core.Converters;

/// <summary>A target time zone: a fixed offset (e.g. Astana +5) or a DST-aware <see cref="TimeZoneInfo"/>.</summary>
public sealed record TimeZoneTarget(string Name, TimeSpan? FixedOffset = null, TimeZoneInfo? Zone = null);

/// <summary>
/// Parses times like <c>3 PM EST</c>, <c>15:00 UTC</c>, <c>9am PST</c>, <c>20:00 GMT+3</c> and
/// shows them in target zones (default Astana +5, Moscow +3, UTC). DST-aware targets go through
/// <see cref="TimeZoneInfo"/>; abbreviation offsets (EST/EDT…) encode DST in the abbreviation itself.
/// Date defaults to today.
/// </summary>
public sealed partial class TimeConverter : IValueConverter
{
    public ValueKind Type => ValueKind.Time;

    private static readonly IReadOnlyList<TimeZoneTarget> DefaultTargets = new[]
    {
        new TimeZoneTarget("Астана", TimeSpan.FromHours(5)),
        new TimeZoneTarget("Москва", TimeSpan.FromHours(3)),
        new TimeZoneTarget("UTC", TimeSpan.Zero),
    };

    // Fixed offsets (minutes) for common zone abbreviations. DST is encoded by the abbreviation
    // itself (EST = standard −5, EDT = daylight −4), so no date-based resolution is needed here.
    private static readonly Dictionary<string, int> Abbreviations = new()
    {
        ["utc"] = 0,
        ["gmt"] = 0,
        ["z"] = 0,
        ["est"] = -300,
        ["edt"] = -240,
        ["cst"] = -360,
        ["cdt"] = -300,
        ["mst"] = -420,
        ["mdt"] = -360,
        ["pst"] = -480,
        ["pdt"] = -420,
        ["akst"] = -540,
        ["akdt"] = -480,
        ["hst"] = -600,
        ["bst"] = 60,
        ["cet"] = 60,
        ["cest"] = 120,
        ["eet"] = 120,
        ["eest"] = 180,
        ["msk"] = 180,
    };

    private readonly IReadOnlyList<TimeZoneTarget> _targets;
    private readonly Func<DateTime> _clock;

    public TimeConverter(IReadOnlyList<TimeZoneTarget>? targets = null, Func<DateTime>? clock = null)
    {
        _targets = targets ?? DefaultTargets;
        _clock = clock ?? (() => DateTime.Now);
    }

    public ConversionResult? TryConvert(string input)
    {
        Match m = TimeRegex().Match(input.Trim().ToLowerInvariant());
        if (!m.Success)
            return null;

        bool hasMinutes = m.Groups[2].Success;
        bool hasMeridiem = m.Groups[3].Success;
        string zone = m.Groups[4].Value.Trim();

        // Require a clear time signal so bare integers are never treated as times.
        if (!hasMinutes && !hasMeridiem && zone.Length == 0)
            return null;

        DateTime now = _clock();
        if (!TryOffset(zone, now, out TimeSpan sourceOffset))
            return null;

        int hour = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        int minute = hasMinutes ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;

        if (hasMeridiem)
        {
            if (hour is < 1 or > 12 || minute > 59)
                return null;
            bool pm = m.Groups[3].Value == "pm";
            hour = pm ? (hour == 12 ? 12 : hour + 12) : (hour == 12 ? 0 : hour);
        }
        else if (hour > 23 || minute > 59)
        {
            return null;
        }

        DateTime date = now.Date;
        var source = new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, sourceOffset);

        var lines = new List<ResultLine>();
        foreach (TimeZoneTarget t in _targets)
        {
            DateTimeOffset converted = t.Zone is not null
                ? TimeZoneInfo.ConvertTime(source, t.Zone)
                : source.ToOffset(t.FixedOffset ?? TimeSpan.Zero);

            string dayShift = converted.Date == source.Date
                ? string.Empty
                : converted.Date > source.Date ? " (+1)" : " (−1)";

            lines.Add(new ResultLine(t.Name, converted.ToString("HH:mm", CultureInfo.InvariantCulture) + dayShift));
        }

        return new ConversionResult(ValueKind.Time, "Время", lines);
    }

    private static bool TryOffset(string zone, DateTime now, out TimeSpan offset)
    {
        if (zone.Length == 0)
        {
            // No zone stated (e.g. "15:00", "3 PM"): assume the machine's local zone.
            offset = TimeZoneInfo.Local.GetUtcOffset(now);
            return true;
        }

        Match m = OffsetRegex().Match(zone);
        if (m.Success)
        {
            offset = m.Groups[1].Success
                ? TimeSpan.FromHours(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                : TimeSpan.Zero;
            return true;
        }

        if (Abbreviations.TryGetValue(zone, out int minutes))
        {
            offset = TimeSpan.FromMinutes(minutes);
            return true;
        }

        offset = TimeSpan.Zero;
        return false;
    }

    [GeneratedRegex(@"^(\d{1,2})(?::(\d{2}))?\s*(am|pm)?\s*([a-z+\-0-9]*)$")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"^(?:utc|gmt|z)([+-]\d{1,2})?$")]
    private static partial Regex OffsetRegex();
}
