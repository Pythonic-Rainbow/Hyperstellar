using SQLite;

namespace Hyperstellar.Sql;

[Table("Alias")]
public sealed class CocAlias(string alias, string cocId) : DbObj<CocAlias>
{
    private string _alias = alias.ToLower();

    [PrimaryKey, NotNull]
    public string Alias
    {
        get => _alias;
        set => _alias = value.ToLower();
    }

    [NotNull]
    public string CocId { get; set; } = cocId;

    public CocAlias() : this("", "") { }

    internal static CocAlias? TryFetch(string alias)
    {
        alias = alias.ToLower();
        return FetchAll().FirstOrDefault(a => a.Alias == alias);
    }
}
