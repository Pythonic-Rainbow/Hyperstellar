using SQLite;

namespace Hyperstellar.Sql;
public class Donation(long time, string cocId, int donated = 0, int received = 0) : DbObj<Donation>
{
    [PrimaryKey, NotNull]
    public long Time { get; set; } = time;

    [PrimaryKey, NotNull]
    public string CocId { get; set; } = cocId;

    [NotNull]
    public int Donated { get; set; } = donated;

    [NotNull]
    public int Received { get; set; } = received;

    public Donation() : this(0, "") { }
}
