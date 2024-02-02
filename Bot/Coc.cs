using ClashOfClans;
using ClashOfClans.Models;
using Hyperstellar.Sql;
using static Hyperstellar.Dc.Discord;

namespace Hyperstellar;

internal static class Coc
{
    private sealed class ClanUtil
    {
        internal readonly Clan _clan;
        internal readonly Dictionary<string, ClanMember> _members = [];
        internal readonly Dictionary<string, ClanMember> _existingMembers = [];
        internal readonly Dictionary<string, ClanMember> _joiningMembers = [];
        internal readonly Dictionary<string, ClanMember> _leavingMembers;

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
            ClanUtil c = new(clan, new(s_clan._members));
            foreach (ClanMember member in clan.MemberList!)
            {
                c._members[member.Tag] = member;
                if (s_clan.HasMember(member))
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

    private sealed class Donate25Node(long time)
    {
        internal long _checkTime = time;
        internal ICollection<string> _ids = [];
    }

    internal readonly struct DonationTuple(int donated, int received)
    {
        internal readonly int _donated = donated;
        internal readonly int _received = received;

        internal DonationTuple Add(DonationTuple dt) => new(_donated + dt._donated, _received + dt._received);
    }

    private const string ClanId = "#2QU2UCJJC"; // 2G8LP8PVV
    private static readonly ClashOfClansClient s_client = new(Secrets.s_coc);
    private static readonly HashSet<string> s_donate25Members = [];  // Members to track donation
    private static readonly Queue<Donate25Node> s_donate25Queue = [];  // Queue for the await task
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private static ClanUtil s_clan;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private static void CheckMembersJoined(ClanUtil clan)
    {
        if (clan._joiningMembers.Count == 0)
        {
            return;
        }

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

        foreach (string id in clan._joiningMembers.Keys)
        {
            Donate25_AddMember(id);
        }
    }

    private static void CheckMembersLeft(ClanUtil clan)
    {
        if (clan._leavingMembers.Count == 0)
        {
            return;
        }

        foreach (string id in clan._leavingMembers.Keys)
        {
            IEnumerable<Alt> alts = new Member(id).GetAltsByMain();
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
            }
            Donate25_RemoveMember(id, altId);
        }


        string[] members = [.. clan._leavingMembers.Keys];
        bool isSuccess = Db.DeleteMembers(members);
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
        s_clan = clanUtil;
    }

    private static async Task CheckDonationsAsync(ClanUtil clan)
    {
        Dictionary<string, DonationTuple> donationsDelta = [];
        Dictionary<string, DonationTuple> donationsTagDelta = [];
        foreach (string tag in clan._existingMembers.Keys)
        {
            ClanMember current = clan._members[tag];
            ClanMember previous = s_clan._members[tag];
            if (current.Donations > previous.Donations || current.DonationsReceived > previous.DonationsReceived)
            {
                donationsDelta[current.Name] = new(current.Donations - previous.Donations, current.DonationsReceived - previous.DonationsReceived);
                donationsTagDelta[current.Tag] = new(current.Donations - previous.Donations, current.DonationsReceived - previous.DonationsReceived);
            }

        }


        foreach (KeyValuePair<string, DonationTuple> dd in donationsTagDelta)
        {
            Console.WriteLine($"{dd.Key}: {dd.Value._donated} {dd.Value._received}");
        }

        // Fold alt data into main
        Dictionary<string, DonationTuple> folded = [];
        foreach (KeyValuePair<string, DonationTuple> donDelta in donationsTagDelta)
        {
            string tag = donDelta.Key;
            DonationTuple dt = donDelta.Value;
            Member member = new(tag);
            Alt? alt = member.TryToAlt();
            if (alt != null)
            {
                tag = alt.MainId;
            }
            folded[tag] = folded.TryGetValue(tag, out DonationTuple value) ? value.Add(dt) : dt;
        }
        donationsTagDelta = folded;

        if (donationsTagDelta.Count > 0)
        {
            Console.WriteLine("---");
        }

        foreach (KeyValuePair<string, DonationTuple> dd in donationsTagDelta)
        {
            Console.WriteLine($"{dd.Key}: {dd.Value._donated} {dd.Value._received}");
        }

        // Everyone is main now
        foreach (KeyValuePair<string, DonationTuple> donDelta in donationsTagDelta)
        {
            string tag = donDelta.Key;
            DonationTuple dt = donDelta.Value;
            if (s_donate25Members.Contains(tag))  // Member is tracked
            {
                IEnumerable<Alt> alts = new Member(tag).GetAltsByMain();
                int altCount = alts.Count();
                int donationTarget = 25 * (altCount + 1);
                int donated = dt._donated;
                int received = dt._received;

                if (donated > received)
                {
                    donated -= received;
                    Donation donation = Db.GetDonation(tag)!;
                    donation.Donated += (uint)donated;
                    Console.WriteLine($"[Donate25] {tag} {donated}");
                    if (donation.Donated >= donationTarget)
                    {
                        Console.WriteLine($"[Donate25] {tag} donated >{donationTarget}, removing from set");
                        s_donate25Members.Remove(tag);
                    }
                    Db.UpdateDonation(donation);
                }
            }
        }

        if (donationsDelta.Count > 0)
        {
            await DonationsChangedAsync(donationsDelta);
        }
    }

