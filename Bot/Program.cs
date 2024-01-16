namespace Hyperstellar;

public class Program
{
    public static async Task Main()
    {
        await Discord.InitAsync();
        await Coc.InitAsync();
        Sql.Db.Init();
        await Task.Delay(-1);
    }
}
