using Hyperstellar.Sql;
using Hyperstellar.Discord;

namespace Hyperstellar.Clash;

internal sealed class Donate25
{
    private sealed class Node(long time)
    {
        internal long _checkTime = time;
        internal ICollection<string> _ids = [];
    }

    private const int TargetPerPerson = 25; // The donation target per week per person
    private const long CheckPeriod = 7 * 24 * 3600; // Seconds
    private static readonly Queue<Node> s_queue = [];  // Queue for the await task

    internal static void Init()
    {
        IEnumerable<IGrouping<long, Donation>> donationGroups = Db.GetDonations()
            .GroupBy(d => d.Checked)
            .OrderBy(g => g.Key);

        // Init donate25 vars
        foreach (IGrouping<long, Donation> group in donationGroups)
        {
            DateTimeOffset lastChecked = DateTimeOffset.FromUnixTimeSeconds(group.Key);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan timePassed = now - lastChecked;

            // If bot was down when a check is due, we will be lenient and wait for another cycle
            DateTimeOffset startingInstant = timePassed.TotalSeconds >= CheckPeriod ? now : lastChecked;
            long targetTime = startingInstant.ToUnixTimeSeconds() + CheckPeriod;

            Node node = new(targetTime);
            foreach (Donation donation in group)
            {
                node._ids.Add(donation.MainId);
            }
            s_queue.Enqueue(node);
        }

        Console.WriteLine("[Donate25] Inited");
    }

    internal static void AltAdded(string altId, string mainId)
    {
        Console.WriteLine($"[Donate25] Removing {altId} -> {mainId} (addalt)");
        foreach (Node node in s_queue)
        {
            if (node._ids.Remove(altId))
            {
                Console.WriteLine($"[Donate25] Removed {altId} in {node._checkTime}");
                node._ids.Add(mainId);
                Donation altDon = Db.GetDonation(altId)!;
                Donation mainDon = Db.GetDonation(mainId)!;
                altDon.Delete();
                mainDon.Donated += altDon.Donated;
                mainDon.Update();
                Console.WriteLine($"[Donate25] Added {mainId} because it replaced {altId} as main");
                break;
            }
        }
    }

    internal static void MemberRemoved(string id, string? newMainId)
    {
        Console.WriteLine($"[Donate25] Removing {id} -> {newMainId}");
        foreach (Node node in s_queue)
        {
            if (node._ids.Remove(id))
            {
                Console.WriteLine($"[Donate25] Removed {id} in {node._checkTime}");
                if (newMainId != null)
                {
                    node._ids.Add(newMainId);
                    Donation donation = Db.GetDonation(id)!;
                    donation.Delete();
                    donation.MainId = newMainId;
                    donation.Insert();
                    Console.WriteLine($"[Donate25] Added {newMainId} because it replaced {id} as main");
                }
                break;
            }
        }
    }

    internal static void MemberAdded(string id)
    {
        Console.WriteLine($"[Donate25] Adding {id}");
        long targetTime = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
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

    internal static async Task CheckAsync()
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
                Donation donation = Db.GetDonation(member)!;
                if (donation.Donated >= donationTarget)
                {
                    Console.WriteLine($"[Donate25] {member} new cycle");
                }
                else
                {
                    violators.Add(member);
                    Console.WriteLine($"[Donate25] {member} violated");
                }
                donation.Donated = 0;
                donation.Checked = node._checkTime;
                donation.Update();
            }

            if (node._ids.Count > 0)
            {
                s_queue.Enqueue(node);
            }

            if (violators.Count > 0)
            {
                await Dc.Donate25Async(violators);
            }
        }
    }
}
