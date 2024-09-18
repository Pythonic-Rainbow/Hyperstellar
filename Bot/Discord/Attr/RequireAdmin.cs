﻿using Discord;
using Discord.Interactions;
using Hyperstellar.Sql;

namespace Hyperstellar.Discord.Attr;

internal sealed class RequireAdmin : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services) => BotAdmin.s_admins.Contains(context.User.Id)
            ? Task.FromResult(PreconditionResult.FromSuccess())
            : Task.FromResult(PreconditionResult.FromError("Bro you're not an admin smh"));
}
