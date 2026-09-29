using System.Text.Json;
using FluentAssertions;
using SQLModelViewer.Core.Rendering;

namespace SQLModelViewer.Core.Tests.Rendering;

public class HtmlDiagramGeneratorTests
{
    private static DiagramViewModel SmallModel() => new(
        ServerName: "test-server",
        DatabaseName: "test-db",
        GeneratedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Schemas: [new SchemaGroupDto("dbo", "dbo", 0, 2)],
        Tables:
        [
            new TableDto("dbo.Parent", "dbo", "Parent", 0, 0, "A parent table.", 10,
                [new ColumnDto("Id", "int", false, false, true, false, null)]),
            new TableDto("dbo.Child", "dbo", "Child", 400, 0, null, null,
                [
                    new ColumnDto("Id", "int", false, false, true, false, null),
                    new ColumnDto("ParentId", "int", false, false, false, true, null)
                ])
        ],
        ForeignKeys: [new ForeignKeyDto("FK_Child_Parent", "dbo.Child", "dbo.Parent", [new ForeignKeyColumnPairDto("ParentId", "Id")], false, false)],
        Gaps: []);

    [Fact]
    public void GenerateFromViewModel_replaces_the_placeholder_and_produces_well_formed_html()
    {
        var html = new HtmlDiagramGenerator().GenerateFromViewModel(SmallModel());

        html.Should().StartWith("<!doctype html>");
        html.Should().Contain("<script>");
        html.Should().Contain("</script>");
        html.Should().NotContain("/*__DIAGRAM_DATA__*/");
        html.Should().Contain("DATA = {");
    }

    [Fact]
    public void GenerateFromViewModel_injected_json_round_trips_to_the_same_counts()
    {
        var vm = SmallModel();
        var html = new HtmlDiagramGenerator().GenerateFromViewModel(vm);

        var match = System.Text.RegularExpressions.Regex.Match(html, @"\r?\nDATA = (\{.*?\});\r?\n", System.Text.RegularExpressions.RegexOptions.Singleline);
        match.Success.Should().BeTrue("the generated HTML should contain a 'DATA = {...};' assignment");

        var roundTripped = JsonSerializer.Deserialize<DiagramViewModel>(match.Groups[1].Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        roundTripped.Should().NotBeNull();
        roundTripped!.Tables.Should().HaveCount(vm.Tables.Count);
        roundTripped.ForeignKeys.Should().HaveCount(vm.ForeignKeys.Count);
        roundTripped.DatabaseName.Should().Be(vm.DatabaseName);
    }

    [Fact]
    public void GenerateFromViewModel_output_is_stable_for_a_fixed_input()
    {
        var html1 = new HtmlDiagramGenerator().GenerateFromViewModel(SmallModel());
        var html2 = new HtmlDiagramGenerator().GenerateFromViewModel(SmallModel());

        html1.Should().Be(html2, "generation should be a pure function of its input");
    }
}
