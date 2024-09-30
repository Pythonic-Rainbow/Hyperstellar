using ClashOfClans.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Hyperstellar.Discord.Attr;
using Hyperstellar.Sql;
using Hyperstellar.Clash;
using Discord;

namespace Hyperstellar.Discord;

public class Cmds : InteractionModuleBase
{
    [RequireOwner]
    [SlashCommand("shutdown", "[Owner] Shuts down the bot")]
    public async Task ShutdownAsync(bool commit = true)
    {
        if (commit)
        {
            Db.Commit();
        }
        await RespondAsync("Ok", ephemeral: true);
        Environment.Exit(0);
    }

    [RequireOwner]
    [SlashCommand("commit", "[Owner] Commits db")]
    public async Task CommitAsync()
    {
        Db.Commit();
        await RespondAsync("Committed", ephemeral: true);
    }

    [RequireOwner]
    [SlashCommand("admin", "[Owner] Makes the Discord user an admin")] // Maybe rename to addadmin
    public async Task AdminAsync(SocketGuildUser user)
    {
        bool success = new BotAdmin(user.Id).Insert() == 1;
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
    [SlashCommand("alt", "[Admin] Links an alt to a main")]
    public async Task AltAsync(Account alt, Account main)
    {
        if (alt.Id == main.Id)
        {
            await RespondAsync("Bro alt must be different from main bruh");
            return;
        }
        if (main.IsAlt())
        {
            await RespondAsync("Main can't be an alt in the database!");
            return;
        }
        if (alt.IsAltMain())
        {
            await RespondAsync("Alt can't be a main in the database!");
            return;
        }

        main.AddAlt(alt);
        ClanMember clanAlt = Coc.GetMember(alt.Id);
        ClanMember clanMain = Coc.GetMember(main.Id);
        await RespondAsync($"`{clanAlt.Name}` is now an alt of `{clanMain.Name}`");
    }

    [RequireAdmin]
    [SlashCommand("discord", "[Admin] Links a Discord account to a Main")]
    public async Task DiscordAsync(Account member, IGuildUser discord)
    {
        Main? main = member.TryToMain();
        if (main == null)
        {
            await RespondAsync("`coc` can't be an alt!");
            return;
        }
        if (discord.IsBot)
        {
            await RespondAsync("`discord` can't be a bot!");
            return;
        }
        main.Discord = discord.Id;
        main.Update();
        await RespondAsync("Linked");
    }

    [SlashCommand("info", "Shows info of a clan member")]
    public async Task InfoAsync(Account clanMember)
    {
        ClanMember cocMem = Coc.GetMember(clanMember.Id);

        EmbedBuilder embed = new()
        {
            Title = cocMem.Name,
            Author = new EmbedAuthorBuilder { Name = cocMem.Tag }
        };

        Alt? alt = clanMember.TryToAlt();
        if (alt == null)
        {
            embed.Description = string.Join(", ", clanMember.GetAltsByMain().Select(static a => Coc.GetMember(a.AltId).Name));
            Main main = clanMember.ToMain();
            if (main.Discord != null)
            {
                embed.AddField("Discord", $"<@{main.Discord}>");
            }
        }
        else
        {
            embed.Description = $"__{Coc.GetMember(alt.MainId).Name}__";
            IEnumerable<string> altNames = alt.GetOtherAlts().Select(static a => Coc.GetMember(a.AltId).Name);
            foreach (string altName in altNames)
            {
                embed.Description += $", {altName}";
            }
            Main main = alt.GetMain();
            if (main.Discord != null)
            {
                embed.AddField("Discord", $"<@{main.Discord}>");
            }
        }

        await RespondAsync(embed: embed.Build());
    }

    [RequireAdmin]
    [SlashCommand("alias", "[Admin] Sets an alias for a Coc member")]
    public async Task AliasAsync(string alias, Account member)
    {
        bool success = new CocAlias(alias, member.Id).Insert() == 1;
        if (success)
        {
            await RespondAsync($"`{alias}` is now an alias of `{member.GetName()}`");
        }
        else
        {
            await RespondAsync("Failed to add alias.");
        }
    }

    [SlashCommand("aliases", "Lists all aliases")]
    public async Task AliasesAsync()
    {
        string msg = "";
        foreach (CocAlias alias in CocAlias.FetchAll())
        {
            ClanMember? clanMember = Coc.TryGetMember(alias.CocId);
            string name = clanMember == null ? "" : clanMember.Name;
            msg += $"{alias.Alias} -> {name} {alias.CocId}\n";
        }
        await RespondAsync(msg);
    }
}
