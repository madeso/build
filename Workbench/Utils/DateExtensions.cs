namespace Workbench.Utils;

internal static class DateExtensions
{
    internal static string GetTimeAgoString(this DateTime d)
    {
        return GetTimeAgoString(DateTime.Now.Subtract(d));
    }

    internal static string GetTimeAgoString(this TimeSpan diff)
    {
        const int oneMinute = 60;
        const int oneHour = 60 * oneMinute;
        const int daysInMonth = 30;
        const int daysInYear = 365;

        var dayDiff = (int)diff.TotalDays;
        
        switch (dayDiff)
        {
            case 0:
            {
                var secDiff = (int)diff.TotalSeconds;

                switch (secDiff)
                {
                    case < oneMinute:
                        return "just now";
                    case < oneMinute * 2:
                        return "1 minute ago";
                    case < oneHour:
                    {
                        var minutes = Math.Floor((double)secDiff / oneMinute);
                        return $"{minutes} minutes ago";
                    }
                    case < oneHour * 2:
                        return "1 hour ago";
                    default:
                    {
                        var hours = Math.Floor((double)secDiff / oneHour);
                        return $"{hours} hours ago";
                    }
                }
            }
            case 1:
                return "yesterday";
            case < 7:
                return $"{dayDiff} days ago";
            case < 31:
            {
                var weeks = Math.Ceiling((double)dayDiff / 7);
                return $"{weeks} weeks ago";
            }
            default:
            {
                if (diff < TimeSpan.FromDays(daysInYear))
                {
                    var months = diff.Days / daysInMonth;
            
                    if(months == 12)
                    {
                        // hack: algorithm prints about 12 months ago and I find that irritating...
                        return "about a year ago";
                    }

                    return diff.Days > daysInMonth
                            ? $"about {months} months ago"
                            : "about a month ago"
                        ;
                }

                var years = diff.Days / daysInYear;
                return years > 1
                        ? $"about {years} years ago"
                        : "about a year ago"
                    ;
            }
        }
    }
}
