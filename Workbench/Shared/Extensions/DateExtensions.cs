using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;

namespace Workbench.Shared.Extensions;

internal static class DateExtensions
{
    internal static string GetTimeAgoString(this DateTime d)
    {
        return DateTime.Now.Subtract(d).GetTimeAgoString();
    }

    internal static string GetTimeAgoString(this TimeSpan diff)
    {
        const int ONE_MINUTE = 60;
        const int ONE_HOUR = 60 * ONE_MINUTE;
        const int DAYS_IN_MONTH = 30;
        const int DAYS_IN_YEAR = 365;

        var day_diff = (int)diff.TotalDays;

        switch (day_diff)
        {
            case 0:
                {
                    var sec_diff = (int)diff.TotalSeconds;

                    switch (sec_diff)
                    {
                        case < ONE_MINUTE:
                            return "just now";
                        case < ONE_MINUTE * 2:
                            return "1 minute ago";
                        case < ONE_HOUR:
                            {
                                var minutes = Math.Floor((double)sec_diff / ONE_MINUTE);
                                return $"{minutes} minutes ago";
                            }
                        case < ONE_HOUR * 2:
                            return "1 hour ago";
                        default:
                            {
                                var hours = Math.Floor((double)sec_diff / ONE_HOUR);
                                return $"{hours} hours ago";
                            }
                    }
                }
            case 1:
                return "yesterday";
            case < 7:
                return $"{day_diff} days ago";
            case < 31:
                {
                    var weeks = Math.Ceiling((double)day_diff / 7);
                    return $"{weeks} weeks ago";
                }
            default:
                {
                    if (diff < TimeSpan.FromDays(DAYS_IN_YEAR))
                    {
                        var months = diff.Days / DAYS_IN_MONTH;

                        if (months == 12)
                        {
                            // hack: algorithm prints about 12 months ago and I find that irritating...
                            return "about a year ago";
                        }

                        return diff.Days > DAYS_IN_MONTH
                                ? $"about {months} months ago"
                                : "about a month ago"
                            ;
                    }

                    var years = diff.Days / DAYS_IN_YEAR;
                    return years > 1
                            ? $"about {years} years ago"
                            : "about a year ago"
                        ;
                }
        }
    }


    public static string ToHumanString(this TimeSpan span)
    {
        var r = new List<string>();

        if (span.Days != 0)
        {
            r.Add($"{span.Days} days");
        }

        if (span.Hours != 0)
        {
            r.Add($"{span.Hours} hours");
        }

        if (span.Minutes != 0)
        {
            r.Add($"{span.Minutes} minutes");
        }

        if (span.Seconds != 0)
        {
            r.Add($"{span.Seconds} seconds");
        }

        return StringListCombiner.EnglishAnd("same time").Combine(r);
    }


    private static DateTime NextDate(this DateTime dt, TimeResolution resolution)
    {
        return resolution switch
        {
            TimeResolution.Year => dt.AddMonths(12),
            TimeResolution.Month => dt.AddMonths(1),
            TimeResolution.Week => dt.AddDays(7),
            TimeResolution.Day => dt.AddDays(1),
            TimeResolution.Hour => dt.AddHours(1),
            TimeResolution.Minute => dt.AddMinutes(1),
            TimeResolution.Second => dt.AddSeconds(1),
            _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
        };
    }



    private static DateTime MoveToStart(DateTime current, TimeResolution resolution)
    {
        return resolution switch
        {
            TimeResolution.Year => DateTest.ToUniqueDateYear(current),
            TimeResolution.Month => DateTest.ToUniqueDateMonth(current),
            TimeResolution.Week => DateTest.ToUniqueDateWeek(current),
            TimeResolution.Day => DateTest.ToUniqueDateDay(current),
            TimeResolution.Hour => DateTest.ToUniqueDateHour(current),
            TimeResolution.Minute => DateTest.ToUniqueDateMinute(current),
            TimeResolution.Second => DateTest.ToUniqueDateSecond(current),
            _ => throw new ArgumentOutOfRangeException(resolution + " was not handled")
        };
    }



    private static string GetDateStringFormat(TimeResolution resolution)
    {
        const string BASE_DATE = "yyyy-MM-dd";
        return resolution switch
        {
            TimeResolution.Year => "yyyy",
            TimeResolution.Month => "MMM yy",
            TimeResolution.Week => BASE_DATE,
            TimeResolution.Day => BASE_DATE,
            TimeResolution.Hour => BASE_DATE + " HH",
            TimeResolution.Minute => BASE_DATE + " HH:mm",
            TimeResolution.Second => BASE_DATE + " HH::mm::ss",
            _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
        };
    }

    public static string ToString(this DateTime dt, TimeResolution res)
    {
        return dt.ToString(GetDateStringFormat(res));
    }

