using System.Text.Json;
using System.Text.Json.Serialization;
using GisDashboard.Domain;

namespace GisDashboard.Application.AiFill;

public sealed record WorkItemAiScan(
    string Status,
    string? Message,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    AiFillResponse? Result,
    WorkItemAiScanBaseline? Baseline);

public sealed record WorkItemAiScanBaseline(
    string Title,
    Guid DocumentTypeId,
    string? PropertyIds,
    int AnnexationCount,
    int CorrectionCount,
    int DeedCount,
    int PlatCount,
    string? WorkedOn,
    string? Survey = null,
    string? Abstract = null,
    string? LotBlock = null,
    string? Subdivision = null,
    string? LegalDescription = null);

public static class WorkItemAiScanJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string CaptureBaseline(WorkItem item) =>
        JsonSerializer.Serialize(
            new WorkItemAiScanBaseline(
                item.Title,
                item.DocumentTypeId,
                item.PropertyIds ?? string.Empty,
                item.AnnexationCount,
                item.CorrectionCount,
                item.DeedCount,
                item.PlatCount,
                item.WorkedOn?.ToString("yyyy-MM-dd"),
                item.Survey,
                item.Abstract,
                item.LotBlock,
                item.Subdivision,
                item.LegalDescription),
            Options);

    public static WorkItemAiScan? FromStored(
        string? status,
        string? message,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        string? resultJson,
        string? baselineJson)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return new WorkItemAiScan(
            status,
            message,
            startedAt,
            completedAt,
            TryDeserialize<AiFillResponse>(resultJson),
            TryDeserialize<WorkItemAiScanBaseline>(baselineJson));
    }

    public static string SerializeResult(AiFillResponse response) =>
        JsonSerializer.Serialize(response, Options);

    public static T? TryDeserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
