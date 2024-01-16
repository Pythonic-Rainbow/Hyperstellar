using ClashOfClans;
using ClashOfClans.Models;

namespace Hyperstellar;

internal class Coc
{
    private class ClanUtil
    {
        internal readonly Clan Clan;
        internal readonly Dictionary<string, ClanMember> Members = [];
        internal readonly Dictionary<string, ClanMember> ExistingMembers = [];
        internal readonly Dictionary<string, ClanMember> JoiningMembers = [];
        internal readonly Dictionary<string, ClanMember> LeavingMembers;

        internal ClanUtil(Clan clan)
        {
            LeavingMembers = s_prevClan.Members;
            foreach (ClanMember member in clan.MemberList!)
            {
                Members[member.Tag] = member;
                if (s_prevClan.HasMember(member))
                {
                    ExistingMembers[member.Tag] = member;
                    LeavingMembers.Remove(member.Tag);
                }
                else
                {
                    JoiningMembers[member.Tag] = member;
                }
            }
            Clan = clan;
        }

        internal ClanUtil(Clan clan, bool init)
        {
            LeavingMembers = [];
            foreach (ClanMember member in clan.MemberList!)
            {
                Members[member.Tag] = member;
            }
            Clan = clan;
        }

        internal bool HasMember(ClanMember member)
        {
            return Members.ContainsKey(member.Tag);
        }
    }

    internal readonly struct DonationTuple(int donated, int received)
    {
        internal readonly int Donated = donated;
        internal readonly int Received = received;
    }

    private const string ClanId = "#2QU2UCJJC";
    internal static readonly ClashOfClansClient s_client = new(Secrets.Coc);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private static ClanUtil s_prevClan;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private static async Task<Clan> GetClanAsync()
    {
        return await s_client.Clans.GetClanAsync(ClanId);
    }

    private static async Task PollAsync()
    {
        var clan = await GetClanAsync();
        if (clan == null) return;
        if (clan.MemberList == null) return;
        ClanUtil clanUtil = new(clan);
        await CheckDonations(clanUtil);
        s_prevClan = clanUtil;
    }

    private static async Task CheckDonations(ClanUtil clan)
    {
        Dictionary<string, DonationTuple> donationsDelta = [];
        foreach (string tag in clan.ExistingMembers.Keys)
        {
            ClanMember current = clan.Members[tag];
            ClanMember previous = s_prevClan.Members[tag];
            if (current.Donations > previous.Donations || current.DonationsReceived > previous.DonationsReceived)
            {
                donationsDelta[current.Name] = new(current.Donations - previous.Donations, current.DonationsReceived - previous.DonationsReceived);
            }
        }
        if (donationsDelta.Count > 0)
        {
            await Discord.DonationsChangedAsync(donationsDelta);
        }
    }

    internal static async Task InitAsync()
    {
        s_prevClan = new(await GetClanAsync(), true);
    }

    internal static async Task BotReadyAsync()
    {
        while (true)
        {
            await PollAsync();
            await Task.Delay(5000);
        }
    }
}