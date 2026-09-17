using GisDashboard.Application.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize(Roles = Domain.Roles.GlobalAdministrator)]
[Route("api/settings/email")]
public sealed class EmailSettingsController : ControllerBase
{
    private readonly IEmailSettingsService _settings;

    public EmailSettingsController(IEmailSettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public Task<EmailSettingsDto> Get(CancellationToken cancellationToken) =>
        _settings.GetAsync(cancellationToken);

    [HttpPut]
    public Task<EmailSettingsDto> Save([FromBody] SaveEmailSettingsRequest request, CancellationToken cancellationToken) =>
        _settings.SaveAsync(request, cancellationToken);

    [HttpPost("test")]
    public Task<EmailTestResult> Test([FromBody] SendTestEmailRequest request, CancellationToken cancellationToken) =>
        _settings.SendTestAsync(request, cancellationToken);
}
