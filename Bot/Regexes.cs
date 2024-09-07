using System.Text.RegularExpressions;

namespace Hyperstellar;

internal static partial class Regexes
{
    [GeneratedRegex("^<@\\d{1,20}>$")]
    internal static partial Regex DiscordUserMention();
}
