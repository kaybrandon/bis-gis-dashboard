namespace GisDashboard.Application.Help;

public static class HelpChips
{
    public const string NeedHelp = "need-help";
    public const string TakeALook = "take-a-look";
    public const string OnMyWay = "on-my-way";
    public const string PingIn5 = "ping-5";
    public const string CantRightNow = "cant-right-now";

    public const int MaxBodyLength = 280;
    public const string OfflineMessage = "Offline — try when they're back.";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [NeedHelp] = "Need help?",
        [TakeALook] = "Can you take a look?",
        [OnMyWay] = "On my way",
        [PingIn5] = "Ping me in 5",
        [CantRightNow] = "Can't right now"
    };

    public static readonly IReadOnlyList<string> SendOrder = [NeedHelp, TakeALook];
    public static readonly IReadOnlyList<string> ReplyOrder = [OnMyWay, PingIn5, CantRightNow];

    public static string? Normalize(string? chip)
    {
        if (string.IsNullOrWhiteSpace(chip))
        {
            return null;
        }

        var value = chip.Trim();
        foreach (var (code, label) in Labels)
        {
            if (value.Equals(code, StringComparison.OrdinalIgnoreCase)
                || value.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        return null;
    }

    public static string Label(string? chip) =>
        chip is not null && Labels.TryGetValue(chip, out var label) ? label : "";
}

public sealed record HelpMessageDto(
    Guid Id,
    Guid FromUserId,
    Guid ToUserId,
    string? Chip,
    string ChipLabel,
    string Body,
    DateTimeOffset CreatedAt,
    bool Mine);

public sealed record HelpThreadResponse(
    Guid WithUserId,
    string WithDisplayName,
    string PresenceStatus,
    bool CanCompose,
    string? ComposeDisabledReason,
    IReadOnlyList<HelpMessageDto> Items);

public sealed record SendHelpMessageRequest(
    Guid ToUserId,
    string? Chip,
    string? Body);

public sealed record HelpInboxThreadDto(
    Guid WithUserId,
    string WithDisplayName,
    string PresenceStatus,
    bool CanCompose,
    string? ComposeDisabledReason,
    string Preview,
    DateTimeOffset LastAt,
    bool Unread,
    int UnreadCount);

public sealed record HelpInboxResponse(
    int UnreadCount,
    IReadOnlyList<HelpInboxThreadDto> Items);
