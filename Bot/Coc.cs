using ClashOfClans;
using ClashOfClans.Models;

namespace Hyperstellar;

internal class Coc
{

    internal readonly struct DonationTuple(int donated, int received)
    {
        internal readonly int Donated = donated;
        internal readonly int Received = received;
    }

    private class ClanUtil
    {
        internal readonly Clan Clan;
        internal readonly Dictionary<string, ClanMember> Members = [];
        internal readonly Dictionary<string, ClanMember> ExistingMembers = [];
        internal readonly Dictionary<string, ClanMember> JoiningMembers = [];
        internal readonly Dictionary<string, ClanMember> LeavingMembers;

        public ClanUtil(Clan clan)
        {
            LeavingMembers = _prevClan.Members;
            foreach (var member in clan.MemberList!)
            {
                Members[member.Tag] = member;
                if (_prevClan.HasMember(member))
                {
                    ExistingMembers[member.Tag] = member;
                    LeavingMembers.Remove(member.Tag);
                } else
                {
                    JoiningMembers[member.Tag] = member;
                }
            }
            Clan = clan;
        }

        internal ClanUtil(Clan clan, bool init)
        {
            LeavingMembers = [];
            foreach (var member in clan.MemberList!)
            {
                Members[member.Tag] = member;
            }
            Clan = clan;
        }

        internal bool HasMember(ClanMember member) => Members.ContainsKey(member.Tag);
    }

    private class MemberComparer : IEqualityComparer<ClanMember>
    {
        public bool Equals(ClanMember? x, ClanMember? y) => x!.Tag.Equals(y!.Tag);

        public int GetHashCode(ClanMember obj) => obj.Tag.GetHashCode();
    }

    const string CLAN_ID = "#2QU2UCJJC";
    const string DIM_ID = "#28QL0CJV2";
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private static ClanUtil _prevClan;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    internal static readonly ClashOfClansClient Client = new(Secrets.Coc);

    private static async Task<Clan> GetClanAsync() => await Client.Clans.GetClanAsync(CLAN_ID);

    private static async Task PollAsync()
    {
        var clan = await GetClanAsync();
        if (clan == null) return;
        if (clan.MemberList == null) return;
        ClanUtil clanUtil = new(clan);
        await CheckDonations(clanUtil);
        _prevClan = clanUtil;
    }

    private static async Task CheckDonations(ClanUtil clan)
    {
        Dictionary<string, DonationTuple> donationsDelta = [];
        foreach (var tag in clan.ExistingMembers.Keys)
        {
            var current = clan.Members[tag];
            var previous = _prevClan.Members[tag];
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

    internal static async Task InitAsync() => _prevClan = new(await GetClanAsync(), true);

    internal static async Task BotReadyAsync()
    {
        while (true)
        {
            await PollAsync();
            await Task.Delay(5000);
        }
    }
}