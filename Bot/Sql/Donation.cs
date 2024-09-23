using SQLite;

namespace Hyperstellar.Sql;
public class Donation(long time, string cocId, int donated, int received) : DbObj<Donation>
{
    [PrimaryKey, NotNull]
    public long Time { get; set; } = time;

    [PrimaryKey, NotNull]
    public string CocID { get; set; } = cocId;

    [NotNull]
    public int Donated { get; set; } = donated;

    [NotNull]
    public int Received { get; set; } = received;

    public Donation() : this(0, "", 0, 0) { }

    public Donation(long time, string cocId) : this(time, cocId, 0, 0) { }
}
