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
}
