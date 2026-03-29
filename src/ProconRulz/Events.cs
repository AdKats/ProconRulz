using System;
using System.Collections.Generic;

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
    public partial class ProconRulz
    {

        #region Callbacks for misc. server EVENTS (player joins/moves, list players, round end etc)

        //********************************************************************************************
        //********************************************************************************************
        //************************* various other events from procon handled here
        //********************************************************************************************
        //********************************************************************************************
        public override void OnPlayerTeamChange(String player_name, Int32 iTeamID, Int32 iSquadID)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                players.team_move(player_name, iTeamID.ToString(), iSquadID.ToString());
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnPlayerTeamChange");
                PrintException(ex);
            }
        }

        public override void OnPlayerSquadChange(String player_name, Int32 iTeamID, Int32 iSquadID)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                players.team_move(player_name, iTeamID.ToString(), iSquadID.ToString());
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnPlayerSquadChange");
                PrintException(ex);
            }
        }

        public override void OnPlayerJoin(String player_name)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: ********************OnPlayerJoin******************************" +
                    player_name);
                if (trace_rules == enumBoolYesNo.Yes) prdebug("counts");
                players.new_player(player_name);

                // update the admin list with this player name if necessary
                admins_add(player_name);
                // scan for any "On Join" rulz
                scan_rules(TriggerEnum.Join, player_name,
                    new Dictionary<SubstEnum, String>(), null, null, null);
                if (trace_rules == enumBoolYesNo.Yes) prdebug("counts");
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnPlayerJoin");
                PrintException(ex);
            }
        }

        public override void OnPlayerLeft(CPlayerInfo cpiPlayer)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: ********************OnPlayerLeft******************************" +
                    cpiPlayer.SoldierName);

                scan_rules(TriggerEnum.Leave, cpiPlayer.SoldierName,
                    new Dictionary<SubstEnum, String>(), null, null, null);
                if (trace_rules == enumBoolYesNo.Yes) prdebug("counts");

                //Removes left player from all lists
                players.remove(cpiPlayer.SoldierName);

                spawn_counts.zero_player(cpiPlayer.SoldierName);
                admins_remove(cpiPlayer.SoldierName);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnPlayerLeft");
                PrintException(ex);
            }
        }

        public override void OnPunkbusterPlayerInfo(CPunkbusterInfo cpbiPlayer)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: ********************OnPunkbusterPlayerInfo******************************" +
                    cpbiPlayer.SoldierName);

                players.update(cpbiPlayer); // add pb_guid and ip
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnPunkbusterPlayerInfo");
                PrintException(ex);
            }
        }

        public override void OnListPlayers(List<CPlayerInfo> lstPlayers, CPlayerSubset cpsSubset)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: ********************OnListPlayers******************************");
                // if (trace_rules == enumBoolYesNo.Yes) prdebug("counts");
                // if 'admin.listPlayers all' then do full update of players list and return
                if (cpsSubset.Subset == CPlayerSubset.PlayerSubsetType.All)
                {
                    players.pre_scrub(); // reset all 'updated' flags to false

                    admins_reset(); // empty list of currently logged on admins

                    foreach (CPlayerInfo cp_info in lstPlayers)
                    {
                        players.update(cp_info);
                        // add this player to list of logged-on admins if required
                        admins_add(cp_info.SoldierName);
                        // create/update a score variable for each player
                        String var_name = "%server_score[" + cp_info.SoldierName + "]%";
                        rulz_vars.set_value(null, var_name, cp_info.Score.ToString(), null);
                    }
                    players.scrub(); // remove all players that were not updated

                }
                if (trace_rules == enumBoolYesNo.Yes) prdebug("counts");
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnListPlayers");
                PrintException(ex);
            }
        }

        public override void OnReservedSlotsList(List<String> lstSoldierNames)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                this.reserved_slot_players = lstSoldierNames;
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnReservedSlotsList");
                PrintException(ex);
            }
        }

        // test_round_over will trigger On RoundOver rulz IF game is earlier than BF4 
        // OR all three BF4 round over events have fired
        // i.e. RoundOver, RoundOverPlayers, RoundOverTeamScores
        public void test_round_over()
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                if (game_id == GameIdEnum.BF3 ||
                    game_id == GameIdEnum.BFBC2 ||
                    game_id == GameIdEnum.MoH ||
                    ++round_over_event_count == 3)
                {
                    // check rules for On Round trigger
                    scan_rules(TriggerEnum.RoundOver, null, new Dictionary<SubstEnum, String>(), null, null, null);
                    round_over_event_count = 0;
                }
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in test_round_over()");
                PrintException(ex);
            }
        }

        public override void OnRoundOver(Int32 iWinningTeamID)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: ********************OnRoundOver******************************");
                // trigger On RoundOver rulz if all round over events complete now
                test_round_over();
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnRoundOver");
                PrintException(ex);
            }
        }

        public override void OnRoundOverPlayers(List<CPlayerInfo> players)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: ********************OnRoundOverPlayers******************************");
                // trigger On RoundOver rulz if all round over events complete now
                test_round_over();
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnRoundOverPlayers");
                PrintException(ex);
            }
        }

        public override void OnRoundOverTeamScores(List<TeamScore> scores)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: ********************OnRoundOverTeamScores******************************");
                // trigger On RoundOver rulz if all round over events complete now
                test_round_over();
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnRoundOverTeamScores");
                PrintException(ex);
            }
        }

        // BFBC2 only
        // on round start we reset some of the ProconRulz counts 
        // (e.g. # times players have triggered rules)
        // the only subtlety is the rules haven't changed so we can keep those, 
        // and hence keep the list of 'watched' items
        // the 'Rates' counts (Rate 5 10) as of v32 continue past round/map change transitions...
        public override void OnLoadingLevel(String strMapFileName, Int32 roundsPlayed, Int32 roundsTotal)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: *******************OnLoadingLevel*****************************" +
                    strMapFileName);
                rulz_enable = true;
                current_map = this.GetMapByFilename(strMapFileName);
                current_map_mode = current_map.GameMode;

                // empty the rulz vars
                rulz_vars.reset();

                zero_counts();
                clear_blocks();
                // remove players no longer on the server from the rates counts
                scrub_rates(players.list_players());
                // exec listPlayers to initialise players global
                ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
                load_reserved_slot_players();
                // initialise ProconRulz On Init vars
                OnInit();
                // check rules for On Round trigger
                scan_rules(TriggerEnum.Round, null, new Dictionary<SubstEnum, String>(), null, null, null);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnLoadingLevel");
                PrintException(ex);
            }
        }

        // BF3 only
        // on round start we reset some of the ProconRulz counts 
        // (e.g. # times players have triggered rules)
        // the only subtlety is the rules haven't changed so we can keep those, 
        // and hence keep the list of 'watched' items
        // the 'Rates' counts (Rate 5 10) as of v32 continue past round/map change transitions...
        public void OnLevelLoaded(String strMapFileName, String strMapMode, Int32 roundsPlayed, Int32 roundsTotal)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: *******************OnLevelLoaded*****************************" +
                    strMapFileName + " " + strMapMode + " " + roundsPlayed + "/" + roundsTotal);
                // initialize counter for 3 BF4 RoundOver events (RoundOver, RoundOverPlayers, RoundOverTeamScores)
                round_over_event_count = 0;
                rulz_enable = true;
                current_map = this.GetMapByFilename(strMapFileName);
                current_map_mode = strMapMode;
                zero_counts();
                clear_blocks();
                // empty the rulz vars
                rulz_vars.reset();
                // remove players no longer on the server from the rates counts
                scrub_rates(players.list_players());
                // exec listPlayers to initialise players global
                ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
                load_reserved_slot_players();
                // initialise ProconRulz On Init vars
                OnInit();
                // check rules for On Round trigger
                scan_rules(TriggerEnum.Round, null, new Dictionary<SubstEnum, String>(), null, null, null);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnLevelLoaded");
                PrintException(ex);
            }
        }

        public void OnCurrentLevel(String strMapFileName)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: *******************OnCurrentLevel*****************************" +
                    strMapFileName);
                current_map = this.GetMapByFilename(strMapFileName);
                current_map_mode = current_map.GameMode;
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnCurrentLevel");
                PrintException(ex);
            }
        }

        public override void OnGlobalChat(String strSpeaker, String strMessage)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                say_rulz(strSpeaker, strMessage);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnGlobalChat");
                PrintException(ex);
            }
        }

        public override void OnTeamChat(String strSpeaker, String strMessage, Int32 iTeamID)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                say_rulz(strSpeaker, strMessage);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnTeamChat");
                PrintException(ex);
            }
        }

        public override void OnSquadChat(String strSpeaker, String strMessage, Int32 iTeamID, Int32 iSquadID)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                say_rulz(strSpeaker, strMessage);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnSquadChat");
                PrintException(ex);
            }
        }

        public virtual void OnServerInfo(CServerInfo serverInfo)
        {
            List<TeamScore> scores = new List<TeamScore>(); // number of tickets remaining per team
            // EVENT EXCEPTION BLOCK:
            try
            {
                WriteDebugInfo("ProconRulz: *******************OnServerInfo*****************************");
                current_map = this.GetMapByFilename(serverInfo.Map);
                current_map_mode = serverInfo.GameMode;
                if (serverInfo.TeamScores != null)
                {
                    // set up team score variables %server_team_score[1]%, %server_team_score[2]% ...
                    foreach (TeamScore t in serverInfo.TeamScores)
                    {
                        if (t.TeamID == null || t.Score == null)
                        {
                            WriteConsole(String.Format("ProconRulz: OnServerInfo TeamID,Score error [{0}][{1}]", t.TeamID, t.Score));
                            break;
                        }
                        String var_name = "%server_team_score[" + t.TeamID.ToString() + "]%";
                        rulz_vars.set_value(null, var_name, t.Score.ToString(), null);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnServerInfo");
                PrintException(ex);
            }

        }

        #endregion

        #region Callbacks for SPAWN and KILL events - this is where most of the magic happens

        //**********************************************************************************************
        //**********************************************************************************************
        //   PROCESS VARIOUS PROCON 'EVENT' PROCEDURES - e.g. OnPlayerSpawned(), OnPlayerKilled()
        //**********************************************************************************************
        //**********************************************************************************************

        //********************************************************************************************
        // this procedure gets called when every player SPAWNS
        // so at that point we will scan the rules and decide whether to apply any actions
        //********************************************************************************************
        public override void OnPlayerSpawned(String player_name, Inventory inv)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                // debug stuff
                String blocks = "";
                if (player_blocks.ContainsKey(player_name))
                    blocks = String.Join("/", player_blocks[player_name].ToArray());
                WriteDebugInfo("********************OnPlayerSpawned***************************" +
                    player_name);
                WriteDebugInfo(String.Format("ProconRulz: OnPlayerSpawned [{0}] with blocks [{1}]",
                    player_name, blocks));
                //end debug

                // create structure to hold substitution keywords and values
                Dictionary<SubstEnum, String> keywords = new Dictionary<SubstEnum, String>();

                // remove this player from the spawn item lists for now
                spawn_counts.zero_player(player_name);
                remove_blocks(player_name);       // remove any player_blocks entries

                // BF bug workaround - 
                // check to see if player can do damage Shotgun before saving spec sp-shotgun_s
                Boolean has_damage_shotgun = false;

                String player_team_id = players.team_id(player_name);

                WriteDebugInfo(String.Format("ProconRulz: OnPlayerSpawned [{0}] team_id {1}",
                    player_name, player_team_id));

                // add player to kit spawned list, if kit is 'watched'
                spawn_counts.add(item_key(inv.Kit), player_team_id, player_name);

                player_kit[player_name] = item_key(inv.Kit); // record kit that this player spawned with
                WriteDebugInfo(String.Format("ProconRulz: OnPlayerSpawned [{0}] kit {1}",
                    player_name, player_kit[player_name]));

                List<String> damages = new List<String>();
                List<String> weapon_keys = new List<String>();
                List<String> weapon_names = new List<String>();
                foreach (Weapon w in inv.Weapons)
                {
                    // add playername to watch list for weapon, if it's being watched
                    spawn_counts.add(item_key(w), player_team_id, player_name);

                    // add playername to watch list for weapon damage, if it's being watched
                    spawn_counts.add(item_key(w.Damage), player_team_id, player_name);
                    // BF bug workaround - remember if player can do damage shotgun
                    if (item_key(w.Damage).ToLower() == "shotgun") has_damage_shotgun = true;

                    // build 'weapons' and damages string lists for debug printout and subst variables
                    weapon_keys.Add(w.Name);
                    weapon_names.Add(weapon_desc(w.Name));
                    damages.Add(item_key(w.Damage));
                } // end looping through weapons in inventory
                // store %wk% subst var (weapon keys)
                if (weapon_keys.Count > 0)
                    keywords.Add(SubstEnum.WeaponKey, String.Join(", ", weapon_keys.ToArray()));
                else
                    keywords.Add(SubstEnum.WeaponKey, "No weapon key");
                // store %w% subst var (weapon names)
                if (weapon_names.Count > 0)
                    keywords.Add(SubstEnum.Weapon, String.Join(", ", weapon_names.ToArray()));
                else
                    keywords.Add(SubstEnum.Weapon, "No weapon");
                // store %d% subst var (damages)
                if (damages.Count > 0)
                    keywords.Add(SubstEnum.Damage, String.Join(", ", damages.ToArray()));
                else
                    keywords.Add(SubstEnum.Damage, "No damage");

                WriteDebugInfo(String.Format("ProconRulz: OnPlayerSpawned [{0}] weapons [{1}]",
                    player_name, keywords[SubstEnum.Weapon]));

                List<String> spec_keys = new List<String>();
                List<String> spec_names = new List<String>();
                foreach (Specialization s in inv.Specializations)
                {
                    // condition is a BF bug workaround
                    // Add this Spec IF player has a weapon that does damage Shotgun, 
                    // OR if the spec is not 12-Gauge Sabot Rounds
                    if (has_damage_shotgun || (!(item_key(s).ToLower() == "sp_shotgun_s")))
                        spawn_counts.add(item_key(s), player_team_id, player_name);

                    spec_keys.Add(item_key(s));
                    spec_names.Add(spec_desc(s));
                } // end looping through specs in inventory
                // store %speck% subst var (spec keys)
                if (spec_keys.Count > 0)
                    keywords.Add(SubstEnum.SpecKey, String.Join(", ", spec_keys.ToArray()));
                else
                    keywords.Add(SubstEnum.SpecKey, "No spec key");
                // store %spec% subst var (specializations)
                if (spec_names.Count > 0)
                    keywords.Add(SubstEnum.Spec, String.Join(", ", spec_names.ToArray()));
                else
                    keywords.Add(SubstEnum.Spec, "No spec");

                WriteDebugInfo(
                    String.Format("^bProconRulz: [{0}] {1} spawned. Kit {2}. Weapons [{3}]. Specs [{4}]. Damages [{5}]",
                                        team_name(player_team_id),
                                        player_name,
                                        item_key(inv.Kit),
                                        keywords[SubstEnum.Weapon],
                                        keywords[SubstEnum.Spec],
                                        keywords[SubstEnum.Damage]
                                        ));

                // debug stuff
                if (trace_rules == enumBoolYesNo.Yes) prdebug("counts");

                //Check if the player carries any of the things we're looking for            
                scan_rules(TriggerEnum.Spawn, player_name, keywords, inv, null, null);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnPlayerSpawn");
                PrintException(ex);
            }
        }

        //********************************************************************************************
        // this procedure gets called when each KILL occurs in the game
        //********************************************************************************************
        public override void OnPlayerKilled(Kill k)
        {
            // EVENT EXCEPTION BLOCK:
            try
            {
                if (k == null)
                {
                    WriteDebugInfo("*******************OnPlayerKilled*****************NULL KILL OBJECT");
                    return;
                }

                if (k.Killer == null)
                {
                    WriteDebugInfo("*******************OnPlayerKilled***************NULL KILLER OBJECT");
                    return;
                }

                CPlayerInfo Killer = k.Killer;
                CPlayerInfo Victim = k.Victim;

                String player_name = Killer.SoldierName;
                String victim_name = Victim.SoldierName;

                // cache the player info in case we want the GUID
                if (player_name != null && player_name != "") player_info[player_name] = Killer;
                if (victim_name != null && victim_name != "") player_info[victim_name] = Victim;

                WriteDebugInfo("*******************OnPlayerKilled*************************" + player_name);

                if (Victim == null)
                {
                    WriteDebugInfo(String.Format("NULL VICTIM on this kill by {0}", player_name));
                    return;
                }

                WriteDebugInfo(String.Format("ProconRulz: OnPlayerKilled [{0}] [{1}]",
                                player_name, victim_name));

                String weapon_key = k.DamageType;
                if (weapon_key == "" || weapon_key == null) weapon_key = "No weapon key";

                WriteDebugInfo(String.Format("ProconRulz: OnPlayerKilled [{0}] weapon_key [{1}]",
                                player_name, weapon_key));

                Weapon weapon_used;
                try
                {
                    if (weapon_key == null) weapon_used = null;
                    else weapon_used = weaponDefines[weapon_key];
                }
                catch { weapon_used = null; }

                String weapon_descr = weapon_desc(weapon_key);

                WriteDebugInfo(String.Format("ProconRulz: OnPlayerKilled [{0}] weapon_descr [{1}]",
                                player_name, weapon_descr));

                String weapon_kit;
                try
                {
                    if (weapon_used == null) weapon_kit = "No weapon kit";
                    else weapon_kit = item_key(weapon_used.KitRestriction);
                }
                catch { weapon_kit = "No weapon kit"; }

                WriteDebugInfo(String.Format("ProconRulz: OnPlayerKilled [{0}] kit [{1}]",
                                player_name, weapon_kit));

                String damage;
                try
                {
                    damage = item_key(weapon_used.Damage);
                }
                catch { damage = "No damage key"; }

                WriteDebugInfo(String.Format("ProconRulz: OnPlayerKilled [{0}] damage [{1}]",
                                player_name, damage));

                add_kill_count(player_name, weapon_key);
                add_kill_count(player_name, damage);
                add_kill_count(player_name, weapon_kit);

                // debug
                String killer_counts = "";

                if (kill_counts.ContainsKey(player_name))
                    foreach (String item_name in kill_counts[player_name].Keys)
                    {
                        List<String> item_list = new List<String>();
                        item_list.Add(item_name);
                        killer_counts += item_name + "(" +
                            count_kill_items(player_name, item_list).ToString() + ") ";
                    }
                else killer_counts = "0 kill counts";

                WriteDebugInfo(
                    String.Format("^bProconRulz: [{0} {1} [{2}]] killed [{3}] with [{4}], damage {5}, range {6}",
                    weapon_kit, player_name, killer_counts,
                    victim_name, weapon_descr, damage, (Int32)k.Distance));
                //end debug

                // clear the dead soldier out of the 'counts' if it's first come first served
                // this will open up an opportunity for someone else to spawn with this players items
                if (reservationMode == ReserveItemEnum.Player_loses_item_when_dead)
                {
                    spawn_counts.zero_player(Victim.SoldierName);
                }

                TriggerEnum kill_type = TriggerEnum.Kill;
                String blocked_item = "";
                if (k.IsSuicide || player_name == null || player_name == "") // BF3 reports no killer with SoldierCollision
                {
                    kill_type = TriggerEnum.Suicide;
                    // - this is just for testing the suicide data
                    WriteDebugInfo("Suicide info: " +
                                    "k.IsSuicide=" + (k.IsSuicide ? "true" : "false") +
                                    ", player_name=" + (player_name == null ? "null" : "\"" + player_name + "\"") +
                                    ", victim_name=" + (victim_name == null ? "null" : "\"" + victim_name + "\"") +
                                    ", weapon_key=" + weapon_key
                                  );

                    if (player_name == null || player_name == "") player_name = victim_name;
                }
                else if (test_block(player_name, weapon_key))
                {
                    kill_type = TriggerEnum.PlayerBlock;
                    blocked_item = weapon_descr;
                    WriteDebugInfo(String.Format("ProconRulz: PlayerBlock [{0}] with weapon [{1}]",
                        player_name, blocked_item));
                }
                else if (test_block(player_name, weapon_kit))
                {
                    kill_type = TriggerEnum.PlayerBlock;
                    blocked_item = weapon_kit;
                    WriteDebugInfo(String.Format("ProconRulz: PlayerBlock [{0}] with kit [{1}]",
                        player_name, blocked_item));
                }
                else if (test_block(player_name, damage))
                {
                    kill_type = TriggerEnum.PlayerBlock;
                    blocked_item = damage;
                    WriteDebugInfo(String.Format("ProconRulz: PlayerBlock [{0}] with damage [{1}]",
                        player_name, blocked_item));
                }
                else if (Killer.TeamID == Victim.TeamID) kill_type = TriggerEnum.TeamKill;

                WriteDebugInfo(String.Format("ProconRulz: OnPlayerKilled for [{0}] is Event {1}",
                                    player_name,
                                    Enum.GetName(typeof(TriggerEnum), kill_type)));
                // now we do the main work of scanning the rules for this KILL
                scan_rules(kill_type, player_name, new Dictionary<SubstEnum, String>(),
                    null, k, blocked_item);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in OnPlayerKilled");
                PrintException(ex);
            }
        }
        #endregion
    }
}
