using SQLite;

namespace Hyperstellar.Sql;

public sealed class MainRequirement(string mainId, long endTime) : DbObj<MainRequirement>
{
    [PrimaryKey, NotNull]
    public string MainId { get; set; } = mainId;

    [PrimaryKey, NotNull]
    public long EndTime { get; set; } = endTime;

    [NotNull]
    public Pass Passed { get; set; }

    [NotNull]
    public int Donated { get; set; }

    [NotNull]
    public int Raided { get; set; }

    public MainRequirement() : this("", 0) { }

    internal override int Update()
    {
        List<MainRequirement>? result = s_db.Query<MainRequirement>("UPDATE MainRequirement SET Pass = ?, Donated = ?, Raided = ? WHERE MainId = ? AND EndTime = ?", Passed, Donated, Raided, MainId, EndTime);
        return result.Count;
    }

    public static MainRequirement Fetch(string mainId, long endTime) => s_db.Table<MainRequirement>()
        .First(req => req.MainId == mainId && req.EndTime == endTime);

    public static MainRequirement FetchLatest(string mainId) => s_db.Table<MainRequirement>()
        .Where(req => req.MainId == mainId)
        .OrderByDescending(req => req.EndTime)
        .First();

    public static List<MainRequirement> FetchEachLatest() => s_db.Query<MainRequirement>(
            """
             SELECT * FROM MainRequirement AS a
             WHERE EndTime = (
                 SELECT MAX(EndTime) FROM MainRequirement AS b WHERE a.MainID = b.MainID
             )
             ORDER BY EndTime;
            """
    );

    /* Is it possible to use TableQuery thru out each filter? */
    public static IEnumerable<MainRequirement> FetchLatest(IEnumerable<string> ids) => s_db.Table<MainRequirement>()
        .OrderByDescending(req => req.EndTime)
        .Where(req => ids.Contains(req.MainId))
        .GroupBy(req => req.MainId)
        .Select(grp => grp.First());
}

public enum Pass
{
    Pending,
    Passed,
    Failed,
    Overdue
}
