namespace GisDashboard.Application.AiFill;

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";
    public const string DefaultDeployment = "gpt-4.1-mini";
    public const string PreferredResource = "oai-bis-deed-ai";

    public const string UnconfiguredMessage =
        "AI fill is not configured. Set AzureOpenAI__Endpoint and AzureOpenAI__ApiKey on the App Service (Key Vault reference for the key). Prefer resource oai-bis-deed-ai, deployment gpt-4.1-mini.";

    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string Deployment { get; set; } = DefaultDeployment;
    public int TimeoutSeconds { get; set; } = 45;
    public int MaxRetries { get; set; } = 2;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiKey);

    public string EffectiveDeployment =>
        string.IsNullOrWhiteSpace(Deployment) ? DefaultDeployment : Deployment.Trim();

    public int EffectiveTimeoutSeconds => TimeoutSeconds is >= 10 and <= 120 ? TimeoutSeconds : 45;

    public int EffectiveMaxRetries => MaxRetries is >= 0 and <= 4 ? MaxRetries : 2;
}
