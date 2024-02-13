using ClashOfClans;
using ClashOfClans.Models;
using Hyperstellar.Discord;
using Hyperstellar.Sql;

namespace Hyperstellar.Clash;

internal static class Coc
{
    private const string ClanId = "#2QU2UCJJC"; // 2G8LP8PVV
    private static readonly ClashOfClansClient s_client = new(Secrets.s_coc);
    internal static ClanUtil Clan { get; private set; } = new();
    internal static event Action<ClanMember>? s_eventMemberJoined;
    internal static event Action<ClanMember, string?>? s_eventMemberLeft;
    internal static event Func<Dictionary<string, DonationTuple>, Task>? s_eventDonation;
    internal static event Func<Dictionary<string, DonationTuple>, Task>? s_eventDonationFolded;

    static Coc() => Dc.s_eventBotReady += BotReadyAsync;

    private static async Task BotReadyAsync()
    {
        while (true)
        {
            try
            {
                await PollAsync();
            }
            catch (Exception ex)
            {
                await Dc.ExceptionAsync(ex);
            }
            await Task.Delay(20000);
        }
    }

    private static void CheckMembersJoined(ClanUtil clan)
    {
        if (clan._joiningMembers.Count == 0)
        {
            return;
        }

        string[] members = [.. clan._joiningMembers.Keys];
        bool isSuccess = Db.AddMembers(members);
        string membersMsg = string.Join(", ", members);
        if (isSuccess)
        {
            Console.WriteLine($"{membersMsg} joined");
        }
        else
        {
            Console.Error.WriteLine($"ERROR MembersJoined {membersMsg}");
        }

        foreach (ClanMember m in clan._joiningMembers.Values)
        {
            s_eventMemberJoined!(m);
        }
    }

    private static void CheckMembersLeft(ClanUtil clan)
    {
        if (clan._leavingMembers.Count == 0)
        {
            return;
        }

        foreach ((string id, ClanMember member) in clan._leavingMembers)
        {
            IEnumerable<Alt> alts = new Member(id).GetAltsByMain();
            string? altId = null;
            if (alts.Any())
            {
                Alt alt = alts.First();
                altId = alt.AltId;
                for (int i = 1; i < alts.Count(); i++)
                {
                    alts.ElementAt(i).UpdateMain(alt.AltId);
                }
                alt.Delete();
            }
            s_eventMemberLeft!(member, altId);  // This is before Db.DelMem below so that we can remap Donation to new mainId
        }

        string[] members = [.. clan._leavingMembers.Keys];
        bool isSuccess = Db.DeleteMembers(members);
        string membersMsg = string.Join(", ", members);
        if (isSuccess)
        {
            Console.WriteLine($"{membersMsg} left");
        }
        else
        {
            Console.Error.WriteLine($"ERROR MembersLeft {membersMsg}");
        }
    }

    private static async Task<Clan> GetClanAsync() => await s_client.Clans.GetClanAsync(ClanId);

    private static async Task PollAsync()
    {
        Clan clan = await GetClanAsync();

        if (clan.MemberList == null)
        {
            return;
        }

        ClanUtil clanUtil = ClanUtil.FromPoll(clan);
        CheckMembersJoined(clanUtil);
        CheckMembersLeft(clanUtil);
        await Task.WhenAll([
            CheckDonationsAsync(clanUtil)
        ]);
        Clan = clanUtil;
    }

    private static async Task CheckDonationsAsync(ClanUtil clan)
    {
        Dictionary<string, DonationTuple> donDelta = [];
        foreach (string tag in clan._existingMembers.Keys)
        {
            ClanMember current = clan._members[tag];
            ClanMember previous = Clan._members[tag];
            if (current.Donations > previous.Donations || current.DonationsReceived > previous.DonationsReceived)
            {
                donDelta[current.Tag] = new(current.Donations - previous.Donations, current.DonationsReceived - previous.DonationsReceived);
            }
        }

        foreach (KeyValuePair<string, DonationTuple> dd in donDelta)
        {
            Console.WriteLine($"{dd.Key}: {dd.Value._donated} {dd.Value._received}");
        }

        // Fold alt data into main
        Dictionary<string, DonationTuple> foldedDelta = [];
        foreach (KeyValuePair<string, DonationTuple> delta in donDelta)
        {
            string tag = delta.Key;
            DonationTuple dt = delta.Value;
            Alt? alt = new Member(tag).TryToAlt();
            if (alt != null)
            {
                tag = alt.MainId;
            }
            foldedDelta[tag] = foldedDelta.TryGetValue(tag, out DonationTuple value) ? value.Add(dt) : dt;
        }

        if (foldedDelta.Count > 0)
        {
            Console.WriteLine("---");
        }

        foreach (KeyValuePair<string, DonationTuple> dd in foldedDelta)
        {
            Console.WriteLine($"{dd.Key}: {dd.Value._donated} {dd.Value._received}");
        }

        ICollection<Task> tasks = [];
        if (donDelta.Count > 0)
        {
            tasks.Add(s_eventDonation!(donDelta));
        }
        if (foldedDelta.Count > 0)
        {
            tasks.Add(s_eventDonationFolded!(foldedDelta));
        }
        await Task.WhenAll(tasks);
    }

    internal static string? GetMemberId(string name)
    {
        ClanMember? result = Clan._clan.MemberList!.FirstOrDefault(m => m.Name == name);
        return result?.Tag;
    }

    internal static ClanMember GetMember(string id) => Clan._members[id];

    internal static async Task InitAsync() => Clan = ClanUtil.FromInit(await GetClanAsync());
}
