using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GisDashboard.Domain;

public static class UserIdentity
{
    private static readonly HashSet<string> RoleOrSystemWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "administrator", "global", "client", "demo", "token", "upload",
        "uploader", "editor", "viewer", "admin", "system", "bis"
    };

    /// <summary>
    /// Header greeting first name: first token of Full name only.
    /// Never email, username, userId, or a fallback person name.
    /// </summary>
    public static string? GreetingFirstName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var tokens = SplitName(fullName);
        if (tokens.Length == 0 || LooksLikeEmail(tokens[0]))
        {
            return null;
        }

        return tokens[0];
    }

    /// <summary>First name plus last initial only, e.g. Alex R. Never the full last name.</summary>
    public static string ShortPublicName(string? fullName, string? displayName, string? userName = null, string? email = null)
    {
        var name = PublicName(fullName, displayName, userName, email);
        var tokens = SplitName(name);
        if (tokens.Length == 0)
        {
            return name;
        }

        if (tokens.Length == 1 || LooksLikeEmail(name))
        {
            return tokens[0];
        }

        var first = TitleToken(tokens[0]);
        var lastInitial = char.ToUpper(tokens[^1][0], CultureInfo.InvariantCulture);
        return $"{first} {lastInitial}.";
    }

    public static string HistoricalName(
        string? fullName,
        string? displayName,
        bool isArchived,
        string? userName = null,
        string? email = null) =>
        WithArchivedSuffix(PublicName(fullName, displayName, userName, email), isArchived);

    public static string WithArchivedSuffix(string? name, bool isArchived)
    {
        var value = (name ?? string.Empty).Trim();
        if (!isArchived || value.Length == 0)
        {
            return value;
        }

        return value.EndsWith("(archived)", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{value} (archived)";
    }

    public static bool CanSignIn(bool isActive, bool isArchived) => isActive && !isArchived;

    /// <summary>
    /// Person label for send-report / emailed / exported surfaces.
    /// Full name when on file, otherwise username/display (never a blank Full name).
    /// </summary>
    public static string ReportName(string? fullName, string? displayName, string? userName = null, string? email = null) =>
        PublicName(fullName, displayName, userName, email);

    public static string PublicName(string? fullName, string? displayName, string? userName = null, string? email = null)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(displayName) && !LooksLikeEmail(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(userName) && !LooksLikeEmail(userName))
        {
            return userName.Trim();
        }

        return (email ?? string.Empty).Trim();
    }

    public static bool LooksLikeEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('@', StringComparison.Ordinal);

    public static bool LooksLikePersonName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || LooksLikeEmail(value))
        {
            return false;
        }

        var tokens = SplitName(value);
        if (tokens.Length < 2)
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (RoleOrSystemWords.Contains(token))
            {
                return false;
            }

            if (!Regex.IsMatch(token, @"^[A-Za-z][A-Za-z'\-]*$"))
            {
                return false;
            }
        }

        return true;
    }

    public static string ToTitleCase(string value)
    {
        var tokens = SplitName(value);
        return string.Join(' ', tokens.Select(TitleToken));
    }

    public static string FromPersonName(string value)
    {
        var tokens = SplitName(value);
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        var last = tokens[^1];
        var initial = tokens[0][0];
        return NormalizeUsername($"{initial}{last}");
    }

    public static string FromEmail(string? email)
    {
        var local = (email ?? string.Empty).Split('@')[0];
        var normalized = NormalizeUsername(local);
        return string.IsNullOrEmpty(normalized) ? "user" : normalized;
    }

    public static string NormalizeUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    public static string UniqueUsername(string desired, ISet<string> takenNormalized)
    {
        var seed = string.IsNullOrEmpty(desired) ? "user" : desired;
        var candidate = seed;
        var n = 2;
        while (takenNormalized.Contains(candidate))
        {
            candidate = $"{seed}{n}";
            n++;
        }

        return candidate;
    }

    public static bool NeedsUsernameRewrite(string? userName, string? email) =>
        string.IsNullOrWhiteSpace(userName)
        || LooksLikeEmail(userName)
        || (!string.IsNullOrWhiteSpace(email)
            && string.Equals(userName, email, StringComparison.OrdinalIgnoreCase));

    private static string[] SplitName(string value) =>
        value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string TitleToken(string token)
    {
        if (token.Length == 0)
        {
            return token;
        }

        return char.ToUpper(token[0], CultureInfo.InvariantCulture)
               + token[1..].ToLowerInvariant();
    }
}
