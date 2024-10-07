using System.Collections;
using SQLite;

namespace Hyperstellar.Sql;

public abstract class Db
{
    private protected static readonly SQLiteConnection s_db = new("Hyperstellar.db");

    internal static void Commit()
    {
        s_db.Commit();
        s_db.BeginTransaction();
    }

    internal static int InsertAll(IEnumerable objects) => s_db.InsertAll(objects);

    internal static int UpdateAll(IEnumerable objects) => s_db.UpdateAll(objects);

    internal static async Task InitAsync()
    {
        s_db.BeginTransaction();
        await Program.TryUntilAsync(static async () =>
        {
            await Task.Delay(5 * 60 * 1000);
            Commit();
        }, runForever: true);
    }

    internal virtual int Insert() => s_db.Insert(this);

    internal int Delete() => s_db.Delete(this);

    internal int Update() => s_db.Update(this);

}

public abstract class DbObj<T> : Db where T : DbObj<T>, new()
{
    internal static TableQuery<T> FetchAll() => s_db.Table<T>();
}
