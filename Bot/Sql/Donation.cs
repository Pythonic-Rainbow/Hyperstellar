using SQLite;

namespace Hyperstellar.Sql;
public class Donation(long time, string accountId, int donated = 0, int received = 0) : DbObj<Donation>
{
    [PrimaryKey, NotNull]
    public long Time { get; set; } = time;

    [PrimaryKey, NotNull]
    public string AccountId { get; set; } = accountId;

    [NotNull]
    public int Donated { get; set; } = donated;

    [NotNull]
    public int Received { get; set; } = received;

    public Donation() : this(0, "") { }
}
