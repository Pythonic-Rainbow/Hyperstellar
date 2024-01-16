using SQLite;

namespace Hyperstellar.Sql;

internal sealed class Db
{
    private static readonly SQLiteConnection s_db = new("Hyperstellar.db");

    internal static void Init()
    {
        var x = s_db.Table<User>();
        foreach (var y in x)
        {
            //Console.WriteLine(y.CocId);
        }
    }
}
