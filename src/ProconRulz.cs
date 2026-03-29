
//******************************************************************************************************
// ProconRulz Procon plugin, by bambam
//******************************************************************************************************
/*  Copyright 2013 Ian Forster-Lewis

    This file is part of my ProconRulz plugin for ProCon.

    ProconRulz plugin for ProCon is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    ProconRulz plugin for ProCon is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with ProconRulz plugin for ProCon.  If not, see <http://www.gnu.org/licenses/>.
 */
#region release notes
// v44 - a. support for BF4
//       b. support for rulz files (Plugins\BF4\proconrulz_*.txt)
//       c. 'Not' modifier now also allowed with If and Text conditions
//       d. added VictimTeamKey
//       e. Enable/Disable rulz.txt files in settings
//       f. Linux support for external rulz files (credit FritzE)
//       g. On Init trigger added, for var initialisations
//       h. On RoundOver trigger added. Bugfix for NULL team
//       j. Update to reload .txt rulz files on plugin enable, and display full path if not found
// v43 - a. added 'rounding' to vars, e.g. %x.3% is 3 decimal places
//       b. Allow "Set %ini% 0" to reset ini file, Set "%ini_<section>% 0" to delete section,
//       c. bugfix for TargetPlayer condition when %targettext% is null. Added '+' for rule continuation
//       d. added %score% var for each player (aka %server_score[<playername>]%)
// v42 - a. Support %ini_XXX% variables stored in settings
//       b. New 'logging' options in plugin settings
//       c. arithmetic in Set clauses e.g. Set %x% %x%+1
//       d. another effort at the procon URLencode settings work-around
//       e. allow spaces in arithmetic e.g. "If %x% + %y% > 10;"
//       f. new subst vars %ts1%, %ts2%, %pts% for teamsize 1, 2, playerteamsize
//       g. new subst var %hms% for time, %seconds% for time in seconds, %ymd% for yyyy-mm-dd
// v41 - a. Allow parsing of chat text e.g. to pick out a ban reason
// v40 - a. Quoted strings in Exec e.g. Exec vars.serverName "Example server name - Admins Online"
//       b. Quoted strings in Set e.g. Set %a% "hello world", $..$ for subst vars
//       c. $ replaced with % in rulz_vars to avoid Procon settings encode bug
//       d. fix for admin kill triggering On Suicide
// v39 - a. TeamSay, SquadSay, TeamYell, SquadYell, 
//          %pcountry%, %pcountrykey%, %vcountry%, %vcountrykey% (country codes for player and victim)
//       b. Allow 'Exec' actions to call Procon commands
//          Allow 'int' value for yell delay (seconds) in Yell actions
//       c. Ban and TempBan ban by 'name' if no GUID available
//       d. %team_score% aka %server_team_score[<teamid>]%
// v38 - a. actions and conditions can be mixed in any order, 
//       b. %streak% is shorthand for %server_streak[%p%]%, 
//          %team_streak% is %server_team_streak[%ptk%]%
//          %squad_streak% is %server_squad_streak[%ptk%][%psk%]%
//          rulz vars can be nested using brackets, e.g. %kills[%weapon%]%
//       c. modify Exec action to support punkBuster commands
//       d. TargetActions are now immediate, TargetPlayer only succeeds IFF 1 player found
//       e. Yell delay (default 5 seconds) added to plugin settings
// v37 - string vars (as well as ints from v35)
//       b. var names can have embedded subst vars, e.g. "server_%v%_deaths" is a var name with a player name embedded
//       c. moved the subst vars processing into assign_keywords
//       d. moved most of the Details help text onto the web
//       e. bugfix for %c% - set value for actions, temp value for conditions
//       f. bugfix for PBBan message
//       g. If "%text% word abc" condition, 
//          allow %vars% in Set %newvar% %p%,%newvar% conditions
//       h. added punkBuster.pb_sv_command pb_sv_updbanfile to speed up PB Bans/Kicks  
// v36 - Punkbuster bans
// v35 - a. Multiple keys in conditions e.g. "Weapon RPG-7,SMAW"
//       b. xyzzy debug
//      c. continue
//      d. rulz vars Set Incr Decr If
//      e. actions thread inline, no decode on rulz, %ps% PlayerSquad
//      f. propagate trigger from prior rulz, default continue unless Kill/Kick/TempBan/Ban/End
// v34 - Protected now Admin, Admin_and_Reserved_Slots, Neither
// v33 - BF3 compatibility
//       TargetPlayer now auto-TargetConfirm if only one match
// v32 - TargetPlayer,TargetAction,TargetConfirm,TargetCancel, %t% substitution for target
//       PlayerCount,TeamCount,ServerCount, %tc% and %sc% substitution for team and server counts
//       TempBan action
//       Ping condition, %ping% substitution
//       PlayerYell,PlayerBoth, AdminSay, %text% substition
//       'Rates' now span round ends
//       bugfix for multi-word weapon keys, e.g. "M1A1 Thompson", heli weapons
//       bugfix for sp_shotgun_s
//       On Join, On Leave triggers
//       Whitelist for clans and players
//       PlayerFirst, TeamFirst, ServerFirst, PlayerOnce conditions
//       Player_loses_item_when_dead bugfix
// v31 - Map, MapMode conditions, On Round trigger, VictimSay action 
//  (31b bugfix for On Spawn;Damage...) 
//  (31c bugfix for teamsize)
// v30 - settings options - rules message as PlayerSay, disable rules message
// v29 - TeamKit, TeamDamage, TeamSpec, TeamWeapon counts conditions
// v28 - updated to use latest Procon 1 API
// v27 - "Protected" condition
// v26 - "On Say; Text xxx;" trigger, Headshot %h% substitution
// v25 - "Range N;" condition
// v24 - new conditions Admin (player an admin), Admins (admins on server)
// v23 - fixed Count bug
// v22 - added rule comments ('#' as first char)
// v20 - changed rule to have <list> of Conditions
// v19 - added "Not [Kit|Spec|Damage|Weapon] X" condition

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

using PRoCon;
using PRoCon.Core;
using PRoCon.Core.Accounts;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Remote;


namespace PRoConEvents
{
    public partial class ProconRulz : PRoConPluginAPI, IPRoConPluginInterface
    {

        public String version = "44j.1";

        #region Types

        // the 'Rates' condition keeps track of this many 'rule trigger times' for each player
        const Int32 RATE_HISTORY = 60;

        //**********************************************************************************************
        //**********************************************************************************************
        //   DEFINE SOME TYPES
        //**********************************************************************************************
        //**********************************************************************************************

        enum GameIdEnum { BFBC2, MoH, BF3, BF4 }

        enum ReserveItemEnum { Player_loses_item_when_dead, Player_reserves_item_until_respawn }

        enum LogFileEnum { PluginConsole, Console, Chat, Events, Discard_Log_Messages }

        // these are the executable actions
        // note that 'TargetAction <action>' does not actually have 
        // 'TargetAction' as a defined action, instead it
        // is treated as a boolean flag on the PartClass to indicate the action is delayed
        enum PartEnum
        {
            Yell, Say, PlayerYell, PlayerSay, VictimSay, TeamYell, TeamSay, SquadYell, SquadSay,
            Log, Both, PlayerBoth, All, AdminSay, // various message actions
            // actions affecting 'target' e.g. playername in say text:
            TargetConfirm, TargetCancel,
            Kill, Kick, PlayerBlock, Ban, TempBan, PBBan, PBKick,
            Execute, Continue, End,
            Headshot, Protected, Admin, Admins, Team, Teamsize,
            Map, MapMode,
            Kit, Weapon, Spec, Damage, TeamKit, TeamWeapon, TeamSpec, TeamDamage,
            Range,
            Count, PlayerCount, TeamCount, ServerCount,
            Rate, Text, TargetPlayer, Ping,
            Set, Incr, Decr, Test
        };


        // flag for rule to fire either on player spawn, or a kill
        // Void mean absence of trigger
        enum TriggerEnum { Void, Round, RoundOver, Join, Kill, Spawn, TeamKill, Suicide, PlayerBlock, Say, Leave, Init }

        enum ItemTypeEnum { None, Kit, Weapon, Spec, Damage }

        // substitution values in messages
        enum SubstEnum
        {
            Player, Victim,
            Weapon, WeaponKey, Damage, DamageKey, Spec, SpecKey,
            Kit, VictimKit, KitKey,
            Count, TeamCount, ServerCount,
            PlayerTeam, PlayerSquad,
            PlayerTeamKey, PlayerSquadKey,
            PlayerCountry, PlayerCountryKey, // from pb info
            VictimCountry, VictimCountryKey,
            Teamsize, Teamsize1, Teamsize2, PlayerTeamsize,
            VictimTeam, VictimTeamKey, Range, BlockedItem, Headshot,
            Map, MapMode, Target, Text, TargetText, Ping, EA_GUID, PB_GUID, IP,
            Hhmmss, Seconds, Date
        };

        static Dictionary<SubstEnum, List<String>> subst_keys = new Dictionary<SubstEnum, List<String>>();

        // which players should be 'protected from kicks, kills and bans by ProconRulz
        enum ProtectEnum { Admins, Admins_and_Reserved_Slots, Neither };

        // a PART is a statement within a rule, i.e. a condition or action
        class PartClass
        {
            // this action should be delayed and applied to another target not current player
            public Boolean target_action;
            public PartEnum part_type; // e.g. Kick
            public Boolean negated; // conditions can be 'Not'
            public List<String> string_list;
            public Int32 int1;
            public Int32 int2;
            public Boolean has_count; // true if this part refers to a rule count (%c% or PlayerCount etc)

            // e.g. "You were kicked for teamkills". Can also be an integer string for Kill action
            public PartClass()
            {
                target_action = false;
                string_list = new List<String>();
                int1 = 0;
                int2 = 0;
                negated = false;
                has_count = false;
            }

            public String ToString()
            {
                String part_string;
                part_string = (negated ? " Not" : "");
                switch (part_type)
                {
                    case PartEnum.Headshot:
                        part_string += " Headshot;";
                        break;

                    case PartEnum.Protected:
                        part_string += " Player protected from kick/kill;";
                        break;

                    case PartEnum.Admin:
                        part_string += " Admin player;";
                        break;

                    case PartEnum.Admins:
                        part_string += " Admins are online;";
                        break;

                    case PartEnum.Ping:
                        part_string += String.Format(" Ping >= {0};", int1);
                        break;

                    case PartEnum.Team:
                        part_string += String.Format(" Team \"{0}\";", keys_join(string_list));
                        break;

                    case PartEnum.Teamsize:
                        part_string += String.Format(" Teamsize <= {0};", int1);
                        break;

                    case PartEnum.Map:
                        part_string += String.Format(" Map name includes \"{0}\";", keys_join(string_list));
                        break;

                    case PartEnum.MapMode:
                        part_string += String.Format(" Map mode is \"{0}\";", keys_join(string_list));
                        break;

                    case PartEnum.Kit:
                        part_string += " Kit \"" +
                            keys_join(string_list) + (int1 == 0 ? "\";" : String.Format("\" max({0});", int1));
                        break;

                    case PartEnum.Weapon:
                        part_string += " Weapon is \"" +
                            keys_join(string_list) + (int1 == 0 ? "\";" : String.Format("\" max({0});", int1));
                        break;

                    case PartEnum.Spec:
                        part_string += " Spec \"" +
                            keys_join(string_list) + (int1 == 0 ? "\";" : String.Format("\" max({0});", int1));
                        break;

                    case PartEnum.Damage:
                        part_string += " Damage \"" +
                            keys_join(string_list) + (int1 == 0 ? "\";" : String.Format("\" max({0});", int1));
                        break;

                    case PartEnum.TeamKit:
                        part_string += " TeamKit \"" +
                            keys_join(string_list) + String.Format("\" max({0});", int1);
                        break;

                    case PartEnum.TeamWeapon:
                        part_string += " TeamWeapon \"" +
                            keys_join(string_list) + String.Format("\" max({0});", int1);
                        break;

                    case PartEnum.TeamSpec:
                        part_string += " TeamSpec \"" +
                            keys_join(string_list) + String.Format("\" max({0});", int1);
                        break;

                    case PartEnum.TeamDamage:
                        part_string += " TeamDamage \"" +
                            keys_join(string_list) + String.Format("\" max({0});", int1);
                        break;

                    case PartEnum.Range:
                        part_string += " Range " + String.Format("over {0};", int1);
                        break;

                    case PartEnum.Count:
                    case PartEnum.PlayerCount:
                        part_string += " Player Rule Count is more than " + String.Format("{0};", int1);
                        break;

                    case PartEnum.TeamCount:
                        part_string += " Team Rule Count is more than " + String.Format("{0};", int1);
                        break;

                    case PartEnum.ServerCount:
                        part_string += " Server Rule Count is more than " + String.Format("{0};", int1);
                        break;

                    case PartEnum.Rate:
                        part_string += " Rate " + String.Format("{0} in {1} seconds;", int1, int2);
                        break;

                    case PartEnum.Text:
                        part_string += " Text " + String.Format("key \"{0}\";", keys_join(string_list));
                        break;

                    case PartEnum.TargetPlayer:
                        if (string_list != null)
                            part_string += " Target player contains " +
                                String.Format("\"{0}\";", keys_join(string_list));
                        else
                            part_string += " Target player %t% found;";
                        break;

                    case PartEnum.Incr:
                        part_string += String.Format(" Incr {0};", string_list[0]);
                        break;

                    case PartEnum.Decr:
                        part_string += String.Format(" Decr {0};", string_list[0]);
                        break;

                    case PartEnum.Set:
                        part_string += String.Format(" Set {0};", keys_join(string_list));
                        break;

                    case PartEnum.Test:
                        part_string += String.Format(" If [{0}];", keys_join(string_list));
                        break;

                    default:
                        part_string += (String.Format(" {0}{1} [Int32: {2}] [String: {3}];",
                                        target_action ? "TargetAction " : "",
                                        Enum.GetName(typeof(PartEnum), part_type),
                                        int1 == null || int1 == 0 ? "" : int1.ToString(),
                                        string_list[0]));
                        break;

                }
                return part_string;
            }

        } // end class PartClass

