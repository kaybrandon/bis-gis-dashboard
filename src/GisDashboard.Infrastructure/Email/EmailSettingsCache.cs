using GisDashboard.Application.Email;

namespace GisDashboard.Infrastructure.Email;

public sealed class EmailSettingsCache : IEmailSettingsCache
{
    private volatile ResolvedEmailSettings _current = new(
        false,
        null,
        587,
        true,
        "noreply@bisconsultants.local",
        "GIS Dashboard",
        null,
        null,
        null,
        false,
        "none");

    public ResolvedEmailSettings Current => _current;

    public void Replace(ResolvedEmailSettings settings) => _current = settings;
}
