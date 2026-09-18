namespace GisDashboard.Domain;

/// <summary>
/// CR11 — one approved status vocabulary for selectors, filters, tiles, charts, and exports.
/// Needs Review stays a workflow status (QC06), distinct from Reviewed Yes/No.
/// Chart stages In Progress / Worked / QC'd are not auto-equated; only the approved
/// display mapping is applied (In Progress→Active, Held→On-Hold, Worked→Complete).
/// QC'd is preserved as stored data and is not remapped.
/// </summary>
public static class StatusDisplay
{
    public const string Active = "Active";
    public const string Pending = "Pending";
    public const string Complete = "Complete";
    public const string OnHold = "On-Hold";
    public const string Cancelled = "Cancelled";
    public const string NeedsReview = "Needs Review";

    /// <summary>BA lock v1 order for dropdowns, filters, tiles, charts, and exports.</summary>
    public static readonly string[] CanonicalNames =
    [
        Active,
        Pending,
        Complete,
        OnHold,
        Cancelled,
        NeedsReview
    ];

    public static string Label(string name) => name switch
    {
        "In Progress" => Active,
        "Held" => OnHold,
        "On Hold" => OnHold,
        "Worked" => Complete,
        _ => name
    };

    public static bool IsCanonical(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return CanonicalNames.Contains(Label(name), StringComparer.Ordinal);
    }

    public static int CanonicalOrder(string name)
    {
        var label = Label(name);
        var index = Array.IndexOf(CanonicalNames, label);
        return index >= 0 ? index : CanonicalNames.Length;
    }

    /// <summary>
    /// Stored names (current + approved aliases) that should match a search term
    /// so renaming labels does not hide existing rows.
    /// </summary>
    public static IReadOnlyList<string> NamesMatchingSearch(string term)
    {
        string[] known =
        [
            Active, "In Progress",
            Pending,
            Complete, "Worked",
            OnHold, "Held", "On Hold",
            Cancelled,
            NeedsReview,
            "QC'd"
        ];

        return known
            .Where(name =>
                name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                Label(name).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Label(term), Label(name), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
