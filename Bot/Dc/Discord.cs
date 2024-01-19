using Discord;
using Discord.WebSocket;
using static Hyperstellar.Coc;

namespace Hyperstellar.Dc;

internal sealed class Discord
{

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private static SocketTextChannel s_botLog;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    internal static readonly DiscordSocketClient s_bot = new();

    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private static async Task Ready()
    {
        s_botLog = (SocketTextChannel)s_bot.GetChannel(Secrets.s_botLogId);
        _ = Task.Run(BotReadyAsync);

        SlashCommandBuilder guildCmd = new SlashCommandBuilder()
            .WithName("shutdown")
            .WithDescription("[Admin] Shuts down the bot")
            .AddOption("commit", ApplicationCommandOptionType.Boolean, "Commit db?");
        await s_bot.CreateGlobalApplicationCommandAsync(guildCmd.Build());

        guildCmd = new SlashCommandBuilder()
            .WithName("commit")
            .WithDescription("[Admin] Commits db");
        await s_bot.CreateGlobalApplicationCommandAsync(guildCmd.Build());
    }

    private static async Task SlashCmdXAsync(SocketSlashCommand cmd)
    {
        switch (cmd.Data.Name)
        {
            case "shutdown":
                await CmdHandlers.ShutdownAsync(cmd);
                break;
            case "commit":
                await CmdHandlers.CommitAsync(cmd);
                break;
        }
    }

    internal static async Task InitAsync()
    {
        s_bot.Log += Log;
        s_bot.Ready += Ready;
        s_bot.SlashCommandExecuted += SlashCmdXAsync;
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
