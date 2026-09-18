using FluentAssertions;
using GisDashboard.Application.AiFill;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.AiFill;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class DocumentDifficultyScorerTests
{
    [Fact]
    public void Lot_block_with_one_parcel_is_easy()
    {
        var fields = Fields(propertyIds: "R123", typeName: "Plat", platCount: 1);
        var score = DocumentDifficultyScorer.Score(
            "Final plat of the Northridge Addition, Lot 4, Block 2, City of Demo. Grantor sold to Grantee. One parcel, no easement.",
            fields,
            0.9,
            null,
            null,
            null);

        score.Band.Should().Be(DocumentDifficultyBands.Easy);
        score.Why.Should().NotBeNullOrWhiteSpace();
        score.Reasons.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public void Metes_and_bounds_with_easements_is_hard()
    {
        var fields = Fields(propertyIds: "R1\nR2\nR3\nR4", typeName: "Deed");
        var score = DocumentDifficultyScorer.Score(
            "Thence North 01 degrees 12 minutes East 240 feet to an iron pin. Subject to an easement and exception for a right of way. Grantor, Grantee, and Trustee.",
            fields,
            0.4,
            null,
            null,
            null);

        score.Band.Should().Be(DocumentDifficultyBands.Hard);
        score.Why.Should().NotBeNullOrWhiteSpace();
        score.Reasons.Should().Contain(reason =>
            reason.Contains("metes", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("parcel", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("Easement", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Model_band_is_used_and_empty_why_falls_back_to_signals()
    {
        var fields = Fields(propertyIds: "R123", typeName: "Plat", platCount: 1);
        var score = DocumentDifficultyScorer.Score(
            "Northridge Addition Lot 4 Block 2.",
            fields,
            0.88,
            "hard",
            null,
            null);

        score.Band.Should().Be(DocumentDifficultyBands.Hard);
        score.Why.Should().NotBeNullOrWhiteSpace();
        score.Reasons.Should().NotBeEmpty();
    }

    [Fact]
    public void Model_reasons_are_clipped_to_three()
    {
        var fields = Fields();
        var score = DocumentDifficultyScorer.Score(
            "Lot 1 Block 1 Addition",
            fields,
            0.7,
            "Medium",
            null,
            ["Readability is fine", "One parcel", "No easements", "Ignored fourth"]);

        score.Band.Should().Be(DocumentDifficultyBands.Medium);
        score.Reasons.Should().HaveCount(3);
        score.Why.Should().NotContain("Ignored fourth");
    }

    private static AiFillFields Fields(
        string? propertyIds = null,
        string? typeName = null,
        int? platCount = null) =>
        new(
            new AiFillStringField(true, "Sample title", 0.9),
            new AiFillTypeField(typeName is not null, typeName is null ? null : SeedIds.TypePlat, typeName, 0.9),
            new AiFillStringField(propertyIds is not null, propertyIds, propertyIds is null ? 0 : 0.8),
            new AiFillIntField(true, 0, 0.5),
            new AiFillIntField(false, null, 0),
            new AiFillIntField(true, 0, 0.5),
            new AiFillIntField(platCount is not null, platCount, platCount is null ? 0 : 0.8),
            new AiFillDateField(true, "2026-03-15", 0.6));
}
