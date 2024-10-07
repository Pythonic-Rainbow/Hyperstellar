using ClashOfClans.Models;
using Hyperstellar.Sql;

namespace Hyperstellar.Clash;

internal sealed class ClanUtil
{
    internal readonly Clan _clan;
    internal readonly Dictionary<string, ClanMember> _members = [];
    internal readonly Dictionary<string, ClanMember> _existingMembers = [];
    internal readonly Dictionary<string, ClanMember> _joiningMembers = [];
    internal readonly Dictionary<string, ClanMember> _leavingMembers;
}

