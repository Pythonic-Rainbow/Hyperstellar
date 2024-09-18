using SQLite;

namespace Hyperstellar.Sql;

public sealed class Main(string id) : DbObj
{
    [PrimaryKey, NotNull]
    public string MainId { get; set; } = id;

    [NotNull]
    public uint Donated { get; set; }

    [NotNull]
    public long Checked { get; set; }

    [Unique]
    public ulong? Discord { get; set; }

    [NotNull]
    public uint Raided { get; set; }

    public Main() : this("") { }

    internal static Main? TryFetch(string id) => s_db.Table<Main>().FirstOrDefault(d => d.MainId == id);

    public bool Update() => Db.s_db.Update(this) == 1;
}
