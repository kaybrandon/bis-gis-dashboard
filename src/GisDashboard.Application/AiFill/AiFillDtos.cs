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
    AiFillDateField WorkedOn,
    AiFillStringField? Survey = null,
    AiFillStringField? Abstract = null,
    AiFillStringField? LotBlock = null,
    AiFillStringField? Subdivision = null,
    AiFillStringField? LegalDescription = null)
{
    public AiFillStringField SurveyOrEmpty => Survey ?? EmptyString;
    public AiFillStringField AbstractOrEmpty => Abstract ?? EmptyString;
    public AiFillStringField LotBlockOrEmpty => LotBlock ?? EmptyString;
    public AiFillStringField SubdivisionOrEmpty => Subdivision ?? EmptyString;
    public AiFillStringField LegalDescriptionOrEmpty => LegalDescription ?? EmptyString;

    private static readonly AiFillStringField EmptyString = new(false, null, 0);
}

public sealed record AiFillStringField(bool Present, string? Value, double Confidence);

public sealed record AiFillIntField(bool Present, int? Value, double Confidence);

public sealed record AiFillDateField(bool Present, string? Value, double Confidence);

public sealed record AiFillTypeField(
    bool Present,
    Guid? DocumentTypeId,
    string? DocumentTypeName,
    double Confidence);

public sealed record AiFillVisionImage(byte[] Bytes, string MediaType, int PageNumber);
