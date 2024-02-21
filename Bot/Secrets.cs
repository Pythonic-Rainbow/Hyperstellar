using Newtonsoft.Json;

namespace Hyperstellar;

internal static class Secrets
{
    internal static readonly string s_discord;  // Your Discord bot token
    internal static readonly string s_coc;  // Your coc token
    internal static readonly ulong s_botLogId;  // Discord TextChannel ID for the bot to send events to
    internal static readonly string s_cocId; // Your coc id, for debugging only

    static Secrets()
    {
        string json = File.ReadAllText("secrets.json");
        var definition = new
        {
            discord = "",
            coc = "",
            botLogId = (ulong)0,
            cocId = ""
        };
        var obj = JsonConvert.DeserializeAnonymousType(json, definition)!;

        s_discord = obj.discord;
        s_coc = obj.coc;
        s_botLogId = obj.botLogId;
        s_cocId = obj.cocId;
    }
}
