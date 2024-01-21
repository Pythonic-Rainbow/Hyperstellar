using Newtonsoft.Json;

internal sealed class Secrets
{
    internal static readonly string s_discord;
    internal static readonly string s_coc;
    internal static readonly ulong s_botLogId;
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
