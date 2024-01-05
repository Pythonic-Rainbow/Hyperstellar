using Newtonsoft.Json;

internal class Secrets
{
    internal static readonly string Discord;
    internal static readonly string Coc;

    static Secrets()
    {
        string json = File.ReadAllText("secrets.json");
        Dictionary<string, string> dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)!;
        Discord = dict["discord"];
        Coc = dict["coc"];
    }
}