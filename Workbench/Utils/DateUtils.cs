using System;

namespace Workbench.Utils;

internal static class DateUtils
{
    internal static string TimeAgo(DateTime d)
    {
        const int ONE_MINUTE = 60;
        const int ONE_HOUR = 60 * ONE_MINUTE;
        const int DAYS_IN_MONTH = 30;
        const int DAYS_IN_YEAR = 365;

        TimeSpan s = DateTime.Now.Subtract(d);
        int dayDiff = (int)s.TotalDays;
        int secDiff = (int)s.TotalSeconds;

        if (dayDiff == 0)
        {
            if (secDiff < ONE_MINUTE) { return "just now"; }
            else if (secDiff < ONE_MINUTE * 2) { return "1 minute ago"; }
            else if (secDiff < ONE_HOUR)
            {
                var minutes = Math.Floor((double)secDiff / ONE_MINUTE);
                return $"{minutes} minutes ago";
            }
            else if (secDiff < ONE_HOUR * 2) { return "1 hour ago"; }
            else
            {
                var hours = Math.Floor((double)secDiff / ONE_HOUR);
                return $"{hours} hours ago";
            }
        }
        
        else if (dayDiff == 1) { return "yesterday"; }
        else if (dayDiff < 7) { return $"{dayDiff} days ago"; }
        else if (dayDiff < 31)
        {
            var weeks = Math.Ceiling((double)dayDiff / 7);
            return $"{weeks} weeks ago";
        }
        else if (s < TimeSpan.FromDays(DAYS_IN_YEAR))
        {
            var months = s.Days / DAYS_IN_MONTH;
            
            if(months == 12)
            {
                // hack: algorithm prints about 12 months ago and I find that irritating...
                return "about a year ago";
            }

            return s.Days > DAYS_IN_MONTH
                ? $"about {months} months ago"
                : "about a month ago"
                ;
        }
        else
        {
            var years = s.Days / DAYS_IN_YEAR;
            return years > 1
                ? $"about {years} years ago"
                : "about a year ago"
                ;
        }
    }
}
