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
    internal static ClanUtil Clan { get; private set; } = new();
    internal static readonly Task s_initTask = InitAsync();
    internal static event Action<ClanMember, Main> EventMemberJoined;
    internal static event Action<ClanMember, string?> EventMemberLeft;
    internal static event Action<ClanCapitalRaidSeason> EventInitRaid;
    internal static event Action<ClanCapitalRaidSeason> EventRaidCompleted;
    internal static event Action<IEnumerable<Tuple<string, int>>> EventDonatedMaxFlow;
    internal static event Func<IEnumerable<Tuple<string, int>>, IEnumerable<Tuple<string, int>>, Task> EventDonated;

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

    private static void CheckMembersLeft(ClanUtil clan)
    {
        if (clan._leavingMembers.Count == 0)
        {
            return;
        }

        foreach ((string id, ClanMember member) in clan._leavingMembers)
        {
            Account fakeMem = new(id);
            IEnumerable<Alt> alts = fakeMem.GetAltsByMain();
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
                // Maybe adapt this in the future if need to modify attributes when replacing main
                Main main = fakeMem.ToMain();
                main.Delete();
                main.MainId = altId;
                main.Insert();
            }

            // This is before Db.DelMem below so that we can remap Donation to new mainId
            // ^ No longer true because the remap is done ABOVE now but I'll still leave this comment
            EventMemberLeft(member, altId);
        }

        string[] members = [.. clan._leavingMembers.Keys];
        foreach (string member in members)
        {
            new Account(member).Delete();
        }

        string membersMsg = string.Join(", ", members);
        Console.WriteLine($"{membersMsg} left");
    }

    private static async Task BotReadyAsync()
    {
        await s_initTask;
        Poll clanPoll = new(10000, 60000, PollAsync);
        Poll raidPoll = new(0, 10000, PollRaidAsync);
        await Task.WhenAll(clanPoll.RunAsync(), raidPoll.RunAsync());
    }

    private static async Task<Clan> GetClanAsync() => await s_client.Clans.GetClanAsync(ClanId);

    private static async Task InitAsync()
    {
        /* Try all tokens and init clan */
        int counter = 1;
        foreach (string token in Secrets.s_coc)
        {
            s_client = new(token);
            try
            {
                Clan = ClanUtil.FromInit(await GetClanAsync());
                Console.WriteLine($"Logged into CoC with token {counter}");

                // Login successful, now try init raid
                ClanCapitalRaidSeason season = await GetRaidSeasonAsync();
                // If last raid happened within a week, we count it as valid
                EventInitRaid(season);
                s_raidSeason = season;

                return;
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
            ClanMember previous = Clan._members[tag];

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
            ClanMember previous = Clan._members[tag];

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

    internal static string? GetMemberId(string name)
    {
        ClanMember? result = Clan._clan.MemberList!.FirstOrDefault(m => m.Name == name);
        return result?.Tag;
    }

    internal static ClanMember? TryGetMember(string id)
    {
        Clan._members.TryGetValue(id, out ClanMember? result);
        return result;
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
}