        // deferred actions from 'TargetAction' actions in a triggered rule
        class TargetActions
        {
            public String target;
            public List<PartClass> actions;

            public TargetActions(String t)
            {
                actions = new List<PartClass>();
                target = t;
            }
        }

        // here's how the rules are stored in ProconRulz (trigger, list of parts)
        class ParsedRule
        {
            public ParsedRule()
            {
                parts = new List<PartClass>();
                comment = false;
                trigger = TriggerEnum.Spawn;
            }
            public Int32 id; // rule identifier 1..n
            public List<PartClass> parts; // e.g. [Not Kit Recon 2]
            public TriggerEnum trigger; // trigger rule e.g. on spawn or kill
            public String unparsed_rule; // the original String parsed into this rule
            public Boolean comment; // this 'rule' is a comment to ignore at runtime
        }

        #endregion

        #region PlayerList class

        class PlayerData
        {
            public Boolean updated; // set to true during admin.listPlayers processing
            public String name;
            public String squad;
            public String team;
            public String ip;
            public String ea_guid;
            public String pb_guid;
            public String clan;
            public Int32 ping;
            public Int32 score;
            public String country_key;
            public String country_name;

            public PlayerData()
            {
                updated = false;
                squad = "-1";
                team = "-1";
                //ip = "no IP";
                //ea_guid = "No_EA_GUID";
                //pb_guid = "No_PB_GUID";
                clan = "No clan";
                ping = -1;
                score = 0;
                country_key = ""; // country code
                country_name = ""; // country
            }
        }

        // this plugin maintains its own list of playernames on the server
        class PlayerList
        {
            // player info is stored as a dictionary playername->CPlayerInfo
            Dictionary<String, PlayerData> info;

            // when a player first joins, we cache their name/team in here (don't have a CPlayerInfo yet)
            Dictionary<String, String> new_player_teams;

            public PlayerList()
            {
                // info is the main player list
                info = new Dictionary<String, PlayerData>();
                // new_player_teams is the cache of players from OnPlayerJoin before 
                // they get CPlayerInfo from OnListPlayers
                new_player_teams = new Dictionary<String, String>();
            }

            public void reset()
            {
                info.Clear();
                new_player_teams.Clear();
            }

            // remove player entries that don't have 'updated' true
            public void scrub()
            {
                new_player_teams.Clear();

                List<String> scrub_keys = new List<String>();
                foreach (String player_name in info.Keys)
                    if (info[player_name].updated == false) scrub_keys.Add(player_name);
                foreach (String player_name in scrub_keys) info.Remove(player_name);
            }

            public void pre_scrub()
            {
                foreach (String player_name in info.Keys) info[player_name].updated = false;
            }

            public void remove(String player_name)
            {
                // remove from main list
                info.Remove(player_name);
                //and remove from new player cache if necessary
                new_player_teams.Remove(player_name);
            }

            // called by OnPlayerJoin
            // a new player has a name and maybe a team, that's all. No CPlayerInfo
            public void new_player(String player_name)
            {
                if (info.ContainsKey(player_name)) return; // already in main player list
                if (new_player_teams.ContainsKey(player_name)) return; // already in new player cache
                // create entry for the player, but put them in team -1 (i.e. they're not in a team yet)
                new_player_teams.Add(player_name, "-1");
            }

            // here's where we add a player to the main list
            // and remove them from the new player cache if necessary
            public void update(CPlayerInfo inf)
            {
                String player_name = inf.SoldierName;
                // remove from new player cache
                new_player_teams.Remove(player_name);
                // add to main player list (update existing entry if necessary)

                if (!info.ContainsKey(player_name) || info[player_name] == null)
                    info[player_name] = new PlayerData();
                info[player_name].name = player_name;
                info[player_name].squad = inf.SquadID.ToString();
                info[player_name].team = inf.TeamID.ToString();
                info[player_name].ea_guid = inf.GUID;
                info[player_name].clan = inf.ClanTag;
                info[player_name].score = inf.Score;
                info[player_name].updated = true;
            }

            // update based on Punkbuster info
            public void update(CPunkbusterInfo inf)
            {
                String player_name = inf.SoldierName;
                if (!info.ContainsKey(player_name) || info[player_name] == null)
                    info[player_name] = new PlayerData();
                info[player_name].name = player_name;
                info[player_name].pb_guid = inf.GUID;
                info[player_name].ip = inf.Ip;
                info[player_name].country_key = inf.PlayerCountryCode;
                info[player_name].country_name = inf.PlayerCountry;
            }

            public void team_move(String player_name, String team_id, String squad_id)
            {
                // attempt 1: update player entry in main list
                if (info.ContainsKey(player_name))
                {
                    info[player_name].team = team_id;
                    info[player_name].squad = squad_id;
                }
                // attempt 2: maybe they've just joined - update player entry in new player list
                if (new_player_teams.ContainsKey(player_name))
                    new_player_teams[player_name] = team_id;
                return;
            }

            // return the current team_id of player
            public String team_id(String player_name)
            {
                if (player_name == null) return "-1";
                // attempt #1: try main player list
                if (info.ContainsKey(player_name)) return info[player_name].team;
                // attempt #2: try cache of new players
                if (new_player_teams.ContainsKey(player_name)) return new_player_teams[player_name];
                else return "-1";
            }

            // return the current squad_id of player
            public String squad_id(String player_name)
            {
                if (player_name == null) return "-1";
                // attempt #1: try main player list
                if (info.ContainsKey(player_name)) return info[player_name].squad;
                return "-1";
            }

            // return the number of players in team
            public Int32 teamsize(String team_id)
            {
                return list_players(team_id).Count;
            }

            // return current ping for this player, as updated in the latest admin.listPlayers
            public Int32 ping(String player_name)
            {
                if (player_name == null ||
                    player_name == "" ||
                    !info.ContainsKey(player_name) ||
                    info[player_name].ping == null) return -1;

                return info[player_name].ping;
            }

            // return current score for this player, as updated in the latest admin.listPlayers
            public Int32 score(String player_name)
            {
                if (player_name == null ||
                    player_name == "" ||
                    !info.ContainsKey(player_name) ||
                    info[player_name].score == null) return -1;

                return info[player_name].score;
            }

            // return EA GUID for this player, as updated in the latest admin.listPlayers
            public String ea_guid(String player_name)
            {
                if (player_name == null ||
                    player_name == "" ||
                    !info.ContainsKey(player_name) ||
                    info[player_name].ea_guid == null) return "";

                return info[player_name].ea_guid;
            }

            // return Punkbuster GUID for this player, as updated in the latest OnPunkbusterInfo
            public String pb_guid(String player_name)
            {
                if (player_name == null ||
                    player_name == "" ||
                    !info.ContainsKey(player_name) ||
                    info[player_name].pb_guid == null) return "";

                return info[player_name].pb_guid;
            }

            // return IP address for this player, as updated in the latest OnPunkbusterInfo
            public String ip(String player_name)
            {
                if (player_name == null ||
                    player_name == "" ||
                    !info.ContainsKey(player_name) ||
                    info[player_name].ip == null) return "";

                return info[player_name].ip;
            }

            // return country name for this player, as updated in the latest OnPunkbusterInfo
            public String cname(String player_name)
            {
                if (player_name == null ||
                    player_name == "" ||
                    !info.ContainsKey(player_name) ||
                    info[player_name].country_name == null) return "";

                return info[player_name].country_name;
            }

            // return country KEY for this player, as updated in the latest OnPunkbusterInfo
            public String ckey(String player_name)
            {
                if (player_name == null ||
                    player_name == "" ||
                    !info.ContainsKey(player_name) ||
                    info[player_name].country_key == null) return "";

                return info[player_name].country_key;
            }

            public List<String> list_new_players()
            {
                return new List<String>(new_player_teams.Keys);
            }

            public List<String> list_players()
            {
                return new List<String>(info.Keys);
            }

            public List<String> list_players(String team_id)
            {
                List<String> player_list = new List<String>();
                foreach (String player_name in info.Keys)
                    if (info[player_name].team == team_id) player_list.Add(player_name);
                // also check new players
                foreach (String player_name in new_player_teams.Keys)
                    if (new_player_teams[player_name] == team_id) player_list.Add(player_name);
                return player_list;
            }

            public Int32 min_teamsize()
            {
                Int32 size1 = teamsize("1");
                Int32 size2 = teamsize("2");
                return size1 < size2 ? size1 : size2;
            }

            // return a list os all the team_ids found in the player list
            public List<String> list_team_ids()
            {
                List<String> team_ids = new List<String>();
                foreach (String player_name in info.Keys)
                    try
                    {
                        if (!team_ids.Contains(info[player_name].team))
                            team_ids.Add(info[player_name].team);
                    }
                    catch { }
                foreach (String player_name in new_player_teams.Keys)
                    try
                    {
                        if (!team_ids.Contains(new_player_teams[player_name]))
                            team_ids.Add(new_player_teams[player_name]);
                    }
                    catch { }

                team_ids.Sort(); // sort ascending
                return team_ids;
            }

            public String clan(String player_name)
            {
                if (player_name == null) return "No clan";
                if (player_name == "") return "No clan";

                try
                {
                    if (info.ContainsKey(player_name))
                    {
                        String clan_name = info[player_name].clan;
                        if (clan_name == null) return "No clan";
                        if (clan_name.Trim() == "") return "No clan";
                        return clan_name;
                    }

                }
                catch { }

                return "No clan";
            }
        }

        #endregion

        #region SpawnCounts class
        // counts of spawned items in team_1 and team_2 - lists player names with each item
        // each entry is <team_id> -><item name>::[<player_name>,...]
        // e.g. Recon -> [sleepy, grumpy, doc]
        class SpawnCounts
        {
            Dictionary<String, Dictionary<String, List<String>>> counts;

            List<String> watched_items;

            public SpawnCounts()
            {
                //                      team_id    ->  (item_name) ->  (List of player names)
                counts = new Dictionary<String, Dictionary<String, List<String>>>();
                watched_items = new List<String>();
            }

            public List<String> list_items()
            {
                return watched_items;
            }

            // return a list of team_ids that have spawn counts recorded against them
            //public List<int> list_team_ids()
            //{
            //    return new List<int>(counts.Keys);
            //}

            // list the players spawned with an item in a given team
            public List<String> list_players(String item_name, String team_id)
            {
                if (!counts.ContainsKey(team_id)) return new List<String>(); // team has no watch items
                // team doesn't have this item watched:
                if (!counts[team_id].ContainsKey(item_name)) return new List<String>();
                return counts[team_id][item_name]; // return list of player names
            }

            // this is a bit subtle - the way we 'watch' items (kits, weapons, specs, damagetypes) is by
            // creating a dictionary key entry for the string name of that item (e.g. "recon").
            // The 'value' of that dictionary entry is the list of string playernames that have spawned
            // with that item.
            // This enables us to count the number of players with this item 
            // (i.e. the length of the player
            // name list in that dictionary entry.).
            public void watch(List<String> items_mixed)
            {
                foreach (String item_mixed in items_mixed)
                    if (!watched_items.Contains(item_mixed.ToLower())) watched_items.Add(item_mixed.ToLower());
            }

            // scrub player "name" from both spawn_counts (on spawn, so we can add a fresh entry)
            public void zero_player(String player_name)
            {
                if (player_name == null) return;
                if (player_name == "") return;
                foreach (String team_id in counts.Keys)
                {
                    foreach (String item in counts[team_id].Keys)
                    {
                        counts[team_id][item].Remove(player_name);
                    }
                }
            }

            // keep watch list but zero all counts (called at round end)
            public void zero()
            {
                counts.Clear();
            }

            // reset to startup status
            public void reset()
            {
                counts.Clear();
                watched_items.Clear();
            }

            // player "name" has just spawned with item 'item', so add to counts for that team
            public void add(String item_mixed, String team_id, String player_name)
            {
                if (player_name == null) return;
                if (player_name == "") return;

                String item_lcase = item_mixed.ToLower();

                if (!watched_items.Contains(item_lcase)) return; // ignore if item is not 'watched'

                if (!counts.ContainsKey(team_id)) // if no entry for team then create one
                    counts[team_id] = new Dictionary<String, List<String>>();

                if (!counts[team_id].ContainsKey(item_lcase)) // if no entry for item then create one
                    counts[team_id][item_lcase] = new List<String>();

                // add team/item/player_name if it's not already there
                if (!counts[team_id][item_lcase].Contains(player_name))
                    counts[team_id][item_lcase].Add(player_name);
            }

