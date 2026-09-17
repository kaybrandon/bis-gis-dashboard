using GisDashboard.Application.Company;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class CompanyContactController : ControllerBase
{
    private readonly ICompanyContactService _company;

    public CompanyContactController(ICompanyContactService company)
    {
        _company = company;
    }

    [AllowAnonymous]
    [HttpGet("public/company")]
    public Task<CompanyContactDto> Public(CancellationToken cancellationToken) =>
        _company.GetAsync(cancellationToken);

    [Authorize]
    [HttpGet("settings/company")]
    public Task<CompanyContactDto> Get(CancellationToken cancellationToken) =>
        _company.GetAsync(cancellationToken);

    [Authorize(Roles = Domain.Roles.GlobalAdministrator)]
    [HttpPut("settings/company")]
    public Task<CompanyContactDto> Save([FromBody] SaveCompanyContactRequest request, CancellationToken cancellationToken) =>
        _company.SaveAsync(request, cancellationToken);
}
