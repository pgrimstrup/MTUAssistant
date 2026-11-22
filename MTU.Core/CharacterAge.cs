namespace MTU.Core;

public static class CharacterAge
{
    /// <summary>The year careers are counted back from: a character with N terms is
    /// 16 + 4N years old on day 1 of this year.</summary>
    public const int BaseYear = 1105;
    public const int DaysInYear = 365;

    /// <summary>Age on the given campaign date. A career starts at 16 and each term adds four
    /// years, which gives the age on day 1 of 1105. After that the character is a year older on
    /// each birthday (a day of the year, 1 to 365).</summary>
    public static long Calculate(int terms, int birthday, int year, int day = 1)
    {
        var born = Math.Clamp(birthday, 1, DaysInYear);
        var start = CareerRules.StartingAge + (long)CareerRules.YearsPerTerm * Math.Max(0, terms);
        // Birthdays since day 1 of the base year. A day-1 birthday falls on that first day, which
        // the starting age already includes; any later birthday in the current year counts once reached.
        var birthdays = (long)year - BaseYear + (born > 1 && day >= born ? 1 : 0);
        return start + birthdays;
    }
}
