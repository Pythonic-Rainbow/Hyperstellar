using SQLite;

namespace Hyperstellar.Sql;

internal sealed class Alt(string altId, string mainId)
{
    [PrimaryKey, NotNull]
    public string AltId { get; set; } = altId;

    [NotNull]
    public string MainId { get; set; } = mainId;

    public Alt() : this("", "") { }
}
