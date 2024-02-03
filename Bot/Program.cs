namespace Hyperstellar;

public class Program
{
    public static async Task Main() => await Task.WhenAll([
        Discord.Dc.InitAsync(),
        Clash.Coc.InitAsync(),
        Sql.Db.InitAsync()
        ]);
}
