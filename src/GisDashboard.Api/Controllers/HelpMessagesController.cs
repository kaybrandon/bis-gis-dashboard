using GisDashboard.Application.Help;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/help-messages")]
public sealed class HelpMessagesController : ControllerBase
{
    private readonly IHelpMessageService _help;

    public HelpMessagesController(IHelpMessageService help)
    {
        _help = help;
    }

    [HttpGet]
    public Task<HelpThreadResponse> Thread([FromQuery] Guid withUserId, CancellationToken cancellationToken) =>
        _help.ListThreadAsync(withUserId, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<HelpMessageDto>> Send(
        [FromBody] SendHelpMessageRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _help.SendAsync(request, cancellationToken);
        return Created($"/api/help-messages/{created.Id}", created);
    }
}
