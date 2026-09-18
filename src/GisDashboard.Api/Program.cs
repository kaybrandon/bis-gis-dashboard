using System.Text;
using System.Threading.RateLimiting;
using Azure.Identity;
using GisDashboard.Api.Middleware;
using GisDashboard.Infrastructure;
using GisDashboard.Infrastructure.Persistence;
using GisDashboard.Application.WorkItems;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var vaultUri = builder.Configuration["KeyVaultUri"]
    ?? builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrWhiteSpace(vaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential());
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplicationInsightsTelemetry();

var uploadOptions = new UploadOptions();
builder.Configuration.GetSection(UploadOptions.SectionName).Bind(uploadOptions);
var requestCeiling = Math.Max(uploadOptions.EffectiveMaxFileBytes + 10_485_760, UploadOptions.HttpRequestCeilingBytes);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = requestCeiling;
    options.ValueLengthLimit = int.MaxValue;
});
builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = requestCeiling);
builder.Services.Configure<KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = requestCeiling);

var jwtKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "gis-dashboard";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "gis-dashboard";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey.Length >= 32
                ? jwtKey
                : "LOCAL-ONLY-DEV-KEY-CHANGE-IN-AZURE-KV!!"))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        var message = context.HttpContext.Request.Path.StartsWithSegments("/api/auth")
            ? "Too many reset requests. Try again later."
            : "Too many requests. Try again shortly.";
        await context.HttpContext.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { message }),
            token);
    };
    options.AddPolicy("public-upload", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("password-reset", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
});

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("spa", policy =>
    {
        if (corsOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
                .WithExposedHeaders("X-Preview-Page", "X-Preview-Page-Count", "X-Preview-More-Pages", "X-Preview-Kind");
        }
        else
        {
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()
                .WithExposedHeaders("X-Preview-Page", "X-Preview-Page-Count", "X-Preview-More-Pages", "X-Preview-Kind");
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seed = scope.ServiceProvider.GetRequiredService<DemoSeed>();
    await seed.RunAsync();
    await SchemaUpgrade.ApplyAsync(db);
    var emailSettings = scope.ServiceProvider.GetRequiredService<GisDashboard.Application.Email.IEmailSettingsService>();
    await emailSettings.RefreshCacheAsync();
}

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("spa");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
