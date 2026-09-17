using System.Text.RegularExpressions;
using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Company;
using GisDashboard.Application.Exceptions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class CompanyContactService : ICompanyContactService
{
    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CompanyContactService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CompanyContactDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<CompanyContactDto> SaveAsync(SaveCompanyContactRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.CanManageGlobalDirectory)
        {
            throw new ForbiddenException("Only a Global Administrator can edit BIS Consultants contact details.");
        }

        var phone = Trim(request.Phone, 40, "Phone");
        var email = Trim(request.Email, 200, "Email");
        var address = Trim(request.Address, 300, "Address");
        var website = Trim(request.Website, 200, "Website");

        if (email is not null && !EmailPattern.IsMatch(email))
        {
            throw new ValidationException("Enter a valid email address.");
        }

        if (website is not null && !IsSafeWebsite(website))
        {
            throw new ValidationException("Enter a website such as www.bisconsultants.com.");
        }

        var row = await LoadRowAsync(cancellationToken) ?? new CompanyContact { Id = CompanyContact.SingletonId };
        row.Phone = phone;
        row.Email = email;
        row.Address = address;
        row.Website = website;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedByUserId = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;

        if (_db.Entry(row).State == EntityState.Detached)
        {
            _db.CompanyContacts.Add(row);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    private Task<CompanyContact?> LoadRowAsync(CancellationToken cancellationToken) =>
        _db.CompanyContacts.FirstOrDefaultAsync(x => x.Id == CompanyContact.SingletonId, cancellationToken);

    private static CompanyContactDto ToDto(CompanyContact? row) =>
        row is null
            ? new(
                CompanyContact.DefaultName,
                CompanyContact.DefaultPhone,
                CompanyContact.DefaultEmail,
                CompanyContact.DefaultAddress,
                CompanyContact.DefaultWebsite,
                null)
            : new(
                CompanyContact.DefaultName,
                row.Phone,
                row.Email,
                row.Address,
                row.Website,
                row.UpdatedAt);

    private static string? Trim(string? value, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > max)
        {
            throw new ValidationException($"{label} must be {max} characters or fewer.");
        }

        return trimmed;
    }

    private static bool IsSafeWebsite(string website)
    {
        var lower = website.ToLowerInvariant();
        if (lower.StartsWith("javascript:") || lower.StartsWith("data:") || lower.Contains("://") && !lower.StartsWith("http://") && !lower.StartsWith("https://"))
        {
            return false;
        }

        return website.Contains('.', StringComparison.Ordinal) && !website.Contains(' ', StringComparison.Ordinal);
    }
}
