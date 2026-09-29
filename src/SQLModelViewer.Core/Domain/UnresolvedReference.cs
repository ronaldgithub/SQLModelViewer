namespace SQLModelViewer.Core.Domain;

public enum GapReason
{
    SelfReferencing,
    NameLooksLikeFkNoConstraintFound
}

public sealed record UnresolvedReference(
    string TableSchema,
    string TableName,
    string ColumnName,
    GapReason Reason,
    string Message);
