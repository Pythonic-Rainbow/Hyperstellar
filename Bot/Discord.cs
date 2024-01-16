using Discord;
using Discord.WebSocket;
using static Hyperstellar.Coc;

namespace Hyperstellar;

internal sealed class Discord
{
#if DEBUG
    private const ulong BotLogId = 666431254312517633;
#else
    private const ulong BotLogId = 1099026457268863017;
#endif
    private static readonly DiscordSocketClient s_bot = new();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private static SocketTextChannel s_botLog;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private static Task Ready()
    {
        s_botLog = (SocketTextChannel)s_bot.GetChannel(BotLogId);
        Task.Run(BotReadyAsync);
        return Task.CompletedTask;
    }

    internal static async Task InitAsync()
    {
        s_bot.Log += Log;
        s_bot.Ready += Ready;
        await s_bot.LoginAsync(TokenType.Bot, Secrets.s_discord);
        await s_bot.StartAsync();
    }

    internal static async Task DonationsChangedAsync(Dictionary<string, DonationTuple> donationsDelta)
    {
        string msg = "[DNT] ";
        List<string> items = new(donationsDelta.Count / 2);
        foreach (string name in donationsDelta.Keys)
        {
            int donated = donationsDelta[name]._donated;
            if (donated > 0)
            {
                items.Add($"{name}: {donated}");
            }
        }
        msg += string.Join(", ", items);
        msg += "\n=> ";
        items.Clear();
        foreach (string name in donationsDelta.Keys)
        {
            int received = donationsDelta[name]._received;
            if (received > 0)
            {
                items.Add($"{name}: {received}");
            }
        }
        msg += string.Join(", ", items);
        await s_botLog.SendMessageAsync(msg);
    }
}
