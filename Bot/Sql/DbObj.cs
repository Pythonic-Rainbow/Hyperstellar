using System.Collections;
using SQLite;

namespace Hyperstellar.Sql;

internal abstract class DbObj
{
    // Make this private after migration
    internal static readonly SQLiteConnection s_db = new("Hyperstellar.db");

    internal static void Commit()
    {
        s_db.Commit();
        s_db.BeginTransaction();
    }

    internal static int InsertAll(IEnumerable objects) => s_db.InsertAll(objects);

    internal virtual int Insert() => s_db.Insert(this);
}
