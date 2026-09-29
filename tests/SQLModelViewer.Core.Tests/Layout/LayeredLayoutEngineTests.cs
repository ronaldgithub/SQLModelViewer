using FluentAssertions;
using SQLModelViewer.Core.Domain;
using SQLModelViewer.Core.Layout;

namespace SQLModelViewer.Core.Tests.Layout;

public class LayeredLayoutEngineTests
{
    private const double W = 260, HEAD = 34, ROW = 22, PAD = 6;

    private static Table MakeTable(string schema, string name, int columnCount = 1) =>
        new(schema, name, Enumerable.Range(0, columnCount).Select(i => new Column($"c{i}", i, "int", null, null, null, true, false, false, null)).ToList(), [], null, null);

    private static ForeignKey Fk(string childSchema, string child, string parentSchema, string parent) =>
        new($"FK_{child}_{parent}", childSchema, child, parentSchema, parent, [new ForeignKeyColumnPair("c0", "c0")]);

    private static Dictionary<string, TablePosition> ByKey(LayoutResult result) =>
        result.Positions.ToDictionary(p => $"{p.Schema}.{p.Name}");

    private static void AssertNoOverlaps(SchemaModel model, LayoutResult result)
    {
        var heights = model.Tables.ToDictionary(t => t.QualifiedName, t => HEAD + t.Columns.Count * ROW + PAD);
        var boxes = result.Positions.Select(p => (
            Key: $"{p.Schema}.{p.Name}",
            X0: p.X, Y0: p.Y, X1: p.X + W, Y1: p.Y + heights[$"{p.Schema}.{p.Name}"]
        )).ToList();

        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i];
                var b = boxes[j];
                var overlap = a.X0 < b.X1 && a.X1 > b.X0 && a.Y0 < b.Y1 && a.Y1 > b.Y0;
                overlap.Should().BeFalse($"{a.Key} and {b.Key} should not overlap");
            }
        }
    }

    [Fact]
    public void Layout_places_linear_chain_left_to_right_with_child_before_parent()
    {
        // A -> B -> C (A is a child of B, B is a child of C)
        var tables = new[] { MakeTable("dbo", "A"), MakeTable("dbo", "B"), MakeTable("dbo", "C") };
        var fks = new[] { Fk("dbo", "A", "dbo", "B"), Fk("dbo", "B", "dbo", "C") };
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, tables, fks, []);

        var result = new LayeredLayoutEngine().Layout(model);
        var pos = ByKey(result);

        pos["dbo.A"].X.Should().BeLessThan(pos["dbo.B"].X);
        pos["dbo.B"].X.Should().BeLessThan(pos["dbo.C"].X);
        AssertNoOverlaps(model, result);
    }

    [Fact]
    public void Layout_handles_diamond_shape_without_overlap()
    {
        // A -> B -> D, A -> C -> D
        var tables = new[] { MakeTable("dbo", "A"), MakeTable("dbo", "B"), MakeTable("dbo", "C"), MakeTable("dbo", "D") };
        var fks = new[] { Fk("dbo", "A", "dbo", "B"), Fk("dbo", "A", "dbo", "C"), Fk("dbo", "B", "dbo", "D"), Fk("dbo", "C", "dbo", "D") };
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, tables, fks, []);

        var result = new LayeredLayoutEngine().Layout(model);
        var pos = ByKey(result);

        pos["dbo.A"].X.Should().BeLessThan(pos["dbo.B"].X);
        pos["dbo.A"].X.Should().BeLessThan(pos["dbo.C"].X);
        pos["dbo.B"].X.Should().BeLessThan(pos["dbo.D"].X);
        pos["dbo.C"].X.Should().BeLessThan(pos["dbo.D"].X);
        AssertNoOverlaps(model, result);
    }

    [Fact]
    public void Layout_terminates_and_does_not_overlap_on_a_three_node_cycle()
    {
        // A -> B -> C -> A
        var tables = new[] { MakeTable("dbo", "A"), MakeTable("dbo", "B"), MakeTable("dbo", "C") };
        var fks = new[] { Fk("dbo", "A", "dbo", "B"), Fk("dbo", "B", "dbo", "C"), Fk("dbo", "C", "dbo", "A") };
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, tables, fks, []);

        var act = () => new LayeredLayoutEngine().Layout(model);
        var result = act.Should().NotThrow().Which;

        result.Positions.Should().HaveCount(3);
        AssertNoOverlaps(model, result);
    }

    [Fact]
    public void Layout_places_self_referencing_table_without_throwing()
    {
        var tables = new[] { MakeTable("dbo", "Employee") };
        var fks = new[] { Fk("dbo", "Employee", "dbo", "Employee") };
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, tables, fks, []);

        var result = new LayeredLayoutEngine().Layout(model);

        result.Positions.Should().ContainSingle();
    }

    [Fact]
    public void Layout_places_disconnected_components_without_overlap()
    {
        var tables = new[] { MakeTable("dbo", "A"), MakeTable("dbo", "B"), MakeTable("dbo", "Lonely1"), MakeTable("dbo", "Lonely2") };
        var fks = new[] { Fk("dbo", "A", "dbo", "B") };
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, tables, fks, []);

        var result = new LayeredLayoutEngine().Layout(model);

        result.Positions.Should().HaveCount(4);
        AssertNoOverlaps(model, result);
    }

    [Fact]
    public void Layout_handles_multi_schema_graph_without_overlap()
    {
        var tables = new[]
        {
            MakeTable("sales", "Order"), MakeTable("sales", "OrderLine"),
            MakeTable("dbo", "Product"), MakeTable("dbo", "Category"),
        };
        var fks = new[]
        {
            Fk("sales", "OrderLine", "sales", "Order"),
            Fk("sales", "OrderLine", "dbo", "Product"),
            Fk("dbo", "Product", "dbo", "Category"),
        };
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, tables, fks, []);

        var result = new LayeredLayoutEngine().Layout(model);

        result.Positions.Should().HaveCount(4);
        AssertNoOverlaps(model, result);
    }

    [Fact]
    public void Layout_returns_empty_result_for_empty_model()
    {
        var model = new SchemaModel("srv", "db", DateTime.UtcNow, [], [], []);

        var result = new LayeredLayoutEngine().Layout(model);

        result.Positions.Should().BeEmpty();
    }
}
