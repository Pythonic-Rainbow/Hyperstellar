using ClashOfClans;
using ClashOfClans.Core;
using ClashOfClans.Models;
using ClashOfClans.Search;
using Hyperstellar.Discord;
using Hyperstellar.Sql;
using QuikGraph;
using QuikGraph.Algorithms.MaximumFlow;

namespace Hyperstellar.Clash;

internal static class Coc
{
    private sealed class RaidAttackerComparer : IEqualityComparer<ClanCapitalRaidSeasonAttacker>
    {
        public bool Equals(ClanCapitalRaidSeasonAttacker? x, ClanCapitalRaidSeasonAttacker? y) => x!.Tag == y!.Tag;
        public int GetHashCode(ClanCapitalRaidSeasonAttacker obj) => obj.Tag.GetHashCode();
    }

    private const string ClanId = "#2QU2UCJJC"; // 2G8LP8PVV
    private static ClashOfClansClient s_client;
    private static ClanCapitalRaidSeason s_raidSeason;
    internal static Clan s_clan;
    internal static event Action<ClanMember, Main> EventMemberJoined;
    internal static event Action<Account[]> EventMemberLeft;
    internal static event Action<ClanCapitalRaidSeason> EventInitRaid;
    internal static event Action<ClanCapitalRaidSeason> EventRaidCompleted;
    internal static event Action<IEnumerable<Tuple<string, int>>> EventDonatedMaxFlow;
    internal static event Func<IEnumerable<Tuple<string, int>>, IEnumerable<Tuple<string, int>>, Task> EventDonated;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    static Coc()
    {
        EventMemberLeft += MembersLeft;
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private static void CheckMembersJoined(ClanUtil clan)
    {
        if (clan._joiningMembers.Count == 0)
        {
            return;
        }

        string[] members = [.. clan._joiningMembers.Keys];
        Account[] memberObjects = [.. members.Select(static memberId => new Account(memberId))];
        Db.InsertAll(memberObjects);
        string membersMsg = string.Join(", ", members);
        Console.WriteLine($"{membersMsg} joined");

        foreach (ClanMember m in clan._joiningMembers.Values)
        {
            Main main = new(m.Tag);
            EventMemberJoined(m, main);
            main.Insert();
        }
    }

    private static void MembersLeft(Account[] leftMembers) => Console.WriteLine($"{string.Join(",", leftMembers.Select(a => a.Id))} left");

    private static async Task<Clan> GetClanAsync() => await s_client.Clans.GetClanAsync(ClanId);

    // Poll clan members
    private static async Task PollClanAsync()
    {
        // Fetch data for comparison
        s_clan = await GetClanAsync();
        if (s_clan.MemberList == null)
        {
            throw new InvalidDataException("Fetched clan member list is null bruh");
        }
        IEnumerable<Account> accounts = Account.FetchAll();

        // Some extra info
        long nowTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string[] memberIds = [.. s_clan.MemberList.Select(m => m.Tag)];

        /* NOTE: We must compare new clan info with db because the db now stores left members as well. */

        /* Extract members and accounts changed */
        Account[] leftMembers = [.. Account.FetchMembers().ExceptBy(memberIds, a => a.Id)];
        foreach (Account account in leftMembers)
        {
            account.LeftTime = nowTime;
        }

        IEnumerable<ClanMember> newMembers = s_clan.MemberList.ExceptBy(accounts.Select(a => a.Id), m => m.Tag);
        IEnumerable<Account> newJoinAccounts = newMembers.Select(m => new Account(m.Tag));

        Account[] prevJoinAccounts = [.. Account.FetchLeft().IntersectBy(memberIds, a => a.Id)];
        foreach (Account account in prevJoinAccounts)
        {
            account.LeftTime = null;
        }

        // Update db
        Db.UpdateAll(leftMembers.Concat(prevJoinAccounts));
        Db.InsertAll(newJoinAccounts);

        // Dispatch events
        if (leftMembers.Length > 0)
        {
            EventMemberLeft(leftMembers);
        }
        CheckMembersJoined(clanUtil);
        await Task.WhenAll([
            CheckDonationsAsync(clanUtil)
        ]);

    }

    private static async Task PollRaidAsync()
    {
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

        static async Task WaitRaidAsync()
        {
            await Task.Delay(s_raidSeason.EndTime - DateTime.UtcNow);
            s_raidSeason = await GetRaidSeasonAsync();
            while (s_raidSeason.State != ClanCapitalRaidSeasonState.Ended)
            {
                await Task.Delay(20000);
                s_raidSeason = await GetRaidSeasonAsync();
            }

            // Raid ended
            Raid[] raids = new Raid[s_raidSeason.Members!.Count];
            long timestamp = ((DateTimeOffset)s_raidSeason.EndTime).ToUnixTimeSeconds();
            for (int i = 0; i < s_raidSeason.Members.Count; i++)
            {
                ClanCapitalRaidSeasonMember attacker = s_raidSeason.Members[i];
                raids[i] = new(timestamp, attacker.Tag, attacker.Attacks);
            }

            Db.InsertAll(raids);

            EventRaidCompleted(s_raidSeason);
        }
    }

    private static async Task<ClanCapitalRaidSeason> GetRaidSeasonAsync()
    {
        Query query = new() { Limit = 1 };
        ClanCapitalRaidSeasons seasons =
            (ClanCapitalRaidSeasons)await s_client.Clans.GetCapitalRaidSeasonsAsync(ClanId, query);
        return seasons.First();
    }

    private static async Task CheckDonationsAsync(ClanUtil clan)
    {
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Dictionary<string, Donation> donations = [];

        List<Tuple<string, int>> donDelta = [], recDelta = [];
        Dictionary<string, string> accToMainAcc = [];
        AdjacencyGraph<string, TaggedEdge<string, int>> graph = new(false);
        graph.AddVertexRange(["s", "t"]);

        foreach (string tag in clan._existingMembers.Keys)
        {
            ClanMember current = clan._members[tag];
            ClanMember previous = current; //Clan._members[tag];

            if (current.Donations > previous.Donations)
            {
                int donated = current.Donations - previous.Donations;

                Donation donation = new(currentTime, tag)
                {
                    Donated = donated
                };
                donations[tag] = donation;

                donDelta.Add(new(current.Tag, donated));

                graph.AddVertex(current.Tag);
                graph.AddEdge(new("s", current.Tag, donated));

                accToMainAcc.TryAdd(current.Tag, new Account(current.Tag).GetEffectiveMain().MainId);
            }
        }

        foreach (string tag in clan._existingMembers.Keys)
        {
            ClanMember current = clan._members[tag];
            ClanMember previous = current; //Clan._members[tag];

            if (current.DonationsReceived > previous.DonationsReceived)
            {
                string vertexName = $"#{current.Tag}"; // Double # for received node
                int received = current.DonationsReceived - previous.DonationsReceived;
                if (donations.TryGetValue(tag, out Donation? value))
                {
                    value.Received = received;
                }
                else
                {
                    Donation donation = new(currentTime, tag)
                    {
                        Received = received
                    };

                    donations[tag] = donation;
                }

                recDelta.Add(new(current.Tag, received));
                graph.AddVertex(vertexName);
                graph.AddEdge(new(vertexName, "t", received));

                foreach (string donor in accToMainAcc
                             .Where(kv => kv.Value != new Account(current.Tag).GetEffectiveMain().MainId)
                             .Select(kv => kv.Key))
                {
                    graph.AddEdge(new(donor, vertexName, received));
                }
            }
        }

        Db.InsertAll(donations.Values);

        if (graph.VertexCount > 2)
        {
            ReversedEdgeAugmentorAlgorithm<string, TaggedEdge<string, int>> reverseAlgo = new(
                graph,
                (s, t) =>
                {
                    TaggedEdge<string, int> e = graph.Edges.First(e => e.Source == t && e.Target == s);
                    return new TaggedEdge<string, int>(s, t, e.Tag);
                });
            reverseAlgo.AddReversedEdges();

            EdmondsKarpMaximumFlowAlgorithm<string, TaggedEdge<string, int>> maxFlowAlgo = new(
                graph,
                e => e.Tag, // capacities
                (_, _) => graph.Edges.First(), // EdgeFactory (isn't actually used by the algo)
                reverseAlgo)
            { Source = "s", Sink = "t" };
            maxFlowAlgo.Compute();

            List<Tuple<string, int>> maxFlowDonations = [];
            foreach ((TaggedEdge<string, int> edge, double capa) in
                     maxFlowAlgo.ResidualCapacities.Where(ed => ed.Key.Source == "s"))
            {
                int donated = (int)(edge.Tag - capa);
                if (donated > 0)
                {
                    maxFlowDonations.Add(new(edge.Target, donated));
                }
            }

            if (maxFlowDonations.Count > 0)
            {
                EventDonatedMaxFlow(maxFlowDonations);
            }

            await EventDonated(donDelta, recDelta);
        }
    }

    internal static ClanMember? TryGetMember(string id) => s_clan.MemberList!.FirstOrDefault(m => m.Tag == id);

    internal static ClanMember? TryGetMemberById(string name) => s_clan.MemberList!.FirstOrDefault(m => m.Name == name);

    internal static ClanMember GetMember(string id) => s_clan.MemberList!.First(m => m.Tag == id);

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

    internal static async Task InitAsync()
    {
        /* Try all tokens and init clan */
        int counter = 1;
        foreach (string token in Secrets.s_coc)
        {
            s_client = new(token);
            try
            {
                await GetClanAsync();
                Console.WriteLine($"Logged into CoC with token {counter}");

                // Login successful, now try init raid
                ClanCapitalRaidSeason season = await GetRaidSeasonAsync();
                // If last raid happened within a week, we count it as valid
                EventInitRaid(season);
                s_raidSeason = season;

                // Wait for Discord ready and start polling tasks
                await Dc.s_readyTcs.Task;
                Poll clanPoll = new(10000, 60000, PollClanAsync);
                Poll raidPoll = new(0, 10000, PollRaidAsync);
                await Task.WhenAll(clanPoll.RunAsync(), raidPoll.RunAsync());
            }
            catch (ClashOfClansException ex)
            {
                if (ex.Error.Reason.StartsWith("accessDenied"))
                {
                    counter++;
                    continue;
                }
                throw;
            }
        }
        throw new InvalidDataException("All CoC tokens are invalid!");
    }
}
