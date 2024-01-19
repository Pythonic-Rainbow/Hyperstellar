using Discord.Interactions;
using Discord.WebSocket;
using Hyperstellar.Sql;

namespace Hyperstellar.Dc;

public class CmdHandlers : InteractionModuleBase
{

    [SlashCommand("shutdown", "[Admin] Shuts down the bot")]
    public async Task ShutdownAsync(bool commit = true)
    {
        if (Context.User.Id != 264756129916125184)
        {
            return;
        }
        await RespondAsync("Ok", ephemeral: true);
        if (commit)
        {
            Db.Commit();
        }
        Environment.Exit(0);
    }

    [SlashCommand("commit", "[Admin] Commits db")]
    public async Task CommitAsync()
    {
        if (Context.User.Id != 264756129916125184)
        {
            return;
        }

        Db.Commit();
        await RespondAsync("Committed", ephemeral: true);
    }
}
