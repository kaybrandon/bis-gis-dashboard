using GisDashboard.Application.WorkItems;

namespace GisDashboard.Application.AiFill;

public sealed record AiFillResponse(
    double OverallConfidence,
    string Deployment,
    string? Warning,
    AiFillFields Fields,
    DocumentDifficulty Difficulty);

public sealed record AiFillFields(
    AiFillStringField Title,
    AiFillTypeField Type,
    AiFillStringField PropertyIds,
    AiFillIntField AnnexationCount,
    AiFillIntField CorrectionCount,
    AiFillIntField DeedCount,
    AiFillIntField PlatCount,
    AiFillDateField WorkedOn);

public sealed record AiFillStringField(bool Present, string? Value, double Confidence);

public sealed record AiFillIntField(bool Present, int? Value, double Confidence);

public sealed record AiFillDateField(bool Present, string? Value, double Confidence);

public sealed record AiFillTypeField(
    bool Present,
    Guid? DocumentTypeId,
    string? DocumentTypeName,
    double Confidence);
