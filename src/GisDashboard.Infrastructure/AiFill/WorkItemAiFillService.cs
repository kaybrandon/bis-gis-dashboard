using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using GisDashboard.Application.Abstractions;
using GisDashboard.Application.AiFill;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GisDashboard.Infrastructure.AiFill;

public sealed class WorkItemAiFillService : IWorkItemAiFillService
{
    private static readonly ConcurrentDictionary<Guid, Task<AiFillResponse>> InFlight = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ICurrentUser _currentUser;
    private readonly IWorkItemService _workItems;
    private readonly IAzureOpenAiCompletions _completions;
    private readonly IFileStorage _storage;
    private readonly AppDbContext _db;
    private readonly ILogger<WorkItemAiFillService> _logger;

    public WorkItemAiFillService(
        ICurrentUser currentUser,
        IWorkItemService workItems,
        IAzureOpenAiCompletions completions,
        IFileStorage storage,
        AppDbContext db,
        ILogger<WorkItemAiFillService> logger)
    {
        _currentUser = currentUser;
        _workItems = workItems;
        _completions = completions;
        _storage = storage;
        _db = db;
        _logger = logger;
    }

    public async Task<AiFillResponse> FillFromPdfAsync(
        Guid workItemId,
        bool rescore = false,
        CancellationToken cancellationToken = default)
    {
        var item = await _workItems.GetAsync(workItemId, cancellationToken);
        if (!_currentUser.CanMutateWorkItems)
        {
            throw new ForbiddenException("Your role cannot edit documents.");
        }

        if (!_completions.IsConfigured)
        {
            await TryMarkScanAsync(
                workItemId,
                WorkItemAiScanStatus.Unconfigured,
                WorkItemAiScanStatus.UnconfiguredMessage,
                result: null,
                cancellationToken);
            throw new ServiceUnavailableException(AzureOpenAIOptions.UnconfiguredMessage);
        }

        if (!IsPdf(item.FileName, item.ContentType))
        {
            throw new ValidationException("AI fill only works on PDF files.");
        }

        try
        {
            var response = await RunExclusiveAsync(
                workItemId,
                () => FillCoreAsync(workItemId, rescore, force: true, cancellationToken));
            await TryMarkScanAsync(
                workItemId,
                WorkItemAiScanStatus.Succeeded,
                WorkItemAiScanStatus.SucceededMessage,
                response,
                cancellationToken);
            return response;
        }
        catch (Exception ex) when (ex is not ForbiddenException and not OperationCanceledException)
        {
            await TryMarkScanAsync(
                workItemId,
                WorkItemAiScanStatus.Failed,
                SanitizeScanMessage(ex.Message),
                result: null,
                cancellationToken);
            throw;
        }
    }

