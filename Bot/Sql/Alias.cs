using SQLite;

namespace Hyperstellar.Sql;

[Table("Alias")]
public sealed class CocMemberAlias(string alias, string cocId)
{
    [PrimaryKey, NotNull]
    public string Alias { get; set; } = alias;

    [NotNull]
    public string CocId { get; set; } = cocId;

    public CocMemberAlias() : this("", "") { }
}
