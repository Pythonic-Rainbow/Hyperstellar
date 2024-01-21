using SQLite;

namespace Hyperstellar.Sql;
public class Member
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

    public void AddAlt(Member altMember)
    {
        Alt alt = new(altMember.CocId, CocId);
        Db.s_db.Insert(alt);
    }
}
