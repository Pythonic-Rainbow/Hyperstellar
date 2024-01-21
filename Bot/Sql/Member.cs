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

    public bool IsAlt()
    {
        TableQuery<Alt> result = Db.s_db.Table<Alt>().Where(a => a.AltId == CocId);
        return result.Count() > 0;
    }

    public bool IsMain()
    {
        TableQuery<Alt> result = Db.s_db.Table<Alt>().Where(a => a.MainId == CocId);
        return result.Count() > 0;
    }
}
