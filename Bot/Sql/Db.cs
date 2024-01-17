using SQLite;

namespace Hyperstellar.Sql;

internal sealed class Db
{
    internal static readonly SQLiteConnection s_db = new("Hyperstellar.db");

    internal static IEnumerable<string> GetMembers() => s_db.Table<Alt>()
        .Select(a => a.AltId)
        .Union(
            s_db.Table<Alt>()
            .Select(a => a.MainId)
            .Distinct()
        );
}
