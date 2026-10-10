namespace DIG.App.Services;

/// <summary>Resolves time zones by trying both Windows and IANA identifiers.</summary>
public static class TimeZoneHelper
{
    public static TimeZoneInfo? Find(params string[] ids)
    {
        foreach (var id in ids)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return null;
    }

    public static readonly TimeZoneInfo? Colombo = Find("Sri Lanka Standard Time", "Asia/Colombo");
    public static readonly TimeZoneInfo? Kolkata = Find("India Standard Time", "Asia/Kolkata");

    public static string Format(TimeZoneInfo? tz, DateTimeOffset utcNow)
    {
        if (tz is null) return "--:--";
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        return local.ToString("HH:mm");
    }
}
