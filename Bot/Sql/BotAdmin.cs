using SQLite;

namespace Hyperstellar.Sql;

internal sealed class BotAdmin
{
    [PrimaryKey, NotNull]
    public ulong Id { get; set; }

    public BotAdmin() => Id = 0;

    public BotAdmin(ulong id) => Id = id;
}
