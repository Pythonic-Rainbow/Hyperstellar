using Discord;
using Discord.WebSocket;
using static Hyperstellar.Coc;

namespace Hyperstellar;

internal class Discord
{
#if DEBUG
    const ulong BOT_LOG_ID = 666431254312517633;
#else
    const ulong BOT_LOG_ID = 1099026457268863017;
#endif

    private static DiscordSocketClient _bot = new();
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private static SocketTextChannel _botLog;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private static Task Ready()
    {
        _botLog = (SocketTextChannel)_bot.GetChannel(BOT_LOG_ID);
        Task.Run(Coc.BotReadyAsync);
        return Task.CompletedTask;
    }

    internal static async Task InitAsync()
    {
        _bot.Log += Log;
        _bot.Ready += Ready;
        await _bot.LoginAsync(TokenType.Bot, Secrets.Discord);
        await _bot.StartAsync();
    }

    internal static async Task DonationsChangedAsync(Dictionary<string, DonationTuple> donationsDelta)
    {
        string msg = "[DNT] ";
        List<string> items = new(donationsDelta.Count / 2);
        foreach (var name in donationsDelta.Keys)
        {
            int donated = donationsDelta[name].Donated;
            if (donated > 0)
            {
                items.Add($"{name}: {donated}");
            }
        }
        msg += string.Join(", ", items);
        msg += "\n=> ";
        items.Clear();
        foreach (var name in donationsDelta.Keys)
        {
            int received = donationsDelta[name].Received;
            if (received > 0)
            {
                items.Add($"{name}: {received}");
            }
        }
        msg += string.Join(", ", items);
        await _botLog.SendMessageAsync(msg);
    }
}