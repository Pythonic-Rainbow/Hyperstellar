using SQLite;

namespace Hyperstellar.Sql;

public sealed class Alt(string altId, string mainId)
{
    [PrimaryKey, NotNull]
    public string AltId { get; set; } = altId;

    [NotNull]
    public string MainId { get; set; } = mainId;

    public Alt() : this("", "") { }

    public TableQuery<Alt> GetOtherAlts() => Db.s_db.Table<Alt>().Where(a => a.MainId == MainId && a.AltId != AltId);

    public Main GetMain() => Db.s_db.Table<Main>().Where(m => m.MainId == MainId).First();

    public bool UpdateMain(string id)
    {
        MainId = id;
        return Db.s_db.Update(this) == 1;
    }

    public bool Delete() => Db.s_db.Delete(this) == 1;
}
