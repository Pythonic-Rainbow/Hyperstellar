using Discord.WebSocket;
using Hyperstellar.Sql;

namespace Hyperstellar.Dc;

internal class CmdHandlers
{
    internal static async Task ShutdownAsync(SocketSlashCommand cmd)
    {
        if (cmd.User.Id != 264756129916125184)
        {
            return;
        }

        bool commit = cmd.Data.Options.Count == 0 || (bool)cmd.Data.Options.First().Value;
        await cmd.RespondAsync("Ok", ephemeral: true);
        if (commit)
        {
            Db.Commit();
        }
        Environment.Exit(0);
    }

    internal static async Task CommitAsync(SocketSlashCommand cmd)
    {
        if (cmd.User.Id != 264756129916125184)
        {
            return;
        }

        Db.Commit();
        await cmd.RespondAsync("Committed", ephemeral: true);
    }
}
