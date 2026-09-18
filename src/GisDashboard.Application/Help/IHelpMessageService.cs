namespace GisDashboard.Application.Help;

public interface IHelpMessageService
{
    Task<HelpThreadResponse> ListThreadAsync(Guid withUserId, CancellationToken cancellationToken = default);
    Task<HelpMessageDto> SendAsync(SendHelpMessageRequest request, CancellationToken cancellationToken = default);
}
