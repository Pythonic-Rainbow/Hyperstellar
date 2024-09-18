using SQLite;

namespace Hyperstellar.Sql;

internal sealed class BotAdmin(ulong id) : DbObj
{
    internal static readonly HashSet<ulong> s_admins = s_db.Table<BotAdmin>().Select(a => a.Id).ToHashSet();


    [PrimaryKey, NotNull]
    public ulong Id { get; set; } = id;

    public BotAdmin() : this(0) { }

    internal override int Insert()
    {
        int count = base.Insert();
        s_admins.Add(Id);
        return count;
    }
}
