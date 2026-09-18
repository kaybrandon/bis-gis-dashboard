using FluentAssertions;
using GisDashboard.Infrastructure.AiFill;

namespace GisDashboard.Tests;

public sealed class PropertyIdsNormalizerTests
{
    [Fact]
    public void Keeps_one_or_two_clear_subject_pids()
    {
        var field = PropertyIdsNormalizer.Normalize(true, "R123\nR456", 0.9);
        field.Present.Should().BeTrue();
        field.Value.Should().Be("R123\nR456");
        field.Confidence.Should().BeApproximately(0.9, 0.001);
    }

    [Fact]
    public void Unwraps_json_array_string_into_line_separated_ids()
    {
        var field = PropertyIdsNormalizer.Normalize(true, """["11402", "11418"]""", 0.88);
        field.Present.Should().BeTrue();
        field.Value.Should().Be("11402\n11418");
        field.Value.Should().NotContain("[");
    }

    [Fact]
    public void Cad_web_map_vacuum_is_dropped_even_at_high_confidence()
    {
        var labels = string.Join('\n', Enumerable.Range(11400, 24).Select(n => n.ToString()));
        var field = PropertyIdsNormalizer.Normalize(true, labels, 0.95);
        field.Present.Should().BeFalse();
        field.Value.Should().BeNull();
        field.Confidence.Should().BeLessThanOrEqualTo(0.35);
    }

    [Fact]
    public void Json_blob_of_map_labels_is_not_left_in_the_field()
    {
        var blob = "[" + string.Join(',', Enumerable.Range(11402, 20).Select(n => $"\"{n}\"")) + "]";
        var field = PropertyIdsNormalizer.Normalize(true, blob, 0.95);
        field.Present.Should().BeFalse();
        field.Value.Should().BeNull();
        field.Value.Should().NotContain("11402");
    }

    [Fact]
    public void Six_numeric_map_labels_count_as_a_vacuum()
    {
        var field = PropertyIdsNormalizer.Normalize(true, "11401\n11402\n11403\n11404\n11405\n11406", 0.92);
        field.Present.Should().BeFalse();
        PropertyIdsNormalizer.IsMassDump(PropertyIdsNormalizer.ParseLines("11401\n11402\n11403\n11404\n11405\n11406"))
            .Should().BeTrue();
    }
}
