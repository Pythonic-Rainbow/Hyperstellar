using ClashOfClans.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Hyperstellar.Dc.Attr;
using Hyperstellar.Sql;

namespace Hyperstellar.Dc;

public class Cmds : InteractionModuleBase
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
    [SlashCommand("admin", "Makes the Discord user an admin")] // Maybe rename to addadmin
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

    [RequireAdmin]
    [SlashCommand("alt", "Links an alt to a main")]
    public async Task AltAsync(Member alt, Member main)
    {
        if (alt.CocId == main.CocId)
        {
            await RespondAsync("Bro alt must be different from main bruh");
            return;
        }
        if (main.IsAlt())
        {
            await RespondAsync("Main can't be an alt in the database!");
            return;
        }
        if (alt.IsMain())
        {
            await RespondAsync("Alt can't be a main in the database!");
            return;
        }

        main.AddAlt(alt);
        ClanMember clanAlt = Coc.GetMember(alt.CocId);
        ClanMember clanMain = Coc.GetMember(main.CocId);
        await RespondAsync($"`{clanAlt.Name}` is an alt of `{clanMain.Name}`");
    }


}
