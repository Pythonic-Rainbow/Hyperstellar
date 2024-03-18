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
    internal static event Func<Task> EventBotReady;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    static Dc()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        s_interactionSvc = new(s_bot); // Dont make it inline instantiate because s_bot.Rest would still be null
        s_interactionSvc.AddTypeConverter<Member>(new MemberConverter());

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
        s_botLog = (SocketTextChannel)s_bot.GetChannel(Secrets.s_botLogId);
        _ = Task.Run(EventBotReady);
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

    private static async Task PhaseViolatedAsync(IEnumerable<Violator> violators)
    {
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

        IEnumerable<string> violatorMsgs = violators.Select(ProcessViolator);
        await s_botLog.SendMessageAsync($"[REQ]\n{string.Join("\n", violatorMsgs)}");
    }

    private static async Task DonationsChangedAsync(IEnumerable<Tuple<string, int>> donDelta, IEnumerable<Tuple<string, int>> recDelta)
    {
        string msg = "[DNT] ";
        msg += string.Join(", ", donDelta.Select(t =>
        {
            (string tag, int donated) = t;
            string name = Coc.GetMember(tag).Name;
            return $"{name}: {donated}";
        }));
        msg += "\n=> ";
        msg += string.Join(", ", recDelta.Select(t =>
        {
            (string tag, int received) = t;
            string name = Coc.GetMember(tag).Name;
            return $"{name}: {received}";
        }));
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

    internal static async Task SendLogAsync(string msg) => await s_botLog.SendMessageAsync(msg);
}
