using SQLite;

namespace Hyperstellar.Sql;

internal sealed class BotAdmin(ulong id)
{
    [PrimaryKey, NotNull]
    public ulong Id { get; set; } = id;

    public BotAdmin() : this(0) { }
}
