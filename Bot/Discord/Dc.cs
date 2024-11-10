using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.Rest;
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
    internal static readonly TaskCompletionSource s_readyTcs = new();
    internal static event Func<Task> EventBotReady;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    static Dc()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        s_interactionSvc = new(s_bot); // Dont make it inline instantiate because s_bot.Rest would still be null
        s_interactionSvc.AddTypeConverter<Account>(new MemberConverter());

        Coc.EventDonated += DonationsChangedAsync;
        Phaser.EventViolated += PhaseViolatedAsync;
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
        s_readyTcs.SetResult();
        s_botLog = (SocketTextChannel)s_bot.GetChannel(Secrets.s_botLogId);
        _ = Task.Run(EventBotReady);
        await s_interactionSvc.RegisterCommandsGloballyAsync();
        s_bot.Ready -= Ready;
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

    private static async Task PhaseViolatedAsync(IEnumerable<Violator> violators)
    {
        IEnumerable<string> violatorMsgs = violators.Select(ProcessViolator);
        await s_botLog.SendMessageAsync($"[REQ]\n{string.Join("\n", violatorMsgs)}");
        return;

        static string ProcessViolator(Violator v)
        {
            string name = Coc.GetMember(v._id).Name;
            ICollection<string> violations = [];
            if (v._donated != null)
            {
                violations.Add($"Donated {v._donated}");
            }
            if (v._raided != null)
            {
                violations.Add($"Raided {v._raided}");
            }
            return $"{name} ({v._id}) {string.Join(", ", violations)}";
        }
    }

    private static async Task DonationsChangedAsync(IDictionary<string, DonRecv> donations)
    {
        List<string> donors = [], receivers = [];
        foreach ((string tag, DonRecv dr) in donations)
        {
            string name = Coc.GetMember(tag).Name;
            if (dr._donated > 0)
            {
                donors.Add($"{name}: {dr._donated}");
            }
            if (dr._received > 0)
            {
                receivers.Add($"{name}: {dr._received}");
            }
        }
        string msg = $"[DNT] {string.Join(", ", donors)}\n=> {string.Join(", ", receivers)}";
        await s_botLog.SendMessageAsync(msg);
    }

    internal static async Task InitAsync()
    {
        await s_interactionSvc.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        await s_bot.LoginAsync(TokenType.Bot, Secrets.s_discord);
        s_botApp = await s_bot.GetApplicationInfoAsync();
        await s_bot.StartAsync();
    }

    internal static async Task<RestUserMessage> NewExceptionLogAsync(Exception ex)
    {
        EmbedBuilder emb = new()
        {
            Title = ex.Message
        };
        if (ex.StackTrace != null)
        {
            emb.Description = Program.GetExceptionStackTraceString(ex);
        }
        string? exName = ex.GetType().FullName;
        if (exName != null)
        {
            emb.Author = new EmbedAuthorBuilder
            {
                Name = exName
            };
        }

        // Always waits until the exception is actually sent
        return await Program.TryUntilAsync<RestUserMessage>(
            async () => await s_botLog.SendMessageAsync(s_botApp.Owner.Mention, embed: emb.Build()),
            msRetryWaitInterval: 5000
            );
    }

    internal static async Task SendLogAsync(string msg) => await s_botLog.SendMessageAsync(msg);
}
