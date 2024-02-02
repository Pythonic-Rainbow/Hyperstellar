using SQLite;

namespace Hyperstellar.Sql;

internal sealed class Donation(string id, long checkTime)
{
    [PrimaryKey, NotNull]
    public string MainId { get; set; } = id;

    [NotNull]
    public uint Donated { get; set; } = 0;

    [NotNull]
    public long Checked { get; set; } = checkTime;

    public Donation() : this("") { }

    public Donation(string id) : this(id, DateTimeOffset.UtcNow.ToUnixTimeSeconds()) { }

    public bool Delete() => Db.s_db.Delete(this) == 1;

    public bool Insert() => Db.s_db.Insert(this) == 1;

    public bool Update() => Db.s_db.Update(this) == 1;
}
