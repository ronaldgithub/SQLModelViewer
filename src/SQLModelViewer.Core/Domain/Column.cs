namespace SQLModelViewer.Core.Domain;

public sealed record Column(
    string Name,
    int OrdinalPosition,
    string SqlTypeName,
    int? MaxLength,
    int? Precision,
    int? Scale,
    bool IsNullable,
    bool IsIdentity,
    bool IsComputed,
    string? Description)
{
    public string TypeLabel
    {
        get
        {
            var t = SqlTypeName;
            return t switch
            {
                "nvarchar" or "nchar" or "varchar" or "char" or "varbinary" or "binary"
                    when MaxLength is int m => m == -1 ? $"{t}(max)" : $"{t}({m})",
                "decimal" or "numeric" when Precision is int p && Scale is int s => $"{t}({p},{s})",
                _ => t
            };
        }
    }
}
