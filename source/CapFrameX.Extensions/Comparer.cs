using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class AlphanumericComparer : IComparer<string>
{
    // Singleton instance for reuse
    public static readonly AlphanumericComparer Instance = new AlphanumericComparer();

    private AlphanumericComparer() { }

    public int Compare(string x, string y)
    {
        if (x == null || y == null) return 0;

        // Use regex to split strings into parts (numbers and text)
        var regex = new Regex(@"(\d+|\D+)");
        var xParts = regex.Matches(x);
        var yParts = regex.Matches(y);

        int i = 0;
        while (i < xParts.Count && i < yParts.Count)
        {
            var xPart = xParts[i].Value;
            var yPart = yParts[i].Value;

            // Compare numeric parts as integers
            if (int.TryParse(xPart, out int xNum) && int.TryParse(yPart, out int yNum))
            {
                int comparison = xNum.CompareTo(yNum);
                if (comparison != 0) return comparison;
            }
            else
            {
                // Compare non-numeric parts lexicographically
                int comparison = string.Compare(xPart, yPart, StringComparison.Ordinal);
                if (comparison != 0) return comparison;
            }
            i++;
        }

        // If one string has more parts, it's considered greater
        return xParts.Count.CompareTo(yParts.Count);
    }
}
