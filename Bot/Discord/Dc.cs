using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hyperstellar.Clash;
using Hyperstellar.Sql;

namespace Hyperstellar.Discord;

internal sealed class Dc
{

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private static SocketTextChannel s_botLog;
    private static InteractionService s_interactionSvc;
    private static IApplication s_botApp;
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
        _ = Task.Run(Coc.BotReadyAsync);
        await s_interactionSvc.RegisterCommandsGloballyAsync();
    }

    private static async Task SlashCmdXAsync(SocketSlashCommand cmd)
    {
        SocketInteractionContext ctx = new(s_bot, cmd);
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
        s_interactionSvc = new(s_bot); // Dont make it inline instantiate because s_bot.Rest would still be null
        s_bot.Log += Log;
        s_bot.Ready += Ready;
        s_bot.SlashCommandExecuted += SlashCmdXAsync;
        s_interactionSvc.InteractionExecuted += InteractionXAsync;

        s_interactionSvc.AddTypeConverter<Member>(new MemberConverter());
        await s_interactionSvc.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        await s_bot.LoginAsync(TokenType.Bot, Secrets.s_discord);
        s_botApp = await s_bot.GetApplicationInfoAsync();
        await s_bot.StartAsync();
    }

    internal static async Task DonationsChangedAsync(Dictionary<string, DonationTuple> donDelta)
    {
        string msg = "[DNT] ";
        List<string> items = new(donDelta.Count / 2);
        foreach (string tag in donDelta.Keys)
        {
            int donated = donDelta[tag]._donated;
            if (donated > 0)
            {
                string name = Coc.GetMember(tag).Name;
                items.Add($"{name}: {donated}");
            }
        }
        msg += string.Join(", ", items);
        msg += "\n=> ";
        items.Clear();
        foreach (string tag in donDelta.Keys)
        {
            int received = donDelta[tag]._received;
            if (received > 0)
            {
                string name = Coc.GetMember(tag).Name;
                items.Add($"{name}: {received}");
            }
        }
        msg += string.Join(", ", items);
        await s_botLog.SendMessageAsync(msg);
    }

    internal static async Task Donate25Async(List<string> violators) => await s_botLog.SendMessageAsync($"[Donate25] {string.Join(", ", violators)}");

    internal static async Task ExceptionAsync(Exception ex)
    {
        EmbedBuilder emb = new()
        {
            Title = ex.Message
        };
        if (ex.StackTrace != null)
        {
            emb.Description = ex.StackTrace;
        }
        string? exName = ex.GetType().FullName;
        if (exName != null)
        {
            emb.Author = new EmbedAuthorBuilder
            {
                Name = exName
            };
        }
        await s_botLog.SendMessageAsync(s_botApp.Owner.Mention, embed: emb.Build());
    }
}
