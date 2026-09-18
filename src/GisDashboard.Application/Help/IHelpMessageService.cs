namespace GisDashboard.Application.Help;

public interface IHelpMessageService
{
    Task<HelpInboxResponse> ListInboxAsync(CancellationToken cancellationToken = default);
    Task<HelpThreadResponse> ListThreadAsync(Guid withUserId, CancellationToken cancellationToken = default);
    Task<HelpMessageDto> SendAsync(SendHelpMessageRequest request, CancellationToken cancellationToken = default);
}
