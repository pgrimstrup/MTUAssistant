using System.Globalization;

namespace MTU.Core;

public static class TravellerAge
{
    // Each comma-separated service ends in its number of terms; a plain number is also valid.
    public static long? Calculate(string? termsServed, int year)
    {
        if (string.IsNullOrWhiteSpace(termsServed)) return null;
        long total = 0;
        foreach (var part in termsServed.Split(','))
        {
            var entry = part.Trim();
            if (entry.Length == 0) return null;
            var index = entry.Length;
            while (index > 0 && char.IsAsciiDigit(entry[index - 1])) index--;
            if (index == entry.Length || (index > 0 && !char.IsWhiteSpace(entry[index - 1]))
                || !long.TryParse(entry[index..], NumberStyles.None, CultureInfo.InvariantCulture, out var count)) return null;
            try { total = checked(total + count); }
            catch (OverflowException) { return null; }
        }
        try { return checked(16L + 4L * total + year - 1105L); }
        catch (OverflowException) { return null; }
    }
}
