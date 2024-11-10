using Hyperstellar.Sql;
using Hyperstellar.Discord;
using ClashOfClans.Models;

namespace Hyperstellar.Clash;

internal static class Phaser
{
    private sealed class Node(long time)
    {
        internal long _checkTime = time;
        internal readonly ICollection<string> _ids = [];
    }

    private const int TargetPerPerson = 25; // The donation target per week per person
    private const long CheckPeriod = 7 * 24 * 3600; // Seconds
    private static readonly Queue<Node> s_queue = [];  // Queue for the await task
    internal static event Func<List<Violator>, Task>? EventViolated;

    static Phaser()
    {
        Coc.EventInitRaid += InitRaid;
        Coc.EventRaidCompleted += ProcessRaid;
        Dc.EventBotReady += BotReadyAsync;
        Account.EventAltAdded += AltAdded;

        Init();
    }

    private static void DebugQueue()
    {
        List<string> msgs = [];
        foreach (Node node in s_queue)
        {
            msgs.Add($"[{node._checkTime} {DateTimeOffset.FromUnixTimeSeconds(node._checkTime)}] {string.Join(", ", node._ids)}");
        }
        Console.WriteLine(string.Join("\n", msgs));
    }

    private static void ProcessRaid(ClanCapitalRaidSeason season)
    {
        foreach (ClanCapitalRaidSeasonAttacker atk in Coc.GetRaidAttackers(season))
        {
            // If in the db, mark as raided
            Account? member = Account.TryFetch(atk.Tag);
            if (member != null)
            {
                Main main = member.GetEffectiveMain();
                main.Raided++;
                main.Update();
            }
        }
    }

    private static void Init()
    {
        // For each MainID in table MainRequirement, get the row with the latest EndTime
        IEnumerable<IGrouping<long, MainRequirement>> eachLatestReq = MainRequirement.FetchEachLatest()
            .GroupBy(req => req.EndTime);
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // Store new req objects to be inserted later
        List<MainRequirement> insertReqs = [];

        foreach (IGrouping<long, MainRequirement> reqGroup in eachLatestReq)
        {
            // If overdue, update the req
            if (reqGroup.Key < currentTime)
            {
                foreach (MainRequirement req in reqGroup)
                {
                    insertReqs.Add(SetPass(req, Pass.Overdue));
                }

                continue;
            }

            // Very rare - now has req to check, update their pass status
            if (reqGroup.Key == currentTime)
            {
                foreach (MainRequirement req in reqGroup)
                {
                    insertReqs.Add(CheckReq(req));
                }
            }
        }

        Task.Delay(-1).Wait();

        IEnumerable<IGrouping<long, Main>> donationGroups = Main.FetchAll()
            .GroupBy(static d => d.Checked)
            .OrderBy(static g => g.Key);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Node expiredNode = new(GetNowNextTime()); // Node for expired donations

        foreach (IGrouping<long, Main> group in donationGroups)
        {
            DateTimeOffset lastChecked = DateTimeOffset.FromUnixTimeSeconds(group.Key);

            // If bot was down when a check is due, we will be lenient and wait for another cycle
            if (now >= lastChecked)
            {
                foreach (Main main in group)
                {
                    expiredNode._ids.Add(main.AccountId);
                    main.Checked = expiredNode._checkTime;
                    main.Donated = 0;
                    main.Update();
                }
            }
            else
            {
                Node node = new(group.Key);
                foreach (Main main in group)
                {
                    node._ids.Add(main.AccountId);
                }
                s_queue.Enqueue(node);
            }
        }
        if (expiredNode._ids.Count > 0)
        {
            s_queue.Enqueue(expiredNode);
        }
        DebugQueue();
        Console.WriteLine("[Donate25] Inited");
    }

    /// Sets a req object's pass status, execute Update and returns the next due req.
    private static MainRequirement SetPass(MainRequirement req, Pass pass)
    {
        req.Passed = pass;
        req.Update();
        return new(req.MainId, req.EndTime + CheckPeriod);
    }

    /// When a req is due, use this function to determine whether it passed and failed.
    /// It will also call SetPass()
    private static MainRequirement CheckReq(MainRequirement req)
    {
        bool donate25 = req.Donated >= 25;
        bool raid1 = req.Raided > 0;

        bool passed = donate25 || raid1;
        return SetPass(req, passed ? Pass.Passed : Pass.Failed);
    }

    private static void AltAdded(Main altMain, Main mainMain)
    {
        string altId = altMain.AccountId, mainId = mainMain.AccountId;
        Console.WriteLine($"[Donate25] Removing {altId} -> {mainId} (addalt)");
        Node? node = s_queue.FirstOrDefault(n => n._ids.Remove(altId));
        if (node != null)
        {
            Console.WriteLine($"[Donate25] Removed {altId} in {node._checkTime}");
            node._ids.Add(mainId);
            mainMain.Donated += altMain.Donated;
            Console.WriteLine($"[Donate25] Added {mainId} because it replaced {altId} as main");
        }
    }

    private static void InitRaid(ClanCapitalRaidSeason season)
    {
        if (!IsDatetimeExpired(season.StartTime))
        {
            ProcessRaid(season);
        }
    }

    private static long GetNowNextTime() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + CheckPeriod;

    private static async Task BotReadyAsync()
    {
        Poll handler = new(0, 1000, CheckQueueAsync);
        await handler.RunAsync();
    }

    private static async Task CheckQueueAsync()
    {
        while (s_queue.Count > 0)
        {
            Node node = s_queue.First();
            if (node._ids.Count == 0)
            {
                s_queue.Dequeue();
                continue;
            }

            int waitDelay = (int)((node._checkTime * 1000) - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (waitDelay > 0)
            {
                await Task.Delay(waitDelay);
            }

            node = s_queue.Dequeue();
            node._checkTime += CheckPeriod;
            List<Violator> violators = [];
            foreach (string member in node._ids)
            {
                IEnumerable<Alt> alts = new Account(member).GetAltsByMain();
                int accountCount = alts.Count() + 1;
                Main main = Main.TryFetch(member)!;
                bool violated = false;
                Violator violator = new(member);

                if (main.Donated < TargetPerPerson * accountCount) // Donation target
                {
                    violator._donated = main.Donated;
                    violated = true;
                }
                if (main.Raided < accountCount)
                {
                    violator._raided = main.Raided;
                    violated = true;
                }

                if (violated)
                {
                    violators.Add(violator);
                }

                main.Donated = 0;
                main.Raided = 0;
                main.Checked = node._checkTime;
                main.Update();
            }

            if (node._ids.Count > 0)
            {
                s_queue.Enqueue(node);
            }
            DebugQueue();

            if (violators.Count > 0)
            {
                await EventViolated!(violators);
            }
        }

        throw new InvalidOperationException("Phaser queue is empty WTF");
    }

    internal static bool IsDatetimeExpired(DateTimeOffset time) => (DateTimeOffset.UtcNow - time).TotalSeconds >= CheckPeriod;
}
