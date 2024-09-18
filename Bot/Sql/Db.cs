using SQLite;

namespace Hyperstellar.Sql;

internal static class Db
{
    internal static readonly SQLiteConnection s_db = DbObj.s_db;

    internal static void Commit() => DbObj.Commit();

    internal static IEnumerable<Member> GetMembers() => s_db.Table<Member>();

    internal static bool AddMembers(string[] members)
    {
        int memberCount = s_db.InsertAll(members.Select(m => new Member(m)));
        return memberCount == members.Length;
    }

    internal static bool DeleteMembers(string[] members) => members.Sum(s_db.Delete<Member>) == members.Length;

    internal static Member? GetMember(string member) => s_db.Table<Member>().FirstOrDefault(m => m.CocId.Equals(member));

    internal static Main? GetMain(string id) => s_db.Table<Main>().FirstOrDefault(d => d.MainId == id);

    internal static Main? GetMainByDiscord(ulong uid) => s_db.Table<Main>().FirstOrDefault(m => m.Discord == uid);

    internal static IEnumerable<Main> GetDonations() => s_db.Table<Main>();

    internal static bool UpdateMain(Main main) => s_db.Update(main) == 1;

    internal static Alt? TryGetAlt(string altId) => s_db.Table<Alt>().FirstOrDefault(a => a.AltId == altId);

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

    internal static async Task InitAsync()
    {
        s_db.BeginTransaction();
        while (true)
        {
            await Task.Delay(5 * 60 * 1000);
            Commit();
        }
    }
}
