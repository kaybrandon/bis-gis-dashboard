namespace GisDashboard.Domain;

public static class DeedPlatFields
{
    public const int ShortMaxLength = 500;
    public const int LegalDescriptionMaxLength = 16000;

    public const string Survey = "Survey";
    public const string Abstract = "Abstract";
    public const string LotBlock = "Lot/Block";
    public const string Subdivision = "Subdivision";
    public const string LegalDescription = "Legal Description";

    public static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    public static string? NormalizeShort(string? value) => Normalize(value, ShortMaxLength);

    public static string? NormalizeLegal(string? value) => Normalize(value, LegalDescriptionMaxLength);

    public static bool TryApplyAi(bool manual, string? current, bool present, string? suggested, int maxLength, out string? next)
    {
        next = current;
        if (manual)
        {
            return false;
        }

        if (!present || string.IsNullOrWhiteSpace(suggested))
        {
            return false;
        }

        next = Normalize(suggested, maxLength);
        return !string.Equals(current ?? string.Empty, next ?? string.Empty, StringComparison.Ordinal);
    }
}
