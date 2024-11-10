using ClashOfClans;
using ClashOfClans.Core;
using ClashOfClans.Models;
using ClashOfClans.Search;
using Hyperstellar.Discord;
using Hyperstellar.Sql;
using QuikGraph;
using QuikGraph.Algorithms.MaximumFlow;

namespace Hyperstellar.Clash;

internal class DonRecv(int donated, int received)
{
    internal int _donated = donated;
    internal int _received = received;

    public static DonRecv operator +(DonRecv dr1, DonRecv dr2) =>
        new(dr1._donated + dr2._donated, dr1._received + dr2._received);

    internal void Add(DonRecv dr)
    {
        _donated += dr._donated;
        _received += dr._received;
    }
}

internal static class Coc
{
    private sealed class RaidAttackerComparer : IEqualityComparer<ClanCapitalRaidSeasonAttacker>
    {
        public bool Equals(ClanCapitalRaidSeasonAttacker? x, ClanCapitalRaidSeasonAttacker? y) => x!.Tag == y!.Tag;
        public int GetHashCode(ClanCapitalRaidSeasonAttacker obj) => obj.Tag.GetHashCode();
    }

    private sealed class ClanMemberComparer : IEqualityComparer<ClanMember>
    {
        public bool Equals(ClanMember? x, ClanMember? y) => x!.Tag == y!.Tag;
        public int GetHashCode(ClanMember obj) => obj.Tag.GetHashCode();
    }

