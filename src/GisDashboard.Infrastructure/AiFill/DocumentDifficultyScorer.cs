using GisDashboard.Application.AiFill;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;

namespace GisDashboard.Infrastructure.AiFill;

public sealed record DocumentDifficultyScore(string Band, string Why, IReadOnlyList<string> Reasons);

/// <summary>
/// Scores Easy / Medium / Hard from the same PDF extract + field JSON the
/// AI-fill pass already produced. No extra OCR or Document Intelligence call.
/// </summary>
public static class DocumentDifficultyScorer
{
    public static DocumentDifficultyScore Score(
        string extractedText,
        AiFillFields fields,
        double overallConfidence,
        string? modelBand,
        string? modelWhy,
        IEnumerable<string>? modelReasons)
    {
        var heuristic = FromSignals(Analyze(extractedText, fields, overallConfidence));
        if (DocumentDifficultyBands.TryNormalize(modelBand, out var band))
        {
            var reasons = NormalizeReasons(modelWhy, modelReasons);
            if (reasons.Count == 0)
            {
                reasons = heuristic.Reasons;
            }

            return new DocumentDifficultyScore(band, DocumentDifficulty.JoinReasons(reasons), reasons);
        }

        return heuristic;
    }

    internal static DocumentDifficultySignals Analyze(
        string extractedText,
        AiFillFields fields,
        double overallConfidence)
    {
        var text = extractedText ?? string.Empty;
        var lower = text.ToLowerInvariant();
        var propertyIds = fields.PropertyIds.Present
            ? fields.PropertyIds.Value
            : null;

        return new DocumentDifficultySignals(
            PoorReadability: IsPoorReadability(text),
            MetesAndBounds: LooksLikeMetesAndBounds(lower),
            LotBlock: LooksLikeLotBlock(lower),
            ParcelCount: CountParcels(propertyIds, lower),
            PartyCount: CountParties(lower),
            HasEasementsOrExceptions: HasEasementsOrExceptions(lower),
            GapCount: CountGaps(fields, overallConfidence),
            OverallConfidence: overallConfidence);
    }

    internal static DocumentDifficultyScore FromSignals(DocumentDifficultySignals signals)
    {
        var hardness = 0;
        var reasons = new List<string>();

        if (signals.PoorReadability)
        {
            hardness += 2;
            reasons.Add("Scan text is hard to read.");
        }

        if (signals.MetesAndBounds)
        {
            hardness += 2;
            reasons.Add("Legal is metes-and-bounds, not lot-and-block.");
        }
        else if (signals.LotBlock)
        {
            reasons.Add("Legal looks like lot-and-block.");
        }

        if (signals.ParcelCount >= 4)
        {
            hardness += 2;
            reasons.Add($"{signals.ParcelCount} parcels.");
        }
        else if (signals.ParcelCount >= 2)
        {
            hardness += 1;
            reasons.Add($"{signals.ParcelCount} parcels.");
        }
        else if (signals.ParcelCount == 1)
        {
            reasons.Add("One parcel.");
        }

        if (signals.PartyCount >= 3)
        {
            hardness += 1;
            reasons.Add("Several parties.");
        }

        if (signals.HasEasementsOrExceptions)
        {
            hardness += 1;
            reasons.Add("Easements or exceptions are present.");
        }

        if (signals.GapCount >= 3)
        {
            hardness += 2;
            reasons.Add("Extract has several gaps or conflicts.");
        }
        else if (signals.GapCount >= 1)
        {
            hardness += 1;
            reasons.Add("Extract is missing some fields.");
        }

        if (signals.OverallConfidence > 0 && signals.OverallConfidence < 0.5)
        {
            hardness += 1;
            if (reasons.Count < 3)
            {
                reasons.Add("Overall extract confidence is low.");
            }
        }

        var band = hardness >= 4
            ? DocumentDifficultyBands.Hard
            : hardness <= 1
                ? DocumentDifficultyBands.Easy
                : DocumentDifficultyBands.Medium;

        if (reasons.Count == 0)
        {
            reasons.Add(band == DocumentDifficultyBands.Easy
                ? "Straightforward extract with a short legal."
                : "Typical GIS document.");
        }

        var clipped = reasons.Take(3).ToList();
        return new DocumentDifficultyScore(band, DocumentDifficulty.JoinReasons(clipped), clipped);
    }