    private static void Donate25_AddMember(string id)
    {
        Console.WriteLine($"[Donate25] Adding {id}");
        s_donate25Members.Add(id);
        long targetTime = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
        Donate25Node node = s_donate25Queue.Last();  // We expect at least 1 member in the db
        if (targetTime == node._checkTime)
        {
            node._ids.Add(id);
            Console.WriteLine($"[Donate25] Added {id} in {node._checkTime} (last node)");
        }
        else if (targetTime > node._checkTime)
        {
            node = new(targetTime);
            node._ids.Add(id);
            s_donate25Queue.Enqueue(node);
            Console.WriteLine($"[Donate25] Added {id} in {node._checkTime} (new node). New queue len: {s_donate25Queue.Count}");
        }
        else
        {
            Task.Run(() => WarnAsync($"This shouldn't happen: Donate25 new member targetTime < last node check time. " +
                $"targetTime: {targetTime} Last node checktime: {node._checkTime}"));
        }
    }

    internal static void Donate25_RemoveMember(string id, string? newMainId)
    {
        Console.WriteLine($"[Donate25] Removing {id} -> {newMainId}");
        s_donate25Members.Remove(id);
        foreach (Donate25Node node in s_donate25Queue)
        {
            if (node._ids.Remove(id))
            {
                Console.WriteLine($"[Donate25] Removed {id} in {node._checkTime}");
                if (newMainId != null)
                {
                    s_donate25Members.Add(newMainId);
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

    internal static void Donate25_AddAlt(string altId, string mainId)
    {
        Console.WriteLine($"[Donate25] Removing {altId} -> {mainId} (addalt)");
        s_donate25Members.Remove(altId);
        foreach (Donate25Node node in s_donate25Queue)
        {
            if (node._ids.Remove(altId))
            {
                Console.WriteLine($"[Donate25] Removed {altId} in {node._checkTime}");
                s_donate25Members.Add(mainId);
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

    private static async Task Donate25Async()
    {
        while (s_donate25Queue.Count > 0)
        {
            Donate25Node node = s_donate25Queue.First();
            if (node._ids.Count > 0)
            {
                int waitDelay = (int)((node._checkTime * 1000) - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await Task.Delay(waitDelay);
                node = s_donate25Queue.Dequeue();
                List<string> violators = [];
                foreach (string member in node._ids)
                {
                    if (s_donate25Members.Add(member))
                    {
                        Console.WriteLine($"[Donate25] {member} new cycle");
                    }
                    else
                    {
                        violators.Add(member);
                        Console.WriteLine($"[Donate25] {member} violated");
                    }
                    Donation donation = new(member, node._checkTime + (long)TimeSpan.FromDays(7).TotalSeconds);
                    Db.UpdateDonation(donation);
                }

                if (node._ids.Count > 0)
                {
                    node._checkTime += (long)TimeSpan.FromDays(7).TotalSeconds;
                    s_donate25Queue.Enqueue(node);
                }

                if (violators.Count > 0)
                {
                    _ = Task.Run(() => Donate25TriggerAsync(violators));
                }
            }
            else
            {
                s_donate25Queue.Dequeue();
            }
        }
    }

    private static void InitDonate25Async()
    {
        IEnumerable<IGrouping<long, Donation>> donationGroups = Db.GetDonations()
            .Where(d => s_clan._members.ContainsKey(d.MainId)) // Maybe can remove
            .GroupBy(d => d.Checked)
            .OrderBy(g => g.Key);

        // Init donate25 vars
        foreach (IGrouping<long, Donation> group in donationGroups)
        {
            DateTimeOffset checkedTime = DateTimeOffset.FromUnixTimeSeconds(group.Key);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan timePassed = now - checkedTime;

            DateTimeOffset startingInstant = timePassed.Days >= 7 ? now : checkedTime;
            long targetTime = startingInstant.AddDays(7).ToUnixTimeSeconds();

            Donate25Node node = new(targetTime);
            foreach (Donation donation in group)
            {
                node._ids.Add(donation.MainId);
                s_donate25Members.Add(donation.MainId); // Add to set so OnDonate will start tracking them
            }
            s_donate25Queue.Enqueue(node);
        }
    }

    internal static string? GetMemberId(string name)
    {
        ClanMember? result = s_clan._clan.MemberList!.FirstOrDefault(m => m.Name == name);
        return result?.Tag;
    }

    internal static ClanMember GetMember(string id) => s_clan._members[id];

    internal static async Task InitAsync() => s_clan = ClanUtil.FromInit(await GetClanAsync());

    internal static async Task BotReadyAsync()
    {
        InitDonate25Async();
        _ = Task.Run(Donate25Async);
        while (true)
        {
            await PollAsync();
            await Task.Delay(20000);
        }
    }
}
