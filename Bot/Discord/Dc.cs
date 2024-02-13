using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hyperstellar.Clash;
using Hyperstellar.Sql;

namespace Hyperstellar.Discord;

internal static class Dc
{
    private static SocketTextChannel s_botLog;
    private static readonly InteractionService s_interactionSvc;
    private static IApplication s_botApp;
    private static readonly DiscordSocketClient s_bot = new();
    internal static event Func<Task> s_eventBotReady;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    static Dc()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        s_interactionSvc = new(s_bot); // Dont make it inline instantiate because s_bot.Rest would still be null
        s_interactionSvc.AddTypeConverter<Member>(new MemberConverter());

        Coc.s_eventDonation += DonationsChangedAsync;
        Donate25.s_eventViolated += Donate25Async;
        s_bot.Log += Log;
        s_bot.Ready += Ready;
        s_bot.SlashCommandExecuted += SlashCmdXAsync;
        s_interactionSvc.InteractionExecuted += InteractionXAsync;
    }

    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private static async Task Ready()
    {
        s_botLog = (SocketTextChannel)s_bot.GetChannel(Secrets.s_botLogId);
        _ = Task.Run(s_eventBotReady);
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

    private static async Task Donate25Async(IEnumerable<string> violators)
    {
        IEnumerable<string> names = violators.Select(v => Coc.GetMember(v).Name);
        await s_botLog.SendMessageAsync($"[Donate25] {string.Join(", ", names)}");
    }

    private static async Task DonationsChangedAsync(Dictionary<string, DonationTuple> donDelta)
    {
        string msg = "[DNT] ";
        List<string> items = new(donDelta.Count / 2);
        foreach ((string tag, DonationTuple dt) in donDelta.Where(d => d.Value._donated > 0))
        {
            string name = Coc.GetMember(tag).Name;
            items.Add($"{name}: {dt._donated}");
        }
        msg += string.Join(", ", items);
        msg += "\n=> ";
        items.Clear();
        foreach ((string tag, DonationTuple dt) in donDelta.Where(d => d.Value._received > 0))
        {
            string name = Coc.GetMember(tag).Name;
            items.Add($"{name}: {dt._received}");
        }
        msg += string.Join(", ", items);
        await s_botLog.SendMessageAsync(msg);
    }

    internal static async Task InitAsync()
    {
        await s_interactionSvc.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        await s_bot.LoginAsync(TokenType.Bot, Secrets.s_discord);
        s_botApp = await s_bot.GetApplicationInfoAsync();
        await s_bot.StartAsync();
    }

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
