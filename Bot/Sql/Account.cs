using Hyperstellar.Clash;
using SQLite;

namespace Hyperstellar.Sql;
public class Account(string id) : DbObj<Account>
{
    internal static event Action<Main, Main>? EventAltAdded;

    [PrimaryKey, NotNull]
    public string Id { get; set; } = id;

    public long? LeftTime { get; set; }

    public Account() : this("") { }

    internal static Account? TryFetch(string cocId) => s_db.Table<Account>().FirstOrDefault(m => m.Id == cocId);

    internal static TableQuery<Account> FetchMembers() => s_db.Table<Account>().Where(a => a.LeftTime == null);

    internal static TableQuery<Account> FetchLeft() => s_db.Table<Account>().Where(a => a.LeftTime != null);

    public void AddAlt(Account altMember)
    {
        Alt alt = new(altMember.Id, Id);
        alt.Insert();
        Main altMain = Main.TryFetch(altMember.Id)!;
        Main mainMain = Main.TryFetch(Id)!;
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
        TableQuery<Alt> result = s_db.Table<Alt>().Where(a => a.AltId == Id);
        return result.Any();
    }

    public bool IsAltMain()
    {
        TableQuery<Alt> result = s_db.Table<Alt>().Where(a => a.MainId == Id);
        return result.Any();
    }

    public Alt? TryToAlt() => s_db.Table<Alt>().FirstOrDefault(a => a.AltId == Id);

    public Main? TryToMain() => s_db.Table<Main>().FirstOrDefault(m => m.MainId == Id);

    public Main ToMain() => s_db.Table<Main>().First(m => m.MainId == Id);

    public TableQuery<Alt> GetAltsByMain() => s_db.Table<Alt>().Where(a => a.MainId == Id);

    public string GetName() => Coc.GetMember(Id).Name;

    public Main GetEffectiveMain()
    {
        Alt? alt = TryToAlt();
        string mainId = alt == null ? Id : alt.MainId;
        return s_db.Table<Main>().First(m => m.MainId == mainId);
    }

    public override string ToString()
    {
        string s = $"{Id} ";
        if (LeftTime == null)
        {
            s += "Member";
        }
        else
        {
            s += $"Left at {LeftTime}";
        }
        return s;
    }
}
