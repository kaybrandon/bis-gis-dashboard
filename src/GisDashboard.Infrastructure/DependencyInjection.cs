using GisDashboard.Application.Abstractions;
using GisDashboard.Application.AiFill;
using GisDashboard.Application.Auth;
using GisDashboard.Application.Company;
using GisDashboard.Application.Directory;
using GisDashboard.Application.Email;
using GisDashboard.Application.Help;
using GisDashboard.Application.Notifications;
using GisDashboard.Application.Presence;
using GisDashboard.Application.Reports;
using GisDashboard.Application.TimeReports;
using GisDashboard.Application.Status;
using GisDashboard.Application.Connections;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.AiFill;
using GisDashboard.Infrastructure.Email;
using GisDashboard.Infrastructure.Persistence;
using GisDashboard.Infrastructure.Reports;
using GisDashboard.Infrastructure.Security;
using GisDashboard.Infrastructure.Services;
using GisDashboard.Infrastructure.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["SqlConnectionString"]
            ?? "Data Source=gisdashboard.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (IsSqlite(connectionString))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddDataProtection()
            .SetApplicationName("GisDashboard");

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<UploadOptions>(configuration.GetSection(UploadOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));
        services.PostConfigure<AzureOpenAIOptions>(options =>
        {
            options.Endpoint = FirstNonEmpty(
                options.Endpoint,
                configuration["AZURE_OPENAI_ENDPOINT"],
                configuration["AzureOpenAIEndpoint"]);
            options.ApiKey = FirstNonEmpty(
                options.ApiKey,
                configuration["AZURE_OPENAI_API_KEY"],
                configuration["AZURE_OPENAI_KEY"],
                configuration["AzureOpenAIKey"]);
            if (string.IsNullOrWhiteSpace(options.Deployment))
            {
                options.Deployment = FirstNonEmpty(
                    configuration["AZURE_OPENAI_DEPLOYMENT"],
                    configuration["AzureOpenAIDeployment"],
                    AzureOpenAIOptions.DefaultDeployment) ?? AzureOpenAIOptions.DefaultDeployment;
            }
        });
        services.Configure<LocalStorageOptions>(configuration.GetSection("LocalStorage"));
        services.Configure<AzureStorageOptions>(configuration.GetSection("AzureStorage"));
        services.PostConfigure<AzureStorageOptions>(options =>
        {
            options.ConnectionString ??= configuration.GetConnectionString("AzureStorage")
                ?? configuration["StorageConnectionString"];
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IOrgScope, OrgScope>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWorkItemService, WorkItemService>();
        services.AddScoped<IDashboardReportService, DashboardReportService>();
        services.AddScoped<IWorkItemAiFillService, WorkItemAiFillService>();
        services.AddSingleton<IAzureOpenAiCompletions, AzureOpenAiCompletions>();
        services.AddScoped<ITimeEntryService, TimeEntryService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ITimeReportService, TimeReportService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPresenceService, PresenceService>();
        services.AddScoped<IHelpMessageService, HelpMessageService>();
        services.AddScoped<IWorkflowComms, WorkflowCommsService>();
        services.AddScoped<IEnvironmentStatusService, EnvironmentStatusService>();
        services.AddSingleton<IEmailSettingsCache, EmailSettingsCache>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IEmailSettingsService, EmailSettingsService>();
        services.AddScoped<ICompanyContactService, CompanyContactService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IConnectionService, ConnectionService>();
        services.AddScoped<DemoSeed>();

        var useAzureBlob = configuration.GetValue("AzureStorage:Enabled", false)
            || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("AzureStorage"))
            || !string.IsNullOrWhiteSpace(configuration["StorageConnectionString"]);
        if (useAzureBlob)
        {
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
        }
        else
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }

        return services;
    }

    public static bool IsSqlite(string connectionString) =>
        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
