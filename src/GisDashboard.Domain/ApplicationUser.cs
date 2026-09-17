using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace GisDashboard.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string? FullName { get; set; }

    [NotMapped]
    public string PublicName => UserIdentity.PublicName(FullName, DisplayName, UserName, Email);
    public string? WorkPhone { get; set; }
    public string? AvatarBlobPath { get; set; }
    public string? AvatarContentType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<UserOrganization> Organizations { get; set; } = new List<UserOrganization>();
}
