using Discord.Interactions;
using Discord.WebSocket;
using Hyperstellar.Sql;

namespace Hyperstellar.Dc;

public class Commands : InteractionModuleBase
{
    [RequireOwner]
    [SlashCommand("shutdown", "Shuts down the bot")]
    public async Task ShutdownAsync(bool commit = true)
    {
        await RespondAsync("Ok", ephemeral: true);
        if (commit)
        {
            Db.Commit();
        }
        Environment.Exit(0);
    }

    [RequireOwner]
    [SlashCommand("commit", "Commits db")]
    public async Task CommitAsync()
    {
        Db.Commit();
        await RespondAsync("Committed", ephemeral: true);
    }

    [RequireOwner]
    [SlashCommand("admin", "Makes the Discord user an admin")]
    public async Task AdminAsync(SocketGuildUser user)
    {
        bool success = Db.AddAdmin(user.Id);
        if (success)
        {
            await RespondAsync("Success!", ephemeral: true);
        }
        else
        {
            await RespondAsync("Error", ephemeral: true);
        }
    }
}
