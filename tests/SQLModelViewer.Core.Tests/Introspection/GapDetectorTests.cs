using FluentAssertions;
using SQLModelViewer.Core.Domain;
using SQLModelViewer.Core.Introspection;

namespace SQLModelViewer.Core.Tests.Introspection;

public class GapDetectorTests
{
    private static Table MakeTable(string name, params string[] columnNames) =>
        new("dbo", name, columnNames.Select(c => new Column(c, 1, "int", null, null, null, true, false, false, null)).ToList(), [], null, null);

    [Fact]
    public void Detect_flags_self_referencing_foreign_key()
    {
        var tables = new[] { MakeTable("Employee", "EmployeeId", "ManagerId") };
        var fks = new[]
        {
            new ForeignKey("FK_Employee_Manager", "dbo", "Employee", "dbo", "Employee", [new ForeignKeyColumnPair("ManagerId", "EmployeeId")]),
        };

        var gaps = GapDetector.Detect(tables, fks);

        gaps.Should().ContainSingle(g => g.Reason == GapReason.SelfReferencing && g.TableName == "Employee");
    }

    [Fact]
    public void Detect_flags_fk_shaped_column_with_exact_matching_table_and_no_constraint()
    {
        var tables = new[]
        {
            MakeTable("Person", "PersonId", "VestigingId"),
            MakeTable("Vestiging", "VestigingId"),
        };

        var gaps = GapDetector.Detect(tables, []);

        gaps.Should().ContainSingle(g =>
            g.Reason == GapReason.NameLooksLikeFkNoConstraintFound &&
            g.TableName == "Person" && g.ColumnName == "VestigingId");
    }

    [Fact]
    public void Detect_flags_fk_shaped_column_against_pluralized_table_name()
    {
        var tables = new[]
        {
            MakeTable("Comment", "CommentId", "UserId"),
            MakeTable("Users", "Id"),
        };

        var gaps = GapDetector.Detect(tables, []);

        gaps.Should().ContainSingle(g => g.ColumnName == "UserId" && g.Message.Contains("Users"));
    }

    [Fact]
    public void Detect_does_not_flag_role_prefixed_fk_shaped_columns()
    {
        // Known, deliberate limitation (matches the hand-curated example's own caveat: "Role-named
        // keys such as operatorid were not scanned") — matching "OwnerUserId" to "Users" would need
        // NLP-ish prefix stripping that's out of scope for this heuristic.
        var tables = new[]
        {
            MakeTable("Posts", "Id", "OwnerUserId"),
            MakeTable("Users", "Id"),
        };

        var gaps = GapDetector.Detect(tables, []);

        gaps.Should().BeEmpty();
    }

    [Fact]
    public void Detect_flags_fk_shaped_column_against_ies_pluralized_table_name()
    {
        var tables = new[]
        {
            MakeTable("Order", "OrderId", "CompanyId"),
            MakeTable("Companies", "CompanyId"),
        };

        var gaps = GapDetector.Detect(tables, []);

        gaps.Should().ContainSingle(g => g.ColumnName == "CompanyId" && g.Message.Contains("Companies"));
    }

    [Fact]
    public void Detect_does_not_flag_column_already_covered_by_a_resolved_foreign_key()
    {
        var tables = new[]
        {
            MakeTable("Person", "PersonId", "VestigingId"),
            MakeTable("Vestiging", "VestigingId"),
        };
        var fks = new[] { new ForeignKey("FK_Person_Vestiging", "dbo", "Person", "dbo", "Vestiging", [new ForeignKeyColumnPair("VestigingId", "VestigingId")]) };

        var gaps = GapDetector.Detect(tables, fks);

        gaps.Should().BeEmpty();
    }

    [Fact]
    public void Detect_does_not_flag_fk_shaped_column_with_no_matching_table()
    {
        // "userid" ends in "id" but there is no "user"/"users" table in this model — should not be flagged.
        var tables = new[] { MakeTable("Session", "SessionId", "UserId") };

        var gaps = GapDetector.Detect(tables, []);

        gaps.Should().BeEmpty();
    }

    [Fact]
    public void Detect_does_not_flag_columns_that_do_not_end_in_id()
    {
        var tables = new[] { MakeTable("Account", "AccountId", "ValidUntil", "Grid") };

        var gaps = GapDetector.Detect(tables, []);

        gaps.Should().BeEmpty();
    }

    [Fact]
    public void Detect_does_not_flag_the_tables_own_primary_key_column()
    {
        var pkColumn = new Column("VestigingId", 1, "int", null, null, null, false, true, false, null);
        var table = new Table("dbo", "Vestiging", [pkColumn], ["VestigingId"], null, null);

        var gaps = GapDetector.Detect([table], []);

        gaps.Should().BeEmpty();
    }
}
