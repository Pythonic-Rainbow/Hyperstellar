using Hyperstellar.Clash;
using SQLite;

namespace Hyperstellar.Sql;
public class Member
{
    [PrimaryKey, NotNull]
    public string CocId { get; set; }

    [Unique]
    public ulong? DiscordId { get; set; }

    public Member() : this("") { }

    public Member(string cocId) => CocId = cocId;

    public Member(string cocId, ulong discordId)
    {
        CocId = cocId;
        DiscordId = discordId;
    }

    public void AddAlt(Member altMember)
    {
        Alt alt = new(altMember.CocId, CocId);
        Donate25.AltAdded(altMember.CocId, CocId);
        Db.s_db.Insert(alt);
    }

    public bool IsAlt()
    {
        TableQuery<Alt> result = Db.s_db.Table<Alt>().Where(a => a.AltId == CocId);
        return result.Any();
    }

    public bool IsAltMain()
    {
        TableQuery<Alt> result = Db.s_db.Table<Alt>().Where(a => a.MainId == CocId);
        return result.Any();
    }

    public Alt? TryToAlt() => Db.s_db.Table<Alt>().Where(a => a.AltId == CocId).FirstOrDefault();

    public TableQuery<Alt> GetAltsByMain() => Db.s_db.Table<Alt>().Where(a => a.MainId == CocId);

    public string GetName() => Coc.GetMember(CocId).Name;
}
