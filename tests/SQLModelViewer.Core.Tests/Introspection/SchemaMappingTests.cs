using FluentAssertions;
using SQLModelViewer.Core.Introspection;

namespace SQLModelViewer.Core.Tests.Introspection;

public class SchemaMappingTests
{
    [Fact]
    public void Assemble_maps_plain_columns_pk_and_descriptions()
    {
        var tables = new[] { new TableRow(1, "dbo", "Customer") };
        var columns = new[]
        {
            new ColumnRow(1, 1, "CustomerId", 1, "int", null, 10, 0, false, true, false),
            new ColumnRow(1, 2, "Name", 2, "nvarchar", 100, null, null, false, false, false),
        };
        var pks = new[] { new PrimaryKeyRow(1, "CustomerId", 1) };
        var descriptions = new[]
        {
            new ExtendedPropertyRow(1, 0, "A customer account."),
            new ExtendedPropertyRow(1, 2, "The customer's display name."),
        };

        var result = SchemaAssembler.Assemble(tables, columns, pks, [], descriptions, []);

        result.Tables.Should().HaveCount(1);
        var table = result.Tables[0];
        table.Description.Should().Be("A customer account.");
        table.PrimaryKeyColumns.Should().Equal("CustomerId");
        table.Columns.Should().HaveCount(2);
        table.Columns[0].Name.Should().Be("CustomerId");
        table.Columns[0].IsIdentity.Should().BeTrue();
        table.Columns[1].Description.Should().Be("The customer's display name.");
        table.Columns[1].TypeLabel.Should().Be("nvarchar(100)");
    }

    [Fact]
    public void Assemble_builds_composite_foreign_key_from_multiple_column_rows()
    {
        var tables = new[]
        {
            new TableRow(1, "dbo", "OrderLine"),
            new TableRow(2, "dbo", "PriceList"),
        };
        var fkRows = new[]
        {
            new ForeignKeyRow(100, "FK_OrderLine_PriceList", 1, 2, 1, "PriceListId", "PriceListId", "dbo", "OrderLine", "dbo", "PriceList"),
            new ForeignKeyRow(100, "FK_OrderLine_PriceList", 1, 2, 2, "PriceListVersion", "Version", "dbo", "OrderLine", "dbo", "PriceList"),
        };

        var result = SchemaAssembler.Assemble(tables, [], [], fkRows, [], []);

        result.ForeignKeys.Should().HaveCount(1);
        var fk = result.ForeignKeys[0];
        fk.Columns.Should().HaveCount(2);
        fk.Columns[0].Should().Be(new SQLModelViewer.Core.Domain.ForeignKeyColumnPair("PriceListId", "PriceListId"));
        fk.Columns[1].Should().Be(new SQLModelViewer.Core.Domain.ForeignKeyColumnPair("PriceListVersion", "Version"));
        fk.IsSelfReferencing.Should().BeFalse();
    }

    [Fact]
    public void Assemble_flags_self_referencing_foreign_key()
    {
        var tables = new[] { new TableRow(1, "dbo", "Employee") };
        var fkRows = new[]
        {
            new ForeignKeyRow(200, "FK_Employee_Manager", 1, 1, 1, "ManagerId", "EmployeeId", "dbo", "Employee", "dbo", "Employee"),
        };

        var result = SchemaAssembler.Assemble(tables, [], [], fkRows, [], []);

        result.ForeignKeys.Single().IsSelfReferencing.Should().BeTrue();
    }

    [Fact]
    public void Assemble_maps_row_counts_when_present_and_leaves_null_when_absent()
    {
        var tables = new[]
        {
            new TableRow(1, "dbo", "WithCount"),
            new TableRow(2, "dbo", "WithoutCount"),
        };
        var rowCounts = new[] { new RowCountRow(1, 42) };

        var result = SchemaAssembler.Assemble(tables, [], [], [], [], rowCounts);

        result.Tables.Single(t => t.Name == "WithCount").RowCount.Should().Be(42);
        result.Tables.Single(t => t.Name == "WithoutCount").RowCount.Should().BeNull();
    }

    [Theory]
    [InlineData("decimal", null, 18, 2, "decimal(18,2)")]
    [InlineData("nchar", 10, null, null, "nchar(10)")]
    [InlineData("varchar", -1, null, null, "varchar(max)")]
    [InlineData("int", null, null, null, "int")]
    [InlineData("uniqueidentifier", null, null, null, "uniqueidentifier")]
    public void Column_TypeLabel_formats_common_sql_types(string sqlType, int? maxLength, int? precision, int? scale, string expected)
    {
        var column = new SQLModelViewer.Core.Domain.Column("c", 1, sqlType, maxLength, precision, scale, true, false, false, null);
        column.TypeLabel.Should().Be(expected);
    }
}
