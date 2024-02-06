﻿using ClashOfClans.Models;
using Hyperstellar.Sql;

namespace Hyperstellar.Clash;

internal sealed class ClanUtil
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
        ClanUtil c = new(clan, new(Coc.Clan._members));
        foreach (ClanMember member in clan.MemberList!)
        {
            c._members[member.Tag] = member;
            if (Coc.Clan.HasMember(member))
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

