using SQLite;

namespace Hyperstellar.Sql;

internal sealed class Alt
{
    [PrimaryKey, NotNull]
    public string AltId { get; set; }

    [NotNull]
    public string MainId { get; set; }

    public Alt() => AltId = MainId = "";

    public Alt(string altId, string mainId)
    {
        AltId = altId;
        MainId = mainId;
    }
}
