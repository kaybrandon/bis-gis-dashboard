using System.Text.Json;
using GisDashboard.Application.AiFill;

namespace GisDashboard.Infrastructure.AiFill;

/// <summary>
/// Subject-PID rule: keep only the instrument's property IDs (often 1–2),
/// never a CAD/web-map vacuum or a JSON array dump. Prompt + post-process only.
/// </summary>
public static class PropertyIdsNormalizer
{
    public const int MaxSubjectIds = 8;
    public const int MassNumericDumpCount = 6;

    public static AiFillStringField Normalize(bool present, string? raw, double confidence)
    {
        if (!present || string.IsNullOrWhiteSpace(raw))
        {
            return new AiFillStringField(false, null, 0);
        }

        var lines = ParseLines(raw);
        if (lines.Count == 0)
        {
            return new AiFillStringField(false, null, 0);
        }

        if (IsMassDump(lines))
        {
            return new AiFillStringField(false, null, Math.Min(confidence, 0.35));
        }

        return new AiFillStringField(true, string.Join('\n', lines), Math.Clamp(confidence, 0, 1));
    }

    public static IReadOnlyList<string> ParseLines(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var text = raw.Trim();
        if (TryReadJsonArray(text, out var fromJson))
        {
            return fromJson;
        }

        return SplitPlain(text);
    }

    public static bool IsMassDump(IReadOnlyList<string> lines)
    {
        if (lines.Count > MaxSubjectIds)
        {
            return true;
        }

        if (lines.Count >= MassNumericDumpCount && lines.All(IsMostlyNumericLabel))
        {
            return true;
        }

        return false;
    }

    private static bool TryReadJsonArray(string text, out List<string> lines)
    {
        lines = [];
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return false;
        }

        var slice = text[start..(end + 1)];
        try
        {
            using var doc = JsonDocument.Parse(slice);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var value = item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.ToString();
                AddLine(lines, value);
            }

            return lines.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<string> SplitPlain(string text)
    {
        var lines = new List<string>();
        foreach (var part in text.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            AddLine(lines, part);
        }

        return lines;
    }

    private static void AddLine(List<string> lines, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim().Trim(',', '"', '\'', '`');
        if (trimmed.Length == 0)
        {
            return;
        }

        if (lines.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        lines.Add(trimmed);
    }

    private static bool IsMostlyNumericLabel(string value)
    {
        var chars = value.Where(ch => !char.IsWhiteSpace(ch) && ch is not '-' and not '.' and not '#').ToArray();
        if (chars.Length == 0)
        {
            return false;
        }

        var digits = chars.Count(char.IsDigit);
        return digits >= chars.Length - 1 && digits >= 3;
    }
}
