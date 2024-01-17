using SQLite;

namespace Hyperstellar.Sql;
internal class Person
{
    [PrimaryKey, NotNull]
    [Column("CocId")]
    public string CocId { get; set; }
    [Unique]
    public ulong? DiscordId { get; set; }

    public Person() => CocId = "";

    public Person(string CocId, ulong? DiscordId)
    {
        this.CocId = CocId;
        this.DiscordId = DiscordId;
    }

    public void AddAlt(string altId)
    {
        Alt alt = new(altId, CocId);
        _ = Db.s_db.Insert(alt);
    }
}