    public async Task RunAutoScanAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_completions.IsConfigured)
            {
                await TryMarkScanAsync(
                    workItemId,
                    WorkItemAiScanStatus.Unconfigured,
                    WorkItemAiScanStatus.UnconfiguredMessage,
                    result: null,
                    cancellationToken);
                return;
            }

            var target = await LoadFillTargetAsync(workItemId, cancellationToken);
            if (target is null)
            {
                return;
            }

            if (!IsPdf(target.FileName, target.ContentType))
            {
                return;
            }

            var response = await RunExclusiveAsync(
                workItemId,
                () => FillCoreAsync(workItemId, rescore: false, force: false, cancellationToken));
            await TryMarkScanAsync(
                workItemId,
                WorkItemAiScanStatus.Succeeded,
                WorkItemAiScanStatus.SucceededMessage,
                response,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Auto AI-scan failed for work item {WorkItemId}", workItemId);
            await TryMarkScanAsync(
                workItemId,
                WorkItemAiScanStatus.Failed,
                SanitizeScanMessage(ex.Message),
                result: null,
                cancellationToken);
        }
    }

    private static async Task<AiFillResponse> RunExclusiveAsync(Guid workItemId, Func<Task<AiFillResponse>> work)
    {
        while (true)
        {
            if (InFlight.TryGetValue(workItemId, out var existing))
            {
                return await existing;
            }

            var created = new TaskCompletionSource<AiFillResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!InFlight.TryAdd(workItemId, created.Task))
            {
                continue;
            }

            try
            {
                var result = await work();
                created.TrySetResult(result);
                return result;
            }
            catch (Exception ex)
            {
                created.TrySetException(ex);
                throw;
            }
            finally
            {
                InFlight.TryRemove(workItemId, out _);
            }
        }
    }

    private async Task<AiFillResponse> FillCoreAsync(
        Guid workItemId,
        bool rescore,
        bool force,
        CancellationToken cancellationToken)
    {
        var target = await LoadFillTargetAsync(workItemId, cancellationToken)
            ?? throw new ValidationException("Document was not found.");

        if (!force
            && WorkItemAiScanStatus.IsSucceeded(target.AiScanStatus)
            && WorkItemAiScanJson.TryDeserialize<AiFillResponse>(target.AiScanResultJson) is { } cached)
        {
            return cached;
        }

        if (!_completions.IsConfigured)
        {
            throw new ServiceUnavailableException(AzureOpenAIOptions.UnconfiguredMessage);
        }

        if (!IsPdf(target.FileName, target.ContentType))
        {
            throw new ValidationException("AI fill only works on PDF files.");
        }

        if (string.IsNullOrWhiteSpace(target.BlobPath))
        {
            throw new ValidationException("This file could not be read as a PDF. Replace it with a text PDF, or type the fields.");
        }

        await TryMarkScanAsync(
            workItemId,
            WorkItemAiScanStatus.Running,
            WorkItemAiScanStatus.PendingMessage,
            result: null,
            cancellationToken);

        await using var download = await _storage.OpenReadAsync(target.BlobPath, cancellationToken);
        await using var buffer = new MemoryStream();
        await download.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        string extracted;
        try
        {
            extracted = PdfTextExtractor.Extract(buffer);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF text extract failed for work item {WorkItemId}", workItemId);
            extracted = string.Empty;
        }

        var types = await _db.DocumentTypes.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);
        var typeNames = types.Select(x => x.Name);

        string raw;
        IReadOnlyList<AiFillVisionImage> pageImages = [];
        var usedVision = false;
        if (PdfTextExtractor.IsUsable(extracted))
        {
            raw = await _completions.CompleteJsonAsync(
                SystemPrompt(typeNames),
                UserPrompt(target.FileName, target.Title, target.DocumentTypeName, extracted),
                cancellationToken);
        }
        else
        {
            buffer.Position = 0;
            try
            {
                pageImages = PdfPageImageRenderer.Render(buffer);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PDF page render failed for work item {WorkItemId}", workItemId);
                throw new ValidationException("This file could not be read as a PDF. Replace it with a text PDF, or type the fields.");
            }

            if (pageImages.Count == 0)
            {
                throw new ValidationException("This PDF could not be converted to page images for AI fill. Try again, or type the fields.");
            }

            usedVision = true;
            raw = await _completions.CompleteJsonAsync(
                VisionSystemPrompt(typeNames),
                VisionUserPrompt(target.FileName, target.Title, target.DocumentTypeName, extracted, pageImages.Count),
                pageImages,
                cancellationToken);
            if (!PdfTextExtractor.IsUsable(extracted))
            {
                extracted = VisionExtractNote;
            }
        }

        AiFillResponse response;
        try
        {
            response = MapResponse(
                raw,
                types.ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase),
                extracted);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI fill JSON parse failed for work item {WorkItemId}", workItemId);
            throw new ValidationException("AI fill could not read the model response. Try again, or type the fields.");
        }

        response = await PersistDifficultyAsync(workItemId, extracted, response, rescore, cancellationToken);
        await TryWriteAuditAsync(workItemId, extracted, raw, response, usedVision, pageImages.Count, cancellationToken);
        return response;
    }

    private async Task<FillTarget?> LoadFillTargetAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        return await _db.WorkItems.AsNoTracking()
            .Where(x => x.Id == workItemId)
            .Select(x => new FillTarget(
                x.FileName,
                x.Title,
                x.DocumentType.Name,
                x.ContentType,
                x.BlobPath,
                x.AiScanStatus,
                x.AiScanResultJson))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed record FillTarget(
        string FileName,
        string Title,
        string DocumentTypeName,
        string? ContentType,
        string? BlobPath,
        string? AiScanStatus,
        string? AiScanResultJson);

    private static bool IsPdf(string? fileName, string? contentType) =>
        WorkItemAiScanStatus.IsPdf(fileName, contentType);

    private const string VisionExtractNote =
        "[Scanned or image-only PDF. Fields and difficulty scored from rendered page images on this pass.]";

    private static string SharedJsonContract(string allowed) =>
        "Return a single JSON object. No markdown.\n" +
        "Never invent or return Status, Assignee, Split, Sketch, Priority, or Reviewed.\n" +
        "Only set present=true when the document supports the value. Do not guess.\n" +
        $"type.value must be one of: {allowed}.\n" +
        "Do not extract or return propertyIds. Property IDs are entered manually by staff.\n" +
        "Always omit propertyIds or set present=false with an empty value. Never copy CAD, web-map, or plat parcel labels into propertyIds.\n" +
        "Also extract survey, abstract, lotBlock, subdivision, and legalDescription when the document supports them.\n" +
        "Leave present=false and do not guess when a deed/plat field is missing. legalDescription may be multiline.\n" +
        "Counts are non-negative integers.\n" +
        "workedOn is YYYY-MM-DD only when a work, recording, or file date is obvious.\n" +
        "Each field is { \"present\": bool, \"value\": ..., \"confidence\": number from 0 to 1 }.\n" +
        "Also include overallConfidence from 0 to 1.\n" +
        "On the same pass, score document difficulty from what this extract already sees — no extra OCR or Document Intelligence.\n" +
        "Signals: scan readability; legal type/length (lot-block vs metes-and-bounds); parcel count; parties; easements/exceptions; extract gaps/conflicts; many unlabeled or crowded map labels.\n" +
        "difficulty is { \"band\": \"Easy\"|\"Medium\"|\"Hard\", \"why\": string or 1-3 short bullets, \"reasons\": optional string array }.";

    private static string SystemPrompt(IEnumerable<string> typeNames) =>
        "You extract GIS work-item fields from PDF text already extracted from the file.\n" +
        SharedJsonContract(string.Join(", ", typeNames));

    private static string VisionSystemPrompt(IEnumerable<string> typeNames) =>
        "You extract GIS work-item fields from scanned or image-only PDF page images.\n" +
        "Read the attached page images. Do not require a text layer.\n" +
        SharedJsonContract(string.Join(", ", typeNames));

    private static string UserPrompt(string fileName, string title, string typeName, string extracted) =>
        $"""
        File name: {fileName}
        Current title: {title}
        Current type: {typeName}

        Extracted PDF text:
        {extracted}
        """;

    private static string VisionUserPrompt(string fileName, string title, string typeName, string extracted, int pageCount)
    {
        var leftover = string.IsNullOrWhiteSpace(extracted)
            ? "None."
            : extracted.Trim();
        return
            $"""
            File name: {fileName}
            Current title: {title}
            Current type: {typeName}

            This PDF has no usable text layer. {pageCount} page image(s) are attached in order.
            Extract fields and difficulty from the page images on this same pass.
            Do not extract Property IDs. Staff enter those manually.

            Unusable extracted text (ignore if the images disagree):
            {leftover}
            """;
    }

    private AiFillResponse MapResponse(string raw, IReadOnlyDictionary<string, Guid> types, string extracted)
    {
        var json = UnwrapJson(raw);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var title = ReadString(root, "title");
        // QC4-01: Property IDs are manual-only. Ignore any model propertyIds and never emit a suggestion.
        var propertyIds = new AiFillStringField(false, null, 0);
        var annex = ReadInt(root, "annexationCount");
        var corr = ReadInt(root, "correctionCount");
        var deeds = ReadInt(root, "deedCount");
        var plats = ReadInt(root, "platCount");
        var worked = ReadDate(root, "workedOn");
        var type = ReadType(root, types);
        var survey = ReadString(root, "survey");
        var abstractField = ReadString(root, "abstract");
        var lotBlock = ReadString(root, "lotBlock");
        var subdivision = ReadString(root, "subdivision");
        var legalDescription = ReadString(root, "legalDescription");

        var present = new List<double>();
        AddIfPresent(present, title.Present, title.Confidence);
        AddIfPresent(present, type.Present, type.Confidence);
        AddIfPresent(present, annex.Present, annex.Confidence);
        AddIfPresent(present, corr.Present, corr.Confidence);
        AddIfPresent(present, deeds.Present, deeds.Confidence);
        AddIfPresent(present, plats.Present, plats.Confidence);
        AddIfPresent(present, worked.Present, worked.Confidence);
        AddIfPresent(present, survey.Present, survey.Confidence);
        AddIfPresent(present, abstractField.Present, abstractField.Confidence);
        AddIfPresent(present, lotBlock.Present, lotBlock.Confidence);
        AddIfPresent(present, subdivision.Present, subdivision.Confidence);
        AddIfPresent(present, legalDescription.Present, legalDescription.Confidence);

        var overall = ReadConfidence(root, "overallConfidence");
        if (overall is null)
        {
            overall = present.Count == 0 ? 0 : present.Average();
        }

        string? warning = null;
        if (present.Count == 0)
        {
            warning = "No fields could be filled from this PDF.";
        }

        var fields = new AiFillFields(
            title,
            type,
            propertyIds,
            annex,
            corr,
            deeds,
            plats,
            worked,
            survey,
            abstractField,
            lotBlock,
            subdivision,
            legalDescription);
        var scored = ReadDifficulty(root, extracted, fields, Clamp(overall.Value));
        return new AiFillResponse(
            Clamp(overall.Value),
            _completions.Deployment,
            warning,
            fields,
            ToDto(scored, overridden: false, keptOverride: false, aiBand: scored.Band));
    }

    private static void AddIfPresent(List<double> scores, bool present, double confidence)
    {
        if (present)
        {
            scores.Add(confidence);
        }
    }

    private static AiFillStringField ReadString(JsonElement root, string name)
    {
        if (!TryGetField(root, name, out var field))
        {
            return new AiFillStringField(false, null, 0);
        }

        var present = ReadPresent(field);
        var value = ReadOptionalString(field, "value");
        if (string.IsNullOrWhiteSpace(value))
        {
            present = false;
            value = null;
        }
        else
        {
            value = value.Trim();
        }

        return new AiFillStringField(present, present ? value : null, ReadFieldConfidence(field));
    }

    private static AiFillIntField ReadInt(JsonElement root, string name)
    {
        if (!TryGetField(root, name, out var field))
        {
            return new AiFillIntField(false, null, 0);
        }

        var present = ReadPresent(field);
        int? value = null;
        if (field.TryGetProperty("value", out var raw) && raw.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            if (raw.ValueKind == JsonValueKind.Number && raw.TryGetInt32(out var n))
            {
                value = Math.Max(0, n);
            }
            else if (raw.ValueKind == JsonValueKind.String && int.TryParse(raw.GetString(), out var parsed))
            {
                value = Math.Max(0, parsed);
            }
        }

        if (value is null)
        {
            present = false;
        }

        return new AiFillIntField(present, present ? value : null, ReadFieldConfidence(field));
    }

    private static AiFillDateField ReadDate(JsonElement root, string name)
    {
        if (!TryGetField(root, name, out var field))
        {
            return new AiFillDateField(false, null, 0);
        }

        var present = ReadPresent(field);
        var raw = ReadOptionalString(field, "value");
        string? value = null;
        if (!string.IsNullOrWhiteSpace(raw)
            && DateTime.TryParse(raw, out var parsed))
        {
            value = parsed.ToString("yyyy-MM-dd");
        }
        else
        {
            present = false;
        }

        return new AiFillDateField(present, present ? value : null, ReadFieldConfidence(field));
    }

    private static AiFillTypeField ReadType(JsonElement root, IReadOnlyDictionary<string, Guid> types)
    {
        if (!TryGetField(root, "type", out var field))
        {
            return new AiFillTypeField(false, null, null, 0);
        }

        var present = ReadPresent(field);
        var name = ReadOptionalString(field, "value") ?? ReadOptionalString(field, "documentTypeName");
        if (!present || string.IsNullOrWhiteSpace(name))
        {
            return new AiFillTypeField(false, null, null, ReadFieldConfidence(field));
        }

        name = name.Trim();
        if (types.TryGetValue(name, out var id))
        {
            return new AiFillTypeField(true, id, CanonicalTypeName(types, id) ?? name, ReadFieldConfidence(field));
        }

        var alias = AliasType(name);
        if (alias is not null && types.TryGetValue(alias, out id))
        {
            return new AiFillTypeField(true, id, CanonicalTypeName(types, id) ?? alias, ReadFieldConfidence(field));
        }

        return new AiFillTypeField(false, null, null, ReadFieldConfidence(field));
    }

    private static string? CanonicalTypeName(IReadOnlyDictionary<string, Guid> types, Guid id) =>
        types.FirstOrDefault(x => x.Value == id).Key;

    private static string? AliasType(string value)
    {
        var n = value.Trim().ToLowerInvariant();
        if (n.Contains("replat") || n.Contains("plat"))
        {
            return "Plat";
        }

        if (n.Contains("survey"))
        {
            return "Survey";
        }

        if (n.Contains("subdivision") || n.Contains("subdiv"))
        {
            return "Subdivision";
        }

        if (n.Contains("deed") || n.Contains("quitclaim") || n.Contains("warranty"))
        {
            return "Deed";
        }

        if (n is "other")
        {
            return "Other";
        }

        return null;
    }

    private static bool TryGetField(JsonElement root, string name, out JsonElement field)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out field) && field.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        field = default;
        return false;
    }

    private static bool ReadPresent(JsonElement field) =>
        field.TryGetProperty("present", out var present)
        && present.ValueKind is JsonValueKind.True or JsonValueKind.False
        && present.GetBoolean();

    private static double ReadFieldConfidence(JsonElement field) =>
        Clamp(ReadConfidence(field, "confidence") ?? 0);

    private static double? ReadConfidence(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var raw) || raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (raw.ValueKind == JsonValueKind.Number && raw.TryGetDouble(out var n))
        {
            return n > 1 ? n / 100d : n;
        }

        if (raw.ValueKind == JsonValueKind.String && double.TryParse(raw.GetString(), out var parsed))
        {
            return parsed > 1 ? parsed / 100d : parsed;
        }

        return null;
    }

    private static string? ReadOptionalString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var raw) || raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return raw.ValueKind == JsonValueKind.String ? raw.GetString() : raw.ToString();
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, 1);

    private static DocumentDifficultyScore ReadDifficulty(
        JsonElement root,
        string extracted,
        AiFillFields fields,
        double overallConfidence)
    {
        string? band = null;
        string? why = null;
        List<string>? reasons = null;
        if (root.TryGetProperty("difficulty", out var difficulty) && difficulty.ValueKind == JsonValueKind.Object)
        {
            band = ReadOptionalString(difficulty, "band") ?? ReadOptionalString(difficulty, "value");
            why = ReadOptionalString(difficulty, "why") ?? ReadOptionalString(difficulty, "reason");
            if (difficulty.TryGetProperty("reasons", out var rawReasons) && rawReasons.ValueKind == JsonValueKind.Array)
            {
                reasons = [];
                foreach (var item in rawReasons.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var text = item.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            reasons.Add(text.Trim());
                        }
                    }
                }
            }
        }
        else
        {
            band = ReadOptionalString(root, "difficultyBand") ?? ReadOptionalString(root, "difficulty");
            why = ReadOptionalString(root, "difficultyWhy");
        }

        return DocumentDifficultyScorer.Score(extracted, fields, overallConfidence, band, why, reasons);
    }

    private async Task<AiFillResponse> PersistDifficultyAsync(
        Guid workItemId,
        string extracted,
        AiFillResponse response,
        bool rescore,
        CancellationToken cancellationToken)
    {
        var entity = await _db.WorkItems.FirstOrDefaultAsync(x => x.Id == workItemId, cancellationToken);
        if (entity is null)
        {
            return response;
        }

        var scored = DocumentDifficultyScorer.Score(
            extracted,
            response.Fields,
            response.OverallConfidence,
            response.Difficulty.Band,
            response.Difficulty.Why,
            response.Difficulty.Reasons);

        var hadOverride = entity.DifficultyOverridden;
        var keepOverride = hadOverride && !rescore;
        entity.ApplyAiDifficulty(scored.Band, scored.Why, replaceOverride: rescore);
        ApplyDeedPlatAi(entity, response.Fields);
        await _db.SaveChangesAsync(cancellationToken);

        return response with
        {
            Difficulty = ToDto(
                scored,
                overridden: entity.DifficultyOverridden,
                keptOverride: keepOverride,
                aiBand: entity.AiDifficultyBand,
                effectiveBand: entity.DifficultyBand,
                effectiveWhy: entity.DifficultyWhy)
        };
    }

    private static void ApplyDeedPlatAi(WorkItem item, AiFillFields fields)
    {
        if (DeedPlatFields.TryApplyAi(
                item.SurveyManual,
                item.Survey,
                fields.SurveyOrEmpty.Present,
                fields.SurveyOrEmpty.Value,
                DeedPlatFields.ShortMaxLength,
                out var survey))
        {
            item.Survey = survey;
        }

        if (DeedPlatFields.TryApplyAi(
                item.AbstractManual,
                item.Abstract,
                fields.AbstractOrEmpty.Present,
                fields.AbstractOrEmpty.Value,
                DeedPlatFields.ShortMaxLength,
                out var abstractValue))
        {
            item.Abstract = abstractValue;
        }

        if (DeedPlatFields.TryApplyAi(
                item.LotBlockManual,
                item.LotBlock,
                fields.LotBlockOrEmpty.Present,
                fields.LotBlockOrEmpty.Value,
                DeedPlatFields.ShortMaxLength,
                out var lotBlock))
        {
            item.LotBlock = lotBlock;
        }

        if (DeedPlatFields.TryApplyAi(
                item.SubdivisionManual,
                item.Subdivision,
                fields.SubdivisionOrEmpty.Present,
                fields.SubdivisionOrEmpty.Value,
                DeedPlatFields.ShortMaxLength,
                out var subdivision))
        {
            item.Subdivision = subdivision;
        }

        if (DeedPlatFields.TryApplyAi(
                item.LegalDescriptionManual,
                item.LegalDescription,
                fields.LegalDescriptionOrEmpty.Present,
                fields.LegalDescriptionOrEmpty.Value,
                DeedPlatFields.LegalDescriptionMaxLength,
                out var legal))
        {
            item.LegalDescription = legal;
        }
    }

    private static DocumentDifficulty ToDto(
        DocumentDifficultyScore scored,
        bool overridden,
        bool keptOverride,
        string? aiBand,
        string? effectiveBand = null,
        string? effectiveWhy = null)
    {
        var band = effectiveBand ?? scored.Band;
        var why = effectiveWhy ?? scored.Why;
        var reasons = DocumentDifficulty.SplitReasons(why);
        if (reasons.Count == 0)
        {
            reasons = scored.Reasons;
            why = scored.Why;
        }

        return new DocumentDifficulty(band, why, reasons, overridden, aiBand, keptOverride);
    }

    private static string UnwrapJson(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return text[start..(end + 1)];
            }
        }

        return text;
    }

    private async Task TryWriteAuditAsync(
        Guid workItemId,
        string extracted,
        string modelJson,
        AiFillResponse response,
        bool usedVision,
        int pageImageCount,
        CancellationToken cancellationToken)
    {
        try
        {
            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            var payload = JsonSerializer.Serialize(new
            {
                workItemId,
                at = DateTimeOffset.UtcNow,
                deployment = response.Deployment,
                usedVision,
                pageImageCount,
                extractedChars = extracted.Length,
                extractedPreview = extracted.Length <= 2000 ? extracted : extracted[..2000],
                modelJson,
                overallConfidence = response.OverallConfidence,
                difficulty = response.Difficulty
            }, JsonOptions);

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            await _storage.SaveRawAsync(
                $"ai-fill/{workItemId:N}/{stamp}.json",
                stream,
                "application/json",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI fill audit blob was not written for {WorkItemId}", workItemId);
        }
    }

    private async Task TryMarkScanAsync(
        Guid workItemId,
        string status,
        string? message,
        AiFillResponse? result,
        CancellationToken cancellationToken)
    {
        try
        {
            var entity = await _db.WorkItems.FirstOrDefaultAsync(x => x.Id == workItemId, cancellationToken);
            if (entity is null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            entity.AiScanStatus = status;
            entity.AiScanMessage = ClipScanMessage(message);
            if (status is WorkItemAiScanStatus.Running or WorkItemAiScanStatus.Pending)
            {
                entity.AiScanStartedAt ??= now;
                entity.AiScanCompletedAt = null;
            }
            else
            {
                entity.AiScanCompletedAt = now;
                entity.AiScanStartedAt ??= now;
            }

            if (result is not null)
            {
                entity.AiScanResultJson = WorkItemAiScanJson.SerializeResult(result);
            }
            else if (status is WorkItemAiScanStatus.Failed or WorkItemAiScanStatus.Unconfigured)
            {
                entity.AiScanResultJson = null;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist AI scan status {Status} for {WorkItemId}", status, workItemId);
        }
    }

    private static string SanitizeScanMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return WorkItemAiScanStatus.FailedMessage;
        }

        var text = message.Trim();
        if (text.Contains("no extractable text", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cannot be AI-filled", StringComparison.OrdinalIgnoreCase))
        {
            return WorkItemAiScanStatus.FailedMessage;
        }

        if (text.Contains("AI scan failed", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not configured", StringComparison.OrdinalIgnoreCase))
        {
            return ClipScanMessage(text) ?? WorkItemAiScanStatus.FailedMessage;
        }

        return ClipScanMessage($"AI scan failed. {text}") ?? WorkItemAiScanStatus.FailedMessage;
    }

    private static string? ClipScanMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }
}
