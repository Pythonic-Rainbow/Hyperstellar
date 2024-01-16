namespace Hyperstellar;

public class Program
{
    public static async Task Main()
    {
        await Discord.InitAsync();
        await Coc.InitAsync();
        await Task.Delay(-1);
    }
}