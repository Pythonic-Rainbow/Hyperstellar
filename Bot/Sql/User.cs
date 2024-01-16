using SQLite;

namespace Hyperstellar.Sql;
internal class User
{
    [PrimaryKey]
    public string CocId { get; set; }
    public ulong? DiscordId;
}
