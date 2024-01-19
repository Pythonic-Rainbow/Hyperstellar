using ClashOfClans;
using ClashOfClans.Models;
using Hyperstellar.Sql;
using static Hyperstellar.Dc.Discord;

namespace Hyperstellar;

internal sealed class Coc
{
    private sealed class ClanUtil
    {
        internal Clan _clan;
        internal readonly Dictionary<string, ClanMember> _members = [];
        internal readonly Dictionary<string, ClanMember> _existingMembers = [];
        internal readonly Dictionary<string, ClanMember> _joiningMembers = [];
        internal Dictionary<string, ClanMember> _leavingMembers;

        private ClanUtil(Clan clan, Dictionary<string, ClanMember> leavingMembers)
        {
            _clan = clan;
            _leavingMembers = leavingMembers;
        }

        internal static ClanUtil FromInit(Clan clan)
        {
            ClanUtil c = new(clan, []);
            IEnumerable<string> existingMembers = Db.GetMembers().Select(m => m.CocId);
            foreach (string dbMember in existingMembers)
            {
                bool stillExists = false;
                foreach (ClanMember clanMember in clan.MemberList!)
                {
                    if (clanMember.Tag.Equals(dbMember))
                    {
                        c._members[dbMember] = clanMember;
                        clan.MemberList.Remove(clanMember);
                        stillExists = true;
                        break;
                    }
                }
                if (!stillExists)
                {
                    c._members[dbMember] = new();  // Fake a member
                }
            }
            return c;
        }

        internal static ClanUtil FromPoll(Clan clan)
        {
            ClanUtil c = new(clan, new(s_prevClan._members));
            foreach (ClanMember member in clan.MemberList!)
            {
                c._members[member.Tag] = member;
                if (s_prevClan.HasMember(member))
                {
                    c._existingMembers[member.Tag] = member;
                    c._leavingMembers.Remove(member.Tag);
                }
                else
                {
                    c._joiningMembers[member.Tag] = member;
                }
            }
            return c;
        }

        internal bool HasMember(ClanMember member) => _members.ContainsKey(member.Tag);
    }

    internal readonly struct DonationTuple(int donated, int received)
    {
        internal readonly int _donated = donated;
        internal readonly int _received = received;
    }

    private const string ClanId = "#2QU2UCJJC";
    internal static readonly ClashOfClansClient s_client = new(Secrets.s_coc);
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

        ClanUtil clanUtil = ClanUtil.FromPoll(clan);
        CheckMembersJoined(clanUtil);
        CheckMembersLeft(clanUtil);
        await Task.WhenAll([
            CheckDonationsAsync(clanUtil),
        ]);
        s_prevClan = clanUtil;
    }

    private static async Task CheckDonationsAsync(ClanUtil clan)
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
            await DonationsChangedAsync(donationsDelta);
        }
    }

    private static void CheckMembersJoined(ClanUtil clan)
    {
        if (clan._joiningMembers.Count > 0)
        {
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
        }
    }

    private static void CheckMembersLeft(ClanUtil clan)
    {
        if (clan._leavingMembers.Count > 0)
        {
            string[] members = [.. clan._leavingMembers.Keys];
            bool isSuccess = Db.RemoveMembers(members);
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
    }

    internal static async Task InitAsync() => s_prevClan = ClanUtil.FromInit(await GetClanAsync());

    internal static async Task BotReadyAsync()
    {
        while (true)
        {
            await PollAsync();
            await Task.Delay(5000);
        }
    }
}
