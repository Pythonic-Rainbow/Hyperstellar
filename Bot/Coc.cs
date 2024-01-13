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
        internal readonly Dictionary<string, ClanMember> Members = [];
        internal readonly Clan Clan;
        internal readonly IEnumerable<ClanMember> existingMembers;

        public ClanUtil(Clan clan)
        {
            foreach (var member in clan.MemberList!)
            {
                Members[member.Tag] = member;
            }
            Clan = clan;
            existingMembers = clan.MemberList!.Intersect(_prevClan.Clan.MemberList!, new MemberComparer());
        }
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
        var existingMemberTags = clan.existingMembers.Select(x => x.Tag);
        foreach (var tag in existingMemberTags)
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

    internal static async Task InitAsync() => _prevClan = new(await GetClanAsync());

    internal static async Task BotReadyAsync()
    {
        while (true)
        {
            await PollAsync();
            await Task.Delay(5000);
        }
    }
}