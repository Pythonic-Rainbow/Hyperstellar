using ClashOfClans;
using ClashOfClans.Models;

namespace Hyperstellar;

internal class Coc
{
    private class ClanUtil
    {
        internal readonly Clan _clan;
        internal readonly Dictionary<string, ClanMember> _members = [];
        internal readonly Dictionary<string, ClanMember> _existingMembers = [];
        internal readonly Dictionary<string, ClanMember> _joiningMembers = [];
        internal readonly Dictionary<string, ClanMember> _leavingMembers;

        internal ClanUtil(Clan clan)
        {
            _leavingMembers = s_prevClan._members;
            foreach (ClanMember member in clan.MemberList!)
            {
                _members[member.Tag] = member;
                if (s_prevClan.HasMember(member))
                {
                    _existingMembers[member.Tag] = member;
                    _ = _leavingMembers.Remove(member.Tag);
                }
                else
                {
                    _joiningMembers[member.Tag] = member;
                }
            }
            _clan = clan;
        }

        internal ClanUtil(Clan clan, bool init)
        {
            _leavingMembers = [];
            foreach (ClanMember member in clan.MemberList!)
            {
                _members[member.Tag] = member;
            }
            _clan = clan;
        }

        internal bool HasMember(ClanMember member) => _members.ContainsKey(member.Tag);
    }

    internal readonly struct DonationTuple(int donated, int received)
    {
        internal readonly int _donated = donated;
        internal readonly int _received = received;
    }

    private const string ClanId = "#2QU2UCJJC";
    internal static readonly ClashOfClansClient s_client = new(Secrets.Coc);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private static ClanUtil s_prevClan;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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

        ClanUtil clanUtil = new(clan);
        await CheckDonations(clanUtil);
        s_prevClan = clanUtil;
    }

    private static async Task CheckDonations(ClanUtil clan)
    {
        Dictionary<string, DonationTuple> donationsDelta = [];
        foreach (string tag in clan._existingMembers.Keys)
        {
            ClanMember current = clan._members[tag];
            ClanMember previous = s_prevClan._members[tag];
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

    internal static async Task InitAsync() => s_prevClan = new(await GetClanAsync(), true);

    internal static async Task BotReadyAsync()
    {
        while (true)
        {
            await PollAsync();
            await Task.Delay(5000);
        }
    }
}