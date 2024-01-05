using Hyperstellar;

public class Program
{
    public static async Task Main(string[] args)
    {
        await Hyperstellar.Discord.InitAsync();
        await Coc.InitAsync();
        await Task.Delay(-1);
    }
}