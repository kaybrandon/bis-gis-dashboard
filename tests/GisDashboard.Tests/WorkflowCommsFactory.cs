using GisDashboard.Application.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

public sealed class WorkflowCommsFactory : ApiFactory
{
    protected override void ExtraConfig(Dictionary<string, string?> config)
    {
        config["Email:Enabled"] = "true";
        config["Email:Smtp:Host"] = "smtp.test.local";
        config["Email:PublicBaseUrl"] = "http://127.0.0.1:47222";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IEmailSender)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IEmailSender, CapturingEmailSender>();
        });
    }
}
