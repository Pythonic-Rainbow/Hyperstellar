using SQLite;

namespace Hyperstellar.Sql;

internal sealed class Db
{
    internal static readonly SQLiteConnection s_db = new("Hyperstellar.db");

    internal static async Task InitAsync()
    {
        while (true)
        {
            s_db.BeginTransaction();
            await Task.Delay(5 * 60 * 1000);
            s_db.Commit();
        }
    }

    internal static IEnumerable<Member> GetMembers() => s_db.Table<Member>();

    internal static bool AddMembers(string[] members) => s_db.InsertAll(members.Select(m => new Member(m))) == members.Length;

    internal static bool RemoveMembers(string[] members)
    {
        int count = 0;
        foreach (string member in members)
        {
            count += s_db.Delete<Member>(member);
        }
        return count == members.Length;
    }
}
