namespace GisDashboard.Domain;

/// <summary>
/// Singleton BIS Consultants contact shown on upload forms. Edited on Admin Settings.
/// </summary>
public sealed class CompanyContact
{
    public static readonly Guid SingletonId = Guid.Parse("ffffffff-0000-0000-0000-000000000002");

    public const string DefaultName = "BIS Consultants";
    public const string DefaultPhone = "800-247-9045";
    public const string DefaultEmail = "gissupport@bisconsultants.com";
    public const string DefaultAddress = "14802 Venture Dr. Farmers Branch Tx 75234";
    public const string DefaultWebsite = "www.bisconsultants.com";

    public Guid Id { get; set; } = SingletonId;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