    private static IReadOnlyList<string> NormalizeReasons(string? why, IEnumerable<string>? modelReasons)
    {
        var fromModel = (modelReasons ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Take(3)
            .ToList();
        if (fromModel.Count > 0)
        {
            return fromModel;
        }

        return DocumentDifficulty.SplitReasons(why);
    }

    private static bool IsPoorReadability(string text)
    {
        if (text.Length < 40)
        {
            return true;
        }

        var letters = text.Count(char.IsLetter);
        var ratio = letters / (double)text.Length;
        if (ratio < 0.45)
        {
            return true;
        }

        var junk = text.Count(ch => ch is '\uFFFD' or '?' or '|');
        return junk > text.Length / 12;
    }

    private static bool LooksLikeMetesAndBounds(string lower) =>
        lower.Contains("thence", StringComparison.Ordinal)
        || lower.Contains("metes", StringComparison.Ordinal)
        || (lower.Contains("degree", StringComparison.Ordinal) && lower.Contains("feet", StringComparison.Ordinal))
        || lower.Contains("bearing", StringComparison.Ordinal)
        || lower.Contains("north 0", StringComparison.Ordinal)
        || lower.Contains("south 0", StringComparison.Ordinal);

    private static bool LooksLikeLotBlock(string lower) =>
        (lower.Contains("lot", StringComparison.Ordinal) && lower.Contains("block", StringComparison.Ordinal))
        || lower.Contains("lot and block", StringComparison.Ordinal)
        || (lower.Contains("addition", StringComparison.Ordinal) && lower.Contains("lot", StringComparison.Ordinal));

    private static int CountParcels(string? propertyIds, string lower)
    {
        if (!string.IsNullOrWhiteSpace(propertyIds))
        {
            var lines = propertyIds
                .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Count(line => line.Length > 0);
            if (lines > 0)
            {
                return lines;
            }
        }

        var count = 0;
        foreach (var token in new[] { "parcel", "tract", "property id" })
        {
            count += CountOccurrences(lower, token);
        }

        return count;
    }

    private static int CountParties(string lower)
    {
        var count = 0;
        foreach (var token in new[] { "grantor", "grantee", "trustee", "and wife", "and husband", "et al" })
        {
            count += CountOccurrences(lower, token);
        }

        return count;
    }

    private static bool HasEasementsOrExceptions(string lower) =>
        lower.Contains("easement", StringComparison.Ordinal)
        || lower.Contains("right of way", StringComparison.Ordinal)
        || lower.Contains("right-of-way", StringComparison.Ordinal)
        || lower.Contains("exception", StringComparison.Ordinal)
        || lower.Contains("reservation", StringComparison.Ordinal)
        || lower.Contains("subject to", StringComparison.Ordinal);

    private static int CountGaps(AiFillFields fields, double overallConfidence)
    {
        var gaps = 0;
        if (!fields.Title.Present) gaps++;
        if (!fields.Type.Present) gaps++;
        if (!fields.PropertyIds.Present) gaps++;
        if (!fields.WorkedOn.Present) gaps++;
        if (overallConfidence > 0 && overallConfidence < 0.55) gaps++;
        if (fields.Title.Present && fields.Title.Confidence > 0 && fields.Title.Confidence < 0.45) gaps++;
        if (fields.Type.Present && fields.PropertyIds.Present
            && string.Equals(fields.Type.DocumentTypeName, "Plat", StringComparison.OrdinalIgnoreCase)
            && (fields.PlatCount.Present && fields.PlatCount.Value == 0))
        {
            gaps++;
        }

        return gaps;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}

public sealed record DocumentDifficultySignals(
    bool PoorReadability,
    bool MetesAndBounds,
    bool LotBlock,
    int ParcelCount,
    int PartyCount,
    bool HasEasementsOrExceptions,
    int GapCount,
    double OverallConfidence);
