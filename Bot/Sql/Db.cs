using SQLite;

namespace Hyperstellar.Sql;

internal sealed class Db
{
    internal static readonly SQLiteConnection s_db = new("Hyperstellar.db");
    internal static readonly HashSet<ulong> s_admins = s_db.Table<BotAdmin>().Select(a => a.Id).ToHashSet();

    internal static void Commit()
    {
        s_db.Commit();
        Console.WriteLine("Db committed");
        s_db.BeginTransaction();
    }

    internal static IEnumerable<Member> GetMembers() => s_db.Table<Member>();

    internal static bool AddMembers(string[] members)
    {
        int memberCount = s_db.InsertAll(members.Select(m => new Member(m)));
        int donationCount = s_db.InsertAll(members.Select(m => new Donation(m)));
        return memberCount == donationCount && memberCount == members.Length;
    }

    internal static bool RemoveMembers(string[] members)
    {
        int count = 0;
        foreach (string member in members)
        {
            count += s_db.Delete<Member>(member);
        }
        return count == members.Length;
    }

    internal static Member? GetMember(string member) => s_db.Table<Member>().Where(m => m.CocId.Equals(member)).FirstOrDefault();

    internal static bool HasMember(string member) => s_db.Table<Member>().Where(m => m.CocId.Equals(member)).Count() == 1;

    internal static bool AddAdmin(ulong id)
    {
        var admin = new BotAdmin(id);
        int count = s_db.Insert(admin);
        s_admins.Add(id);
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
