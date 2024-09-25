using SQLite;

namespace Hyperstellar.Sql;
public class Raid(long endTime, string cocId, int count)
{
    [PrimaryKey, NotNull]
    public long EndTime { get; set; } = endTime;

    [PrimaryKey, NotNull]
    public string CocId { get; set; } = cocId;

    [NotNull]
    public int Count { get; set; } = count;

    public Raid() : this(0, "", 0) { }
}
