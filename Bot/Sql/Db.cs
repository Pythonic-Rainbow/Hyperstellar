using SQLite;

namespace Hyperstellar.Sql;

internal static class Db
{
    internal static readonly SQLiteConnection s_db = DbObj.s_db;

    internal static Main? GetMainByDiscord(ulong uid) => s_db.Table<Main>().FirstOrDefault(m => m.Discord == uid);

    internal static IEnumerable<Main> GetDonations() => s_db.Table<Main>();

    internal static bool UpdateMain(Main main) => s_db.Update(main) == 1;

    internal static CocMemberAlias? TryGetAlias(string alias)
    {
        alias = alias.ToLower();
        return s_db.Table<CocMemberAlias>().FirstOrDefault(a => a.Alias == alias);
    }

    internal static IEnumerable<CocMemberAlias> GetAliases() => s_db.Table<CocMemberAlias>();

    internal static bool AddAlias(string alias, Member member)
    {
        CocMemberAlias cocMemberAlias = new(alias.ToLower(), member.CocId);
        int count = s_db.Insert(cocMemberAlias);
        return count == 1;
    }
}