    public static IEnumerable<TValue> GroupOnTime<TIn, TValue>(
        this IEnumerable<TIn> entry_list,
        Func<TIn, DateTime> time,
        TimeResolution res,
        Func<DateTime, List<TIn>, TValue> to_value)
    {
        var entries = entry_list
            .Select(e => new { Time = time(e), Value = e })
            .OrderBy(e => e.Time)
            .ToImmutableArray();

        List<TIn> counts = new();
        DateTime? first = null;
        foreach (var e in entries)
        {
            if (first.HasValue == false)
            {
                first = e.Time;
            }
            else
            {
                if(DateTest.IsSame(res, first.Value, e.Time))
                {
                }
                else
                {
                    yield return to_value(first.Value, counts);
                    first = e.Time;
                    counts.Clear();
                }
            }
            counts.Add(e.Value);
        }

        if (counts.Count > 0)
        {
            Debug.Assert(first.HasValue);
            yield return to_value(first.Value, counts);
        }
    }

}


public enum TimeResolution
{
    [EnumString("month")]
    Year,

    [EnumString("month")]
    Month,

    [EnumString("week")]
    Week,

    [EnumString("day")]
    Day,

    [EnumString("hour")]
    Hour,

    [EnumString("minute")]
    Minute,

    [EnumString("second")]
    Second
}



internal static class DateTest
{
    public static DateTime StartOfDay(DateTime d) => new(d.Year, d.Month, d.Day, 0, 0, 0);
    public static DateTime EndOfDay(DateTime d) => new(d.Year, d.Month, d.Day, 23, 59, 59);

    private static bool IsSameYear(DateTime lhs, DateTime rhs) => lhs.Year == rhs.Year;

    public static bool IsSameMonth(DateTime lhs, DateTime rhs) => IsSameYear(lhs, rhs) && lhs.Month == rhs.Month;

    public static bool IsSameDay(DateTime lhs, DateTime rhs) => IsSameMonth(lhs, rhs) && lhs.Day == rhs.Day;

    public static bool IsSameHour(DateTime lhs, DateTime rhs) => IsSameDay(lhs, rhs) && lhs.Hour == rhs.Hour;

    public static bool IsSameMinute(DateTime lhs, DateTime rhs) => IsSameHour(lhs, rhs) && lhs.Minute == rhs.Minute;

    public static bool IsSameSecond(DateTime lhs, DateTime rhs) => IsSameMinute(lhs, rhs) && lhs.Second == rhs.Second;

    

    public static bool IsSameWeek(DateTime lhs, DateTime rhs)
    {
        Debug.Assert(DateTimeFormatInfo.CurrentInfo != null, "DateTimeFormatInfo.CurrentInfo != null");
        var cal = DateTimeFormatInfo.CurrentInfo.Calendar;
        var rule = CalendarWeekRule.FirstFullWeek;
        var start = DayOfWeek.Monday;
        return cal.GetWeekOfYear(lhs, rule, start) == cal.GetWeekOfYear(rhs, rule, start);
    }

    public static bool IsSame(TimeResolution res, DateTime lhs, DateTime rhs)
        => res switch
        {
            TimeResolution.Year => IsSameYear(lhs, rhs),
            TimeResolution.Month => IsSameMonth(lhs, rhs),
            TimeResolution.Week => IsSameWeek(lhs, rhs),
            TimeResolution.Day => IsSameDay(lhs, rhs),
            TimeResolution.Hour => IsSameHour(lhs, rhs),
            TimeResolution.Minute => IsSameMinute(lhs, rhs),
            TimeResolution.Second => IsSameSecond(lhs, rhs),
            _ => throw new ArgumentOutOfRangeException(nameof(res), res, null)
        };


    public static DateTime ToUniqueDateYear(DateTime c) => new(c.Year, 1, 1, 0, 0, 0, 0);
    public static DateTime ToUniqueDateMonth(DateTime c) => new(c.Year, c.Month, 1, 0, 0, 0, 0);
    public static DateTime ToUniqueDateWeek(DateTime c)
    {
        var change = get_until_monday_this_week(c.DayOfWeek);
        var nd = c.AddDays(change);
        Debug.Assert(nd.DayOfWeek == DayOfWeek.Monday);
        return ToUniqueDateDay(nd);

        static int get_until_monday_this_week(DayOfWeek day_of_week)
        {
            return day_of_week switch
            {
                DayOfWeek.Sunday => -6,
                DayOfWeek.Monday => 0,
                DayOfWeek.Tuesday => -1,
                DayOfWeek.Wednesday => -2,
                DayOfWeek.Thursday => -3,
                DayOfWeek.Friday => -4,
                DayOfWeek.Saturday => -5,
                _ => throw new ArgumentOutOfRangeException(nameof(day_of_week), day_of_week, null)
            };
        }
    }
    public static DateTime ToUniqueDateDay(DateTime c) => new(c.Year, c.Month, c.Day, 0, 0, 0, 0);
    public static DateTime ToUniqueDateHour(DateTime c) => new(c.Year, c.Month, c.Day, c.Hour, 0, 0, 0);
    public static DateTime ToUniqueDateMinute(DateTime c) => new(c.Year, c.Month, c.Day, c.Hour, c.Minute, 0, 0);
    public static DateTime ToUniqueDateSecond(DateTime c) => new(c.Year, c.Month, c.Day, c.Hour, c.Minute, c.Second, 0);
}

