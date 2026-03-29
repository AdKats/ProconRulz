# ProconRulz -- Procon v2 Plugin

## Project Overview

ProconRulz is a C# rules engine plugin for Procon v2 (Battlefield game server administration). It allows server admins to define rules triggered by in-game events (kills, spawns, player joins, chat, rounds) with conditions and actions (kick, kill, ban, say, yell, etc.). The legacy Procon v1 version lives on the `legacy` branch.

- **Language:** C#
- **License:** GPLv3
- **Version:** 44j.1
- **Author:** bambam (Ian Forster-Lewis), with ProconRulzNL enhancements by LCARSx64 merged
- **Supported games:** BFBC2, MoH, BF3, BF4
- **Dependencies:** None (uses only Procon core and .NET Framework types)

## Architecture

The plugin uses partial classes split across multiple files in `src/`:

- **`src/ProconRulz.cs`** -- main entry point: metadata, lifecycle, enums, inner classes (PartClass, ParsedRule, PlayerList, SpawnCounts, VarsClass), global state, constructor, plugin startup/enable/disable, admin/protected utilities, misc utility functions, console output, debug commands, say event processing, plugin details HTML
- **`src/ProconRulz/Settings.cs`** -- plugin variable management: GetDisplayPluginVariables, SetPluginVariable, load_rulz_from_files
- **`src/ProconRulz/Events.cs`** -- Procon event handlers: player join/leave/team change, list players, round events, chat events, server info, spawn, kill
- **`src/ProconRulz/RuleParser.cs`** -- rule text parsing: parse_rules, parse_rule, parse_part, all parse_* methods for each condition/action type
- **`src/ProconRulz/RuleExecutor.cs`** -- rule evaluation at runtime: scan_rules, process_rule, process_parts, process_part (condition evaluation), test_spawn_item, test_kill_item, counts/rates/blocks tracking
- **`src/ProconRulz/Actions.cs`** -- action execution: take_action, do_action (say/yell/kill/kick/ban/exec), kill thread, newline escape support in yell messages (merged from ProconRulzNL)

All files share: `namespace PRoConEvents { public partial class ProconRulz { } }`
Main file additionally extends: `PRoConPluginAPI, IPRoConPluginInterface`

## Key Inner Classes

- **PartClass** -- represents a single condition or action within a rule
- **ParsedRule** -- a complete parsed rule with trigger, parts list, and metadata
- **PlayerList / PlayerData** -- maintains server player state
- **SpawnCounts** -- tracks items players have spawned with per team
- **VarsClass** -- manages runtime rule variables (Set, Incr, Decr, If) with INI file persistence

## Event Registrations

Registered in `OnPluginLoaded`: OnPlayerSpawned, OnPlayerKilled, OnPlayerTeamChange, OnPlayerSquadChange, OnPlayerJoin, OnPlayerLeft, OnListPlayers, OnReservedSlotsList, OnRoundOver, OnRoundOverPlayers, OnRoundOverTeamScores, OnLoadingLevel, OnLevelLoaded, OnCurrentLevel, OnGlobalChat, OnTeamChat, OnSquadChat, OnServerInfo, OnPunkbusterPlayerInfo

## Code Style

Style is enforced by `.editorconfig` and checked via `dotnet format` in CI.

**Critical conventions:**
- **Use `String`, `Int32`, `Boolean`, `Double`** -- NOT `string`, `int`, `bool`, `double`. The codebase uses explicit System type names everywhere.
- **Allman brace style** -- opening brace on its own line
- **4 spaces** for indentation, LF line endings
- **Block-scoped namespaces** (not file-scoped)
- **`using` directives outside namespace**, System usings first

## Build & CI

- `ProconRulz.csproj` at root is a **CI-only artifact** for `dotnet format`. It is NOT a real build file -- Procon v2 assemblies are unavailable for compilation.
- **CI workflow** (`.github/workflows/ci.yml`): runs on push to `master` and PRs. Checks `dotnet format whitespace` and `dotnet format style --exclude-diagnostics IDE1007`.
- **Release workflow** (`.github/workflows/release.yml`): triggered by `v*` tags. Packages `.cs` files from `src/` into a zip and creates a GitHub Release.

### Running style checks locally

```bash
dotnet restore
dotnet format whitespace --verify-no-changes
dotnet format style --verify-no-changes --severity warn --exclude-diagnostics IDE1007
```

## Branch Structure

- `master` -- current development, Procon v2 only
- `legacy` -- archived Procon v1 version, no longer maintained