    private const string ClanId = "#2QU2UCJJC"; // 2G8LP8PVV
    private static ClashOfClansClient s_client;
    private static ClanCapitalRaidSeason s_raidSeason;
    internal static Clan s_clan;
    internal static event Action<Account[]> EventMemberJoined;
    internal static event Action<Account[]> EventMemberRejoined;
    internal static event Action<Account[]> EventMemberLeft;
    internal static event Action<ClanCapitalRaidSeason> EventInitRaid;
    internal static event Action<ClanCapitalRaidSeason> EventRaidCompleted;
    internal static event Func<IDictionary<string, DonRecv>, Task> EventDonated;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    static Coc()
    {
        EventMemberLeft += MembersLeft;
        EventMemberRejoined += MembersRejoined;
        EventMemberJoined += MembersJoined;
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private static void MembersLeft(Account[] leftMembers) => Console.WriteLine($"{string.Join(",", leftMembers.Select(a => a.Id))} left");

    private static void MembersRejoined(Account[] rejoinedMembers) => Console.WriteLine($"{string.Join(",", rejoinedMembers.Select(a => a.Id))} rejoined");

    private static void MembersJoined(Account[] newJoinedMembers) => Console.WriteLine($"{string.Join(",", newJoinedMembers.Select(a => a.Id))} joined");

    private static Dictionary<string, DonRecv> DonationMaxFlow(Dictionary<string, DonRecv> original)
    {
        Dictionary<string, DonRecv> result = [];

        // Fold alts into main
        foreach ((string tag, DonRecv dr) in original)
        {
            Main effectiveMain = new Account(tag).GetEffectiveMain();
            if (!result.TryAdd(effectiveMain.AccountId, dr))
            {
                result[effectiveMain.AccountId].Add(dr);
            }
        }

        // Init graph
        AdjacencyGraph<string, TaggedEdge<string, int>> graph = new(false);
        graph.AddVertexRange(["s", "t"]);

        // Add donation nodes
        List<string> donTags = []; // List of tags that have donated
        foreach ((string tag, DonRecv dr) in result)
        {
            if (dr._donated > 0)
            {
                donTags.Add(tag);
                graph.AddVertex(tag);
                graph.AddEdge(new("s", tag, dr._donated));
            }
        }

        // Add receive nodes
        foreach ((string tag, DonRecv dr) in result)
        {
            if (dr._received > 0)
            {
                string nodeName = 'r' + tag;  // Receive node names will be 'r#XXX'
                graph.AddVertex(nodeName);

                // Connect all donation nodes except same tag to this receive node
                foreach (string donTag in donTags.Where(t => t != tag))
                {
                    graph.AddEdge(new(donTag, nodeName, dr._received));
                }

                // Connect this receive node to the sink
                graph.AddEdge(new(nodeName, "t", dr._received));
            }
        }

        // Compute MaxFlow
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

        /* Update the result dict.
        * DonNodes that may appear here must already been existed in result dict and _donated > 0 cuz
        * otherwise it won't be in the graph in the first place.
        * So we don't need to clear result dict first.
        */

        // Update donations
        foreach ((TaggedEdge<string, int> edge, double capa) in
                 maxFlowAlgo.ResidualCapacities.Where(ed => ed.Key.Source == "s"))
        {
            int donated = (int)(edge.Tag - capa);
            result[edge.Target]._donated = donated >= 0 ? donated
                : throw new InvalidDataException($"After MaxFlow, donation < 0! ({edge.Target} {donated})");
        }

        // Update receive
        foreach ((TaggedEdge<string, int> edge, double capa) in
                 maxFlowAlgo.ResidualCapacities.Where(ed => ed.Key.Target == "t"))
        {
            int received = (int)(edge.Tag - capa);
            string receiverTag = edge.Source[1..]; // Remove first char because edge.Source is "r#XXX"
            result[receiverTag]._received = received >= 0 ? received
                : throw new InvalidDataException($"After MaxFlow, donation < 0! ({edge.Target} {received})");
        }

        // Remove DonRecvs without data
        foreach ((string key, DonRecv _) in result.Where(kvp => kvp.Value is { _donated: 0, _received: 0 }))
        {
            result.Remove(key);
        }

        return result;
    }

    private static async Task<Clan> GetClanAsync() => await s_client.Clans.GetClanAsync(ClanId);

    // Poll clan members
    private static async Task PollClanAsync()
    {
        // Fetch data for comparison
        Clan oldClan = s_clan;
        s_clan = await GetClanAsync();
        if (s_clan.MemberList == null)
        {
            throw new InvalidDataException("Fetched clan member list is null bruh");
        }
        IEnumerable<Account> accounts = Account.FetchAll();

        // Some extra info
        long nowTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string[] memberIds = [.. s_clan.MemberList.Select(m => m.Tag)];

        /* TODO:
         We currently compare newClan with db for each poll.
         Technically we only need to compare with db during Coc.InitAsync because
         for each poll later, db should be in sync with previous s_clan.

         Create a ClanDelta class that will be instantiated in CoC.InitAsync with a single Clan arg.
         In the ctor, we compare db with this first clan data.
         For each poll, clanDelta.Update(newClan)

         Try to reduce amount of SQL queries in order to save access time
         */

        /* TODO:
        If db access time for this ENTIRE function is too long,
        we can pass-by-ref newJoinAccounts, prevJoinAccounts and leftAccounts to those consumer functions
        and ONLY CALl db.update / db.insert in THIS function.
        Eliminates multiple db operations in consumer functions.
        Probably means that these consumer functions can't be async tho
         */

        /* Extract members and accounts changed */
        Account[] leftMembers = [.. Account.FetchMembers().ExceptBy(memberIds, a => a.Id)];

        foreach (Account account in leftMembers)
        {
            account.LeftTime = nowTime;
        }

        IEnumerable<ClanMember> newMembers = s_clan.MemberList.ExceptBy(accounts.Select(a => a.Id), m => m.Tag);
        Account[] newJoinAccounts = [.. newMembers.Select(m => new Account(m.Tag))];

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
        if (prevJoinAccounts.Length > 0)
        {
            EventMemberRejoined(prevJoinAccounts);
        }
        if (newJoinAccounts.Length > 0)
        {
            EventMemberJoined(newJoinAccounts);
        }

        // Clan instance comparison tasks
        await CheckDonationsAsync(oldClan);
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

    private static async Task CheckDonationsAsync(Clan oldClan)
    {
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /* TODO:
         After the ClanDelta code from above is done, for all new members,
         process their MainRequirement and Donation in ClanDelta ctor.
         Instead of the db triggers currently used, programatically insert Account, Main and MainReq instances
         because we can set MainReq.Donated etc. before inserting.
         */

        /* TODO: IMPORTANT
         Insert all member donations, then MaxFlow all donations and update MainReq
         */

        static DonRecv ExtractDonations(ClanMember current, ClanMember previous)
        {
            int donated = current.Donations >= previous.Donations
                ? current.Donations - previous.Donations : current.Donations;
            int received = current.DonationsReceived >= previous.DonationsReceived
                ? current.DonationsReceived - previous.DonationsReceived : current.DonationsReceived;
            return new DonRecv(donated, received);
        }

        // Get new and existing members
        ClanMember[] newMembers = [.. s_clan.MemberList!.Except(oldClan.MemberList!, new ClanMemberComparer())];
        var existingMembers = s_clan.MemberList!.Join(
            oldClan.MemberList!,
            current => current.Tag,
            previous => previous.Tag,
            (current, previous) => new { Current = current, Previous = previous }
        ).ToArray();

        // Extract DonRecv from members
        Dictionary<string, DonRecv> donRecvs = [];
        foreach (ClanMember member in newMembers)
        {
            int donated = member.Donations;
            int received = member.DonationsReceived;
            if (donated > 0 || received > 0)
            {
                donRecvs[member.Tag] = new DonRecv(member.Donations, member.DonationsReceived);
            }
        }
        foreach (var currentPrevious in existingMembers)
        {
            ClanMember current = currentPrevious.Current;
            ClanMember previous = currentPrevious.Previous;
            string tag = current.Tag;
            DonRecv dr = ExtractDonations(current, previous);
            if (dr._donated > 0 || dr._received > 0)  // Only add if there is a change
            {
                donRecvs[tag] = dr;
            }
        }

        // Ret if DonRecv empty
        if (donRecvs.Count == 0)
        {
            return;
        }

        // Generate the Donation records and insert
        IEnumerable<Donation> donations = donRecvs.Select(kvp =>
        {
            (string tag, DonRecv dr) = kvp;
            return new Donation(currentTime, tag, dr._donated, dr._received);
        });
        Db.InsertAll(donations);

        // MaxFlow DonRecv
        Dictionary<string, DonRecv> maxFlowDonRecvs = DonationMaxFlow(donRecvs);

        // Ret if MaxFlowDonRecv empty
        if (maxFlowDonRecvs.Count == 0)
        {
            return;
        }

        // Fetch MainReq of all the mains that have donated in MaxFlow graph
        MainRequirement[] maxFlowMainReq = [.. MainRequirement.FetchLatest(maxFlowDonRecvs
            .Where(kvp => kvp.Value._donated > 0)
            .Select(kvp => kvp.Key)
        )];

        // Update the MainReqs
        foreach (MainRequirement mainReq in maxFlowMainReq)
        {
            DonRecv dr = maxFlowDonRecvs[mainReq.MainId];
            mainReq.Donated += dr._donated;
            mainReq.Update();
        }

        await EventDonated(donRecvs);
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
                s_clan = await GetClanAsync();
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
