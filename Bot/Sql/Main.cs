using SQLite;

namespace Hyperstellar.Sql;

public sealed class Main(string id)
{
    [PrimaryKey, NotNull]
    public string MainId { get; set; } = id;

    [NotNull]
    public uint Donated { get; set; }

    [NotNull]
    public long Checked { get; set; }

    [Unique]
    public ulong? Discord { get; set; }

    public Main() : this("") { }

    public bool Delete() => Db.s_db.Delete(this) == 1;

    public bool Insert() => Db.s_db.Insert(this) == 1;

    public bool Update() => Db.s_db.Update(this) == 1;
}
