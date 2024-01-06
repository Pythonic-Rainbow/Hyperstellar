using ClashOfClans;
using ClashOfClans.Models;

namespace Hyperstellar;

internal class Coc
{

    internal readonly struct DonationTuple
    {
        internal readonly int Donated;
        internal readonly int Received;

        public DonationTuple(int donated, int received)
        {
            Donated = donated;
            Received = received;
        }
    }

    internal readonly struct ClanMemberInfo
    {
        internal readonly string Name;
        internal readonly DonationTuple Donation;

        public ClanMemberInfo(string name, DonationTuple donation)
        {
            Name = name;
            Donation = donation;
        }

        internal readonly int Donated => Donation.Donated;
        internal readonly int Received => Donation.Received;
    }

    private readonly struct ClanInfo
    {
        internal readonly Dictionary<string, ClanMemberInfo> Members = new();

        public ClanInfo(Clan clan)
        {
            foreach (var member in clan.MemberList!)
            {
                DonationTuple donation = new(member.Donations, member.DonationsReceived);
                Members[member.Tag] = new(member.Name, donation);
            }
        }
    }

    const string CLAN_ID = "#2QU2UCJJC";
    const string DIM_ID = "#28QL0CJV2";
    private static ClanInfo _prevClan;
    internal static readonly ClashOfClansClient Client = new(Secrets.Coc);

    private static async Task<Clan> GetClanAsync() => await Client.Clans.GetClanAsync(CLAN_ID);

    private static async Task PollAsync()
    {
        var clan = await GetClanAsync();
        ClanInfo clanInfo = new(clan);
        await CheckDonations(clanInfo, clan);
        _prevClan = clanInfo;
    }

    private static async Task CheckDonations(ClanInfo clan, Clan c)
    {
        Dictionary<string, DonationTuple> donationsDelta = new();
        foreach (var memberTag in clan.Members.Keys)
        {
            bool existsInPrevClan = _prevClan.Members.TryGetValue(memberTag, out ClanMemberInfo previous);
            if (existsInPrevClan)
            {
                ClanMemberInfo current = clan.Members[memberTag];
                if (current.Donated > previous.Donated || current.Received > previous.Received)
                {
                    donationsDelta[current.Name] = new(current.Donated - previous.Donated, current.Received - previous.Received);
                }
            }
        }
        if (donationsDelta.Count > 0)
        {
            await Discord.DonationsChangedAsync(donationsDelta);
        }
    }

    internal static async Task InitAsync()
    {
        _prevClan = new(await GetClanAsync());
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