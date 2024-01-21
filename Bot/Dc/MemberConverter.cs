using ClashOfClans.Models;
using Discord;
using Discord.Interactions;
using Hyperstellar.Sql;

namespace Hyperstellar.Dc;

internal class MemberConverter : TypeConverter
{
    public override bool CanConvertTo(System.Type type) => typeof(Member).IsAssignableFrom(type);
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        string input = (string)option.Value;
        Member? member = Db.GetMember(input);
        if (member == null)
        {
            string? id = Coc.GetMemberId(input);
            if (id != null)
            {
                member = Db.GetMember(id);
            }
        }
        return member == null
            ? Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ConvertFailed,
                                                                 $"Invalid `{option.Name}`: To specify a clan member, enter his name/ID with #"))
            : Task.FromResult(TypeConverterResult.FromSuccess(member));
    }
}
