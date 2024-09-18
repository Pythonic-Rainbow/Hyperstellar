using Hyperstellar.Clash;
using SQLite;

namespace Hyperstellar.Sql;
public class Member(string cocId) : DbObj
{
    internal static event Action<Main, Main>? EventAltAdded;

    [PrimaryKey, NotNull]
    public string CocId { get; set; } = cocId;

    public Member() : this("") { }

    internal static IEnumerable<Member> FetchAll() => s_db.Table<Member>();

    internal static Member? TryFetch(string cocId) => s_db.Table<Member>().FirstOrDefault(m => m.CocId == cocId);

    public void AddAlt(Member altMember)
    {
        Alt alt = new(altMember.CocId, CocId);
        s_db.Insert(alt);
        Main altMain = Main.TryFetch(altMember.CocId)!;
        Main mainMain = Main.TryFetch(CocId)!;
        EventAltAdded!(altMain, mainMain);
        if (altMain.Discord != null)
        {
            mainMain.Discord = altMain.Discord;
        }
        mainMain.Update();
        altMain.Delete();
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

    public Main? TryToMain() => Db.s_db.Table<Main>().FirstOrDefault(m => m.MainId == CocId);

    public Main ToMain() => Db.s_db.Table<Main>().First(m => m.MainId == CocId);

    public TableQuery<Alt> GetAltsByMain() => Db.s_db.Table<Alt>().Where(a => a.MainId == CocId);

    public string GetName() => Coc.GetMember(CocId).Name;

    public Main GetEffectiveMain()
    {
        Alt? alt = TryToAlt();
        string mainId = alt == null ? CocId : alt.MainId;
        return Db.s_db.Table<Main>().First(m => m.MainId == mainId);
    }
}
