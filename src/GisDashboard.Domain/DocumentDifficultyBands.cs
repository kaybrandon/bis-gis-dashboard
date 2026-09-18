namespace GisDashboard.Domain;

public static class DocumentDifficultyBands
{
    public const string Easy = "Easy";
    public const string Medium = "Medium";
    public const string Hard = "Hard";

    public static readonly string[] All = [Easy, Medium, Hard];

    public static bool TryNormalize(string? value, out string band)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            band = string.Empty;
            return false;
        }

        var key = value.Trim();
        if (key.Equals(Easy, StringComparison.OrdinalIgnoreCase)
            || key.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            band = Easy;
            return true;
        }

        if (key.Equals(Medium, StringComparison.OrdinalIgnoreCase)
            || key.Equals("m", StringComparison.OrdinalIgnoreCase)
            || key.Equals("med", StringComparison.OrdinalIgnoreCase))
        {
            band = Medium;
            return true;
        }

        if (key.Equals(Hard, StringComparison.OrdinalIgnoreCase)
            || key.Equals("h", StringComparison.OrdinalIgnoreCase))
        {
            band = Hard;
            return true;
        }

        band = string.Empty;
        return false;
    }

    public static int SortKey(string? band)
    {
        if (!TryNormalize(band, out var normalized))
        {
            return 3;
        }

        return normalized switch
        {
            Easy => 0,
            Medium => 1,
            Hard => 2,
            _ => 3
        };
    }
}
