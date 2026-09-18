namespace GisDashboard.Application.AiFill;

public interface IAzureOpenAiCompletions
{
    bool IsConfigured { get; }
    string Deployment { get; }
    Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<AiFillVisionImage> images,
        CancellationToken cancellationToken = default);
}
