using System.Diagnostics.CodeAnalysis;
using ClashOfClans;
using ClashOfClans.Core;
using ClashOfClans.Models;
using ClashOfClans.Search;
using Hyperstellar.Discord;
using Hyperstellar.Sql;

namespace Hyperstellar.Clash;

internal static class Coc
{
    private class RaidAttackerComparer : IEqualityComparer<ClanCapitalRaidSeasonAttacker>
    {
        public bool Equals(ClanCapitalRaidSeasonAttacker? x, ClanCapitalRaidSeasonAttacker? y) => x!.Tag == y!.Tag;
        public int GetHashCode([DisallowNull] ClanCapitalRaidSeasonAttacker obj) => obj.Tag.GetHashCode();
    }

    private const string ClanId = "#2QU2UCJJC"; // 2G8LP8PVV
    private static readonly ClashOfClansClient s_client = new(Secrets.s_coc);
    private static ClashOfClansException? s_exception;
    private static ClanCapitalRaidSeason s_raidSeason;
    internal static ClanUtil Clan { get; private set; } = new();
    internal static event Action<ClanMember, Main> EventMemberJoined;
    internal static event Action<ClanMember, string?> EventMemberLeft;
    internal static event Action<ClanCapitalRaidSeason> EventInitRaid;
    internal static event Action<ClanCapitalRaidSeason> EventRaidCompleted;
    internal static event Func<Dictionary<string, DonationTuple>, Task> EventDonated;
    internal static event Func<Dictionary<string, DonationTuple>, Task> EventDonatedFold;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    static Coc() => Dc.EventBotReady += BotReadyAsync;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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

    private static async Task BotReadyAsync()
    {
        while (true)
        {
            try
            {
                await PollAsync();
                s_exception = null;
                await Task.Delay(10000);
            }
            catch (ClashOfClansException ex)
            {
                if (s_exception == null || s_exception.Error.Reason != ex.Error.Reason || s_exception.Error.Message != ex.Error.Message)
                {
                    s_exception = ex;
                    await Dc.ExceptionAsync(ex);
                }
                await Task.Delay(60000);
            }
            catch (Exception ex)
            {
                await Dc.ExceptionAsync(ex);
                await Task.Delay(60000);
            }
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

    private static async Task PollRaidAsync()
    {
        static async Task WaitRaidAsync()
        {
            await Task.Delay(s_raidSeason.EndTime - DateTime.UtcNow);
            s_raidSeason = await GetRaidSeasonAsync();
            while (s_raidSeason.State != ClanCapitalRaidSeasonState.Ended)
            {
                await Task.Delay(20000);
                s_raidSeason = await GetRaidSeasonAsync();
            }
            EventRaidCompleted(s_raidSeason);
        }

        // Check if there is an ongoing raid
        if (s_raidSeason.EndTime > DateTime.UtcNow)
        {
            await WaitRaidAsync();
        }
        while (true)
        {
            await Task.Delay(60 * 60 * 1000); // 1 hour
            ClanCapitalRaidSeason season = await GetRaidSeasonAsync();
            if (season.StartTime != s_raidSeason.StartTime) // New season started
            {
                s_raidSeason = season;
                await WaitRaidAsync();
            }
        }
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
            tasks.Add(EventDonated(donDelta));
        }
        if (foldedDelta.Count > 0)
        {
            tasks.Add(EventDonatedFold(foldedDelta));
        }
        await Task.WhenAll(tasks);
    }

    internal static string? GetMemberId(string name)
    {
        ClanMember? result = Clan._clan.MemberList!.FirstOrDefault(m => m.Name == name);
        return result?.Tag;
    }

    internal static ClanMember GetMember(string id) => Clan._members[id];

    internal static HashSet<ClanCapitalRaidSeasonAttacker> GetRaidAttackers(ClanCapitalRaidSeason season)
    {
        HashSet<ClanCapitalRaidSeasonAttacker> set = new(new RaidAttackerComparer());
        foreach (ClanCapitalRaidSeasonAttackLogEntry capital in season.AttackLog)
        {
            foreach (ClanCapitalRaidSeasonDistrict district in capital.Districts)
            {
                if (district.Attacks != null)
                {
                    foreach (ClanCapitalRaidSeasonAttack atk in district.Attacks)
                    {
                        set.Add(atk.Attacker);
                    }
                }
            }
        }
        return set;
    }

    internal static async Task<ClanCapitalRaidSeason> GetRaidSeasonAsync()
    {
        Query query = new() { Limit = 1 };
        ClanCapitalRaidSeasons seasons = (ClanCapitalRaidSeasons)await s_client.Clans.GetCapitalRaidSeasonsAsync(ClanId, query);
        return seasons.First();
    }

    internal static async Task InitAsync()
    {
        static async Task InitClanAsync() { Clan = ClanUtil.FromInit(await GetClanAsync()); }
        static async Task InitRaidAsync()
        {
            ClanCapitalRaidSeason season = await GetRaidSeasonAsync();
            // If last raid happened within a week, we count it as valid
            EventInitRaid(season);
            s_raidSeason = season;
            _ = Task.Run(PollRaidAsync);
        }

        await Task.WhenAll([InitClanAsync(), InitRaidAsync()]);
    }
}
