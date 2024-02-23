using Hyperstellar.Sql;
using Hyperstellar.Discord;
using ClashOfClans.Models;

namespace Hyperstellar.Clash;

internal static class Donate25
{
    private sealed class Node(long time)
    {
        internal long _checkTime = time;
        internal readonly ICollection<string> _ids = [];
    }

    private const int TargetPerPerson = 25; // The donation target per week per person
    private const long CheckPeriod = 7 * 24 * 3600; // Seconds
    private static readonly Queue<Node> s_queue = [];  // Queue for the await task
    internal static event Func<List<string>, Task>? EventViolated;

    static Donate25()
    {
        Coc.EventMemberJoined += MemberAdded;
        Coc.EventMemberLeft += MemberLeft;
        Coc.EventDonatedFold += DonationChanged;
        Dc.EventBotReady += BotReadyAsync;
        Member.EventAltAdded += AltAdded;
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

    private static void Init()
    {
        IEnumerable<IGrouping<long, Main>> donationGroups = Db.GetDonations()
            .GroupBy(d => d.Checked)
            .OrderBy(g => g.Key);
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
                    expiredNode._ids.Add(main.MainId);
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
                    node._ids.Add(main.MainId);
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

    private static async Task BotReadyAsync()
    {
        try
        {
            await CheckQueueAsync();
        }
        catch (Exception ex)
        {
            await Dc.ExceptionAsync(ex);
        }
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
            await Task.Delay(waitDelay);

            node = s_queue.Dequeue();
            node._checkTime += CheckPeriod;
            List<string> violators = [];
            foreach (string member in node._ids)
            {
                IEnumerable<Alt> alts = new Member(member).GetAltsByMain();
                int altCount = alts.Count();
                int donationTarget = TargetPerPerson * (altCount + 1);
                Main main = Db.GetMain(member)!;
                if (main.Donated >= donationTarget)
                {
                    Console.WriteLine($"[Donate25] {member} new cycle");
                }
                else
                {
                    violators.Add(member);
                    Console.WriteLine($"[Donate25] {member} violated");
                }
                main.Donated = 0;
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
    }

    private static Task DonationChanged(Dictionary<string, DonationTuple> foldedDelta)
    {
        foreach ((string tag, DonationTuple dt) in foldedDelta)
        {
            int donated = dt._donated;
            int received = dt._received;

            if (donated > received)
            {
                donated -= received;
                Main main = Db.GetMain(tag)!;
                main.Donated += (uint)donated;
                Console.WriteLine($"[Donate25] {tag} {donated}");
                Db.UpdateMain(main);
            }
        }
        return Task.CompletedTask;
    }

    private static void AltAdded(Main altMain, Main mainMain)
    {
        string altId = altMain.MainId, mainId = mainMain.MainId;
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

    private static void MemberAdded(ClanMember member, Main main)
    {
        main.Checked = GetNowNextTime();
        string id = member.Tag;
        Console.WriteLine($"[Donate25] Adding {id}");
        long targetTime = GetNowNextTime();
        Node node = s_queue.Last();  // We expect at least 1 member in the db
        if (targetTime == node._checkTime)
        {
            node._ids.Add(id);
            Console.WriteLine($"[Donate25] Added {id} in {node._checkTime} (last node)");
        }
        else if (targetTime > node._checkTime)
        {
            node = new(targetTime);
            node._ids.Add(id);
            s_queue.Enqueue(node);
            Console.WriteLine($"[Donate25] Added {id} in {node._checkTime} (new node). New queue len: {s_queue.Count}");
        }
        else
        {
            throw new InvalidOperationException($"New member targetTime < last node check time. targetTime: {targetTime} Last node checktime: {node._checkTime}");
        }
    }

    private static void MemberLeft(ClanMember member, string? newMainId)
    {
        string id = member.Tag;
        Console.WriteLine($"[Donate25] Removing {id} -> {newMainId}");
        Node? node = s_queue.FirstOrDefault(n => n._ids.Remove(id));
        if (node != null)
        {
            Console.WriteLine($"[Donate25] Removed {id} in {node._checkTime}");
            if (newMainId != null)
            {
                node._ids.Add(newMainId);
                Console.WriteLine($"[Donate25] Added {newMainId} because it replaced {id} as main");
            }
        }
    }

    private static long GetNowNextTime() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + CheckPeriod;
}
