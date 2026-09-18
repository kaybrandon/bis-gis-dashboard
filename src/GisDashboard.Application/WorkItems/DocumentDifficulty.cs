namespace GisDashboard.Application.WorkItems;

public sealed record DocumentDifficulty(
    string? Band,
    string? Why,
    IReadOnlyList<string> Reasons,
    bool Overridden,
    string? AiBand = null,
    bool KeptOverride = false)
{
    public static DocumentDifficulty? FromStored(
        string? band,
        string? why,
        bool overridden,
        string? aiBand = null)
    {
        if (string.IsNullOrWhiteSpace(band) && string.IsNullOrWhiteSpace(aiBand))
        {
            return null;
        }

        var reasons = SplitReasons(why);
        return new DocumentDifficulty(
            string.IsNullOrWhiteSpace(band) ? null : band.Trim(),
            string.IsNullOrWhiteSpace(why) ? null : why.Trim(),
            reasons,
            overridden,
            string.IsNullOrWhiteSpace(aiBand) ? null : aiBand.Trim());
    }

    public static IReadOnlyList<string> SplitReasons(string? why)
    {
        if (string.IsNullOrWhiteSpace(why))
        {
            return [];
        }

        return why
            .Split(['\n', '\r', '•', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', '*', ' ').Trim())
            .Where(line => line.Length > 0)
            .Take(3)
            .ToList();
    }

    public static string JoinReasons(IReadOnlyList<string> reasons)
    {
        return string.Join('\n', reasons.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Take(3));
    }
}
