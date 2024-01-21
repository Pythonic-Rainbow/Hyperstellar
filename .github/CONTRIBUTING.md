# Contents
1. [Prerequisites](#prerequisites)
2. [Structure Overview](#overview)
3. [Coding Guidelines](#guidelines)

<a name="prerequisites"></a>
# Prerequisites

1. You need to install Visual Studio with .NET Desktop development workload.

    Only the .NET SDK is essential tbh. This project uses .NET 8  

    You can also use VSCode + C# extension and just download the .NET 8 SDK separately, tho I haven't tried.

2. You need to create `secrets.json` in the folder.
```json
{
    "discord": "YOUR DISCORD BOT TOKEN",
    "coc": "YOUR COC KEY, IT IS IP SENSITIVE",
    ...
    // Please refer to the variable 'definition' in Secrets.cs for all the required data
}
```

3. You need the SQLite file `Hyperstellar.db`. Just ask me for that

<a name="overview"></a>
# Structure Overview

* `Program.cs`: Main class
* `Secrets.cs`: Loads tokens
* `Coc.cs`: Module for interacting with CoC API
* `Discord.cs`: Module for interacting with Discord API
* `Sql/`: Interacts with the database

The sole purpose of Program.cs is to fire `InitAsync()` in Coc.cs, Sql/Db.cs and Discord.cs, then wait forever.

Discord.cs requires some time to be 'ready'. When it's ready, it calls `Coc.BotReadyAsync`, which is an endless loop that processes Coc.cs tasks every 5 seconds.

## Coc.cs
When Discord bot is ready, Coc.cs fetches the first copy of clan data. `PollAsync` is called every 5s.

For each PollAsync call, it fetches the latest Clan data and compares it with the previous one. The result is stored in a `ClanUtil` and is used to dispatch other tasks within Coc.cs. When all the tasks are done, it will replace the previous clan data with the current one.

For each task run, if it needs to interact with Discord, it will call the appropriate methods in `Discord.cs`.

<a name="guidelines"></a>
# Coding Guidelines

1. No variables should be shared between `Discord.cs`, `Coc.cs` and `Db.cs`. If you need functionalities in another module, expose a public method and call it instead.
2. Access modifiers: Make things as restrictive as possible. If a var/func shouldn't be shared, make it private.
3. SQL: All SQL statements should be executed only in `Sql/`. Same logic as above
4. How to order things in a class:
```cs
/* Nested class/struct etc. */
private class NestedClass
{
    // Everything here should be formated according to this rule again
}

/* Variables
 The following shows how to order variables.
 For naming, see https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names */

 const float EarthGravity = 9.81; // const first
 // Static vars first, order by access modifiers from private to public
 private static int s_PRI = 0;
 public static int PublicStatic = 1;
 // Single blank line

// Instant vars, also order by access modifiers
int _pri = 2;
public int PublicInt = 3;
//Single blank line

// CS8618 vars: Ignore nullable warning
#pragma warning disable CS8618
// Order here is same as above
#pragma warning restore CS8618

/* Constructors: Again, static first */
static Order() { }
// If multiple instant constructors: Fewest param count first
public Order(int param1) { }
public Order(int param1, int param2) { }

/* Factory methods */
public static Order CreateOrder(int param1) { }

/* Methods: Static first, then by access modifiers, then by Sync/Async */
private static async Task<string> GetClanNameAsync() { }
private List<uint> FindFactors(uint number) { }
private async Task ReportToCIA() { }
public uint ComputeFactorial(uint number) { }
public async Task ProcessPayment() { }
```