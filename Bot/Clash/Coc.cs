using ClashOfClans;
using ClashOfClans.Core;
using ClashOfClans.Models;
using Hyperstellar.Discord;
using Hyperstellar.Sql;

namespace Hyperstellar.Clash;

internal static class Coc
{
    private const string ClanId = "#2QU2UCJJC"; // 2G8LP8PVV
    private static readonly ClashOfClansClient s_client = new(Secrets.s_coc);
    private static bool s_inMaintenance;
    internal static ClanUtil Clan { get; private set; } = new();
    internal static event Action<ClanMember, Main>? EventMemberJoined;
    internal static event Action<ClanMember, string?>? EventMemberLeft;
    internal static event Func<Dictionary<string, DonationTuple>, Task>? EventDonated;
    internal static event Func<Dictionary<string, DonationTuple>, Task>? EventDonatedFold;

    static Coc() => Dc.EventBotReady += BotReadyAsync;

    private static async Task BotReadyAsync()
    {
        while (true)
        {
            try
            {
                await PollAsync();
                s_inMaintenance = false;
                await Task.Delay(10000);
            }
            catch (ClashOfClansException ex)
            {
                if (ex.Error.Reason == "inMaintenance")
                {
                    if (!s_inMaintenance)
                    {
                        s_inMaintenance = true;
                        await Dc.SendLogAsync(ex.Error.Message);
                    }
                    await Task.Delay(60000);
                }
                else
                {
                    await Dc.ExceptionAsync(ex);
                }
            }
            catch (Exception ex)
            {
                await Dc.ExceptionAsync(ex);
            }
        }
    }

    private static void CheckMembersJoined(ClanUtil clan)
    {
        if (clan._joiningMembers.Count == 0)
        {
            return;
        }

        string[] members = [.. clan._joiningMembers.Keys];
        Db.AddMembers(members);
        string membersMsg = string.Join(", ", members);
        Console.WriteLine($"{membersMsg} joined");

        foreach (ClanMember m in clan._joiningMembers.Values)
        {
            Main main = new(m.Tag);
            EventMemberJoined!(m, main);
            main.Insert();
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
            Main main = Db.GetMain(id)!;
            main.Delete();
            if (alts.Any())
            {
                Alt alt = alts.First();
                altId = alt.AltId;
                for (int i = 1; i < alts.Count(); i++)
                {
                    alts.ElementAt(i).UpdateMain(alt.AltId);
                }
                alt.Delete();
                // Maybe adapt this in the future if need to modify attributes when replacing main
                main.MainId = altId;
                main.Insert();
            }
            // This is before Db.DelMem below so that we can remap Donation to new mainId
            // ^ No longer true because the remap is done ABOVE now but I'll still leave this comment
            EventMemberLeft!(member, altId);
        }

        string[] members = [.. clan._leavingMembers.Keys];
        Db.DeleteMembers(members);
        string membersMsg = string.Join(", ", members);
        Console.WriteLine($"{membersMsg} left");
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
            tasks.Add(EventDonated!(donDelta));
        }
        if (foldedDelta.Count > 0)
        {
            tasks.Add(EventDonatedFold!(foldedDelta));
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
