using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ftp/settings")]
public sealed class FtpSettingsController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { nightlyHourUtc = 7 });

    [HttpPut]
    public IActionResult Save() => Ok(new { nightlyHourUtc = 7 });
}
