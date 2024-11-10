using System.Text.RegularExpressions;
using ClashOfClans.Models;
using Discord;
using Discord.Interactions;
using Hyperstellar.Clash;
using Hyperstellar.Sql;
using Type = System.Type;

namespace Hyperstellar.Discord;

internal sealed class MemberConverter : TypeConverter
{
    public override bool CanConvertTo(Type type) => typeof(Account).IsAssignableFrom(type);
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        string input = (string)option.Value;

        // Check whether input matches a Discord user mention
        Match dUserMentionMatch = Regexes.DiscordUserMention().Match(input);
        if (dUserMentionMatch.Success)
        {
            // Extract uid from mention
            ReadOnlySpan<char> captured = dUserMentionMatch.ValueSpan;  // <@123>
            ulong uid = Convert.ToUInt64(captured[2..^1].ToString());  // 123

            // Checks whether this Discord user is linked to a main
            Main? main = Main.TryFetchByDiscord(uid);
            if (main == null)
            {
                return TypeConverters.Error("This Discord user isn't linked to any CoC account.");
            }

            // TODO: REMOVE THIS AFTER DB REDESIGN - SKIPPING THE CHECK BELOW BECUZ FOR NOW, IN DB = MUST BE IN CLAN
            Account sqlMember = Account.TryFetch(main.AccountId)!;
            return TypeConverters.Success(sqlMember);

            /*
            // Check whether the main is still in the clan
            string cocId = main.MainId;
            ClanMember? member = Coc.TryGetMember(cocId);

            return member == null
                ? TypeConverters.Error("The main of this Discord user isn't in the clan.")
                : Task.FromResult(TypeConverterResult.FromSuccess(member));
            */
        }

        // Check whether input matches an alias
        CocAlias? dbAlias = CocAlias.TryFetch(input);
        if (dbAlias != null)
        {
            // Check whether the coc account of the alias is still in the clan
            string aliasCocId = dbAlias.CocId;
            ClanMember? aliasClanMember = Coc.TryGetMember(aliasCocId);
            Account sqlMember = Account.TryFetch(aliasCocId)!;

            return aliasClanMember == null
                ? TypeConverters.Error("The player of this alias isn't in the clan.")
                : TypeConverters.Success(sqlMember);
        }

        // Check whether input is the name of a clan member
        ClanMember? member = Coc.TryGetMemberById(input);
        if (member != null)
        {
            Account sqlMember = Account.TryFetch(member.Tag)!;
            return TypeConverters.Success(sqlMember);
        }

        // Check whether input is the tag of a clan member
        member = Coc.TryGetMember(input);
        if (member != null)
        {
            Account sqlMember = Account.TryFetch(input)!;
            return TypeConverters.Success(sqlMember);
        }

        return TypeConverters.Error(
            $"""
             Invalid `{option.Name}`. To specify a clan member:
             * Enter his name (非速本主義Arkyo), alias (arkyo) or ID with # (#28QL0CJV2)
             * Mention his Discord (@Dim) __which will refer to his main__
             """);
    }


}
