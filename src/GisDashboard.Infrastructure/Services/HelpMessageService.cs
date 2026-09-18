using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.Help;
using GisDashboard.Application.Presence;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class HelpMessageService : IHelpMessageService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPresenceService _presence;

    public HelpMessageService(AppDbContext db, ICurrentUser currentUser, IPresenceService presence)
    {
        _db = db;
        _currentUser = currentUser;
        _presence = presence;
    }

    public async Task<HelpThreadResponse> ListThreadAsync(Guid withUserId, CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        if (withUserId == Guid.Empty || withUserId == _currentUser.UserId)
        {
            throw new ValidationException("Pick someone else.");
        }

        if (!await _presence.CanSeePresenceUserAsync(withUserId, cancellationToken))
        {
            throw new ForbiddenException("That person is not in your Who’s online list.");
        }

        var now = DateTimeOffset.UtcNow;
        var unread = await _db.HelpMessages
            .Where(x => x.FromUserId == withUserId && x.ToUserId == _currentUser.UserId && x.ReadAt == null)
            .ToListAsync(cancellationToken);
        foreach (var row in unread)
        {
            row.ReadAt = now;
        }

        if (unread.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        var rows = (await _db.HelpMessages.AsNoTracking()
            .Where(x =>
                (x.FromUserId == _currentUser.UserId && x.ToUserId == withUserId)
                || (x.FromUserId == withUserId && x.ToUserId == _currentUser.UserId))
            .OrderByDescending(x => x.CreatedAtSort)
            .Take(50)
            .ToListAsync(cancellationToken))
            .OrderBy(x => x.CreatedAtSort)
            .ToList();

        var name = await DisplayNameAsync(withUserId, cancellationToken);
        var status = await _presence.PresenceStatusOfAsync(withUserId, cancellationToken) ?? "Offline";
        var online = status == "Online";
        return new HelpThreadResponse(
            withUserId,
            name,
            status,
            online,
            online ? null : HelpChips.OfflineMessage,
            rows.Select(ToDto).ToList());
    }

    public async Task<HelpMessageDto> SendAsync(SendHelpMessageRequest request, CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var toUserId = request.ToUserId;
        if (toUserId == Guid.Empty || toUserId == _currentUser.UserId)
        {
            throw new ValidationException("Pick someone else.");
        }

        if (!await _presence.CanSeePresenceUserAsync(toUserId, cancellationToken))
        {
            throw new ForbiddenException("That person is not in your Who’s online list.");
        }

        var status = await _presence.PresenceStatusOfAsync(toUserId, cancellationToken);
        if (status != "Online")
        {
            throw new ValidationException(HelpChips.OfflineMessage);
        }

        var chip = HelpChips.Normalize(request.Chip);
        if (request.Chip is not null && chip is null)
        {
            throw new ValidationException("Unknown message chip.");
        }

        var body = (request.Body ?? string.Empty).Trim();
        if (body.Length > HelpChips.MaxBodyLength)
        {
            throw new ValidationException("Keep it short (280 characters).");
        }

        if (chip is null && body.Length == 0)
        {
            throw new ValidationException("Pick a chip or type a short message.");
        }

        if (chip is not null && body.Length == 0)
        {
            body = HelpChips.Label(chip);
        }

        var now = DateTimeOffset.UtcNow;
        var row = new HelpMessage
        {
            Id = Guid.NewGuid(),
            FromUserId = _currentUser.UserId,
            ToUserId = toUserId,
            Chip = chip,
            Body = body,
            CreatedAt = now,
            CreatedAtSort = now.ToUnixTimeMilliseconds()
        };
        _db.HelpMessages.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    private void EnsureStaff()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        if (!_currentUser.CanSeePresence)
        {
            throw new ForbiddenException("Need help messages are available to staff only.");
        }
    }

    private async Task<string> DisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        return user is null
            ? "Someone"
            : UserIdentity.PublicName(user.FullName, user.DisplayName, user.UserName, user.Email);
    }

    private HelpMessageDto ToDto(HelpMessage row) =>
        new(
            row.Id,
            row.FromUserId,
            row.ToUserId,
            row.Chip,
            HelpChips.Label(row.Chip),
            row.Body,
            row.CreatedAt,
            row.FromUserId == _currentUser.UserId);
}
