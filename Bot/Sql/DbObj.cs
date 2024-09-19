using System.Collections;
using SQLite;

namespace Hyperstellar.Sql;

public abstract class DbObj
{
    // Make this private after migration
    internal static readonly SQLiteConnection s_db = new("Hyperstellar.db");

    internal static void Commit()
    {
        s_db.Commit();
        s_db.BeginTransaction();
    }

    internal static int InsertAll(IEnumerable objects) => s_db.InsertAll(objects);

    internal static async Task InitAsync()
    {
        s_db.BeginTransaction();
        await Program.TryUntilAsync(async () =>
        {
            await Task.Delay(5 * 60 * 1000);
            Commit();
        }, runForever: true);
    }

    internal virtual int Insert() => s_db.Insert(this);

    internal int Delete() => s_db.Delete(this);
}
