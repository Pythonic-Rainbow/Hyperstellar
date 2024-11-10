using SQLite;

namespace Hyperstellar.Sql;

public sealed class Alt(string altId, string mainId) : DbObj<Alt>
{
    [PrimaryKey, NotNull]
    public string AltId { get; set; } = altId;

    [NotNull]
    public string MainId { get; set; } = mainId;

    public Alt() : this("", "") { }

    public TableQuery<Alt> GetOtherAlts() => FetchAll().Where(a => a.MainId == MainId && a.AltId != AltId);

    public Main GetMain() => Main.FetchAll().First(m => m.AccountId == MainId);

    public bool UpdateMain(string id)
    {
        MainId = id;
        return s_db.Update(this) == 1;
    }
}