            // how many players on team currently have any 'item' on list
            public Int32 count(List<String> items_mixed, String team_id)
            {
                if (!counts.ContainsKey(team_id)) return 0; // if team has no watched items then 0
                List<String> items_lcase = new List<String>();
                if (items_mixed != null)
                    foreach (String i in items_mixed) items_lcase.Add(i.ToLower());
                List<String> player_list = new List<String>(); // list of players with these items
                foreach (String item_lcase in items_lcase)
                {
                    if (!watched_items.Contains(item_lcase)) continue; // if not watched then 0
                    // if team does not have this item then 0
                    if (!counts[team_id].ContainsKey(item_lcase)) continue;
                    // return length of player name list for this team/item
                    player_list.AddRange(counts[team_id][item_lcase]);
                }
                return player_list.Count;
            }

            // return 'true' if player has any item on the list
            public Boolean has_item(List<String> items_mixed, String player_name)
            {
                if (player_name == null) return false;
                if (player_name == "") return false;
                List<String> items_lcase = new List<String>();
                if (items_mixed != null)
                    foreach (String i in items_mixed) items_lcase.Add(i.ToLower());
                foreach (String item_lcase in items_lcase)
                {
                    if (!watched_items.Contains(item_lcase)) continue;// if not watched then 0
                    foreach (String team_id in counts.Keys)
                        if (counts[team_id].ContainsKey(item_lcase) &&
                                counts[team_id][item_lcase].Contains(player_name)) return true;
                }
                return false;
            }
        }

        #endregion

        #region VarsClass class - this manages the rulz variables (Set, Incr, Decr, If)
        // stores values of rulz variables
        class VarsClass
        {
            // basic proconrulz vars
            Dictionary<String, String> vars;
            // persistent vars - stored in Configs/proconrulz.ini
            Dictionary<String, Dictionary<String, String>> ini_vars;

            String ini_filename;

            public VarsClass(String file_hostname_port)
            {
                ini_filename = "Configs" + Path.DirectorySeparatorChar + file_hostname_port + "_proconrulz.ini";
                // as of ver 37 all vars stored as strings
                vars = new Dictionary<String, String>();
                ini_vars = new Dictionary<String, Dictionary<String, String>>();
            }

            // keep list but zero all values (called at round end)
            public void zero()
            {
                vars.Clear();
            }

            // reset to startup status
            public void reset()
            {
                vars.Clear();
                ini_vars.Clear();
                ini_load(ini_filename);
            }

            // MANGLE is a fairly important function in ProconRulz vars processing...
            // all vars are converted to 'server' variables but the usage in rulz allows
            // them to appear as 'per-player' variables.
            // i.e. %streak% in a rule is a UNIQUE variable for each player
            // the rulz processing converts this to %server_streak[<playername>]% as the unique name
            // so in effect, %streak% is shorthand for %server_streak[%p%]%
            //
            // We convert the var name into something valid globally
            // i.e. %kills% -> %server_kills[<playername>]%
            // %squad_kills% -> %server_squad_kills[1][2]% (where 1 = team, 2 = squad id's)
            // %team_kills% -> %server_team_kills[1]% (where "1" is team id for player)
            // %server_kills% -> unchanged
            private String mangle(String player_name, String var_name)
            {
                var_name = var_name.Replace("$", "%");
                // check for a valid var name %...%
                if (var_name == null ||
                    var_name.Length < 3 ||
                    !var_name.StartsWith("%") ||
                    !var_name.EndsWith("%")) return null;
                // replace [%vars%] in this var name with their value
                var_name = replace_index_vars(player_name, var_name);
                // if it's a 'server variable' return it unchanged
                if (var_name.ToLower().StartsWith("%server_")) return var_name;
                // ini var name - return unchanged
                if (var_name.ToLower() == "%ini%") return var_name;
                if (var_name.ToLower().StartsWith("%ini_")) return var_name;
                // see if it's a team, squad or a player variable which means we much have a valid player name
                if (player_name == null) return null;
                // raw_name is a var name without the % % 
                String raw_name = var_name.Substring(1, var_name.Length - 2);
                if (var_name.ToLower().StartsWith("%team_"))
                    // e.g. %team_kills% -> %server_team_kills[1]% (where "1" is team id for player)
                    return "%server_" + raw_name + "[" + players.team_id(player_name).ToString() + "]%";
                if (var_name.ToLower().StartsWith("%squad_"))
                    // e.g. %squad_kills% -> %server_squad_kills[1][2]% (where 1 = team, 2 = squad id's)
                    return "%server_" + raw_name + "[" + players.team_id(player_name).ToString() + "][" +
                        players.squad_id(player_name).ToString() + "]%";
                // proc has been called with a 'player variable', 
                // e.g. "%streak%", mangle to "server_streak[playername]%"
                return "%server_" + raw_name + "[" + player_name + "]%";
            }

            // value of an expression : replace %v%-type subst values, then replace %streak%-type rulz vars:
            private String get_value(String player_name, String input_exp, Dictionary<SubstEnum, String> keywords)
            {
                // replace substitution variables, e.g. %p% with playername from keywords
                String substituted_exp = replace_keys(input_exp, keywords);
                // now exp doesn't contain any substitution vars
                // replace user vars with their values, e.g. %server_kills% or whatever user has used in rulz
                String replaced_exp = replace_vars(player_name, substituted_exp);
                // now exp has no vars, but may include arithmetic
                String reduced_exp = reduce(replaced_exp); // "1+1" -> "2"
                return reduced_exp;
            }

            // convert %ini_<section>_<var>% to Array[0..2]<string> [ini,<section_name>,<var_name>]
            String[] var_to_ini(String full_var_name)
            {
                String[] ini_parts = new String[3];
                if (full_var_name == "%ini%")
                {
                    ini_parts[0] = "ini";
                }
                else if (full_var_name.StartsWith("%ini_"))
                {
                    ini_parts[0] = "ini";
                    // e.g. %ini_vars_plugin_settings% 
                    String[] var_parts = full_var_name.Split('_');
                    if (var_parts.Length >= 2)
                    {
                        if (var_parts.Length == 2) // no section name, e.g. %ini_myvar% so default to [vars]
                        {
                            ini_parts[1] = var_parts[1].TrimEnd(new Char[] { '%' }); // var name
                        }
                        else
                        {
                            ini_parts[1] = var_parts[1]; // section name
                            ini_parts[2] = full_var_name.Substring(5 + var_parts[1].Length + 1).TrimEnd(new Char[] { '%' });
                        }
                    }
                }
                return ini_parts;
            }

            // find the value of an 'atom' i.e. a string, int or variable
            // exp can be a variable name or an integer or a string
            private String atom_value(String player_name, String exp)
            {
                if (exp == null || exp == "") return "";
                // try to get a number
                float i;
                try
                {
                    i = float.Parse(exp);
                    return i.ToString();
                }
                catch { }
                // ok, we didn't get an int, so try a variable lookup
                // if not a %..% var just return the string
                if (!exp.StartsWith("%") || !exp.EndsWith("%")) return exp;
                String full_var_name = mangle(player_name, exp);
                if (full_var_name == null) return "";
                // return the variable value if there is one
                if (vars.ContainsKey(full_var_name))
                    return vars[full_var_name];
                else // we'll try the ini values
                {
                    //convert %ini_<section>_<varname>% into list [section_name, var_name]
                    String[] ini_parts = var_to_ini(full_var_name);
                    if (ini_parts[0] != null)
                    {
                        String section_name = ((ini_parts[1] == null) ? "vars" : ini_parts[1]);
                        String var_name = ini_parts[2];
                        if (ini_vars.ContainsKey(section_name) &&
                             var_name != null &&
                             ini_vars[section_name].ContainsKey(var_name))
                        {
                            return ini_vars[section_name][var_name];
                        }
                    }
                }

                // this *is* a %..% var but we didn't get a value, so we'll return "0"
                return "0";
            }

