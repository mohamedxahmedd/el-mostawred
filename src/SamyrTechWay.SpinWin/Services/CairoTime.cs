namespace SamyrTechWay.SpinWin.Services;

/// <summary>All timestamps are stored in UTC and shown in Egypt time.</summary>
public static class CairoTime
{
    private static readonly TimeZoneInfo Zone = Resolve();

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Africa/Cairo", "Egypt Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        }
        return TimeZoneInfo.Utc;
    }

    public static DateTime ToCairo(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static string Format(DateTime utc, string format = "dd MMM yyyy, HH:mm") =>
        ToCairo(utc).ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Start of the Cairo-local day containing <paramref name="utc"/>, expressed in UTC.</summary>
    public static DateTime StartOfCairoDayUtc(DateTime utc)
    {
        var local = ToCairo(utc).Date;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
    }

    /// <summary>Converts a Cairo-local calendar date to the UTC instant at its midnight.</summary>
    public static DateTime CairoDateToUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone);
}
