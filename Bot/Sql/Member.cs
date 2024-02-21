using Hyperstellar.Clash;
using SQLite;

namespace Hyperstellar.Sql;
public class Member
{
    internal static event Action<Alt>? EventAltAdded;

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
        Db.s_db.Insert(alt);
        EventAltAdded!(alt);
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

    public Alt? TryToAlt() => Db.s_db.Table<Alt>().FirstOrDefault(a => a.AltId == CocId);

    public TableQuery<Alt> GetAltsByMain() => Db.s_db.Table<Alt>().Where(a => a.MainId == CocId);

    public string GetName() => Coc.GetMember(CocId).Name;
}
