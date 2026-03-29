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
    public partial class ProconRulz
    {

        #region Step through the rules, looking for trigger that matches the event

        //**********************************************************************************************
        //**********************************************************************************************
        //   CHECK THE RULES to see if we should kill, kick, say
        //**********************************************************************************************
        //**********************************************************************************************
        // here is where we scan the rules after a player has joined, spawned or a kill has occurred
        private void scan_rules(TriggerEnum trigger, String name,
                                Dictionary<SubstEnum, String> keywords,
                                Inventory inv, Kill k, String item)
        {
            WriteDebugInfo(String.Format("ProconRulz: Scan_rules[{0}] with Event {1}",
                                name,
                                Enum.GetName(typeof(TriggerEnum), trigger)));

            // don't do anything if rulz_enable has been set to false
            if (!rulz_enable) return;

            // CATCH EXCEPTIONS
            try
            {
                // initial population of the 'keywords' dictionary
                assign_initial_keywords(name, ref keywords);

                // loop through the rules
                foreach (ParsedRule rule in parsed_rules)
                {
                    // skip comments
                    if (rule.comment) continue;

                    if (rule.trigger == trigger)
                    {
                        WriteDebugInfo(String.Format("ProconRulz: scan_rules[{0}] [{1}]",
                            name, rule.unparsed_rule));
                        if (process_rule(trigger, rule, name, ref keywords, inv, k, item))
                        {
                            WriteDebugInfo(String.Format("ProconRulz: scan_rules[{0}] [{1}] FIRED",
                                name, rule.unparsed_rule));
                            break; // break if any rule fires
                        }
                    }
                    // else WriteDebugInfo(String.Format("ProconRulz: scan_rules[{0}] skipped", name));
                } // end looping through the rules
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in scan_rules");
                PrintException(ex);
            }
        }

        // test a rule (we have already confirmed Spawn or Kill trigger is true)
        // will return 'true' if the rule is applied
        private Boolean process_rule(TriggerEnum trigger, ParsedRule rule, String player_name,
                                        ref Dictionary<SubstEnum, String> keywords,
                                        Inventory inv, Kill k, String item)
        {
            WriteDebugInfo(String.Format("ProconRulz:   process_rule[{0}] with event {1}",
                                player_name,
                                Enum.GetName(typeof(TriggerEnum), trigger)));

            // CATCH EXCEPTIONS
            try
            {
                List<PartClass> parts = new List<PartClass>(rule.parts);

                if (trigger == TriggerEnum.Say) keywords[SubstEnum.Text] = item;
                // Populate the Counts AS IF THIS RULE SUCCEEDED so conditions can use them
                keywords[SubstEnum.ServerCount] = count_server_rule(rule.id).ToString() + 1;
                keywords[SubstEnum.Count] = count_rule(player_name, rule.id).ToString() + 1;
                keywords[SubstEnum.TeamCount] = count_team_rule(players.team_id(player_name), rule.id).ToString() + 1;

                // populate the 'keywords' dictionary
                assign_keywords(trigger, rule, player_name, ref keywords, inv, k, item);

                if (!process_parts(rule, parts, player_name, ref keywords, k, item))
                {
                    WriteDebugInfo(String.Format("ProconRulz:   process_rule[{0}] in rule [{1}] tests NEGATIVE",
                        player_name, rule.unparsed_rule));
                    return false;
                }

                WriteDebugInfo(String.Format("ProconRulz:   process_rule[{0}] in rule [{1}] all conditions OK",
                    player_name, rule.unparsed_rule));

                // return 'true' to quit rulz checks after this rule
                // if rule contains End, Kill, Kick, TempBan, Ban unless it contains Continue.
                return end_rulz(parts);
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in test_rule_and_exec(" +
                    rule.unparsed_rule + ")");
                PrintException(ex);
                return false;
            }
        }

        private Boolean process_parts(ParsedRule rule, List<PartClass> parts, String player_name,
                                        ref Dictionary<SubstEnum, String> keywords,
                                        Kill k, String item)
        {
            PartClass current_part = null;
            try
            {
                Boolean playercount_updated = false; // we only update PlayerCount etc ONCE either after a successful
                // PlayerCount, or before the use of %c% etc
                // check each of the PARTS.
                // for each part in the rule, call process_part()
                foreach (PartClass p in parts)
                {
                    current_part = p; // so we can display the part that caused exception if needed
                    // see if we should update PlayerCount etc here
                    // rule can by NULL if parts is a list of TargetActions
                    if (rule != null && !playercount_updated && p.has_count)
                    {
                        update_counts(player_name, rule.id, ref keywords);
                        playercount_updated = true;
                    }
                    // HERE IS WHERE WE LEAVE THE PROC AND RETURN FALSE IF A CONDITION FAILS
                    if (!process_part(rule, p,
                            player_name, k, item, ref keywords))
                        return false;
                    WriteDebugInfo(String.Format("ProconRulz:     process_parts [{0}] OK", player_name));
                }
                return true;
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in process_parts (ProconRulz will continue)");
                WriteConsole("ProconRulz: Rule was: " + rule.unparsed_rule);
                if (current_part != null)
                {
                    WriteConsole("ProconRulz: Rule part was: " + current_part.ToString());
                }
                else
                {
                    WriteConsole("ProconRulz: Rule part was null");
                }
                PrintException(ex);
                return false;
            }
        }

        // return 'true' to end rulz if rule contains End, Kill, Kick, TempBan, Ban unless it contains Continue.
        Boolean end_rulz(List<PartClass> parts)
        {
            foreach (PartClass p in parts)
            {
                if (p.part_type == PartEnum.Continue) return false;
                if (p.part_type == PartEnum.End) return true;
            }
            foreach (PartClass p in parts)
            {
                if (p.part_type == PartEnum.Kill ||
                    p.part_type == PartEnum.Kick ||
                    p.part_type == PartEnum.TempBan ||
                    p.part_type == PartEnum.Ban ||
                    p.part_type == PartEnum.PBBan ||
                    p.part_type == PartEnum.PBKick
                    ) return true;
            }
            return false;
        }

        // return true if this part refers to a Count
        Boolean has_a_count(PartClass p)
        {
            if (p.part_type == PartEnum.PlayerCount ||
                p.part_type == PartEnum.TeamCount ||
                p.part_type == PartEnum.ServerCount) return true;

            foreach (String s in p.string_list)
            {
                foreach (String k in subst_keys[SubstEnum.Count])
                {
                    if (s.Contains(k)) return true;
                }
                foreach (String k in subst_keys[SubstEnum.TeamCount])
                {
                    if (s.Contains(k)) return true;
                }
                foreach (String k in subst_keys[SubstEnum.ServerCount])
                {
                    if (s.Contains(k)) return true;
                }
            }
            return false;
        }

        private void update_counts(String player_name, Int32 rule_id, ref Dictionary<SubstEnum, String> keywords)
        {
            // increment the 'count' for this player for this rule
            add_rule_count(player_name, rule_id);
            // Now populate the Counts properly for the ACTIONS
            keywords[SubstEnum.ServerCount] = count_server_rule(rule_id).ToString();
            keywords[SubstEnum.Count] = count_rule(player_name, rule_id).ToString();
            keywords[SubstEnum.TeamCount] = count_team_rule(keywords[SubstEnum.PlayerTeamKey], rule_id).ToString();
        }

        private void assign_initial_keywords(String player_name, ref Dictionary<SubstEnum, String> keywords)
        {

            keywords[SubstEnum.Date] = DateTime.Now.ToString("yyyy_MM_dd");
            keywords[SubstEnum.Hhmmss] = DateTime.Now.ToString("HH:mm:ss");
            keywords[SubstEnum.Seconds] = Math.Floor(DateTime.Now.Subtract(new DateTime(2012, 1, 1, 0, 0, 0)).TotalSeconds).ToString();

            // put the map name and mode into the subst keys
            if (current_map != null)
            {
                keywords[SubstEnum.Map] = current_map.PublicLevelName;
                keywords[SubstEnum.MapMode] = current_map_mode;
            }

            keywords[SubstEnum.Teamsize] = players.min_teamsize().ToString();
            keywords[SubstEnum.Teamsize1] = players.teamsize("1").ToString();
            keywords[SubstEnum.Teamsize2] = players.teamsize("2").ToString();

            //debug -- these check values say where the exception was...
            Int32 check = 1;
            if (player_name != null)
            {
                try
                {
                    check = 2;
                    keywords[SubstEnum.Player] = player_name;
                    check = 3;
                    keywords[SubstEnum.Ping] = players.ping(player_name).ToString();
                    check = 4;
                    keywords[SubstEnum.EA_GUID] = players.ea_guid(player_name);
                    check = 5;
                    keywords[SubstEnum.PB_GUID] = players.pb_guid(player_name);
                    check = 6;
                    keywords[SubstEnum.IP] = players.ip(player_name);
                    check = 7;

                    String player_team_id = players.team_id(player_name);
                    check = 8;
                    String player_squad_id = players.squad_id(player_name);
                    check = 9;

                    keywords[SubstEnum.PlayerTeamsize] = players.teamsize(player_team_id).ToString();
                    check = 10;

                    keywords[SubstEnum.PlayerTeam] = team_name(player_team_id);
                    check = 11;
                    keywords[SubstEnum.PlayerSquad] = squad_name(player_squad_id);
                    check = 12;
                    keywords[SubstEnum.PlayerTeamKey] = player_team_id;
                    check = 13;
                    keywords[SubstEnum.PlayerSquadKey] = player_squad_id;
                    check = 14;
                    keywords[SubstEnum.PlayerCountry] = players.cname(player_name);
                    check = 15;
                    keywords[SubstEnum.PlayerCountryKey] = players.ckey(player_name);
                }
                catch (Exception ex)
                {
                    WriteConsole("ProconRulz: recoverable exception in assign_initial_keywords #1 check(" + check.ToString() + ")");
                    PrintException(ex);
                    return;
                }
            }
            else
            {
                keywords[SubstEnum.Player] = "";
            }
        }

        private void assign_keywords(TriggerEnum trigger, ParsedRule rulex, String player_name,
                                        ref Dictionary<SubstEnum, String> keywords,
                                        Inventory inv, Kill k, String item)
        {
            // HERE's WHERE WE UPDATE THE SUBST KEYWORDS (and conditions can as well, 
            // e.g. TargetPlayer)
            // this would be more efficient if I only updated the keywords that are
            // actually used in the rulz


            // update player count etc for this rule (for use of %c% parameter 
            // or subsequent Count condition)

            if (trigger == TriggerEnum.Spawn)
            {
                keywords[SubstEnum.Kit] = kit_desc(item_key(inv.Kit));
                keywords[SubstEnum.KitKey] = item_key(inv.Kit);
                // keywords[Weaponkey, Weapon, Damage, SpecKey, Spec] all set in OnPlaeyrSpawned
            }
            else if (trigger == TriggerEnum.Kill ||
                trigger == TriggerEnum.TeamKill ||
                trigger == TriggerEnum.Suicide ||
                trigger == TriggerEnum.PlayerBlock
                )
            {
                try
                {
                    // we're in a 'kill' type event here
                    // as far as I can tell, 'k.DamageType' usually contains 
                    // weapon key, but sometimes can be null

                    // with BF3, k.Killer.SoldierName can be empty (null or ""?)
                    if (k == null) return;

                    String victim_name = (k.Victim == null) ? "No victim" : k.Victim.SoldierName;
                    keywords[SubstEnum.Victim] = victim_name;

                    String weapon_key = k.DamageType;

                    Weapon weapon_used;
                    try
                    {
                        if (weapon_key == null) weapon_used = null;
                        else weapon_used = weaponDefines[weapon_key];
                    }
                    catch { weapon_used = null; }

                    String weapon_descr = weapon_desc(weapon_key);

                    String damage;
                    try
                    {
                        damage = (weapon_used == null) ? "No damage key" : item_key(weapon_used.Damage);
                    }
                    catch { damage = "No damage key"; }

                    keywords[SubstEnum.Weapon] = weapon_descr;
                    keywords[SubstEnum.WeaponKey] = weapon_key;

                    keywords[SubstEnum.KitKey] = spawned_kit(player_name);
                    keywords[SubstEnum.Kit] = kit_desc(spawned_kit(player_name));

                    keywords[SubstEnum.VictimKit] = kit_desc(spawned_kit(victim_name));
                    keywords[SubstEnum.VictimTeamKey] = players.team_id(victim_name);
                    keywords[SubstEnum.VictimTeam] = team_name(players.team_id(victim_name));

                    keywords[SubstEnum.VictimCountry] = players.cname(victim_name);
                    keywords[SubstEnum.VictimCountryKey] = players.ckey(victim_name);

                    keywords[SubstEnum.Damage] = damage;

                    keywords[SubstEnum.Range] = k.Distance.ToString("0.0");
                    keywords[SubstEnum.Headshot] = k.Headshot ? "Headshot" : "";
                }
                catch (Exception ex)
                {
                    WriteConsole("ProconRulz: recoverable exception in assign_keywords #2");
                    PrintException(ex);
                    return;
                }
            }

            if (trigger == TriggerEnum.PlayerBlock)
            {
                keywords.Add(SubstEnum.BlockedItem, item);
                WriteDebugInfo(String.Format("ProconRulz: test_rule[{0}] is PlayerBlock event for [{1}] OK",
                                    player_name, item));
            }

        }

        #endregion

        #region Process a single 'part' of rulz i.e. CONDITION or ACTION

        // check a condition (e.g. "Kit Recon 2") in the current rule
        // rule.trigger is already confirmed to be the current event (e.g. Kill, Spawn)
        private Boolean process_part(ParsedRule rule, PartClass p,
                                        String player_name,
                                        Kill k, String msg,
                                        ref Dictionary<SubstEnum, String> keywords)
        {
            // CATCH EXCEPTIONS
            try
            {
                String not = p.negated ? "Not " : "";
                Boolean return_val = false;
                String player_team_id = "-1";
                if (keywords.ContainsKey(SubstEnum.PlayerTeamKey))
                {
                    player_team_id = keywords[SubstEnum.PlayerTeamKey];
                }
                switch (p.part_type)
                {
                    case PartEnum.Headshot:
                        // test "Headshot"
                        return_val = p.negated ? !k.Headshot : k.Headshot;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}Headshot {2}",
                            player_name, not, return_val));
                        return return_val;

                    case PartEnum.Protected:
                        // test player os on reserved slots list
                        return_val = p.negated ? !protected_player(player_name) : protected_player(player_name);
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}Protected {2}",
                            player_name, not, return_val));
                        return return_val;

                    case PartEnum.Admin:
                        // test player is an admin
                        return_val = p.negated ? !is_admin(player_name) : is_admin(player_name);
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}Admin {2}",
                            player_name, not, return_val));
                        return return_val;

                    case PartEnum.Admins:
                        // test if any admins are currently online
                        return_val = p.negated ? !admins_present() : admins_present();
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}Admins {2}",
                            player_name, not, return_val));
                        return return_val;

                    case PartEnum.Team:
                        // test "team attack|defend"
                        Boolean team_matches = team_match(p.string_list, player_team_id);
                        return_val = p.negated ? !team_matches : team_matches;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}Actual team {2} versus {3} {4}",
                            player_name, not, team_key(player_team_id), keys_join(p.string_list), return_val));
                        return return_val;

                    case PartEnum.Ping:
                        // test "Ping N"
                        Int32 current_ping = players.ping(player_name);
                        return_val = p.negated ? current_ping < p.int1 : current_ping >= p.int1;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}Ping {2} versus limit {3} {4}",
                            player_name, not, current_ping, p.int1, return_val));
                        return return_val;

                    case PartEnum.Teamsize:
                        // test "Teamsize N"
                        Int32 min_teamsize = players.min_teamsize();
                        return_val = p.negated ? min_teamsize > p.int1 : min_teamsize <= p.int1;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}Teamsize {2} versus limit {3} {4}",
                            player_name, not, min_teamsize, p.int1, return_val));
                        return return_val;

                    case PartEnum.Map:
                        // test map name or filename contains string1
                        return_val = p.negated ? !map_match(p.string_list) : map_match(p.string_list);
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}Actual map {2} versus {3} {4}",
                            player_name, not,
                                                        current_map.PublicLevelName + " or " + current_map.FileName,
                                                        keys_join(p.string_list), return_val));
                        return return_val;

                    case PartEnum.MapMode:
                        // test "mapmode rush|conquest" 
                        return_val = p.negated ? !mapmode_match(p.string_list) : mapmode_match(p.string_list);
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}Actual MapMode {2} versus {3} {4}",
                            player_name, not, current_map_mode, keys_join(p.string_list), return_val));
                        return return_val;

                    case PartEnum.Kit:
                    case PartEnum.Weapon:
                    case PartEnum.Spec:
                    case PartEnum.Damage:
                        // test "Kit Recon 2" etc 
                        if (rule.trigger == TriggerEnum.Spawn)
                            // will check *player* item as well as team count (spawn)
                            return test_spawn_item(player_team_id, player_name, p);
                        else
                            // will also test kill item for TeamKill and Suicide
                            return test_kill_item(k, p);

                    case PartEnum.TeamKit:
                    case PartEnum.TeamWeapon:
                    case PartEnum.TeamSpec:
                    case PartEnum.TeamDamage:
                        return test_spawned_count(player_team_id, p);

                    case PartEnum.Range:
                        // test "Range > int1"
                        return_val = p.negated ? !(k.Distance < p.int1) : k.Distance > p.int1;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}range {2} > limit {3} {4}",
                            player_name, not, k.Distance, p.int1, return_val));
                        return return_val;

                    case PartEnum.Count:
                    case PartEnum.PlayerCount:
                        // check how many times PLAYER has triggered this rule
                        Int32 current_count = count_rule(player_name, rule.id);
                        Boolean count_valid = current_count > p.int1;
                        return_val = p.negated ? !count_valid : count_valid;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}PlayerCount {2} (actual {3}) {4}",
                            player_name, not, p.int1, current_count, return_val));
                        return return_val;

                    case PartEnum.TeamCount:
                        // check how many times PLAYER'S TEAM has triggered this rule
                        Int32 current_team_count = count_team_rule(players.team_id(player_name), rule.id);
                        Boolean count_team_valid = current_team_count > p.int1;
                        return_val = p.negated ? !count_team_valid : count_team_valid;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}TeamCount {2} (actual {3}) {4}",
                            player_name, not, p.int1, current_team_count, return_val));
                        return return_val;

                    case PartEnum.ServerCount:
                        // check how many times ALL PLAYERS have triggered this rule
                        Int32 current_server_count = count_server_rule(rule.id);
                        Boolean count_server_valid = current_server_count > p.int1;
                        return_val = p.negated ? !count_server_valid : count_server_valid;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}ServerCount {2} (Actual {3}) {4}",
                            player_name, not, p.int1, current_server_count, return_val));
                        return return_val;

                    case PartEnum.Rate:
                        // check condition "Rate X Y" i.e. X hits on this rule in Y seconds
                        add_rate(player_name, rule.id);
                        Boolean rate_valid = check_rate(player_name, rule.id, p.int1, p.int2);
                        return p.negated ? !rate_valid : rate_valid;

                    case PartEnum.Text:
                        // check say text condition e.g. "Text ofc 4 ever,teamwork is everything"
                        Int32 index = -1;
                        foreach (String t in p.string_list)
                        {
                            index = msg.ToLower().IndexOf(t.ToLower());
                            if (index >= 0 &&
                                    keywords[SubstEnum.Text] != null &&
                                    keywords[SubstEnum.Text].Length >= t.Length + 2)
                            {
                                // set up TargetText for TargetPlayer
                                keywords[SubstEnum.TargetText] = keywords[SubstEnum.Text].Substring(index + t.Length).Trim();
                            }
                            if (index >= 0) break;
                        }
                        return_val = p.negated ? index == -1 : index != -1;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}text {2} {3}",
                            player_name, not, keys_join(p.string_list), return_val));
                        return return_val;

                    case PartEnum.TargetPlayer:
                        // check TargetPlayer condition, i.e. can we extract a playername from the say text
                        // updated from v33 for find_players to return a LIST of player names
                        // if only ONE playername matches, then automatically add TargetConfirm to action list...
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] checking TargetPlayer[{1}]",
                            player_name, keywords[SubstEnum.Text]));
                        List<String> player_names = new List<String>();
                        if (p.string_list != null && p.string_list.Count != 0)
                        {
                            // here the 'targettext' is specified in the rule e.g. "TargetPlayer bambam"
                            player_names = find_players(rulz_vars.replace_vars(player_name,
                                                                                replace_keys(p.string_list[0], keywords)));
                        }
                        // note only 1 playername is allowed in condition
                        else                                               // because it could contain rulz_item_separator
                        {
                            // here the targettext from a previous "Text" condition will be used
                            // if successful, we will modify TargetText to be AFTER the playername match string
                            String[] t_words = null;
                            if (keywords.ContainsKey(SubstEnum.TargetText))
                            {
                                t_words = quoted_split(keywords[SubstEnum.TargetText]);
                            }
                            if (t_words != null && t_words.Length > 0)
                            {
                                player_names = find_players(t_words[0]);
                                if (keywords[SubstEnum.TargetText].Length - t_words[0].Length > 1)
                                {
                                    keywords[SubstEnum.TargetText] =
                                        keywords[SubstEnum.TargetText].Substring(t_words[0].Length + 1);
                                }
                                else
                                {
                                    keywords[SubstEnum.TargetText] = "";
                                }
                            }
                        }
                        return_val = p.negated ? player_names.Count == 0 : player_names.Count == 1;
                        keywords[SubstEnum.Target] = player_names.Count == 0 ? "" : player_names[0];
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1}TargetPlayer {2} {3} with {4}",
                            player_name, not, keys_join(p.string_list), return_val, String.Join(",", player_names.ToArray())));
                        return return_val;

                    case PartEnum.Set:
                        // set rulz variable
                        rulz_vars.set_value(player_name, p.string_list[0], p.string_list[1], keywords);
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] Set {1} {2}",
                            player_name, keys_join(p.string_list), true));
                        return true; // set always succeeds

                    case PartEnum.Incr:
                        rulz_vars.incr(player_name, p.string_list[0], keywords);
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] Increment {1} {2}",
                            player_name, p.string_list[0], true));
                        return true; // Incr always succeeds

                    case PartEnum.Decr:
                        rulz_vars.decr(player_name, p.string_list[0], keywords);
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] Decrement {1} {2}",
                            player_name, p.string_list[0], true));
                        return true; // Decr always succeeds

                    case PartEnum.Test: // aka If
                        // test var1 compare var2 (c.string_list[0..2])
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] IF %c% is [{1}]",
                            player_name, keywords[SubstEnum.Count]));
                        return_val = rulz_vars.test(player_name, p.string_list[0], p.string_list[1], p.string_list[2], keywords);
                        if (p.negated) return_val = !return_val;
                        WriteDebugInfo(String.Format("ProconRulz:     check_condition [{0}] {1} IF {2} {3}",
                            player_name, not, keys_join(p.string_list), return_val));
                        return return_val;

                    default:
                        take_action(player_name, p, keywords);
                        return true;
                }
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in process_part (ProconRulz will continue...)");
                WriteConsole("ProconRulz: process_part rule.unparsed_rule = " +
                                ((rule == null) ?
                                    "(rule=null)" :
                                    (rule.unparsed_rule == null ? "(null)" : "[" + rule.unparsed_rule + "]")
                                )
                            );
                WriteConsole("ProconRulz: process_part player_name = " +
                                ((player_name == null) ? "(null)" : "[" + player_name + "]"));
                WriteConsole("ProconRulz: process_part p.part_type = " +
                                ((p == null) ?
                                    "(p=null)" :
                                    (p.part_type == null ? "(null)" : p.part_type.ToString())
                                )
                            );
                WriteConsole("ProconRulz: process_part k.Killer.SoldierName = " +
                                ((k == null) ?
                                    "(k=null)" :
                                    ((k.Killer == null) ?
                                        "(k.Killer=null)" :
                                        ((k.Killer.SoldierName == null) ? "(null)" : ("[" + k.Killer.SoldierName + "]"))
                                    )
                                )
                            );
                PrintException(ex);
                return false;
            }

        }

        // test for an item at SPAWN
        // not_test is added to do a "Not Kit Recon" type test
        private Boolean test_spawn_item(String team_id, String player_name, PartClass c)
        {
            // e.g. "Kit Recon 2" => c.part_type = Kit, c.string1 = "Recon", c.int1 = 2
            Boolean found = spawn_counts.has_item(c.string_list, player_name);
            WriteDebugInfo(String.Format("ProconRulz: Test spawn item [KIT {0}] {1}",
                keys_join(c.string_list), found ? "Found" : "Not Found"));

            // if we have NOT found the item then return FALSE for a regular condition,
            // or TRUE for a 'Not' condition:
            if (!found) return c.negated;

            // item found

            // if no count specified in condition then we can return now 
            // (success for normal rule, failure for 'Not' rule)
            if (c.int1 == 0) return !c.negated;

            // proceed to check item count
            return test_spawned_count(team_id, c);
        }

        // test for an item on a KILL
        private Boolean test_kill_item(Kill k, PartClass c)
        {
            try
            {
                if (k == null) return false;

                String weapon_key = k.DamageType;
                if (weapon_key == "") weapon_key = null;

                Weapon weapon_used;
                try
                {
                    if (weapon_key == null) weapon_used = null;
                    else weapon_used = weaponDefines[weapon_key];
                }
                catch { weapon_used = null; }

                String weapon_descr = weapon_desc(weapon_key);

                String weapon_kit;
                try
                {
                    if (weapon_used == null) weapon_kit = "No weapon kit";
                    else weapon_kit = item_key(weapon_used.KitRestriction);
                }
                catch { weapon_kit = "No weapon kit"; }

                String damage;
                try
                {
                    damage = item_key(weapon_used.Damage);
                }
                catch { damage = "No damage key"; }

                switch (c.part_type)
                {
                    case PartEnum.Weapon:
                        WriteDebugInfo(String.Format("ProconRulz: Test kill item [WEAPON {0}]",
                            keys_join(c.string_list)));
                        if (weapon_key == null) return c.negated;

                        if (keys_match(weapon_key, c.string_list))
                        {
                            WriteDebugInfo(String.Format("ProconRulz: Test kill item [WEAPON {0}] found",
                                weapon_key));
                            break;
                        }
                        // not found, so return false unless c.negated
                        return c.negated;

                    case PartEnum.Damage:
                        WriteDebugInfo(String.Format("ProconRulz: Test kill item [DAMAGE {0}]",
                            keys_join(c.string_list)));
                        if (keys_match(damage, c.string_list))
                        {
                            WriteDebugInfo(String.Format("ProconRulz: Test kill item [DAMAGE {0}] found",
                                damage));
                            break;
                        }
                        return c.negated;

                    case PartEnum.Kit:
                        WriteDebugInfo(String.Format("ProconRulz: Test kill item [KIT {0}]", keys_join(c.string_list)));
                        String test_kit = weapon_kit;
                        // either use the kit type of the weapon, or the kit the player spawned with
                        if (player_kit.ContainsKey(k.Killer.SoldierName))
                            test_kit = player_kit[k.Killer.SoldierName];
                        if (keys_match(test_kit, c.string_list))
                        {
                            WriteDebugInfo(String.Format("ProconRulz: Test kill item [KIT {0}] found",
                                test_kit));
                            break;
                        }
                        return c.negated;

                    default: // item type can be None
                        WriteDebugInfo(String.Format("ProconRulz: Test kill item [ignored] OK"));
                        return true;
                } // end switch on item type

                Boolean success;
                if (c.int1 == 0)
                {
                    success = true;
                    if (c.negated) success = !success;
                    WriteDebugInfo(String.Format("ProconRulz: Test kill item {0}", success));
                    // The item is being counted, and the count is above the limit in the rule
                    return success;
                }
                // check the item_limit value
                WriteDebugInfo(String.Format("ProconRulz: Test kill item [{0}] has {1}({2}) versus rule limit {3}",
                        k.Killer.SoldierName,
                        keys_join(c.string_list),
                        count_kill_items(k.Killer.SoldierName, c.string_list),
                        c.int1));
                success = count_kill_items(k.Killer.SoldierName, c.string_list) > c.int1;
                if (c.negated) success = !success;
                return success;
            }
            catch (Exception ex)
            {
                WriteConsole("ProconRulz: recoverable exception in test_kill_item");
                PrintException(ex);
                return false;
            }
        }

        // test for an item at SPAWN
        // not_test is added to do a "Not Kit Recon" type test
        private Boolean test_spawned_count(String team_id, PartClass c)
        {
            // e.g. "TeamKit Recon 2" => c.part_type = Kit, c.string1 = "Recon", c.int1 = 2

            // check the item limit value
            Int32 count = spawn_counts.count(c.string_list, team_id);
            WriteDebugInfo(String.Format("ProconRulz: Test spawn item count {0} versus limit {1}",
                count, c.int1));
            if (count <= c.int1) return c.negated;

            return !c.negated; // i.e. spawn item above count, non-negated rule => return true
        }


        #endregion

        #region     Track various real-time counts of kills, rule rates etc

        #region Count resets, on startup and round change
        //**********************************************************************************************
        //**********************************************************************************************
        //   blocks, spawn counts and kill counts routines to keep track of 
        //   spawn(team) and kill(player) counts
        //**********************************************************************************************
        //**********************************************************************************************

        // zero all counts but keep keys in spawn counts - e.g. on round start, map load
        private void zero_counts()
        {
            spawn_counts.zero(); // remove entries for which players  have spawned with watched items
            kill_counts.Clear(); // empty out all kill counts for each player/item
            player_blocks.Clear(); // reset all player blocks
            rule_counts.Clear(); // reset number of times players have triggered rulz to 0
            //rule_times.Clear(); // reset timestamps of prior rules firing
            player_kit.Clear(); // reset the kit each player spawned with
        }

        // reset e.g. on plugin startup and loading new rules
        private void reset_counts()
        {
            spawn_counts.reset();
            kill_counts.Clear();
            player_blocks.Clear();
            rule_counts.Clear();
            rule_times.Clear(); // reset timestamps of prior rules firing
            player_kit.Clear(); // reset the kit each player spawned with
        }

        #endregion

        #region Kill counts for watched items

        private void add_kill_count(String player_name, String item_name)
        {
            String item_lcase = item_name == null ? "" : item_name.ToLower();
            List<String> item_names = new List<String>();
            item_names.Add(item_lcase);
            WriteDebugInfo(String.Format("ProconRulz:  add_kill_count to [{0}({1})] for [{2}]",
                                item_name,
                                count_kill_items(player_name, item_names),
                                player_name));
            if (item_lcase == "none" || item_name == null || item_name == "" || player_name == null ||
                item_lcase == "no kit key" || item_lcase == "no weapon key" ||
                item_lcase == "no damage key" || item_lcase == "no spec key" ||
                player_name == "") return;
            if (!kill_counts.ContainsKey(player_name))
            {
                kill_counts[player_name] = new Dictionary<String, Int32>();
            }
            if (!kill_counts[player_name].ContainsKey(item_lcase))
            {
                kill_counts[player_name].Add(item_lcase, 1);
                return;
            }
            kill_counts[player_name][item_lcase] = kill_counts[player_name][item_lcase] + 1;
        }

        // return total count of kills with these items
        private Int32 count_kill_items(String player_name, List<String> item_names)
        {
            if (item_names == null || item_names.Count == 0 || player_name == null || player_name == "")
                return 0;
            if (!kill_counts.ContainsKey(player_name)) return 0;
            Int32 count = 0;
            foreach (String i in item_names)
            {
                String item_lcase = i.ToLower();
                if (!kill_counts[player_name].ContainsKey(item_lcase)) continue;
                count += kill_counts[player_name][item_lcase];
            }
            return count;
        }

        #endregion

        #region Player rule counts

        // **********************************************************************************************
        // *************************** RULE COUNTS               ****************************************
        private void add_rule_count(String player_name, Int32 rule_id)
        {
            if (player_name == null) // e.g. this could be an "On Round" rule
            {
                // if no player name then we can only update the 'server' count
                add_rule_count("proconrulz_server", rule_id);
                return;
            }
            // if this player is NOT special (i.e. team or server), then 
            // also add rule counts for the server and this player's team

            try
            {
                // this player isn't special, then add_rule_count for team and server
                if (!special_player(player_name))
                {
                    add_rule_count("proconrulz_server", rule_id);
                    add_rule_count(team_id_to_special_name(players.team_id(player_name)), rule_id);
                }
            }
            catch
            {
                WriteConsole(
                    String.Format("ProconRulz: RECOVERABLE ERROR exception in add_rule_count({0},{1})",
                        player_name, rule_id));
            }

            WriteDebugInfo(String.Format("ProconRulz:  add_rule_count to [{0}({1})] for [{2}]",
                                rule_id,
                                count_rule(player_name, rule_id),
                                player_name));
            if (!rule_counts.ContainsKey(player_name))
            {
                rule_counts[player_name] = new Dictionary<Int32, Int32>();
            }
            if (!rule_counts[player_name].ContainsKey(rule_id))
            {
                rule_counts[player_name].Add(rule_id, 1);
                return;
            }
            rule_counts[player_name][rule_id] = rule_counts[player_name][rule_id] + 1;
            return;
        }

        private Int32 count_rule(String player_name, Int32 rule_id)
        {
            String p = player_name;
            if (player_name == null) player_name = "proconrulz_server"; // e.g. could happen with "On Round" rule
            if (!rule_counts.ContainsKey(player_name)) return 0;
            if (!rule_counts[player_name].ContainsKey(rule_id)) return 0;
            return rule_counts[player_name][rule_id];
        }


        // manage counts for TEAM and SERVER
        private Int32 count_team_rule(String team_id, Int32 rule_id)
        {
            return count_rule(team_id_to_special_name(team_id), rule_id);
        }

        private Int32 count_server_rule(Int32 rule_id)
        {
            return count_rule("proconrulz_server", rule_id);
        }

        // we store total counts for the team and server, as well as the 'player counts'
        // the count for a team is stored as if there's a payer called "proconrulz_team_1" etc.
        private String team_id_to_special_name(String team_id)
        {
            if (team_id == null) return "proconrulz_team_unknown";
            return String.Format("proconrulz_team_{0}", team_id);
        }

        // returns true if this player name is a special name (i.e. team or server)
        private Boolean special_player(String player_name)
        {
            if (player_name == null) return false;
            return (player_name + "************").Substring(0, 10) == "proconrulz";
        }

        #endregion

        #region Rates functions

        // **********************************************************************************************
        // *************************** RATES FUNCTIONS           ****************************************

        // add a value into the 'rule_times' global for the most recent time 
        // this rule was triggered for this player
        private void add_rate(String player_name, Int32 rule_id)
        {
            WriteDebugInfo(String.Format("ProconRulz:  add_rate to rule {0} for [{1}]",
                                rule_id,
                                player_name));
            // need to be careful if this gets called for an On Round event (i.e. no playername)
            if (player_name == null) return;
            if (player_name == "") return;
            if (!rule_times.ContainsKey(player_name))
            {
                rule_times[player_name] = new Dictionary<Int32, DateTime[]>();
            }
            if (!rule_times[player_name].ContainsKey(rule_id))
            {
                rule_times[player_name][rule_id] = new DateTime[RATE_HISTORY];
                rule_times[player_name][rule_id][0] = DateTime.Now;
                return;
            }
            // at the moment we shuffle all the times up by 1, and give entry [0] the current time
            for (Int32 i = RATE_HISTORY - 1; i > 0; i--)
            {
                rule_times[player_name][rule_id][i] = rule_times[player_name][rule_id][i - 1];
            }
            rule_times[player_name][rule_id][0] = DateTime.Now;
            return;
        }

        // return true IF player_name has triggered rule[rule_id] rate_count times over rate_time seconds
        private Boolean check_rate(String player_name, Int32 rule_id, Int32 rate_count, Int32 rate_time)
        {
            WriteDebugInfo(String.Format("ProconRulz:  check_rate to rule {0} for [{1}]",
                                rule_id,
                                player_name));
            if (player_name == null || player_name == "" || !rule_times.ContainsKey(player_name))
            {
                WriteDebugInfo(String.Format("ProconRulz:  check_rate for player [{1}] not in rule_times!",
                                    player_name));
                return false;
            }
            if (!rule_times[player_name].ContainsKey(rule_id))
            {
                WriteDebugInfo(String.Format("ProconRulz:  check_rate no time for rule {0} for player [{1}]",
                                    rule_id,
                                    player_name));
                return false;
            }
            try
            {
                // if we're checking "Rate 5 10" (rule hit 5 times in 10 seconds)
                // we look up the timestamp of the 4th previous hit, 
                // and subtract that from Now, and see if thats <10 seconds
                DateTime prev = rule_times[player_name][rule_id][rate_count - 1];
                DateTime now = DateTime.Now;
                Double period = (now.Subtract(prev)).TotalSeconds;
                WriteDebugInfo(
                    String.Format("ProconRulz:  check_rate to rule {0} for [{1}], period {2} seconds (versus min {3})",
                                    rule_id,
                                    player_name,
                                    period,
                                    rate_time));
                return period < rate_time;
            }
            catch { return false; }
        }

        // we leave the rates accumulating through round changes, 
        // so we need a way of scrubbing out players that have
        // left the server (even though their arrays of timestamps will now be static).
        // scrub_rates will be given a list of players *currently* on the server, 
        // and will remove other entries
        // currently called on loading level
        private void scrub_rates(List<String> player_list)
        {
            List<String> rates_players = new List<String>(rule_times.Keys);
            foreach (String rates_player in rates_players)
            {
                if (!player_list.Contains(rates_player)) rule_times.Remove(rates_player);
            }
        }

        #endregion

        #region PlayerBlocks (i.e. players can be blocked from spawning with a given item

        //*********************************************************************************************
        // player_blocks Dictionary <string playername, List<string> item_names>
        // add_block(player_name, item_name)
        // remove_blocks(player_name)
        // test_block (player_name, item_name)
        // clear_blocks()

        private void add_block(String player_name, String item_name)
        {
            if (!player_blocks.ContainsKey(player_name))
            {
                player_blocks[player_name] = new List<String>();
            }
            player_blocks[player_name].Add(item_name);
            return;
        }

        private void remove_blocks(String player_name)
        {
            player_blocks.Remove(player_name);
        }

        private Boolean test_block(String player_name, String item_name)
        {
            if (!player_blocks.ContainsKey(player_name)) return false;
            if (player_blocks[player_name].Contains(item_name)) return true;
            return false;
        }

        private void clear_blocks() // remove all blocks at start of round etc.
        {
            player_blocks.Clear();
        }

        // track kit player spawned with
        private String spawned_kit(String player_name)
        {
            if (player_kit.ContainsKey(player_name)) return player_kit[player_name];
            else return "No kit key";
        }

        #endregion

        #endregion
    }
}
