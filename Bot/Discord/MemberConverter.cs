using Discord;
using Discord.Interactions;
using Hyperstellar.Clash;
using Hyperstellar.Sql;

namespace Hyperstellar.Discord;

internal sealed class MemberConverter : TypeConverter
{
    public override bool CanConvertTo(Type type) => typeof(Member).IsAssignableFrom(type);
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        string input = (string)option.Value;
        // Console.WriteLine($"\"{input}\"");
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
            ? Task.FromResult(TypeConverterResult.FromError(
                InteractionCommandError.ConvertFailed,
                @$"Invalid `{option.Name}`. To specify a clan member:
* Enter his name (非速本主義Arkyo), alias (Arkyo) or ID with # (#28QL0CJV2)
* Mention his Discord (@Dim) __which will refer to his main__"))
            : Task.FromResult(TypeConverterResult.FromSuccess(member));
    }
}
