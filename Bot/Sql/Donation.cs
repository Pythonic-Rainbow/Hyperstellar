using SQLite;

namespace Hyperstellar.Sql;

internal sealed class Donation(string id)
{
    [PrimaryKey, NotNull]
    public string MainId { get; set; } = id;

    [NotNull]
    public uint Donated { get; set; } = 0;

    [NotNull]
    public long Checked { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public Donation() : this("") { }
}
