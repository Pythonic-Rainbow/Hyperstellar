using SQLite;

namespace Hyperstellar.Sql;

internal sealed class Db
{
    private static readonly SQLiteConnection s_db = new("Hyperstellar.db");
}
