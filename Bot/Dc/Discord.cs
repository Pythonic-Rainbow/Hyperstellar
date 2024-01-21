using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hyperstellar.Sql;
//using Microsoft.Extensions.DependencyInjection;
using static Hyperstellar.Coc;

namespace Hyperstellar.Dc;

internal sealed class Discord
{

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private static SocketTextChannel s_botLog;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private static readonly InteractionService s_interactionSvc = new(s_bot);
    internal static readonly DiscordSocketClient s_bot = new();

    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private static async Task Ready()
    {
        s_botLog = (SocketTextChannel)s_bot.GetChannel(Secrets.s_botLogId);
        Task.Run(BotReadyAsync);
        //await s_interactionSvc.RegisterCommandsGloballyAsync();
        SlashCommandBuilder guildCmd = new SlashCommandBuilder().WithName("alt").WithDescription("Links an alt to a main");
        guildCmd.AddOption("alt", ApplicationCommandOptionType.String, "alt", isRequired: true);
        guildCmd.AddOption("main", ApplicationCommandOptionType.String, "main", isRequired: true);
        await s_bot.CreateGlobalApplicationCommandAsync(guildCmd.Build());
        Console.WriteLine("Registered commands");
        //return Task.CompletedTask;
    }

    private static async Task SlashCmdXAsync(SocketSlashCommand cmd)
    {
        var ctx = new SocketInteractionContext(s_bot, cmd);
        await s_interactionSvc.ExecuteCommandAsync(ctx, null);
    }

    private static async Task InteractionXAsync(ICommandInfo info, IInteractionContext ctx, IResult result)
    {
        if (!result.IsSuccess)
        {
            await ctx.Interaction.RespondAsync(result.ErrorReason);
        }
    }

    internal static async Task InitAsync()
    {
        s_bot.Log += Log;
        s_bot.Ready += Ready;
        s_bot.SlashCommandExecuted += SlashCmdXAsync;
        s_interactionSvc.InteractionExecuted += InteractionXAsync;

        s_interactionSvc.AddTypeConverter<Member>(new MemberConverter());

        await s_interactionSvc.AddModulesAsync(Assembly.GetEntryAssembly(), null); // Custom SP
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
