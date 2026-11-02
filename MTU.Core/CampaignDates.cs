namespace MTU.Core;

public static class CampaignDates
{
    public static long AbsoluteDay(int year, int day) => ((long)year - 1) * 365 + day - 1;
    public static string Format(long absoluteDay)
    {
        var year = Math.DivRem(absoluteDay, 365, out var remainder);
        if (remainder < 0) { year--; remainder += 365; }
        return $"{remainder + 1}-{year + 1}";
    }
    public static long Today(Campaign campaign) => AbsoluteDay(campaign.Year, campaign.Day);
    public static long RemoteTarget(long today, int distance, Func<int> d6) => today - (distance > 3 ? 13 : 6) - d6();
    public static (string Text, string Style) Status(long? target, Campaign? campaign)
    {
        if (campaign is null) return ("Select campaign", "unknown");
        if (target is null) return ("Date unknown", "unknown");
        var days = Today(campaign) - target.Value;
        if (days <= 0) return ("Current", "current");
        return days < 7 ? ($"Outdated {days} days", "outdated") : ($"Expired {days} days", "expired");
    }
}
