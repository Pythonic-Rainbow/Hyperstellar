using SQLite;

namespace Hyperstellar.Sql
{
    internal class Db
    {
        private static readonly SQLiteConnection _db = new("Hyperstellar.db");
    }
}
