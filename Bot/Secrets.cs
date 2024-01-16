using Newtonsoft.Json;

internal sealed class Secrets
{
    internal static readonly string s_discord;
    internal static readonly string s_coc;

    static Secrets()
    {
        string json = File.ReadAllText("secrets.json");
        Dictionary<string, string> dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)!;
        s_discord = dict["discord"];
        s_coc = dict["coc"];
    }
}