using FluentAssertions;
using SQLModelViewer.Core.Domain;
using SQLModelViewer.Core.Layout;
using SQLModelViewer.Core.Rendering;

namespace SQLModelViewer.Core.Tests.Rendering;

public class JsonPayloadBuilderTests
{
    private static Column Col(string name, bool nullable = true) => new(name, 1, "int", null, null, null, nullable, false, false, null);

    [Fact]
    public void Build_flags_pk_and_fk_columns_correctly()
    {
        var parent = new Table("dbo", "Parent", [Col("Id", false)], ["Id"], null, null);
        var child = new Table("dbo", "Child", [Col("Id", false), Col("ParentId")], ["Id"], null, null);
        var fk = new ForeignKey("FK_Child_Parent", "dbo", "Child", "dbo", "Parent", [new ForeignKeyColumnPair("ParentId", "Id")]);
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, [parent, child], [fk], []);
        var layout = new LayeredLayoutEngine().Layout(model);

        var vm = JsonPayloadBuilder.Build(model, layout);

        var childDto = vm.Tables.Single(t => t.Name == "Child");
        childDto.Columns.Single(c => c.Name == "Id").IsPk.Should().BeTrue();
        childDto.Columns.Single(c => c.Name == "ParentId").IsFk.Should().BeTrue();
        vm.ForeignKeys.Should().ContainSingle(f => f.Child == "dbo.Child" && f.Parent == "dbo.Parent");
    }

    [Fact]
    public void Build_drops_foreign_keys_pointing_outside_the_selection_and_reports_them_as_external()
    {
        var child = new Table("dbo", "Child", [Col("Id", false), Col("ParentId")], ["Id"], null, null);
        var fk = new ForeignKey("FK_Child_Parent", "dbo", "Child", "dbo", "Parent", [new ForeignKeyColumnPair("ParentId", "Id")]);
        // "Parent" is referenced but not part of the introspected/selected table set.
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, [child], [fk], []);
        var layout = new LayeredLayoutEngine().Layout(model);

        var vm = JsonPayloadBuilder.Build(model, layout);

        vm.ForeignKeys.Should().BeEmpty();
        vm.Gaps.Should().ContainSingle(g => g.Reason == "ExternalReference" && g.Table == "dbo.Child");
    }

    [Fact]
    public void Build_assigns_distinct_hues_to_each_schema_group()
    {
        var t1 = new Table("sales", "Order", [Col("Id", false)], ["Id"], null, null);
        var t2 = new Table("dbo", "Product", [Col("Id", false)], ["Id"], null, null);
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, [t1, t2], [], []);
        var layout = new LayeredLayoutEngine().Layout(model);

        var vm = JsonPayloadBuilder.Build(model, layout);

        vm.Schemas.Should().HaveCount(2);
        vm.Schemas.Select(s => s.Hue).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void Build_carries_gaps_through_unchanged()
    {
        var t = new Table("dbo", "Employee", [Col("Id", false), Col("ManagerId")], ["Id"], null, null);
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, [t], [],
            [new UnresolvedReference("dbo", "Employee", "ManagerId", GapReason.SelfReferencing, "test message")]);
        var layout = new LayeredLayoutEngine().Layout(model);

        var vm = JsonPayloadBuilder.Build(model, layout);

        vm.Gaps.Should().ContainSingle(g => g.Message == "test message" && g.Reason == "SelfReferencing");
    }
}
