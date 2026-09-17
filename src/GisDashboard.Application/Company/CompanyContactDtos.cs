namespace GisDashboard.Application.Company;

public sealed class SaveCompanyContactRequest
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
}

public sealed record CompanyContactDto(
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    string? Website,
    DateTimeOffset? UpdatedAt);

public interface ICompanyContactService
{
    Task<CompanyContactDto> GetAsync(CancellationToken cancellationToken = default);
    Task<CompanyContactDto> SaveAsync(SaveCompanyContactRequest request, CancellationToken cancellationToken = default);
}
