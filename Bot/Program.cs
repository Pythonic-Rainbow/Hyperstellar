namespace Hyperstellar;

public class Program
{
    public static async Task Main() => await Task.WhenAll([
        Discord.InitAsync(),
        Coc.InitAsync(),
        Sql.Db.InitAsync()
        ]);
}
