using Hyperstellar.Clash;
using SQLite;

namespace Hyperstellar.Sql;
public class Member(string cocId)
{
    internal static event Action<Main, Main>? EventAltAdded;

    [PrimaryKey, NotNull]
    public string CocId { get; set; } = cocId;

    public Member() : this("") { }

    public void AddAlt(Member altMember)
    {
        Alt alt = new(altMember.CocId, CocId);
        Db.s_db.Insert(alt);
        Main altMain = Db.GetMain(altMember.CocId)!;
        Main mainMain = Db.GetMain(CocId)!;
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

    public TableQuery<Alt> GetAltsByMain() => Db.s_db.Table<Alt>().Where(a => a.MainId == CocId);

    public string GetName() => Coc.GetMember(CocId).Name;

    public Main GetEffectiveMain()
    {
        Alt? alt = TryToAlt();
        string mainId = alt == null ? CocId : alt.MainId;
        return Db.s_db.Table<Main>().First(m => m.MainId == mainId);
    }
}
