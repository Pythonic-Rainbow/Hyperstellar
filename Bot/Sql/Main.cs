using SQLite;

namespace Hyperstellar.Sql;
public sealed class Main(string id) : DbObj<Main>
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

    internal static Main? TryFetch(string id) => FetchAll().FirstOrDefault(d => d.MainId == id);

    internal static Main? TryFetchByDiscord(ulong uid) => FetchAll().FirstOrDefault(m => m.Discord == uid);
}
