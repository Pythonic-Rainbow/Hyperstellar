namespace Hyperstellar;

public class Program
{
    public static async Task Main()
    {
        await Discord.InitAsync();
        await Task.Delay(-1);
    }
}
