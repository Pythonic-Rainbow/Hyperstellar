using SQLite;

namespace Hyperstellar.Sql;
internal class Member
{
    [PrimaryKey, NotNull]
    public string CocId { get; set; }
    [Unique]
    public ulong? DiscordId { get; set; }

    public Member() => CocId = "";

    public Member(string cocId) => CocId = cocId;

    public Member(string CocId, ulong DiscordId)
    {
        this.CocId = CocId;
        this.DiscordId = DiscordId;
    }

    public void AddAlt(string altId)
    {
        Alt alt = new(altId, CocId);
        Db.s_db.Insert(alt);
    }
}
