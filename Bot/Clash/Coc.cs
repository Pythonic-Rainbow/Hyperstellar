using ClashOfClans;
using ClashOfClans.Models;
using Hyperstellar.Sql;
using static Hyperstellar.Discord.Dc;

namespace Hyperstellar.Clash;

internal static class Coc
{

    internal readonly struct DonationTuple(int donated, int received)
    {
        internal readonly int _donated = donated;
        internal readonly int _received = received;

        internal DonationTuple Add(DonationTuple dt) => new(_donated + dt._donated, _received + dt._received);
    }

    private const string ClanId = "#2QU2UCJJC"; // 2G8LP8PVV
    private static readonly ClashOfClansClient s_client = new(Secrets.s_coc);
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    internal static ClanUtil s_clan { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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

        foreach (string id in clan._joiningMembers.Keys)
        {
            Donate25.MemberAdded(id);
        }
    }

    private static void CheckMembersLeft(ClanUtil clan)
    {
        if (clan._leavingMembers.Count == 0)
        {
            return;
        }

        foreach (string id in clan._leavingMembers.Keys)
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
            Donate25.MemberRemoved(id, altId);
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
        if (clan == null)
        {
            return;
        }

        if (clan.MemberList == null)
        {
            return;
        }

        ClanUtil clanUtil = ClanUtil.FromPoll(clan);
        CheckMembersJoined(clanUtil);
        CheckMembersLeft(clanUtil);
        await Task.WhenAll([
            CheckDonationsAsync(clanUtil),
        ]);
        s_clan = clanUtil;
    }

    private static async Task CheckDonationsAsync(ClanUtil clan)
    {
        Dictionary<string, DonationTuple> donationsDelta = [];
        Dictionary<string, DonationTuple> donationsTagDelta = [];
        foreach (string tag in clan._existingMembers.Keys)
        {
            ClanMember current = clan._members[tag];
            ClanMember previous = s_clan._members[tag];
            if (current.Donations > previous.Donations || current.DonationsReceived > previous.DonationsReceived)
            {
                donationsDelta[current.Name] = new(current.Donations - previous.Donations, current.DonationsReceived - previous.DonationsReceived);
                donationsTagDelta[current.Tag] = new(current.Donations - previous.Donations, current.DonationsReceived - previous.DonationsReceived);
            }

        }


        foreach (KeyValuePair<string, DonationTuple> dd in donationsTagDelta)
        {
            Console.WriteLine($"{dd.Key}: {dd.Value._donated} {dd.Value._received}");
        }

        // Fold alt data into main
        Dictionary<string, DonationTuple> folded = [];
        foreach (KeyValuePair<string, DonationTuple> donDelta in donationsTagDelta)
        {
            string tag = donDelta.Key;
            DonationTuple dt = donDelta.Value;
            Member member = new(tag);
            Alt? alt = member.TryToAlt();
            if (alt != null)
            {
                tag = alt.MainId;
            }
            folded[tag] = folded.TryGetValue(tag, out DonationTuple value) ? value.Add(dt) : dt;
        }
        donationsTagDelta = folded;

        if (donationsTagDelta.Count > 0)
        {
            Console.WriteLine("---");
        }

        foreach (KeyValuePair<string, DonationTuple> dd in donationsTagDelta)
        {
            Console.WriteLine($"{dd.Key}: {dd.Value._donated} {dd.Value._received}");
        }

        // Everyone is main now
        foreach (KeyValuePair<string, DonationTuple> donDelta in donationsTagDelta)
        {
            string tag = donDelta.Key;
            DonationTuple dt = donDelta.Value;
            int donated = dt._donated;
            int received = dt._received;

            if (donated > received)
            {
                donated -= received;
                Donation donation = Db.GetDonation(tag)!;
                donation.Donated += (uint)donated;
                Console.WriteLine($"[Donate25] {tag} {donated}");
                Db.UpdateDonation(donation);
            }
        }

        if (donationsDelta.Count > 0)
        {
            await DonationsChangedAsync(donationsDelta);
        }
    }

    internal static string? GetMemberId(string name)
    {
        ClanMember? result = s_clan._clan.MemberList!.FirstOrDefault(m => m.Name == name);
        return result?.Tag;
    }

    internal static ClanMember GetMember(string id) => s_clan._members[id];

    internal static async Task InitAsync() => s_clan = ClanUtil.FromInit(await GetClanAsync());

    internal static async Task BotReadyAsync()
    {
        Donate25.Init();
        _ = Task.Run(Donate25.CheckAsync);
        while (true)
        {
            await PollAsync();
            await Task.Delay(20000);
        }
    }
}