            // SET the value of a variable
            // if playername is null, var must be %server_..%
            // keywords can be null (so no keyword substitutions)
            public void set_value(String player_name, String var_name, String assign_value, Dictionary<SubstEnum, String> keywords)
            {
                // substitute the %mm% type subst vars
                var_name = keywords == null ? var_name : replace_keys(var_name, keywords);

                String full_var_name = mangle(player_name, var_name);
                if (full_var_name == null) return;

                // now get result of assign_value
                String result = get_value(player_name, assign_value, keywords);

                // see if this var has a 'rounding' attribute, e.g. %xxx.2%
                Int32 var_index = var_name.LastIndexOf('.');
                if (var_index > 0)
                {
                    // ok so it IS a 'rounding var'
                    // so check if the result has a '.' in it...
                    Int32 result_index = result.LastIndexOf('.');
                    if (result_index >= 0)
                    {
                        // so now we're looking at "Set %x.2% 1.23456"
                        try
                        {
                            // decimals is the number in the var name after the '.'
                            Int32 decimals = Int32.Parse(var_name.Substring(var_index + 1, var_name.Length - var_index - 2));
                            // add/subtract 0.005 (or similar) so truncate = rounding
                            Double result_float = Double.Parse(result);
                            Double adjust = 0.5 / Math.Pow(10, decimals); // here is rounding adjust value
                            if (result_float >= 0)
                            {
                                result_float = result_float + adjust;
                            }
                            else
                            {
                                result_float = result_float - adjust;
                            }
                            result = result_float.ToString();

                            result_index = result.LastIndexOf('.'); // did '.' move ?
                            if (decimals == 0)
                            {
                                // for %x.0% remove the '.' as well as the decimal digits...
                                result = result.Substring(0, result_index);
                            }
                            else
                            {
                                // here's where we update 1.23456 to 1.23
                                result = result.Substring(0, result_index + decimals + 1);
                                // an exception could occur above if the string is too short, so result is unchanged
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                // try and get ini var parts from full_var_name
                String[] ini_parts = var_to_ini(full_var_name);
                if (ini_parts[0] == null)
                {
                    // didn't parse an ini section_name/var_name so do simple vars assign
                    vars[full_var_name] = result;
                }
                else
                {
                    // successfully parsed %ini_section_var% so assign and update ini file
                    String ini_section_name = ini_parts[1];
                    String ini_var_name = ini_parts[2];
                    // load entire file...
                    ini_load(ini_filename);
                    // update our local value ini_vars
                    ini_set_value(ini_section_name, ini_var_name, result);
                    // write all ini_vars out to proconrulz.ini file
                    ini_save(ini_filename);
                }
                return;
            }

            // INCREMENT THE VALUE OF A VARIABLE
            public void incr(String player_name, String var_name, Dictionary<SubstEnum, String> keywords)
            {
                Int32 result = 1;
                try
                {
                    result = Int32.Parse(get_value(player_name, var_name, keywords)) + 1;
                }
                catch { }
                set_value(player_name, var_name, result.ToString(), keywords);
                return;
            }

            // DECREMENT THE VALUE OF A VARIABLE
            public void decr(String player_name, String var_name, Dictionary<SubstEnum, String> keywords)
            {
                Int32 result = 0;
                try
                {
                    result = Int32.Parse(get_value(player_name, var_name, keywords)) - 1;
                    if (result < 0) result = 0;
                }
                catch { }
                set_value(player_name, var_name, result.ToString(), keywords);
                return;
            }

            public Boolean test(String player_name, String val_i, String cond, String val_j, Dictionary<SubstEnum, String> keywords)
            {
                String i = get_value(player_name, val_i, keywords).ToLower();
                String j = get_value(player_name, val_j, keywords).ToLower();
                switch (cond.ToLower())
                {
                    case "=": return i == j;
                    case "==": return i == j;
                    case "!=": return i != j;
                    case "<>": return i != j;
                    case ">": return bigger(i, j);                  // i > j
                    case "<": return (i != j) && !bigger(i, j);    // i < j
                    case "=>": return (i == j) || bigger(i, j);    // i >= j
                    case ">=": return (i == j) || bigger(i, j);    // i >= j
                    case "<=": return !bigger(i, j);               // i <= j;
                    case "=<": return !bigger(i, j);               // i <= j;
                    case "contains": return i.Contains(j); // i contains j
                    case "word": return Regex.IsMatch(i, string.Format(@"\b{0}\b", j), System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    default: return false;
                }
            }

            // 
            private Boolean bigger(String x, String y)
            {
                float i = 0, j = 0;
                try
                {
                    i = float.Parse(x);
                    j = float.Parse(y);
                    return i > j;
                }
                catch { }
                return String.Compare(x, y, true) > 0;
            }

            // reduce arithmetic expression to their value e.g. "2+2" -> "4"
            public String reduce(String exp)
            {
                if (!exp.Contains("+") && !exp.Contains("-") && !exp.Contains("*") && !exp.Contains("/"))
                {
                    return exp;
                }
                String left, right, left_result, right_result;
                float left_float = 0, right_float = 0;
                Boolean left_num = false;
                Boolean right_num = false;
                foreach (Char op in new Char[4] { '+', '-', '*', '/' })
                {
                    Int32 i = exp.IndexOf(op);
                    if (i > 0)
                    {
                        left = exp.Substring(0, i);
                        right = exp.Substring(i + 1, exp.Length - i - 1);
                        left_result = reduce(left);
                        right_result = reduce(right);
                        try
                        {
                            left_float = float.Parse(left_result);
                            left_num = true;
                        }
                        catch { };
                        try
                        {
                            right_float = float.Parse(right_result);
                            right_num = true;
                        }
                        catch { };
                        if (left_num && right_num)
                        {
                            switch (op)
                            {
                                case '+':
                                    return (left_float + right_float).ToString();

                                case '-':
                                    return (left_float - right_float).ToString();

                                case '*':
                                    return (left_float * right_float).ToString();

                                case '/':
                                    if (right_float == 0) return "0";
                                    return (left_float / right_float).ToString();

                            }
                        }
                        if (left_num)
                        {
                            return left_float.ToString() + op + right_result;
                        }
                        if (right_num)
                        {
                            return left_result + op + right_float.ToString();
                        }
                        return left_result + op + right_result;
                    }
                }
                return exp;
            }

            // replace %var_name% in message with the var value
            // note it could be a var name using subst variables %server_%p%_deaths%
            // message already has subst vars replaced (e.g. %p%)
            public String replace_vars(String player_name, String message)
            {
                message = message.Replace("$", "%");
                // e.g. message is "player K/D is %kills%/%deaths%"
                if (message == null) return null;
                if (message.Length < 3) return message;

                // first replace rulz vars inside [ brackets e.g. %kills[%killer%]% -> %kills[bambam]%
                message = replace_index_vars(player_name, message);

                Int32 j = 0;
                Int32 fragment_start = 0;
                String vars_message = "";
                while (fragment_start < message.Length)
                {
                    Int32 i = message.IndexOf("%", fragment_start);
                    j = -1; // second % not found - update if we find first %
                    if (i >= 0 && message.Length - i > 2) j = message.IndexOf("%", i + 2);
                    // now i is index of 1st %, and j the 2nd
                    // add the non-var fragment
                    if (i < 0 || j < 0) // didn't find the first % or second %
                    {
                        vars_message += message.Substring(fragment_start);
                        break;
                    }
                    // add the non-var piece
                    vars_message += message.Substring(fragment_start, i - fragment_start);
                    // add the var subsitution
                    vars_message += atom_value(player_name, message.Substring(i, j - i + 1));
                    fragment_start = j + 1;
                }
                return vars_message;
            }

            // here we replace the %...[%...%]% vars between []
            // e.g. %server_kills[%server_killername%]% -> %server_kills[bambam]%
            // note that the user could wrap a non-nested var in [] in a message, e.g. [%x%]
            public String replace_index_vars(String player_name, String message)
            {
                if (message == null) return null;
                if (message.Length < 8) return message; // "%x[%y%]%" is min length for any nested var

                Int32 j = 0;
                Int32 fragment_start = 0;
                String vars_message = "";
                Boolean in_var = false; // toggle to keep track of whether we're inside a %..% var or not
                while (fragment_start < message.Length)
                {
                    // find the start of the non-nested var
                    Int32 i = message.IndexOf("%", fragment_start);
                    if (i < 0) // didn't find the first %
                    {
                        vars_message += message.Substring(fragment_start);
                        break;
                    }
                    // found a '%'
                    if (!in_var)
                    {
                        in_var = true;
                        // i is pointing to the second % of a non-nested var
                        // add the non-var piece up to and including the second %
                        vars_message += message.Substring(fragment_start, i - fragment_start + 1);
                        // copy fragment up to here and continue
                        fragment_start = i + 1;
                        continue;
                    }

                    // in_var = true
                    if (message[i - 1] != '[')
                    {
                        // in a var, but this is not [% opening a nested var
                        in_var = false;
                        // i is pointing to the second % of a non-nested var
                        // add the non-var piece up to and including the second %
                        vars_message += message.Substring(fragment_start, i - fragment_start + 1);
                        // copy fragment up to here and continue
                        fragment_start = i + 1;
                        continue;

                    }
                    // now i points to % in [% at start of nested var
                    // so HERE we have start of a nested var with % at i after [
                    j = -1; // now find %]
                    if (message.Length - i > 3) j = message.IndexOf("%]", i + 2);
                    // now i is index of 1st %, and j the index of %]
                    // add the non-var fragment
                    if (j < 0) // didn't find the %]
                    {
                        vars_message += message.Substring(fragment_start);
                        break;
                    }
                    // add the non-var piece up to and including the [
                    vars_message += message.Substring(fragment_start, i - fragment_start);
                    // add the var subsitution
                    vars_message += atom_value(player_name, message.Substring(i, j - i + 1));
                    // now add the closing ]
                    vars_message += "]";
                    fragment_start = j + 2;
                }
                return vars_message;
            }

            // return all the vars - called on "prdebug dump"
            public Dictionary<String, String> dump() { return vars; }

            /* Read/Write .ini Files
            /// 
            /// Version 1, 2009-08-15
            /// http://www.Stum.de
            /// It supports the simple .INI Format:
            /// 
            /// [SectionName]
            /// Key1=Value1
            /// Key2=Value2
            /// 
            /// [Section2]
            /// Key3=Value3
            /// 
            /// You can have empty lines (they are ignored), but comments are not supported
            /// Key4=Value4 ; This is supposed to be a comment, but will be part of Value4
            /// 
            /// Whitespace is not trimmed from the beginning and end of either Key and Value
            /// 
            /// Licensed under WTFPL
            /// http://sam.zoy.org/wtfpl/
            */

            private readonly Regex _sectionRegex = new Regex(@"^\[(?<SectionName>[^\]]+)(?=\])");
            private readonly Regex _keyValueRegex = new Regex(@"(?<Key>[^=]+)=(?<Value>.+)");

            /// Get a specific value from the .ini file
            /// <returns>The value of the given key in the given section, or NULL if not found</returns>
            public String ini_get_value(String sectionName, String key)
            {
                if (ini_vars.ContainsKey(sectionName) && ini_vars[sectionName].ContainsKey(key))
                    return ini_vars[sectionName][key];
                else
                    return null;
            }

            /// Set a specific value in a section
            public void ini_set_value(String sectionName, String key, String value)
            {
                // remove entry if value is 0
                if (value == "0")
                {
                    if (sectionName != null)
                    {
                        if (ini_vars.ContainsKey(sectionName))
                        {
                            if (key != null)
                            {
                                //remove this variable
                                ini_vars[sectionName].Remove(key);
                                // if section is now empty, remove that
                                if (ini_vars[sectionName].Count == 0)
                                {
                                    ini_vars.Remove(sectionName);
                                }
                            }
                            else
                            {
                                // sectionName not null, key is null, so remove section
                                ini_vars.Remove(sectionName);
                            }
                        }
                    }
                    else
                    {
                        // sectionName is null, so reset ALL variables
                        ini_vars.Clear();
                    }
                }
                else
                {
                    if (!ini_vars.ContainsKey(sectionName)) ini_vars[sectionName] = new Dictionary<String, String>();
                    ini_vars[sectionName][key] = value;
                }
            }

            /// Get all the Values for a section
            public Dictionary<String, String> ini_get_section(String sectionName)
            {
                if (ini_vars.ContainsKey(sectionName))
                    return new Dictionary<String, String>(ini_vars[sectionName]);
                else
                    return new Dictionary<String, String>();
            }

            /// Set an entire sections values
            public void ini_set_section(String sectionName, IDictionary<String, String> sectionValues)
            {
                if (sectionValues == null) return;
                ini_vars[sectionName] = new Dictionary<String, String>(sectionValues);
            }

            /// Load an .INI File
            public Boolean ini_load(String filename)
            {
                if (File.Exists(filename))
                {
                    try
                    {
                        String[] content;
                        content = File.ReadAllLines(filename);
                        ini_vars = new Dictionary<String, Dictionary<String, String>>();
                        String currentSectionName = string.Empty;
                        foreach (String line in content)
                        {
                            Match m = _sectionRegex.Match(line.Trim());
                            if (m.Success)
                            {
                                currentSectionName = m.Groups["SectionName"].Value;
                            }
                            else
                            {
                                m = _keyValueRegex.Match(line);
                                if (m.Success)
                                {
                                    String key = m.Groups["Key"].Value.Trim();
                                    String value = m.Groups["Value"].Value.Trim();

                                    Dictionary<String, String> kvpList;
                                    if (ini_vars.ContainsKey(currentSectionName))
                                    {
                                        kvpList = ini_vars[currentSectionName];
                                    }
                                    else
                                    {
                                        kvpList = new Dictionary<String, String>();
                                    }
                                    kvpList[key] = value;
                                    ini_vars[currentSectionName] = kvpList;
                                }
                            }
                        }
                        return true;
                    }
                    catch
                    {
                        return false;
                    }

                }
                else
                {
                    return false;
                }
            }

            /// Save the content of this class to an INI File
            public Boolean ini_save(String filename)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (ini_vars != null)
                {
                    foreach (String sectionName in ini_vars.Keys)
                    {
                        sb.AppendFormat("[{0}]\r\n", sectionName);
                        foreach (String keyValue in ini_vars[sectionName].Keys)
                        {
                            sb.AppendFormat("{0}={1}\r\n", keyValue, ini_vars[sectionName][keyValue]);
                        }
                    }
                }
                try
                {
                    File.WriteAllText(filename, sb.ToString());
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion

        #region Global Vars

        #region server_ip.cfg variables
        //**********************************************************************************************
        //**********************************************************************************************
        //   VARIABLES USED IN server_ip.cfg
        //**********************************************************************************************
        //**********************************************************************************************

        // game type (for loading rulz files)
        GameIdEnum game_id = GameIdEnum.BF4;

        Int32 yell_delay = 5; // how long to have the yell message on screen

        Int32 kill_delay = 5000; // default (milliseconds) delay if the *rule* does not specify a delay 
                                 // between the event and the player being killed

        Int32 ban_delay = 60 * 60 * 24 * 7; // default temp ban is for a week in secs (not server_ip.cfg)

        // If 'yes' then players on procon 'Reserved' 
        // list have immunity from kick/kill
        ProtectEnum protect_players = ProtectEnum.Admins;

        // installing admin has to check the "Rules of Conduct" checkbox
        enumBoolYesNo roc_read = enumBoolYesNo.No;

        ReserveItemEnum reservationMode = ReserveItemEnum.Player_reserves_item_until_respawn;

        // a flag if yes will output debug console writes
        static enumBoolYesNo trace_rules = enumBoolYesNo.No;

        // this list stores the rules read from server_ip.cfg and user rulz files
        //static Dictionary<string, List<string>> unparsed_rulz = new Dictionary<string, List<string>>();
        static List<String> unparsed_rules = new List<String>();

        // list of rulz.txt filenames to load
        static List<String> rulz_filenames = new List<String>();

        List<String> whitelist_players = new List<String>(); // list of playernames to be protected

        List<String> whitelist_clans = new List<String>(); // list of clans to be protected

        LogFileEnum log_file = LogFileEnum.PluginConsole;

        #endregion

        //**********************************************************************************************
        //**********************************************************************************************
        //   GLOBAL VARIABLES
        //**********************************************************************************************
        //**********************************************************************************************

        String auth_name = "bambam_ofc"; // Bambam, author of this plugin (see forum.myrcon.com)

        Boolean plugin_enabled; // set true and false by Procon, when admin enables/disables this plugin

        WeaponDictionary weaponDefines;       // <String, Weapon>
        SpecializationDictionary specDefines; // <String, Specialization>

        static PlayerList players = new PlayerList();

        // record GUIDs of players
        Dictionary<String, CPlayerInfo> player_info = new Dictionary<String, CPlayerInfo>();

        // lists of players spawned with watched items
        SpawnCounts spawn_counts = new SpawnCounts();

        // kill_counts accumulates the item counts on KILL for each player
        // e.g. kill_counts["bambam"]["AUG"] = 3 means bambam has 3 AUG kills.
        // count added with add_kill_count(player_name, item_name)
        // count retrieved with count_kill_item(player_name, item_name)
        Dictionary<String, Dictionary<String, Int32>> kill_counts
            = new Dictionary<String, Dictionary<String, Int32>>();

        // the count of number of times each player has triggered each rule
        // i.e. player_name -> <rule_id,count>
        // reset at round start
        Dictionary<String, Dictionary<Int32, Int32>> rule_counts
            = new Dictionary<String, Dictionary<Int32, Int32>>();

        // timestamps of players triggering rules, for 'Rate' calculations
        // i.e. player_name -> <rule_id,count>
        // reset when plugin loaded. Meanwhile players are individually 
        // 'scrubbed' out of the timestamp lists
        Dictionary<String, Dictionary<Int32, DateTime[]>> rule_times
            = new Dictionary<String, Dictionary<Int32, DateTime[]>>();

        // player_blocks will block a player with a given item, triggering PlayerBlock rule
        Dictionary<String, List<String>> player_blocks = new Dictionary<String, List<String>>();

        // Dictionary playername->kit_key of the Kit the player spawned with
        Dictionary<String, String> player_kit = new Dictionary<String, String>();

        // whitelist of players not to kick or kill
        List<String> reserved_slot_players = new List<String>();

        // list of unparsed rules from user rulz files
        Dictionary<String, String[]> filez_rulz = new Dictionary<String, String[]>();

        // list of parsed rules loaded from user
        List<ParsedRule> parsed_rules = new List<ParsedRule>();

        CMap current_map; // map info of current loaded map, set by OnLoadingLevel()
        String current_map_mode = "None"; // mod for BF3, cannot derive map mode from map name

        Int32 rulz_spam_limit = 2; // max # times a player can request RULZ/MOTD

        Boolean rulz_enable = true;

        // WriteDebugInfo will increment this - Procon bug may duplicate lines in log?
        Int32 debug_write_count = 0;

        Char rulz_key_separator = '&'; // Char used to replace ' ' in rulz
        static Char rulz_item_separator = ','; // separator for items in a condition, e.g. Weapon SMAW,RPG-7

        // BF4 only trigger OnRoundOver when ALL 3 events have completed (OnRoundOver/Players/TeamScores)
        Int32 round_over_event_count = 0; // zeroed in OnLevelLoaded, incr in RoundOver events

        // object to hold runtime rulz variables
        VarsClass rulz_vars = null;

        ParsedRule rule_prefix; // used in parse_rules() as default first parts if rule has no trigger
                                // at start of parse initialized to TriggerEnum.Spawn

        #endregion

        #region Populate dictionary of %% substitution keys and startup default rules

        public ProconRulz()
        {
            subst_keys.Add(SubstEnum.Player, new List<String>());
            subst_keys[SubstEnum.Player].Add("%p%");
            subst_keys[SubstEnum.Player].Add("$p$");
            subst_keys.Add(SubstEnum.Victim, new List<String>());
            subst_keys[SubstEnum.Victim].Add("%v%");
            subst_keys.Add(SubstEnum.Weapon, new List<String>());
            subst_keys[SubstEnum.Weapon].Add("%w%");
            subst_keys.Add(SubstEnum.WeaponKey, new List<String>());
            subst_keys[SubstEnum.WeaponKey].Add("%wk%");
            subst_keys.Add(SubstEnum.Damage, new List<String>());
            subst_keys[SubstEnum.Damage].Add("%d%");
            subst_keys.Add(SubstEnum.DamageKey, new List<String>());
            subst_keys[SubstEnum.DamageKey].Add("%dk%");
            subst_keys.Add(SubstEnum.Kit, new List<String>());
            subst_keys[SubstEnum.Kit].Add("%k%");
            subst_keys.Add(SubstEnum.VictimKit, new List<String>());
            subst_keys[SubstEnum.VictimKit].Add("%vk%");
            subst_keys.Add(SubstEnum.KitKey, new List<String>());
            subst_keys[SubstEnum.KitKey].Add("%kk%");
            subst_keys.Add(SubstEnum.Spec, new List<String>()); // available On Spawn only
            subst_keys[SubstEnum.Spec].Add("%spec%"); // available On Spawn only
            subst_keys.Add(SubstEnum.SpecKey, new List<String>()); // available On Spawn only
            subst_keys[SubstEnum.SpecKey].Add("%speck%"); // available On Spawn only
            subst_keys.Add(SubstEnum.Count, new List<String>());
            subst_keys[SubstEnum.Count].Add("%c%");
            subst_keys.Add(SubstEnum.TeamCount, new List<String>());
            subst_keys[SubstEnum.TeamCount].Add("%tc%");
            subst_keys.Add(SubstEnum.ServerCount, new List<String>());
            subst_keys[SubstEnum.ServerCount].Add("%sc%");
            subst_keys.Add(SubstEnum.PlayerTeam, new List<String>());
            subst_keys[SubstEnum.PlayerTeam].Add("%pt%");
            subst_keys.Add(SubstEnum.PlayerSquad, new List<String>());
            subst_keys[SubstEnum.PlayerSquad].Add("%ps%");
            subst_keys.Add(SubstEnum.PlayerTeamKey, new List<String>());
            subst_keys[SubstEnum.PlayerTeamKey].Add("%ptk%");
            subst_keys.Add(SubstEnum.VictimTeamKey, new List<String>());
            subst_keys[SubstEnum.VictimTeamKey].Add("%vtk%");
            subst_keys.Add(SubstEnum.PlayerSquadKey, new List<String>());
            subst_keys[SubstEnum.PlayerSquadKey].Add("%psk%");
            subst_keys.Add(SubstEnum.VictimTeam, new List<String>());
            subst_keys[SubstEnum.VictimTeam].Add("%vt%");
            subst_keys.Add(SubstEnum.Range, new List<String>());
            subst_keys[SubstEnum.Range].Add("%r%");
            subst_keys.Add(SubstEnum.BlockedItem, new List<String>());
            subst_keys[SubstEnum.BlockedItem].Add("%b%");
            subst_keys.Add(SubstEnum.Teamsize, new List<String>());
            subst_keys[SubstEnum.Teamsize].Add("%n%");
            subst_keys.Add(SubstEnum.Teamsize1, new List<String>());
            subst_keys[SubstEnum.Teamsize1].Add("%ts1%");
            subst_keys.Add(SubstEnum.Teamsize2, new List<String>());
            subst_keys[SubstEnum.Teamsize2].Add("%ts2%");
            subst_keys.Add(SubstEnum.PlayerTeamsize, new List<String>());
            subst_keys[SubstEnum.PlayerTeamsize].Add("%pts%");
            subst_keys.Add(SubstEnum.Headshot, new List<String>());
            subst_keys[SubstEnum.Headshot].Add("%h%");
            subst_keys.Add(SubstEnum.Map, new List<String>());
            subst_keys[SubstEnum.Map].Add("%m%");
            subst_keys.Add(SubstEnum.MapMode, new List<String>());
            subst_keys[SubstEnum.MapMode].Add("%mm%");
            subst_keys.Add(SubstEnum.Target, new List<String>()); // e.g. playername from say text
            subst_keys[SubstEnum.Target].Add("%t%"); // e.g. playername from say text
            subst_keys.Add(SubstEnum.Text, new List<String>()); // e.g. say text
            subst_keys[SubstEnum.Text].Add("%text%"); // e.g. say text
            subst_keys.Add(SubstEnum.TargetText, new List<String>()); // used only by TargetPlayer
            subst_keys[SubstEnum.TargetText].Add("%targettext%"); // used only by TargetPlayer
            subst_keys.Add(SubstEnum.Ping, new List<String>()); // ping in milliseconds (i.e as displayed)
            subst_keys[SubstEnum.Ping].Add("%ping%"); // ping in milliseconds (i.e as displayed)
            subst_keys.Add(SubstEnum.EA_GUID, new List<String>()); // from admin.listPlayers
            subst_keys[SubstEnum.EA_GUID].Add("%ea_guid%"); // from admin.listPlayers
            subst_keys.Add(SubstEnum.PB_GUID, new List<String>()); // from OnPunkbusterPlayerInfo
            subst_keys[SubstEnum.PB_GUID].Add("%pb_guid%"); // from OnPunkbusterPlayerInfo
            subst_keys.Add(SubstEnum.IP, new List<String>()); // IP address
            subst_keys[SubstEnum.IP].Add("%ip%"); // IP address
            subst_keys.Add(SubstEnum.PlayerCountry, new List<String>()); // from pb info
            subst_keys[SubstEnum.PlayerCountry].Add("%pcountry%"); // from pb info
            subst_keys.Add(SubstEnum.PlayerCountryKey, new List<String>()); // from pb info
            subst_keys[SubstEnum.PlayerCountryKey].Add("%pcountrykey%"); // from pb info
            subst_keys.Add(SubstEnum.VictimCountry, new List<String>()); // from pb info
            subst_keys[SubstEnum.VictimCountry].Add("%vcountry%"); // from pb info
            subst_keys.Add(SubstEnum.VictimCountryKey, new List<String>()); // from pb info
            subst_keys[SubstEnum.VictimCountryKey].Add("%vcountrykey%"); // from pb info

            subst_keys.Add(SubstEnum.Hhmmss, new List<String>()); // time HH:MM:SS
            subst_keys[SubstEnum.Hhmmss].Add("%hms%");
            subst_keys.Add(SubstEnum.Seconds, new List<String>()); // time seconds
            subst_keys[SubstEnum.Seconds].Add("%seconds%");
            subst_keys.Add(SubstEnum.Date, new List<String>()); // time seconds
            subst_keys[SubstEnum.Date].Add("%ymd%");

            // default rulz
            unparsed_rules.Add("#             JOINER/LEAVER LOG");
            unparsed_rules.Add("On Join;Say ^2%p%^0 has joined the server");
            unparsed_rules.Add("On Leave;Say ^2%p%^0 has left the server");

            rulz_filenames.Add("proconrulz_rules.txt");

        }

        #endregion

        #region Plugin startup routines and On Init procedure
        //**********************************************************************************************
        //**********************************************************************************************
        //   PROCON STARTUP ROUTINES
        //**********************************************************************************************
        //**********************************************************************************************

        public String GetPluginName() { return "ProconRulz"; }

        public String GetPluginVersion() { return version; }

        public String GetPluginAuthor() { return auth_name; }

        public String GetPluginWebsite() { return ""; }

        public String GetPluginDescription() { return get_details(); }

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion)
        {
            rulz_vars = new VarsClass(String.Format("{0}_{1}", strHostName, strPort));
            WriteConsole("ProconRulz loaded");
            try
            {
                weaponDefines = GetWeaponDefines();
            }
            catch
            {
                WriteConsole("ProconRulz: ^1failed to load weapon definitions");
            }
            try
            {
                specDefines = GetSpecializationDefines();
            }
            catch
            {
                WriteConsole("ProconRulz: ^1failed to load spec definitions");
            }

            WriteConsole(String.Format("weaponDefines size = {0}, specDefines size = {1}",
                                            weaponDefines.Count,
                                            specDefines.Count));

            this.RegisterEvents(this.GetType().Name,
                                                        "OnPlayerSpawned",
                                                        "OnPlayerKilled",
                                                        "OnPlayerTeamChange",
                                                        "OnPlayerSquadChange",
                                                        "OnPlayerJoin",
                                                        "OnPlayerLeft",
                                                        "OnListPlayers",
                                                        "OnReservedSlotsList",
                                                        "OnRoundOver",
                                                        "OnRoundOverPlayers",
                                                        "OnRoundOverTeamScores",
                                                        "OnLoadingLevel", // for BFBC2
                                                        "OnLevelLoaded",  // for BF3
                                                        "OnCurrentLevel", // BFBC2 only
                                                        "OnGlobalChat",
                                                        "OnTeamChat",
                                                        "OnSquadChat",
                                                        "OnServerInfo",
                                                        "OnPunkbusterPlayerInfo"
                                                        );

            // exec listPlayers to initialise players global
            ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
        }

        public void OnPluginEnable()
        {
            plugin_enabled = true;
            this.WriteConsole(String.Format("^bProconRulz: ^2plugin enabled, version {0}", version));
            WriteDebugInfo(String.Format("weaponDefines size = {0}, specDefines size = {1}",
                                            weaponDefines.Count,
                                            specDefines.Count));
            rulz_enable = true;
            load_rulz_from_files();
            reset_counts(); // reset the list of 'watched' items and their counts
            // reset rulz runtime vars
            rulz_vars.reset();

            // search for user rulz file (Plugins/<gameid>/*rulz.txt)
            //load_rulz_from_files(game_id);

            parse_rules();
            load_reserved_slot_players();
            // exec currentLevel to initialise current_map global, so we don't have to wait for a map load
            // ** NOT working in BF3 R8
            ExecuteCommand("procon.protected.send", "admin.currentLevel");
        }

        public void OnPluginDisable()
        {
            plugin_enabled = false;
            WriteConsole("^bProconRulz: ^1plugin disabled");

            // the rest of this proc is just to output some debug info when user selects "disable plugin"
            WriteDebugInfo("ProconRulz: These rules were loaded from settings:");
            foreach (ParsedRule rule in parsed_rules)
            {
                print_parsed_rule(rule);
            } // end looping through parsed_rules
            WriteDebugInfo(String.Format("ProconRulz: These were the 'watched' items in the rules:"));
            WriteDebugInfo(String.Format("ProconRulz: {0}",
                string.Join(", ", spawn_counts.list_items().ToArray())));

        }

        // proconRulz On Init (on plugin load, enable, round start)
        public void OnInit()
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: ********************OnInit******************************");

                scan_rules(TriggerEnum.Init, null,
                    new Dictionary<SubstEnum, String>(), null, null, null);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnInit");
                PrintException(ex);
            }
        }

        #endregion

        #region Utility functions to test player Admin/Admins/Protected

        //**********************************************************************************************
        //*********************** Keep track of admins on the server  **********************************
        //**********************************************************************************************

        // admins_list is a list of playernames og logged-on admins
        List<String> admins_list = new List<String>();


        // return true if reserved list is being used AND player is on reserved list
        // or player or clan is explicitly listed in whitelist or player is admin
        private Boolean protected_player(String name)
        {
            // see if PLAYER NAME is on whitelist
            if (whitelist_players.Contains(name)) return true;
            // see if CLAN is on whitelist
            if (whitelist_clans.Contains(players.clan(name))) return true;
            // if we're not checking admins/reserved slots, then return false now
            if (protect_players == ProtectEnum.Neither) return false;
            // see if name is on ReservedSlots list
            if (protect_players == ProtectEnum.Admins_and_Reserved_Slots && reserved_slot_players.Contains(name))
                return true;
            // last shot - is this player an admin ?
            if (is_admin(name)) return true;
            // nope? return false then
            return false;
        }

        //**********************************************************************************************
        //*********************** Ask Procon if player is an admin  ************************************

        // do the procon api call to see if player_name has any procon admin rights
        Boolean procon_admin(String player_name)
        {
            CPrivileges p = this.GetAccountPrivileges(player_name);
            try
            {
                if (p.CanKillPlayers) return true;
            }
            catch { }
            // debug - have ProconRulz always accept [OFc] bambam
            return player_name == "PRDebug" || player_name == auth_name;
        }

        public void admins_add(String player_name)
        {
            // if player_name already on list the nothing needs to be done so just return immediately
            if (admins_list.Contains(player_name)) return;
            // otherwise check the procon admin status and add to list if necessary
            if (procon_admin(player_name)) admins_list.Add(player_name);
            return;
        }

        public void admins_remove(String player_name)
        {
            admins_list.Remove(player_name);
        }

        public Boolean is_admin(String player_name)
        {
            return player_name == "PRDebug" || admins_list.Contains(player_name);
        }

        public void admins_reset()
        {
            admins_list.Clear();
        }

        public Boolean admins_present()
        {
            return admins_list.Count > 0;
        }

        #endregion

        #region Misc utility functions (convert item key to item description, team names etc)

        //**********************************************************************************************
        //**********************************************************************************************
        //   UTILITY PROCEDURES
        //**********************************************************************************************
        //**********************************************************************************************

        // a bit of funky c# overloading to convert item to string key
        String item_key(Kits k)
        {
            if (k == null || k == Kits.None) return "No kit key";
            try
            {
                return Enum.GetName(typeof(Kits), k);
            }
            catch { }
            return "No kit key";
        }

        String item_key(Weapon w)
        {
            if (w == null) return "No weapon key";
            try
            {
                return w.Name;
            }
            catch { }
            return "No weapon key";
        }

        String item_key(Specialization s)
        {
            if (s == null) return "No spec key";
            try
            {
                return s.Name;
            }
            catch { }
            return "No spec key";
        }

        String item_key(DamageTypes d)
        {
            if (d == null) return "No damage key";
            try
            {
                return Enum.GetName(typeof(DamageTypes), d);
            }
            catch { }
            return "No damage key";
        }

        // this func will return a list of strings where input was key1|key2|key3..
        // and replace '&' chars with ' ', so "M15&AT&MINE" becomes "M15 AT MINE"
        List<String> item_keys(String keys_in)
        {
            List<String> key_list = new List<String>();
            if (keys_in != null)
                try
                {
                    String[] key_strings = keys_split(keys_in);
                    foreach (String k in key_strings)
                        key_list.Add(k.Replace(rulz_key_separator, ' '));
                    return key_list;
                }
                catch { }
            //key_list.Add("No key"); 
            return key_list;
        }

        // split a x|y|z string into an array
        String[] keys_split(String keys)
        {
            if (keys == null) return null;
            return keys.Split(new Char[] { rulz_item_separator }, StringSplitOptions.RemoveEmptyEntries);
        }

        // split a x|y|z string into an array
        static String keys_join(List<String> keys)
        {
            if (keys == null) return null;
            return String.Join(rulz_item_separator.ToString(), keys.ToArray());
        }

        String keys_join(String[] keys)
        {
            if (keys == null) return null;
            return String.Join(rulz_item_separator.ToString(), keys);
        }

        // return true if exact key found in keys
        Boolean keys_match(String key, List<String> keys)
        {
            if (keys == null) return false;
            foreach (String k in keys) if (k.ToLower() == key.ToLower()) return true;
            return false;
        }

        // convert a key to 'rulz' format including '&' chars replacing spaces
        String rulz_key(String k)
        {
            if (k == null) return "No_key";
            try
            {
                return k.Replace(' ', rulz_key_separator);
            }
            catch { }
            return "No_key";
        }

        // convert an item key to its description (e.g. m95 -> "M95 Sniper Rifle")
        String kit_desc(String key)
        {
            if (key == null || key == "" || key == "No kit key" || key == "None") return "No kit";
            try
            {
                return this.GetLocalized(key, String.Format("global.Kits.{0}", key));
            }
            catch { }
            return key + "(Kit has no Procon name)";
        }

        String weapon_desc(String key)
        {
            if (key == null || key == "" || key == "None" || key == "No weapon key") return "No weapon";
            try
            {
                return this.GetLocalized(key, String.Format("global.Weapons.{0}", key.ToLower()));
            }
            catch { }
            return key + "(Weapon has no Procon name)";
        }

        String damage_desc(DamageTypes damage)
        {
            if (damage == null) return "No damage";
            try
            {
                return Enum.GetName(typeof(DamageTypes), damage);
            }
            catch { }
            return "No damage";
        }

        String spec_desc(Specialization s)
        {
            if (s == null) return "No spec";
            try
            {
                return this.GetLocalized(s.Name, String.Format("global.Specialization.{0}",
                    s.Name.ToLower()));
            }
            catch { }
            return "No spec";
        }

        // apply the %..% substitution vars to message (e.g. "hello %p%" becomes "hello bambam")
        static String replace_keys(String message, Dictionary<SubstEnum, String> keywords)
        {
            if (message == null) return null;
            if (keywords == null) return message;
            foreach (SubstEnum keyval in keywords.Keys)
            {
                foreach (String k in subst_keys[keyval])
                {
                    message = message.Replace(k, keywords[keyval]);
                }

            }
            return message;
        }

        // return the 'localization key' for the team with this ID
        private String team_key(String team_id)
        {
            try
            {
                if (current_map == null) return "No team key";

                foreach (CTeamName team in current_map.TeamNames)
                {
                    if (team.TeamID == Int32.Parse(team_id))
                        return team.LocalizationKey;
                }
            }
            catch { }
            return "No team key";
        }

        // return the 'localization key' for the squad with this ID
        private String squad_key(String squad_id)
        {
            try
            {
                Int32 id = Int32.Parse(squad_id);
                if (id < 0) return "No squad key";
            }
            catch
            {
                return "No squad key";
            }
            return "global.Squad" + squad_id;
        }

        // convert an int 'team_id' into display name for team - varies by map
        private String team_name(String team_id)
        {
            String name = String.Format("[Map unknown](team:{0})", team_id);
            if (current_map == null) return name;

            try
            {
                String team_localization_key = team_key(team_id);
                if (team_localization_key == null)
                    name = String.Format("[Map OK team key unknown](team:{0})", team_id);
                else
                    name = this.GetLocalized(team_localization_key, team_localization_key);
            }
            catch { }
            return name;
        }

        // convert an int 'team_id' into display name for team - varies by map
        private String squad_name(String squad_id)
        {
            try
            {
                String localization_key = squad_key(squad_id);
                return this.GetLocalized(localization_key, localization_key);
            }
            catch { return "No squad name"; }
        }

        // test whether current map localization key (text) has any condition team as substring, or teamid
        private Boolean team_match(List<String> condition_teams, String team_id)
        {
            String key = team_key(team_id);
            try
            {
                foreach (String t in condition_teams)
                {
                    try
                    {
                        if (t == team_id) return true;
                    }
                    catch
                    {
                        if (key.ToLower().IndexOf(t.ToLower()) >= 0) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        // test whether current map matches any in condition
        private Boolean map_match(List<String> condition_maps)
        {
            if (current_map == null) return false;
            foreach (String m in condition_maps)
                if ((current_map.PublicLevelName.ToLower().IndexOf(m.ToLower()) != -1) ||
                                 (current_map.FileName.ToLower().IndexOf(m.ToLower()) != -1)) return true;
            return false;
        }

        // test whether current map matches any in condition
        private Boolean mapmode_match(List<String> condition_modes)
        {
            if (current_map == null) return false;
            foreach (String m in condition_modes)
                if (current_map_mode.ToLower().IndexOf(m.ToLower()) != -1) return true;
            return false;
        }

        private void load_reserved_slot_players()
        {
            WriteDebugInfo(String.Format("ProconRulz: loading protected players list"));
            ExecuteCommand("procon.protected.send", "reservedSlots.list");
        }

        private String strip_braces(String s)
        {
            return s.Replace("{", "~(").Replace("}", ")~");
        }

        private List<String> find_players(String partname)
        {
            List<String> player_names = new List<String>();
            // debug
            if (partname == "PRDebug") player_names.Add("PRDebug");

            foreach (String player_name in players.list_players())
            {
                if (player_name.ToLower().IndexOf(partname.ToLower()) != -1)
                {
                    WriteDebugInfo(String.Format("ProconRulz:       find_player with {0} found {1}",
                        partname, player_name));
                    player_names.Add(player_name);
                }
            }
            WriteDebugInfo(String.Format("ProconRulz:       find_player with {0} matches", player_names.Count));
            return player_names;
        }

        // split a string into elements separated by spaces, binding quoted strings into one element
        // e.g. Exec vars.serverName "OFc Server - no nubs" will be parsed to [vars.serverName,"OFc Server - no nubs"]
        private String[] quoted_split(String str)
        {
            String quoted_str = null; // var to accumulate full String, quoted or not
            Char? quote_char = null; // ? makes Char accept nulls --  null or opennig quote Char of current quoted String
                                     // quote_char != null used as flag to confirm we are mid-quoted-string

            List<String> result = new List<String>();

            if (str == null) return result.ToArray();

            foreach (String s in str.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (s.StartsWith("\"") || s.StartsWith("\'")) // start of quoted String - allow matching " or'
                {
                    quote_char = s[0];
                }
                if (quote_char == null) // NOT in quoted-String so just add this element to list
                {
                    result.Add(s);
                }
                else //we're in a quoted String so accumulate
                {
                    if (quoted_str == null)
                    {
                        quoted_str = s; // no accumulated quoted String so far so start with s
                    }
                    else
                    {
                        quoted_str += " " + s; // append s to accumulated quoted_str
                    }
                }
                // check if we just ended a quoted string
                if (quote_char != null && s.EndsWith(quote_char.ToString())) // end of quoted String
                {
                    result.Add(quoted_str.Substring(1).Substring(0, quoted_str.Length - 2));
                    quoted_str = null;
                    quote_char = null; // quoted_str is now complete
                }
            }
            // check to see if we've ended with an incomplete quoted string... if so add it
            if (quote_char != null && quoted_str != null)
            {
                result.Add(quoted_str);
            }
            return result.ToArray();
        }

        #endregion

        #region Processing of 'On Say' events to display 'rulz' message or scan rules

        private void say_rulz(String player_name, String msg)
        {
            WriteDebugInfo("****************say_rulz******************************" + player_name);
            if (msg.Contains("prdebug"))
            {
                prdebug(msg);
                return;
            }
            WriteDebugInfo(String.Format("ProconRulz: say_rulz({0},{1})", player_name, msg));
            if (player_name != "Server")             // scan for any "On Say" rulz
                scan_rules(TriggerEnum.Say, player_name,
                    new Dictionary<SubstEnum, String>(), null, null, msg);

            WriteDebugInfo("say_rulz(" + player_name + "," + msg + ") - testing protected player");
            if (is_admin(player_name))
                if (msg.ToLower().IndexOf("rulz on") != -1)
                {
                    WriteDebugInfo("say_rulz(" + player_name + "," + msg + ") - rulz on");
                    rulz_enable = true;
                    ExecuteCommand("procon.protected.send",
                        "admin.say", "Rulz enabled", "all");
                    return;
                }
                else if (msg.ToLower().IndexOf("rulz off") != -1)
                {
                    rulz_enable = false;
                    ExecuteCommand("procon.protected.send",
                        "admin.say", "Rulz disabled until next round", "player", player_name);
                    return;
                }
                // *** DEBUG rcon tests ***
                // test #1 - KICK (all servers)
                else if (msg.ToLower().IndexOf("xyzzy") == 0)
                {
                    // IMPORTANT DEBUG - limit these tests to ADMIN only
                    // this test should work on all game servers
                    if (!is_admin(player_name)) return; // safety in case top check removed
                    List<String> player_names = find_players(msg.Substring(6));
                    if (player_names.Count != 1) return;
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", player_names[0], "Player disconnected");
                }
                // test #2 - MOVE PLAYER (BFBC2-BF4)
                else if (msg.ToLower().IndexOf("xyzzm") == 0)
                {
                    // IMPORTANT DEBUG- limit these tests to ADMIN only
                    // this test for BF4 only
                    if (!is_admin(player_name)) return; // safety in case top check removed
                    List<String> player_names = find_players(msg.Substring(6));
                    if (player_names.Count != 1) return;
                    String team_id = players.team_id(player_names[0]);
                    if (team_id == "-1") return;
                    if (team_id == "0") return;
                    if (team_id == "1") team_id = "2";
                    else if (team_id == "2") team_id = "1";
                    else if (team_id == "3") team_id = "4";
                    else if (team_id == "4") team_id = "3";
                    else return;
                    ExecuteCommand("procon.protected.send", "admin.movePlayer", player_names[0], team_id, "0", "true");
                }
                // test #3 - SQUAD LEADER (all games)
                else if (msg.ToLower().IndexOf("xyzzl") == 0)
                {
                    // IMPORTANT DEBUG- limit these tests to ADMIN only
                    // this test for BF4 only
                    if (!is_admin(player_name)) return; // safety in case top check removed
                    List<String> player_names = find_players(msg.Substring(6));
                    if (player_names.Count != 1) return;
                    String team_id = players.team_id(player_names[0]);
                    String squad_id = players.squad_id(player_names[0]);
                    if (team_id == "-1" || squad_id == "-1") return;
                    ExecuteCommand("procon.protected.send", "squad.leader", team_id, squad_id, player_names[0]);
                }
                // any admin player can type "prv" and see ProconRulz version
                else if (msg.ToLower().IndexOf("prversion") == 0)
                {
                    ExecuteCommand("procon.protected.send",
                        "admin.say", "ProconRulz version " + version.ToString(), "player", player_name);
                }
            //WriteDebugInfo(String.Format("ProconRulz: testing Rulz for [{0}] who said \"{1}\"",player_name, msg));
        }

        #endregion

        #region debug commands (via say text 'prdebug' commands)

        private void prdebug(String say_text)
        {
            if (say_text.IndexOf("players") >= 0)
            {
                WriteConsole("ProconRulz: ***************Debug command players*********************");
                WriteConsole(String.Format("ProconRulz: players in new player cache = {0}",
                                                String.Join(",", players.list_new_players().ToArray())
                                                ));

                foreach (String team_id in players.list_team_ids())
                {
                    WriteConsole(String.Format("ProconRulz: players (team {0}:{1}) = {2}",
                                                team_id,
                                                team_name(team_id),
                                                String.Join(",", players.list_players(team_id).ToArray())
                                                ));
                }
                return;
            }

            if (say_text.IndexOf("count") >= 0)
            {
                WriteConsole("ProconRulz: ****************Debug command counts**********************");
                prdebug("players");

                prdebug("teamsize");

                prdebug("watched");

                return;
            }

            if (say_text.IndexOf("watched") >= 0)
            {
                WriteConsole("ProconRulz: ********************Debug command watched******************");

                List<String> watched_items = spawn_counts.list_items();

                WriteConsole(String.Format("ProconRulz: Watched items are: {0}",
                    string.Join(", ", spawn_counts.list_items().ToArray())));

                List<String> debug_list = new List<String>();

                foreach (String team_id in players.list_team_ids())
                {
                    foreach (String item_name in watched_items)
                    {
                        List<String> item_list = new List<String>();
                        item_list.Add(item_name);
                        debug_list.Add(String.Format("{0}({1}:{2})",
                                            item_name,
                                            spawn_counts.count(item_list, team_id),
                                            String.Join(",", spawn_counts.list_players(item_name, team_id).ToArray())
                                        ));
                    }

                    WriteConsole(String.Format("ProconRulz: spawn_counts (team {0}:{1}) = {2}",
                        team_id, team_name(team_id), String.Join(" ", debug_list.ToArray())));
                }
                return;
            }

            if (say_text.IndexOf("teamsize") >= 0)
            {
                WriteConsole("ProconRulz: ********************Debug command teamsize******************");

                WriteConsole(String.Format("ProconRulz: min teamsize {0}", players.min_teamsize()));
                foreach (String team_id in players.list_team_ids())
                {
                    WriteConsole(String.Format("ProconRulz: players (team {0}:{1}) = {2} players: {3}",
                                                team_id,
                                                team_name(team_id),
                                                players.teamsize(team_id),
                                                String.Join(",", players.list_players(team_id).ToArray())
                                                ));
                }
                return;
            }

            Int32 xsay_pos = say_text.IndexOf("xsay");
            if (xsay_pos >= 0)
            {
                say_rulz("PRDebug", say_text.Substring(xsay_pos + 5));
                return;
            }

            if (say_text.IndexOf("dump") >= 0)
            {
                WriteConsole(String.Format("ProconRulz: Listing the rulz_vars:"));
                Dictionary<String, String> vars = rulz_vars.dump();
                foreach (String var_name in vars.Keys)
                {
                    WriteConsole(String.Format("ProconRulz: rulz_vars[{0}] = \"{1}\"",
                                                var_name,
                                                vars[var_name]
                                                ));
                }
                WriteConsole(String.Format("ProconRulz: Listing complete"));
                return;
            }

            WriteConsole("ProconRulz: Debug command not valid: \"" + say_text + "\"");
        }
        #endregion

        #region Print rulz

        // this whole procedure is just for debugging purposes
        // it prints the rule out to the Procon console
        private void print_parsed_rule(ParsedRule rule)
        {
            if (rule.comment)
            {
                WriteDebugInfo(String.Format("ProconRulz: Rule {0}: {1}", rule.id, rule.unparsed_rule));
                return;
            }

            // read each rule variable, convert to string, and then print formatted
            String trigger_string =
                String.Format("On {0}:", Enum.GetName(typeof(TriggerEnum), rule.trigger));

            String parts_string = (rule.parts == null || rule.parts.Count == 0) ? " CONTINUE;" : " ";

            foreach (PartClass p in rule.parts)
            {
                parts_string += p.ToString();
            }

            WriteDebugInfo(String.Format("ProconRulz: Rule {0}: {1}{2}",
                rule.id, trigger_string, parts_string));
        }

        #endregion

        #region Display plugin details

        // get_details() returns the "Details" HTML description to display in Procon
        private String get_details()
        {
            String[] kit_keys = Enum.GetNames(typeof(Kits));
            String[] kit_lines = new String[kit_keys.Length + 3];
            Int32 wi = 2;
            kit_lines[0] = "<table>";
            kit_lines[1] = "<tr><th>Description</th><th>Kit key</th></tr>";

            String kit_descr;
            while (wi < kit_keys.Length + 2)
            {
                kit_descr = kit_desc(kit_keys[wi - 2]);
                kit_lines[wi] = String.Format("<tr><td><b>{0}</b></td><td>{1}</td></tr>",
                                            kit_descr,
                                            kit_keys[wi - 2]
                                            );
                wi++;
            }
            kit_lines[wi] = "</table>";
            String kits_string = string.Join(" ", kit_lines);

            String[] weapons = new String[weaponDefines.Count + 3];
            weapons[0] = "<table>";
            weapons[1] = "<tr><th>Description</th><th>Weapon key</th><th>Damage</th><th>Kit</th></tr>";
            wi = 2;
            String wdesc, wname, wdamage, wkit;
            while (wi < weaponDefines.Count + 2) //weapons[wi++] = "<tr><td>xxx</td><td>yyy</td><td>zzz</td></tr>";
            {
                wdesc = this.GetLocalized(weaponDefines[wi - 2].Name, String.Format("global.Weapons.{0}", weaponDefines[wi - 2].Name.ToLower()));
                wname = rulz_key(weaponDefines[wi - 2].Name);
                wdamage = Enum.GetName(typeof(DamageTypes), weaponDefines[wi - 2].Damage);
                wkit = Enum.GetName(typeof(Kits), weaponDefines[wi - 2].KitRestriction);
                weapons[wi++] = String.Format("<tr><td><b>{0}</b></td><td>{1}</td><td>{2}</td><td>{3}</td></tr>",
                                             wdesc, //this.GetLocalized(weaponDefines[wi-2].Name,String.Format("global.Weapons.{0}",weaponDefines[wi-2].Name.ToLower())),
                                             wname, //weaponDefines[wi-2].Name,
                                             wdamage, //Enum.GetName(typeof(DamageTypes), weaponDefines[wi-2].Damage)
                                             wkit
                                             );
            }
            weapons[wi] = "</table>";
            String weapon_string = string.Join(" ", weapons);

            String[] specs = new String[specDefines.Count + 3];
            specs[0] = "<table>";
            specs[1] = "<tr><th>Description</th><th>Specialization key</th></tr>";
            wi = 2;
            String sdesc, sname;
            while (wi < specDefines.Count + 2)
            {
                sdesc = this.GetLocalized(specDefines[wi - 2].Name, String.Format("global.Specialization.{0}", specDefines[wi - 2].Name.ToLower()));
                sname = item_key(specDefines[wi - 2]);
                specs[wi++] = String.Format("<tr><td><b>{0}</b></td><td>{1}</td></tr>",
                                            sdesc,
                                            sname
                                            );
            }
            specs[wi] = "</table>";
            String spec_string = string.Join("", specs);

            String desc = String.Format(@"<h2>ProconRulz Procon plugin</h2>
                <p>Please see <a href=""http://www.forsterlewis.com/proconrulz.pdf"">the ONLINE documentation</a>
                (RIGHT-CLICK and select Open in New Window...)
                for a fuller explanation of how to use ProconRulz.</p>

                <p>You can 'right-click' and select 'Print...' to print this page.</p>

                <p>Apply admin commands (e.g. Kill, Kick, Say) to players<br/>
                according to certain 'conditions' (e.g. spawned with Kit Recon)<br/>
                Allows programming of weapon or kit limits, with suitable messages.</p>

                <p><b>actions</b> include kick, ban, or just a warning (yell, say).</p>

                <p><b>conditions</b> include kit type, weapon type, and can be applied at 
                    Spawn time or on a Kill.</p>
                <p>Each rule has three parts:</p>
                <ol>
                    <li><b>Trigger</b> - i.e. when the rule should fire, On Spawn, On Kill, On Teamkill etc</li>
                    <li><b>Conditions</b> - list of tests to apply before actions are done, e.g. Headshot, Kit Recon etc</li>
                    <li><b>Actions</b> - list of admin actions to take if all conditions succeed, e.g. Kill, Kick, Say</li>
                </ol>
                        <h2>List of all weapons, kits and specializations</h2>
                        <h3>Kits</h3>{0}{4}{0}
                        <h3>Weapons</h3>{0}{1}{0}
                        <h3>Damage</h3>{0}{2}<br/><br/>{0}
                        <h3>Specializations</h3>{0}{3}{0}
                        ",
                        Environment.NewLine,
                        weapon_string,
                        string.Join(", ", Enum.GetNames(typeof(DamageTypes))),
                        spec_string,
                        kits_string
                        );
            return desc;
        }

        #endregion

        #region Console, Chat window output routines

        public String CreateEnumString(String Name, String[] valueList)
        {
            return string.Format("enum.{0}_{1}({2})", GetType().Name, Name, string.Join("|", valueList));
        }
        public String CreateEnumString(Type enumeration)
        {
            return CreateEnumString(enumeration.Name, Enum.GetNames(enumeration));
        }

        public void PrintException(Exception ex)
        {
            WriteConsole("ProconRulz: " + ex.ToString());
        }

        public void WriteDebugInfo(String message)
        {
            if (trace_rules == enumBoolYesNo.Yes)
                ExecuteCommand("procon.protected.pluginconsole.write", strip_braces(message));
        }

        public void WriteLog(String message)
        {
            //ExecuteCommand("procon.protected.pluginconsole.write", message);
            String m = strip_braces(message);
            switch (log_file)
            {
                case LogFileEnum.PluginConsole:
                    ExecuteCommand("procon.protected.pluginconsole.write", m);
                    break;
                case LogFileEnum.Console:
                    ExecuteCommand("procon.protected.console.write", m);
                    break;
                case LogFileEnum.Chat:
                    ExecuteCommand("procon.protected.chat.write", m);
                    break;
                case LogFileEnum.Events:
                    ExecuteCommand("procon.protected.events.write", m);
                    break;
                //case LogFileEnum.Discard_Log_Messages:
                default:
                    break;
            }
            if (message.IndexOf("prdebug") >= 0) prdebug(message);
        }

        public void WriteConsole(String message)
        {
            //ExecuteCommand("procon.protected.pluginconsole.write", message);
            ExecuteCommand("procon.protected.pluginconsole.write", strip_braces(message));
        }

        #endregion

        #region Commented Out listing of other Procon callbacks

        // updated for BF3

        #region Global/Login
        /*
        public virtual void OnLogin() { }
        public virtual void OnLogout() { }
        public virtual void OnQuit() { }
        public virtual void OnVersion(String serverType, String version) { }
        public virtual void OnHelp(List<String> commands) { }

        public virtual void OnRunScript(String scriptFileName) { }
        public virtual void OnRunScriptError(String scriptFileName, Int32 lineError, String errorDescription) { }

        public virtual void OnServerInfo(CServerInfo serverInfo) { }
        public virtual void OnResponseError(List<String> requestWords, String error) { }

        public virtual void OnYelling(String message, Int32 messageDuration, CPlayerSubset subset) { }
        public virtual void OnSaying(String message, CPlayerSubset subset) { }
        */
        #endregion

        #region Map Functions
        /*
        public virtual void OnRestartLevel() { }
        public virtual void OnSupportedMaps(String playlist, List<String> lstSupportedMaps) { }
        public virtual void OnListPlaylists(List<String> playlists) { }

        public virtual void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
        }

        public virtual void OnEndRound(Int32 iWinningTeamID) { }
        public virtual void OnRunNextLevel() { }
        public virtual void OnCurrentLevel(String mapFileName) { }
        */
        #region BFBC2
        /*
        public virtual void OnPlaylistSet(String playlist) { }
        */
        #endregion

        #endregion

        #region Banlist
        /*
        public virtual void OnBanAdded(CBanInfo ban) { }
        public virtual void OnBanRemoved(CBanInfo ban) { }
        public virtual void OnBanListClear() { }
        public virtual void OnBanListSave() { }
        public virtual void OnBanListLoad() { }
        public virtual void OnBanList(List<CBanInfo> banList) { }
        */
        #endregion

        #region Text Chat Moderation
        /// BFBC2 & MoH

        /*
        public virtual void OnTextChatModerationAddPlayer(TextChatModerationEntry playerEntry) { }
        public virtual void OnTextChatModerationRemovePlayer(TextChatModerationEntry playerEntry) { }
        public virtual void OnTextChatModerationClear() { }
        public virtual void OnTextChatModerationSave() { }
        public virtual void OnTextChatModerationLoad() { }
        public virtual void OnTextChatModerationList(TextChatModerationDictionary moderationList) { }
        */
        #endregion

        #region Maplist
        /*
        public virtual void OnMaplistConfigFile(String configFileName) { }
        public virtual void OnMaplistLoad() { }
        public virtual void OnMaplistSave() { }

        public virtual void OnMaplistList(List<MaplistEntry> lstMaplist) { }
        public virtual void OnMaplistCleared() { }
        public virtual void OnMaplistMapAppended(String mapFileName) { }
        public virtual void OnMaplistNextLevelIndex(Int32 mapIndex) { }
        public virtual void OnMaplistGetMapIndices(Int32 mapIndex, Int32 nextIndex) { } // BF3
        public virtual void OnMaplistMapRemoved(Int32 mapIndex) { }
        public virtual void OnMaplistMapInserted(Int32 mapIndex, String mapFileName) { }
        */
        #endregion

        #region Variables

        #region Details
        /*
        public virtual void OnServerName(String serverName) { }
        public virtual void OnServerDescription(String serverDescription) { }
        public virtual void OnBannerURL(String url) { }
        */
        #endregion

        #region Configuration
        /*
        public virtual void OnGamePassword(String gamePassword) { }
        public virtual void OnPunkbuster(Boolean isEnabled) { }
        public virtual void OnRanked(Boolean isEnabled) { }
        public virtual void OnRankLimit(Int32 iRankLimit) { }
        public virtual void OnPlayerLimit(Int32 limit) { }
        public virtual void OnMaxPlayerLimit(Int32 limit) { }
        public virtual void OnCurrentPlayerLimit(Int32 limit) { }
        public virtual void OnIdleTimeout(Int32 limit) { }
        public virtual void OnProfanityFilter(Boolean isEnabled) { }
        */
        #endregion

        #region Gameplay
        /*
        public virtual void OnFriendlyFire(Boolean isEnabled) { }
        public virtual void OnHardcore(Boolean isEnabled) { }
        */
        #region BFBC2
        /*
        public virtual void OnTeamBalance(Boolean isEnabled) { }
        public virtual void OnKillCam(Boolean isEnabled) { }
        public virtual void OnMiniMap(Boolean isEnabled) { }
        public virtual void OnCrossHair(Boolean isEnabled) { }
        public virtual void On3dSpotting(Boolean isEnabled) { }
        public virtual void OnMiniMapSpotting(Boolean isEnabled) { }
        public virtual void OnThirdPersonVehicleCameras(Boolean isEnabled) { }
        */
        #endregion

        #endregion

        #region Team Kill
        /*
        public virtual void OnTeamKillCountForKick(Int32 limit) { }
        public virtual void OnTeamKillValueIncrease(Int32 limit) { }
        public virtual void OnTeamKillValueDecreasePerSecond(Int32 limit) { }
        public virtual void OnTeamKillValueForKick(Int32 limit) { }
        */
        #endregion

        #region Level Variables
        /// NOT BF3
        /*
        public virtual void OnLevelVariablesList(LevelVariable requestedContext, List<LevelVariable> returnedValues) { }
        public virtual void OnLevelVariablesEvaluate(LevelVariable requestedContext, LevelVariable returnedValue) { }
        public virtual void OnLevelVariablesClear(LevelVariable requestedContext) { }
        public virtual void OnLevelVariablesSet(LevelVariable requestedContext) { }
        public virtual void OnLevelVariablesGet(LevelVariable requestedContext, LevelVariable returnedValue) { }
        */
        #endregion

        #region Text Chat Moderation Settings
        /// NOT BF3
        /*
        public virtual void OnTextChatModerationMode(ServerModerationModeType mode) { }
        public virtual void OnTextChatSpamTriggerCount(Int32 limit) { }
        public virtual void OnTextChatSpamDetectionTime(Int32 limit) { }
        public virtual void OnTextChatSpamCoolDownTime(Int32 limit) { }
        */
        #endregion

        #region Reserved/Specate Slots
        /// Note: This covers MoH's reserved spectate slots as well.
        /// NOT BF3 (yet)
        /*
        public virtual void OnReservedSlotsConfigFile(String configFileName) { }
        public virtual void OnReservedSlotsLoad() { }
        public virtual void OnReservedSlotsSave() { }
        public virtual void OnReservedSlotsPlayerAdded(String soldierName) { }
        public virtual void OnReservedSlotsPlayerRemoved(String soldierName) { }
        public virtual void OnReservedSlotsCleared() { }
        public virtual void OnReservedSlotsList(List<String> soldierNames) { }
        */
        #endregion

        #endregion

        #region Player Actions
        /*
        public virtual void OnPlayerKilledByAdmin(String soldierName) { }
        public virtual void OnPlayerKickedByAdmin(String soldierName, String reason) { }
        public virtual void OnPlayerMovedByAdmin(String soldierName, Int32 destinationTeamId, Int32 destinationSquadId, Boolean forceKilled) { }
        */
        #endregion

        // These events are sent from the server without any initial request from the client.
        #region Game Server Requests (Events)

        #region Players
        /*
        public virtual void OnPlayerJoin(String soldierName) {
        }

        public virtual void OnPlayerLeft(CPlayerInfo playerInfo) {
        }

        public virtual void OnPlayerAuthenticated(String soldierName, String guid) { }
        public virtual void OnPlayerKilled(Kill kKillerVictimDetails) { }
        public virtual void OnPlayerKicked(String soldierName, String reason) { }
        public virtual void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) { }

        public virtual void OnPlayerTeamChange(String soldierName, Int32 teamId, Int32 squadId) { }
        public virtual void OnPlayerSquadChange(String soldierName, Int32 teamId, Int32 squadId) { }
        */
        #endregion

        #region Chat
        /*
        public virtual void OnGlobalChat(String speaker, String message) { }
        public virtual void OnTeamChat(String speaker, String message, Int32 teamId) { }
        public virtual void OnSquadChat(String speaker, String message, Int32 teamId, Int32 squadId) { }
        */
        #endregion

        #region Round Over Events
        /*
        public virtual void OnRoundOverPlayers(List<CPlayerInfo> players) { }
        public virtual void OnRoundOverTeamScores(List<TeamScore> teamScores) { }
        public virtual void OnRoundOver(Int32 winningTeamId) { }
        */
        #endregion

        #region Levels
        /*
        public virtual void OnLoadingLevel(String mapFileName, Int32 roundsPlayed, Int32 roundsTotal) { }
        public virtual void OnLevelStarted() { }
        public virtual void OnLevelLoaded(String mapFileName, String Gamemode, Int32 roundsPlayed, Int32 roundsTotal) { } // BF3
        */
        #endregion

        #region Punkbuster
        /*
        public virtual void OnPunkbusterMessage(String punkbusterMessage) { }

        public virtual void OnPunkbusterBanInfo(CBanInfo ban) { }

        public virtual void OnPunkbusterUnbanInfo(CBanInfo unban) { }

        public virtual void OnPunkbusterBeginPlayerInfo() { }

        public virtual void OnPunkbusterEndPlayerInfo() { }

        public virtual void OnPunkbusterPlayerInfo(CPunkbusterInfo playerInfo) {
        }
        */
        #endregion

        #endregion

        #region Internal Procon Events

        #region Accounts
        /*
        public virtual void OnAccountCreated(String username) { }
        public virtual void OnAccountDeleted(String username) { }
        public virtual void OnAccountPrivilegesUpdate(String username, CPrivileges privileges) { }

        public virtual void OnAccountLogin(String accountName, String ip, CPrivileges privileges) { }
        public virtual void OnAccountLogout(String accountName, String ip, CPrivileges privileges) { }

        */
        #endregion

        #region Command Registration
        /*
        public virtual void OnAnyMatchRegisteredCommand(String speaker, String text, MatchCommand matchedCommand, CapturedCommand capturedCommand, CPlayerSubset matchedScope) { }

        public virtual void OnRegisteredCommand(MatchCommand command) { }

        public virtual void OnUnregisteredCommand(MatchCommand command) { }
        */
        #endregion

        #region Battlemap Events
        /*
        public virtual void OnZoneTrespass(CPlayerInfo playerInfo, ZoneAction action, MapZone sender, Point3D tresspassLocation, float tresspassPercentage, Object trespassState) { }
        */
        #endregion

        #region HTTP Server
        /*
        public virtual HttpWebServerResponseData OnHttpRequest(HttpWebServerRequestData data) {
        }
        */
        #endregion

        #endregion

        #region Layer Procon Events

        #region Variables
        /*
        public virtual void OnReceiveProconVariable(String variableName, String value) { }
        */
        #endregion

        #endregion

        #region previous callbcks for BFBC2
        //*************************************************************************************************
        //*************************************************************************************************
        //   UNUSED PROCON METHODS
        //*************************************************************************************************
        //*************************************************************************************************
        /*        
                public void OnAccountCreated(String strUsername) { }
                public void OnEndRound(Int32 iWinningTeamID) { }
                public void OnAccountDeleted(String strUsername) { }
                public void OnAccountPrivilegesUpdate(String strUsername, CPrivileges spPrivs) { }
                public void OnReceiveProconVariable(String strVariableName, String strValue) { }
                public void OnConnectionClosed() { }
                public void OnPlayerAuthenticated(String strSoldierName, String strGuid) { }
                public void OnPlayerKilled(String strKillerSoldierName, String strVictimSoldierName) { }
                public void OnPunkbusterMessage(String strPunkbusterMessage) { }
                public void OnPunkbusterBanInfo(CBanInfo cbiPunkbusterBan) { }
                public void OnPunkbusterPlayerInfo(CPunkbusterInfo cpbiPlayer) { }
                public void OnResponseError(List<String> lstRequestWords, String strError) { }
                public void OnHelp(List<String> lstCommands) { }
                public void OnVersion(String strServerType, String strVersion) { }
                public void OnLogin() { }
                public void OnLogout() { }
                public void OnQuit() { }
                public void OnRunScript(String strScriptFileName) { }
                public void OnRunScriptError(String strScriptFileName, Int32 iLineError, String strErrorDescription) { }
                public void OnServerInfo(CServerInfo csiServerInfo) { }
                public void OnYelling(String strMessage, Int32 iMessageDuration, CPlayerSubset cpsSubset) { }
                public void OnSaying(String strMessage, CPlayerSubset cpsSubset) { }
                public void OnSupportedMaps(String strPlayList, List<String> lstSupportedMaps) { }
                public void OnPlaylistSet(String strPlaylist) { }
                public void OnListPlaylists(List<String> lstPlaylists) { }
                public void OnPlayerKicked(String strSoldierName, String strReason) { }
                public void OnPlayerSquadChange(String strSoldierName, Int32 iTeamID, Int32 iSquadID) { }
                public void OnBanList(List<CBanInfo> lstBans) { }
                public void OnBanAdded(CBanInfo cbiBan) { }
                public void OnBanRemoved(CBanInfo cbiUnban) { }
                public void OnBanListClear() { }
                public void OnBanListLoad() { }
                public void OnBanListSave() { }
                public void OnReservedSlotsConfigFile(String strConfigFilename) { }
                public void OnReservedSlotsLoad() { }
                public void OnReservedSlotsSave() { }
                public void OnReservedSlotsPlayerAdded(String strSoldierName) { }
                public void OnReservedSlotsPlayerRemoved(String strSoldierName) { }
                public void OnReservedSlotsCleared() { }
                public void OnMaplistConfigFile(String strConfigFilename) { }
                public void OnMaplistLoad() { }
                public void OnMaplistSave() { }
                public void OnMaplistMapAppended(String strMapFileName) { }
                public void OnMaplistCleared() { }
                public void OnMaplistList(List<String> lstMapFileNames) { }
                public void OnMaplistNextLevelIndex(Int32 iMapIndex) { }
                public void OnMaplistMapRemoved(Int32 iMapIndex) { }
                public void OnMaplistMapInserted(Int32 iMapIndex, String strMapFileName) { }
                public void OnRunNextLevel() { }
                public void OnCurrentLevel(String strCurrentLevel) { }
                public void OnRestartLevel() { }
                public void OnLevelStarted() { }
                public void OnGamePassword(String strGamePassword) { }
                public void OnPunkbuster(Boolean blEnabled) { }
                public void OnHardcore(Boolean blEnabled) { }
                public void OnRanked(Boolean blEnabled) { }
                public void OnRankLimit(Int32 iRankLimit) { }
                public void OnTeamBalance(Boolean blEnabled) { }
                public void OnFriendlyFire(Boolean blEnabled) { }
                public void OnMaxPlayerLimit(Int32 iMaxPlayerLimit) { }
                public void OnCurrentPlayerLimit(Int32 iCurrentPlayerLimit) { }
                public void OnPlayerLimit(Int32 iPlayerLimit) { }
                public void OnBannerURL(String strURL) { }
                public void OnServerDescription(String strServerDescription) { }
                public void OnKillCam(Boolean blEnabled) { }
                public void OnMiniMap(Boolean blEnabled) { }
                public void OnCrossHair(Boolean blEnabled) { }
                public void On3dSpotting(Boolean blEnabled) { }
                public void OnMiniMapSpotting(Boolean blEnabled) { }
                public void OnThirdPersonVehicleCameras(Boolean blEnabled) { }
                public void OnPlayerLeft(CPlayerInfo cpiPlayer) { }
                public void OnServerName(String strServerName) { }
                public void OnTeamKillCountForKick(Int32 iLimit) { }
                public void OnTeamKillValueIncrease(Int32 iLimit) { }
                public void OnTeamKillValueDecreasePerSecond(Int32 iLimit) { }
                public void OnTeamKillValueForKick(Int32 iLimit) { }
                public void OnIdleTimeout(Int32 iLimit) { }
                public void OnProfanityFilter(Boolean isEnabled) { }
                public void OnRoundOverTeamScores(List<TeamScore> lstTeamScores) { }
                public void OnLevelVariablesList(LevelVariable lvRequestedContext, List<LevelVariable> lstReturnedValues) { }
                public void OnLevelVariablesEvaluate(LevelVariable lvRequestedContext, LevelVariable lvReturnedValue) { }
                public void OnLevelVariablesClear(LevelVariable lvRequestedContext) { }
                public void OnLevelVariablesSet(LevelVariable lvRequestedContext) { }
                public void OnLevelVariablesGet(LevelVariable lvRequestedContext, LevelVariable lvReturnedValue) { }

         */
        #endregion
        #endregion
    }
}
