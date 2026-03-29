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

        #region Parse the rulz
        //**********************************************************************************************
        //**********************************************************************************************
        //   PARSE THE RULES THAT HAVE BEEN READ FROM THE PROCON SETTINGS (Configs\server_ip.cfg)
        //**********************************************************************************************
        //**********************************************************************************************

        // simple routine to output a console message when parse_rules chokes on a user input rule
        void parse_error(String key, String rule)
        {
            WriteConsole(
                String.Format("ProconRulz ^1SKIPPING RULE: Bad \"{0}\" clause in your rule \"{1}\" ",
                key, rule));
            return;
        }

        // Here is where we try and parse the list of unparsed rules that 
        // have been read from the server_ip.cfg
        // Input is the List<string> unparsed_rules, as read from server_ip.cfg
        // Output is the List<ParsedRule> parsed_rules
        // each rule is split into parts, with each part split into fragments e.g.
        // "team attack;teamsize 8;kit recon 2;say No More Snipers!;kill"
        // the first part is "team attack"
        // the first fragment is "team"
        void parse_rules()
        {
            Int32 rule_id = 1; // initialise first value of rule identifier

            // start with a default trigger of "On Spawn" and update as rulz with triggers are parsed
            rule_prefix = new ParsedRule();

            String rule_string_buffer = "empty"; // used to hold current rule for WriteConsole in case of exception

            try
            {
                if (!plugin_enabled) return;
                //WriteLog(String.Format("ProconRulz: Parsing your rules"));
                parsed_rules.Clear(); // start with an empty list

                ParsedRule parsed_rule = new ParsedRule(); // start with blank parsed rule
                parsed_rule.id = rule_id++;

                Boolean first_rule = true;

                // Concatenate user rules from settings AND user rulz files
                List<String> all_unparsed = new List<String>(unparsed_rules);
                foreach (KeyValuePair<String, String[]> rulz in filez_rulz)
                {
                    all_unparsed.AddRange(rulz.Value);
                }

                WriteConsole("ProconRulz: loading " + all_unparsed.Count.ToString() + " rulz");

                foreach (String rule_string in all_unparsed)
                {
                    rule_string_buffer = rule_string;
                    WriteDebugInfo("ProconRulz: parsing " + rule_string);

                    if ((rule_string.Length > 0 && rule_string[0] != '+') || rule_string.Length == 0)
                    { // this is not a rule continuation, store accumulated rule
                        if (!first_rule)
                        {
                            parsed_rules.Add(parsed_rule); // add previous rule
                            WriteDebugInfo("ProconRulz: storing rule " + parsed_rule.id.ToString() +
                                            " as " + parsed_rule.unparsed_rule);
                            parsed_rule = new ParsedRule(); // start new rule
                            parsed_rule.trigger = TriggerEnum.Void; // we can check this to see if rule has trigger
                            parsed_rule.id = rule_id++;
                        }
                    }
                    if (parse_rule(ref parsed_rule, rule_string))
                    {
                        first_rule = false;
                        WriteDebugInfo("ProconRulz: parsed ok");
                        parsed_rule.unparsed_rule += rule_string;
                        // if rule did NOT have an On trigger, prepend the rule_prefix...
                        if (parsed_rule.trigger == TriggerEnum.Void)
                        {
                            parsed_rule.trigger = rule_prefix.trigger;
                            List<PartClass> l = new List<PartClass>();
                            foreach (PartClass p in rule_prefix.parts) l.Add(p);
                            foreach (PartClass p in parsed_rule.parts) l.Add(p);
                            parsed_rule.parts = l;
                        }
                        else
                        {
                            // rule DID have a trigger, so use this rule as new rule_prefix...
                            rule_prefix.trigger = parsed_rule.trigger;
                            rule_prefix.parts = new List<PartClass>(parsed_rule.parts);
                        }

                    }
                } // loop to next rule
                if (!first_rule) // flush last rule
                {
                    parsed_rules.Add(parsed_rule);
                    WriteDebugInfo("ProconRulz: storing last rule " + parsed_rule.id.ToString() +
                                    " as " + parsed_rule.unparsed_rule);
                }
                WriteConsole(String.Format("ProconRulz: {0} rules loaded", rule_id - 1));
                // run 'On Init' rulz
                OnInit();
            }
            catch (Exception ex)
            {
                WriteConsole(String.Format("^1ProconRulz: Exception occurred parsing your rules"));
                WriteConsole(String.Format("^1ProconRulz: rule String was: " + rule_string_buffer));
                PrintException(ex);
            }
        }

        // parse a single rule string e.g. "On Kill;Damage SniperRifle;Kill 300"
        // return 'true' if parsed successfully
        private Boolean parse_rule(ref ParsedRule parsed_rule, String rule_string)
        {
            if (rule_string == null || rule_string.Length < 4)
            {
                return false;
            }
            // only parse if this rule is not a comment
            if (rule_string[0] == '#') parsed_rule.comment = true;
            else
            {
                String parse_string;
                if (rule_string[0] == '+')
                {
                    parse_string = ";" + rule_string.Substring(1);
                }
                else
                {
                    parse_string = rule_string;
                }
                String[] parts
                    = parse_string.Replace("%3b", ";").Split(
                        new Char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (String part in parts)
                {
                    // note we use Trim() to take off leading/trailing whitespace from part
                    if (!parse_part(ref parsed_rule, part.Trim(), false)) return false;
                } // loop to next part
            }    // looping through rule parts completed - now add to list "parsed_rules"
            return true;
        }

        // parse a 'part' of a rule, e.g. "Damage SniperRifle" or "TargetAction Kill 300"
        // returns 'true' if parse succeeded
        private Boolean parse_part(ref ParsedRule parsed_rule, String part, Boolean target_action)
        {

            try
            {
                Boolean rule_fail = false; // flag used to skip rest of rule on a parse error
                Boolean parsed_int = false; // flag used to confirm a part format with optional Int32 was parsed ok

                // create action to be added to parsed_rule if we need it
                PartClass new_action = new PartClass();
                new_action.target_action = target_action;

                String[] fragments = quoted_split(part); // v40b update to allow quoted strings
                if (fragments == null || fragments.Length == 0) return true;
                switch (fragments[0].ToLower())
                {
                    // TRIGGER ("On Spawn" or "On Kill")
                    case "on": // trigger the rule on Spawn or a Kill
                        rule_fail = !parse_on(ref parsed_rule, fragments);
                        break;

                    // CONDITIONS: i.e. what is needed for this rule to fire
                    case "headshot": // e.g. "Headshot"
                        rule_fail = !parse_headshot(ref parsed_rule, false);
                        break;
                    case "protected": // e.g. "Protected" - this player is on reserved slots list
                        rule_fail = !parse_protected(ref parsed_rule, false);
                        break;
                    case "admin": // e.g. "Admin" - this player is an admin
                        rule_fail = !parse_admin(ref parsed_rule, false);
                        break;
                    case "admins": // e.g. "Admins" - admins are on the server
                        rule_fail = !parse_admins(ref parsed_rule, false);
                        break;
                    case "team": // e.g. "Team Attack" or defend
                        rule_fail = !parse_team(ref parsed_rule, fragments, false);
                        break;
                    case "ping": // e.g. "Ping 300"
                        rule_fail = !parse_ping(ref parsed_rule, fragments, false);
                        break;
                    case "teamsize":
                        // e.g. "Teamsize 8" this rule only applies to teams this small or smaller
                        rule_fail = !parse_teamsize(ref parsed_rule, fragments, false);
                        break;
                    case "map": // e.g. "Map Oasis" this rule only applies to map oasis
                        rule_fail = !parse_map(ref parsed_rule, part, false);
                        break;
                    case "mapmode": // e.g. "MapMode Rush" this rule only applies to maps in Rush mode
                        rule_fail = !parse_mapmode(ref parsed_rule, fragments, false);
                        break;
                    case "kit": // e.g. "Kit Recon 2" - max 2 recons on the team
                        rule_fail = !parse_kit(ref parsed_rule, fragments, false);
                        break;
                    case "weapon": // e.g. "Weapon AUG 3"
                        rule_fail = !parse_weapon(ref parsed_rule, fragments, false);
                        break;
                    case "spec": // e.g. "Spec sp_vdamage 3"
                        rule_fail = !parse_spec(ref parsed_rule, fragments, false);
                        break;
                    case "damage": // e.g. "Damage SniperRifle 8"
                        rule_fail = !parse_damage(ref parsed_rule, fragments, false);
                        break;
                    case "teamkit": // e.g. "TeamKit Recon 2" - max 2 recons on the team
                        rule_fail = !parse_teamkit(ref parsed_rule, fragments, false);
                        break;
                    case "teamweapon": // e.g. "TeamWeapon AUG 3"
                        rule_fail = !parse_teamweapon(ref parsed_rule, fragments, false);
                        break;
                    case "teamspec": // e.g. "TeamSpec sp_vdamage 3"
                        rule_fail = !parse_teamspec(ref parsed_rule, fragments, false);
                        break;
                    case "teamdamage": // e.g. "TeamDamage SniperRifle 8"
                        rule_fail = !parse_teamdamage(ref parsed_rule, fragments, false);
                        break;
                    case "range": // e.g. "Range 100"
                        rule_fail = !parse_range(ref parsed_rule, fragments, false);
                        break;
                    case "not": // e.g. "Not Damage SniperRifle"
                        rule_fail = !parse_not(ref parsed_rule, fragments, part);
                        break;
                    case "count": // e.g. "Count 8" - how many times PLAYER can trigger this rule
                    case "playercount":
                        // e.g. "PlayerCount 8" - how many times PLAYER can trigger this rule
                        rule_fail = !parse_count(ref parsed_rule, fragments, false);
                        break;
                    case "teamcount": // e.g. "TeamCount 8" - how many times TEAM can trigger this rule
                        rule_fail = !parse_teamcount(ref parsed_rule, fragments, false);
                        break;
                    case "servercount":
                        // e.g. "ServerCount 8" - how many times SERVER can trigger this rule
                        rule_fail = !parse_servercount(ref parsed_rule, fragments, false);
                        break;
                    case "playerfirst":
                    case "teamfirst":
                    case "serverfirst":
                    case "playeronce":
                        rule_fail = !parse_first(ref parsed_rule, fragments[0].ToLower(), false);
                        break;
                    case "rate": // e.g. "Rate 5 20" this rule triggered 5 times in 20 seconds
                        rule_fail = !parse_rate(ref parsed_rule, fragments, false);
                        break;
                    case "text": // e.g. "On Say;Text ofc;Yell OFc 4 Ever"
                        rule_fail = !parse_text(ref parsed_rule, part, false);
                        break;
                    case "targetplayer": // e.g. "TargetPlayer" (extract playername from say text)
                        rule_fail = !parse_targetplayer(ref parsed_rule, part, false);
                        break;
                    case "incr": // e.g. "Incr kill_count"
                        rule_fail = !parse_incr(ref parsed_rule, fragments);
                        break;
                    case "decr": // e.g. "Decr kill_count"
                        rule_fail = !parse_decr(ref parsed_rule, fragments);
                        break;
                    case "set": // e.g. "Set kill_count 0"
                        rule_fail = !parse_set(ref parsed_rule, fragments);
                        break;
                    case "if": // e.g. "If kill_count > 7"
                        rule_fail = !parse_test(ref parsed_rule, part, false);
                        break;

                    // ACTIONS i.e. what to do when conditions are true
                    // ************************************************
                    case "say": // e.g. "Say No more Snipers!"
                        if (part.Length < 5)
                        {
                            parse_error("Say", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.Say;
                        new_action.string_list.Add(part.Substring(4));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "playersay": // e.g. "PlayerSay No more Snipers!"
                        if (part.Length < 11)
                        {
                            parse_error("PlayerSay", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.PlayerSay;
                        new_action.string_list.Add(part.Substring(10));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "squadsay": // e.g. "SquadSay No more Snipers!"
                        if (part.Length < 10)
                        {
                            parse_error("SquadSay", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.SquadSay;
                        new_action.string_list.Add(part.Substring(9));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "teamsay": // e.g. "TeamSay No more Snipers!"
                        if (part.Length < 9)
                        {
                            parse_error("TeamSay", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.TeamSay;
                        new_action.string_list.Add(part.Substring(8));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "victimsay": // e.g. "VictimSay You were killed by %p%, range %r%!"
                        if (part.Length < 11)
                        {
                            parse_error("VictimSay", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.VictimSay;
                        new_action.string_list.Add(part.Substring(10));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "adminsay": // e.g. "AdminSay HACKER WARNING on %p% (5 headshots in 15 seconds)!!"
                        if (part.Length < 10)
                        {
                            parse_error("AdminSay", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.AdminSay;
                        new_action.string_list.Add(part.Substring(9));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "yell":
                        if (part.Length < 6)
                        {
                            parse_error("Yell", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.Yell;
                        parsed_int = false; // assume parsing yell_delay fails
                        try
                        {
                            new_action.int1 = Int32.Parse(fragments[1]); // try and pick up yell delay (seconds)
                            new_action.string_list.Add(String.Join(" ", fragments, 2, fragments.Length - 2)); // rest of String
                            parsed_int = true;
                        }
                        catch { }
                        if (!parsed_int) // we didn't parse a yell delay so use default
                        {
                            new_action.int1 = yell_delay;
                            new_action.string_list.Add(part.Substring(5));
                        }
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "playeryell": // e.g. "PlayerYell No more Snipers!"
                        if (part.Length < 12)
                        {
                            parse_error("PlayerYell", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.PlayerYell;
                        parsed_int = false; // assume parsing yell_delay fails
                        try
                        {
                            new_action.int1 = Int32.Parse(fragments[1]); // try and pick up yell delay (seconds)
                            new_action.string_list.Add(String.Join(" ", fragments, 2, fragments.Length - 2)); // rest of String
                            parsed_int = true;
                        }
                        catch { }
                        if (!parsed_int) // we didn't parse a yell delay so use default
                        {
                            new_action.int1 = yell_delay;
                            new_action.string_list.Add(part.Substring(11));
                        }
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "squadyell": // e.g. "SquadYell No more Snipers!"
                        if (part.Length < 11)
                        {
                            parse_error("SquadYell", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.SquadYell;
                        parsed_int = false; // assume parsing yell_delay fails
                        try
                        {
                            new_action.int1 = Int32.Parse(fragments[1]); // try and pick up yell delay (seconds)
                            new_action.string_list.Add(String.Join(" ", fragments, 2, fragments.Length - 2)); // rest of String
                            parsed_int = true;
                        }
                        catch { }
                        if (!parsed_int) // we didn't parse a yell delay so use default
                        {
                            new_action.int1 = yell_delay;
                            new_action.string_list.Add(part.Substring(10));
                        }
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "teamyell": // e.g. "TeamYell No more Snipers!"
                        if (part.Length < 10)
                        {
                            parse_error("TeamYell", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.TeamYell;
                        parsed_int = false; // assume parsing yell_delay fails
                        try
                        {
                            new_action.int1 = Int32.Parse(fragments[1]); // try and pick up yell delay (seconds)
                            new_action.string_list.Add(String.Join(" ", fragments, 2, fragments.Length - 2)); // rest of String
                            parsed_int = true;
                        }
                        catch { }
                        if (!parsed_int) // we didn't parse a yell delay so use default
                        {
                            new_action.int1 = yell_delay;
                            new_action.string_list.Add(part.Substring(9));
                        }
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "log":
                        if (part.Length < 5)
                        {
                            parse_error("Log", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.Log;
                        new_action.string_list.Add(part.Substring(4));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "all":  // say and yell and log
                        if (part.Length < 5)
                        {
                            parse_error("All", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.All;
                        new_action.string_list.Add(part.Substring(4));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "both": // say and yell
                        if (part.Length < 6)
                        {
                            parse_error("Both", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.Both;
                        new_action.string_list.Add(part.Substring(5));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "playerboth": // say and yell to a player
                        if (part.Length < 12)
                        {
                            parse_error("PlayerBoth", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.PlayerBoth;
                        new_action.string_list.Add(part.Substring(11));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "kill": // kill player that triggered this rule
                        new_action.part_type = PartEnum.Kill;
                        if (fragments.Length == 2) new_action.string_list.Add(fragments[1]);
                        else new_action.string_list.Add(kill_delay.ToString());
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "kick": // kick player that triggered this rule
                        new_action.part_type = PartEnum.Kick;
                        if (fragments.Length >= 2) new_action.string_list.Add(part.Substring(5));
                        else new_action.string_list.Add("Kicked automatically");
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "ban": // ban player that triggered this rule
                        new_action.part_type = PartEnum.Ban;
                        if (fragments.Length >= 2) new_action.string_list.Add(part.Substring(4));
                        else new_action.string_list.Add("[%p%] Banned automatically");
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "tempban": // temp ban player that triggered this rule
                        new_action.part_type = PartEnum.TempBan;
                        // try and pick up number of seconds for ban from fragments[1]
                        Boolean tempban_default = true;
                        if (fragments.Length >= 2)
                            try
                            {
                                new_action.int1 = Int32.Parse(fragments[1]);
                                tempban_default = false;
                            }
                            catch
                            { new_action.int1 = ban_delay; }
                        else new_action.int1 = ban_delay;

                        // now try and dig out message
                        // "TempBan" or "TempBan 7777"
                        if (fragments.Length == 1 || (!tempban_default && fragments.Length == 2))
                        {
                            new_action.string_list.Add("[%p%] automatic temp ban");
                        }
                        // TempBan <message>
                        else if (tempban_default && fragments.Length >= 2)
                        {
                            new_action.string_list.Add(part.Substring(8));
                        }
                        // TempBan <N> <message>
                        else if (!tempban_default && fragments.Length > 2)
                        {
                            new_action.string_list.Add(part.Substring(8 + fragments[1].Length));
                        }
                        else
                        {
                            parse_error("TempBan", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "pbban": // ban player that triggered this rule via PunkBuster
                        new_action.part_type = PartEnum.PBBan;
                        if (fragments.Length >= 2) new_action.string_list.Add(part.Substring(6));
                        else new_action.string_list.Add("[%p%] Banned automatically");
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "pbkick": // temp ban player that triggered this rule (i.e. kick with timeout via PunkBuster)
                        new_action.part_type = PartEnum.PBKick;
                        // try and pick up number of MINUTES for ban from fragments[1]
                        Boolean pbkick_default = true;
                        if (fragments.Length >= 2)
                            try
                            {
                                new_action.int1 = Int32.Parse(fragments[1]);
                                pbkick_default = false;
                            }
                            catch
                            { new_action.int1 = 0; } // default is NO temp ban on PB kick
                        else new_action.int1 = 0;

                        // now try and dig out message
                        // "PBKick" or "PBKick 77"
                        if (fragments.Length == 1 || (!pbkick_default && fragments.Length == 2))
                        {
                            new_action.string_list.Add("[%p%] automatic temp kick/ban");
                        }
                        // PBKick <message>
                        else if (pbkick_default && fragments.Length >= 2)
                        {
                            new_action.string_list.Add(part.Substring(7));
                        }
                        // PBKick <N> <message>
                        else if (!pbkick_default && fragments.Length > 2)
                        {
                            new_action.string_list.Add(part.Substring(7 + fragments[1].Length));
                        }
                        else
                        {
                            parse_error("PBKick", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "playerblock": // block player name from using item
                        new_action.part_type = PartEnum.PlayerBlock;
                        if (fragments.Length == 2) new_action.string_list.Add(fragments[1]); // item key
                        else new_action.string_list.Add("unknown");
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "targetconfirm": // trigger previous TargetAction actions from this player
                        if (fragments.Length != 1) parse_error("TargetConfirm", parsed_rule.unparsed_rule);
                        new_action.part_type = PartEnum.TargetConfirm;
                        new_action.string_list.Add("");
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "targetcancel": // cancel previous TargetAction actions from this player
                        if (fragments.Length != 1) parse_error("TargetCancel", parsed_rule.unparsed_rule);
                        new_action.part_type = PartEnum.TargetCancel;
                        new_action.string_list.Add("");
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "targetaction": // store delayed actions on target from this player
                        rule_fail = !parse_part(ref parsed_rule, part.Substring(13), true);
                        break;
                    case "exec": // e.g. "Exec levelVars.set level levels/mp_007gr vehiclesDisabled false"
                        if (part.Length < 6)
                        {
                            parse_error("Execute", parsed_rule.unparsed_rule); rule_fail = true; break;
                        }
                        new_action.part_type = PartEnum.Execute;
                        new_action.string_list.Add(part.Substring(5));
                        new_action.has_count = has_a_count(new_action);
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "continue":
                        new_action.part_type = PartEnum.Continue;
                        new_action.string_list.Add("");
                        parsed_rule.parts.Add(new_action);
                        break;
                    case "end":
                        new_action.part_type = PartEnum.End;
                        new_action.string_list.Add("");
                        parsed_rule.parts.Add(new_action);
                        break;
                    default:
                        WriteConsole(String.Format("^1ProconRulz: Unrecognised rule {0}",
                            parsed_rule.unparsed_rule));
                        rule_fail = true;
                        break;
                }
                return !rule_fail;
            }
            catch (Exception ex)
            {
                WriteConsole(String.Format("^1ProconRulz: Exception occurred parsing your rules"));
                WriteConsole(String.Format("^1ProconRulz: Rule was \"{0}\"", parsed_rule.unparsed_rule));
                WriteConsole(String.Format("^1ProconRulz: Part that failed was \"{0}\"", part));
                PrintException(ex);
                return false;
            }

        }

        private Boolean parse_on(ref ParsedRule parsed_rule, String[] fragments)
        {
            //WriteDebugInfo(String.Format("ProconRulz: Parsing On"));
            if (fragments.Length != 2)
            {
                parse_error("On", parsed_rule.unparsed_rule);
                return false;
            }
            if (fragments[1].ToLower().StartsWith("roundover"))
            {
                parsed_rule.trigger = TriggerEnum.RoundOver;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("round"))
            {
                parsed_rule.trigger = TriggerEnum.Round;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("join"))
            {
                parsed_rule.trigger = TriggerEnum.Join;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("leave"))
            {
                parsed_rule.trigger = TriggerEnum.Leave;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("spawn"))
            {
                parsed_rule.trigger = TriggerEnum.Spawn;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("kill"))
            {
                parsed_rule.trigger = TriggerEnum.Kill;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("teamkill"))
            {
                parsed_rule.trigger = TriggerEnum.TeamKill;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("suicide"))
            {
                parsed_rule.trigger = TriggerEnum.Suicide;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("playerblock"))
            {
                parsed_rule.trigger = TriggerEnum.PlayerBlock;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("say"))
            {
                parsed_rule.trigger = TriggerEnum.Say;
                return true;
            }
            if (fragments[1].ToLower().StartsWith("init"))
            {
                parsed_rule.trigger = TriggerEnum.Init;
                return true;
            }
            parse_error("On", parsed_rule.unparsed_rule);
            return false;
        }

        private Boolean parse_headshot(ref ParsedRule parsed_rule, Boolean negated)
        {
            PartClass c = new PartClass();
            c.part_type = PartEnum.Headshot;
            c.negated = negated;
            parsed_rule.parts.Add(c);
            return true;
        }

        // this player is on reserved slots list
        private Boolean parse_protected(ref ParsedRule parsed_rule, Boolean negated)
        {
            PartClass c = new PartClass();
            c.part_type = PartEnum.Protected;
            c.negated = negated;
            parsed_rule.parts.Add(c);
            return true;
        }

        // this player is an admin
        private Boolean parse_admin(ref ParsedRule parsed_rule, Boolean negated)
        {
            PartClass c = new PartClass();
            c.part_type = PartEnum.Admin;
            c.negated = negated;
            parsed_rule.parts.Add(c);
            return true;
        }

        // admins are currently on the server
        private Boolean parse_admins(ref ParsedRule parsed_rule, Boolean negated)
        {
            PartClass c = new PartClass();
            c.part_type = PartEnum.Admins;
            c.negated = negated;
            parsed_rule.parts.Add(c);
            return true;
        }

        private Boolean parse_team(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length < (negated ? 3 : 2))
            {
                parse_error("Team", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Team;
            c.negated = negated;
            c.string_list = item_keys(fragments[negated ? 2 : 1].ToLower());
            parsed_rule.parts.Add(c);
            return true;
        }

        private Boolean parse_ping(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length < (negated ? 3 : 2))
            {
                parse_error("Ping", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Ping;
            c.negated = negated;
            try
            {
                c.int1 = Int32.Parse(fragments[negated ? 2 : 1]);
            }
            catch
            {
                parse_error("Ping", parsed_rule.unparsed_rule);
                return false;
            }
            parsed_rule.parts.Add(c);
            return true;
        }

        private Boolean parse_teamsize(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length < (negated ? 3 : 2))
            {
                parse_error("Teamsize", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Teamsize;
            c.negated = negated;
            try
            {
                c.int1 = Int32.Parse(fragments[negated ? 2 : 1]);
            }
            catch
            {
                parse_error("Teamsize", parsed_rule.unparsed_rule);
                return false;
            }
            parsed_rule.parts.Add(c);
            return true;
        }

        private Boolean parse_map(ref ParsedRule parsed_rule, String part, Boolean negated)
        {
            if (part.Length < 5) { parse_error("Map", parsed_rule.unparsed_rule); return false; }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Map;
            c.negated = negated;
            c.string_list = negated ? item_keys(part.Substring(8).ToLower()) : item_keys(part.Substring(4).ToLower());
            parsed_rule.parts.Add(c);
            return true;
        }

        private Boolean parse_mapmode(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length != (negated ? 3 : 2))
            {
                parse_error("MapMode", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.MapMode;
            c.negated = negated;
            c.string_list = item_keys(fragments[negated ? 2 : 1]);
            parsed_rule.parts.Add(c);
            return true;
        }

        private Boolean parse_kit(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length < (negated ? 3 : 2))
            {
                parse_error("Kit", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Kit;
            c.negated = negated;
            if (fragments.Length == (negated ? 4 : 3))
                try
                {
                    c.int1 = Int32.Parse(fragments[negated ? 3 : 2]);
                }
                catch
                {
                    parse_error("Kit", parsed_rule.unparsed_rule);
                    return false;
                }
            try
            {
                c.string_list = item_keys(fragments[negated ? 2 : 1]);
                foreach (String kit_key in c.string_list)
                {
                    try
                    {
                        Kits k = (Kits)Enum.Parse(typeof(Kits), kit_key, true);
                    }
                    catch (ArgumentException)
                    {
                        WriteConsole(String.Format("ProconRulz: ^1Warning, kit {0} not found in Procon (but you can still use the key in ProconRulz)", kit_key));
                    }
                }
                parsed_rule.parts.Add(c);
                spawn_counts.watch(c.string_list);
            }
            catch { parse_error("Kit", parsed_rule.unparsed_rule); return false; }
            return true;
        }

        // updated to allow '&' char in weapon key instead of a space
        // this is complicated by the fact that weapon keys can 
        // INCLUDE SPACES (thankyou EA) eg "M1A1 Thompson" and heli weapons
        // Weapon AUG
        // Not Weapon AUG
        // Weapon AUG 3
        // Not Weapon AUG 3
        // Weapon M1A1 Thompson
        // Weapon M1A1 Thompson 3
        // Not Weapon M1A1 Thompson
        // Not Weapon M1A1 Thompson 3
        // multiple weapon keys can be separated with rulz_item_separator E.g. Weapon SMAW,RPG-7
        private Boolean parse_weapon(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            //WriteConsole(String.Format("rule {0}", parsed_rule.unparsed_rule));
            //WriteConsole(String.Format("length {0}, negated {1}", fragments.Length, negated));
            //WriteConsole(String.Format("fragments {0}", String.Join("---",fragments)));
            if (fragments.Length < (negated ? 3 : 2))
            {
                parse_error("Weapon", parsed_rule.unparsed_rule);
                return false;
            }
            // ok lets try and figure out what we've got in the rule
            List<String> weapon_keys = item_keys((negated ? fragments[2] : fragments[1]));
            // try and make a count out of the last fragment
            Int32 weapon_count = -1;
            try
            {
                weapon_count = Int32.Parse(fragments[fragments.Length - 1]);
            }
            catch
            {
            }
            //WriteConsole(String.Format("weapon_count {0}", weapon_count));

            PartClass c = new PartClass();
            c.part_type = PartEnum.Weapon;
            c.negated = negated;
            if (weapon_count > -1) c.int1 = weapon_count;
            foreach (String weapon_key in weapon_keys)
            {
                if (!weaponDefines.Contains(weapon_key))
                    WriteConsole(String.Format("ProconRulz: ^1Warning, weapon {0} not found in Procon (but you can still use the key in ProconRulz)", weapon_key));
            }
            c.string_list = weapon_keys;
            parsed_rule.parts.Add(c);
            spawn_counts.watch(c.string_list);
            //else { parse_error("Weapon", parsed_rule.unparsed_rule); return false; }
            return true;
        }

        private Boolean parse_spec(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length < (negated ? 3 : 2))
            {
                parse_error("Spec", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Spec;
            c.negated = negated;
            if (fragments.Length == (negated ? 4 : 3))
                try
                {
                    c.int1 = Int32.Parse(fragments[negated ? 3 : 2]);
                }
                catch
                {
                    parse_error("Spec", parsed_rule.unparsed_rule);
                    return false;
                }
            List<String> spec_keys = item_keys(fragments[negated ? 2 : 1]);
            foreach (String spec_key in spec_keys)
            {
                if (!specDefines.Contains(spec_key))
                    WriteConsole(String.Format("ProconRulz: ^1Warning, Specialization {0} not found in Procon (but you can still use the key in ProconRulz)", spec_key));
            }
            c.string_list = spec_keys;
            parsed_rule.parts.Add(c);
            spawn_counts.watch(c.string_list);
            return true;
        }

        private Boolean parse_damage(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length < (negated ? 3 : 2))
            {
                parse_error("Damage", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Damage;
            c.negated = negated;
            if (fragments.Length == (negated ? 4 : 3))
                try
                {
                    c.int1 = Int32.Parse(fragments[negated ? 3 : 2]);
                }
                catch
                {
                    parse_error("Damage", parsed_rule.unparsed_rule);
                    return false;
                }
            try
            {
                c.string_list = item_keys(fragments[negated ? 2 : 1]);
                foreach (String damage_key in c.string_list)
                {
                    try
                    {
                        DamageTypes d = (DamageTypes)Enum.Parse(typeof(DamageTypes), damage_key, true);
                    }
                    catch (ArgumentException)
                    {
                        WriteConsole(String.Format("ProconRulz: ^1Warning, damage {0} not found in Procon (but you can still use the key in ProconRulz)", damage_key));
                    }
                }
                parsed_rule.parts.Add(c);
                spawn_counts.watch(c.string_list);
            }
            catch { parse_error("Damage", parsed_rule.unparsed_rule); return false; }
            return true;
        }

        // count of spawned Kits on team
        private Boolean parse_teamkit(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length != (negated ? 4 : 3))
            {
                parse_error("TeamKit", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.TeamKit;
            c.negated = negated;
            try
            {
                c.int1 = Int32.Parse(fragments[negated ? 3 : 2]);

                c.string_list = item_keys(fragments[negated ? 2 : 1]);
                foreach (String kit_key in c.string_list)
                {
                    try
                    {
                        Kits k = (Kits)Enum.Parse(typeof(Kits), kit_key, true);
                    }
                    catch (ArgumentException)
                    {
                        WriteConsole(String.Format("ProconRulz: ^1Warning, kit {0} not found in Procon (but you can still use the key in ProconRulz)", kit_key));
                    }
                }
                parsed_rule.parts.Add(c);
                spawn_counts.watch(c.string_list);
            }
            catch { parse_error("TeamKit", parsed_rule.unparsed_rule); return false; }
            return true;
        }

        // see parse_weapon for more comments (multi-word weapon keys)
        private Boolean parse_teamweapon(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            //WriteConsole(String.Format("rule {0}", parsed_rule.unparsed_rule));
            //WriteConsole(String.Format("length {0}, negated {1}", fragments.Length, negated));
            //WriteConsole(String.Format("fragments {0}", String.Join("---",fragments)));
            if (fragments.Length < (negated ? 3 : 2))
            {
                parse_error("TeamWeapon", parsed_rule.unparsed_rule);
                return false;
            }
            // ok lets try and figure out what we've got in the rule
            List<String> weapon_keys = item_keys(negated ? fragments[2] : fragments[1]);
            // try and make a count out of the last fragment
            Int32 weapon_count = -1;
            try
            {
                weapon_count = Int32.Parse(fragments[fragments.Length - 1]);
            }
            catch
            {
            }
            //WriteConsole(String.Format("weapon_count {0}", weapon_count));
            //WriteConsole(String.Format("weapon_key {0}", weapon_key));

            PartClass c = new PartClass();
            c.part_type = PartEnum.TeamWeapon;
            c.negated = negated;
            if (weapon_count > -1) c.int1 = weapon_count;

            foreach (String weapon_key in weapon_keys)
            {
                if (!weaponDefines.Contains(weapon_key))
                    WriteConsole(String.Format("ProconRulz: ^1Warning, weapon {0} not found in Procon (but you can still use the key in ProconRulz)", weapon_key));
            }
            c.string_list = weapon_keys;
            parsed_rule.parts.Add(c);
            spawn_counts.watch(c.string_list);

            return true;
        }

        // count of spawned specializations on team
        private Boolean parse_teamspec(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length != (negated ? 4 : 3))
            {
                parse_error("TeamSpec", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.TeamSpec;
            c.negated = negated;
            c.int1 = Int32.Parse(fragments[negated ? 3 : 2]);

            List<String> spec_keys = item_keys(fragments[negated ? 2 : 1]);
            foreach (String spec_key in spec_keys)
            {
                if (!specDefines.Contains(spec_key))
                    WriteConsole(String.Format("ProconRulz: ^1Warning, Specialization {0} not found in Procon (but you can still use the key in ProconRulz)", spec_key));
            }
            c.string_list = spec_keys;
            parsed_rule.parts.Add(c);
            spawn_counts.watch(c.string_list);
            return true;
        }

        private Boolean parse_teamdamage(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length != (negated ? 4 : 3))
            {
                parse_error("TeamDamage", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Damage;
            c.negated = negated;
            try
            {
                c.int1 = Int32.Parse(fragments[negated ? 3 : 2]);
                c.string_list = item_keys(fragments[negated ? 2 : 1]);
                foreach (String damage_key in c.string_list)
                {
                    try
                    {
                        DamageTypes d = (DamageTypes)Enum.Parse(typeof(DamageTypes), damage_key, true);
                    }
                    catch (ArgumentException)
                    {
                        WriteConsole(String.Format("ProconRulz: ^1Warning, damage {0} not found in Procon (but you can still use the key in ProconRulz)", damage_key));
                    }
                }
                parsed_rule.parts.Add(c);
                spawn_counts.watch(c.string_list);
            }
            catch { parse_error("TeamDamage", parsed_rule.unparsed_rule); return false; }
            return true;
        }

        // range of the kill
        private Boolean parse_range(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length == (negated ? 3 : 2))
            {
                PartClass c = new PartClass();
                c.part_type = PartEnum.Range;
                c.negated = negated;
                c.int1 = Int32.Parse(fragments[negated ? 2 : 1]);
                parsed_rule.parts.Add(c);
                return true;
            }
            parse_error("Range", parsed_rule.unparsed_rule);
            return false;
        }

        // "Not <condition>" e.g. "Not Team Attack"
        // this implementation is inelegant - I should be recursively calling parse_part(...)
        private Boolean parse_not(ref ParsedRule parsed_rule, String[] fragments, String part)
        {
            if (fragments.Length < 2)
            {
                parse_error("Not", parsed_rule.unparsed_rule);
                return false;
            }
            switch (fragments[1].ToLower())
            {
                case "if":
                    return parse_test(ref parsed_rule, part, true);

                case "text":
                    return parse_text(ref parsed_rule, part, true);

                case "headshot":
                    return parse_headshot(ref parsed_rule, true);

                case "protected":
                    return parse_protected(ref parsed_rule, true);

                case "targetplayer":
                    return parse_targetplayer(ref parsed_rule, part, true);

                case "admin":
                    return parse_admin(ref parsed_rule, true);

                case "admins":
                    return parse_admins(ref parsed_rule, true);

                case "ping":
                    return parse_ping(ref parsed_rule, fragments, true);

                case "team":
                    return parse_team(ref parsed_rule, fragments, true);

                case "teamsize":
                    return parse_teamsize(ref parsed_rule, fragments, true);

                case "map":
                    return parse_map(ref parsed_rule, part, true);

                case "mapmode":
                    return parse_mapmode(ref parsed_rule, fragments, true);

                case "kit":
                    return parse_kit(ref parsed_rule, fragments, true);

                case "spec":
                    return parse_spec(ref parsed_rule, fragments, true);

                case "weapon":
                    return parse_weapon(ref parsed_rule, fragments, true);

                case "damage":
                    return parse_damage(ref parsed_rule, fragments, true);

                case "teamkit":
                    return parse_teamkit(ref parsed_rule, fragments, true);

                case "teamspec":
                    return parse_teamspec(ref parsed_rule, fragments, true);

                case "teamweapon":
                    return parse_teamweapon(ref parsed_rule, fragments, true);

                case "teamdamage":
                    return parse_teamdamage(ref parsed_rule, fragments, true);

                case "range":
                    return parse_range(ref parsed_rule, fragments, true);

                case "count":
                case "playercount":
                    return parse_count(ref parsed_rule, fragments, true);

                case "teamcount":
                    return parse_teamcount(ref parsed_rule, fragments, true);

                case "servercount":
                    return parse_servercount(ref parsed_rule, fragments, true);

                case "playerfirst":
                case "teamfirst":
                case "serverfirst":
                case "playeronce":
                    return parse_first(ref parsed_rule, fragments[1].ToLower(), true);

                case "rate":
                    return parse_rate(ref parsed_rule, fragments, true);

                default:
                    parse_error("Not", parsed_rule.unparsed_rule);
                    break;
            }
            return false;
        }

        private Boolean parse_count(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length == (negated ? 3 : 2))
            {
                PartClass p = new PartClass();
                p.part_type = PartEnum.Count;
                p.negated = negated;
                p.int1 = Int32.Parse(fragments[negated ? 2 : 1]);
                p.has_count = true;
                parsed_rule.parts.Add(p);
                return true;
            }
            parse_error("Count", parsed_rule.unparsed_rule);
            return false;
        }

        private Boolean parse_teamcount(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length == (negated ? 3 : 2))
            {
                PartClass p = new PartClass();
                p.part_type = PartEnum.TeamCount;
                p.negated = negated;
                p.int1 = Int32.Parse(fragments[negated ? 2 : 1]);
                p.has_count = true;
                parsed_rule.parts.Add(p);
                return true;
            }
            parse_error("TeamCount", parsed_rule.unparsed_rule);
            return false;
        }

        private Boolean parse_servercount(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            if (fragments.Length == (negated ? 3 : 2))
            {
                PartClass p = new PartClass();
                p.part_type = PartEnum.ServerCount;
                p.negated = negated;
                p.int1 = Int32.Parse(fragments[negated ? 2 : 1]);
                p.has_count = true;
                parsed_rule.parts.Add(p);
                return true;
            }
            parse_error("ServerCount", parsed_rule.unparsed_rule);
            return false;
        }

        private Boolean parse_rate(ref ParsedRule parsed_rule, String[] fragments, Boolean negated)
        {
            PartClass p = new PartClass();
            if (fragments.Length != (negated ? 4 : 3))
            {
                parse_error("Rate", parsed_rule.unparsed_rule);
                return false;
            }
            try
            {
                p.part_type = PartEnum.Rate;
                p.negated = negated;
                p.int1 = Int32.Parse(fragments[negated ? 2 : 1]);
                p.int2 = Int32.Parse(fragments[negated ? 3 : 2]);
            }
            catch { parse_error("Rate", parsed_rule.unparsed_rule); return false; }
            parsed_rule.parts.Add(p);
            return true;
        }

        // Here we translate some convenience conditions into actual equivalents:
        // PlayerFirst -> Not PlayerCount 1 (i.e. player count is not > 1, i.e. count MUST be 1)
        // TeamFirst -> Not TeamCount 1
        // ServerFirst -> Not ServerCount 1
        // PlayerOnce -> Not Rate 2 100000 (tricky to explain... rule has NOT fired twice in 100000 seconds,
        //                                  which is only true the FIRST time the rule fires for this player
        //                                  because the time period is long enough that the second time it fires
        //                                  will be inside the 100000 second window so the condition will fail
        //                                  the second (and later) time.
        //                                  This rule takes advantage of the fact that RATES continue across
        //                                  new round starts. Rates reset for a player on a new round when they are
        //                                  NOT online. Told you it was tricky).
        private Boolean parse_first(ref ParsedRule parsed_rule, String condition, Boolean negated)
        {
            PartClass p = new PartClass();
            p.int1 = 1; // 1 for 'first's, will change to 2 for PlayerOnce...
            p.negated = !negated; // i.e. "Not PlayerFirst" -> "PlayerCount 1", i.e. the Not inverts
            switch (condition)
            {
                case "playerfirst":
                    p.part_type = PartEnum.PlayerCount;
                    break;
                case "teamfirst":
                    p.part_type = PartEnum.TeamCount;
                    break;
                case "serverfirst":
                    p.part_type = PartEnum.ServerCount;
                    break;
                case "playeronce":
                    p.part_type = PartEnum.Rate;
                    p.int1 = 2; // is 1 for the above conditions, change to 2 here
                    p.int2 = 100000; // period for Rate set to 100000 seconds i.e. about a day
                    break;
            }
            p.has_count = true;
            parsed_rule.parts.Add(p);
            return true;

        }

        private Boolean parse_text(ref ParsedRule parsed_rule, String part, Boolean negated)
        {
            if (part.Length < 6) { parse_error("Text", parsed_rule.unparsed_rule); return false; }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Text;
            c.negated = negated;
            c.string_list = new List<String>(part.Substring(negated ? 9 : 5).ToLower().Split(new Char[] { rulz_item_separator }, StringSplitOptions.RemoveEmptyEntries));
            parsed_rule.parts.Add(c);
            return true;
        }

        // a player name can be extracted from the say text (into %t%)
        private Boolean parse_targetplayer(ref ParsedRule parsed_rule, String part, Boolean negated)
        {
            PartClass c = new PartClass();
            c.part_type = PartEnum.TargetPlayer;
            c.negated = negated;
            if (part.Length > 12) c.string_list.Add(part.Substring(13));
            parsed_rule.parts.Add(c);
            return true;
        }

        // RULZ VARIABLE INCR, DECR, SET and TEST conditions
        private Boolean parse_incr(ref ParsedRule parsed_rule, String[] fragments)
        {
            if (fragments.Length != 2) { parse_error("Incr", parsed_rule.unparsed_rule); return false; }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Incr;
            c.negated = false;
            c.string_list.Add(fragments[1].ToLower());
            parsed_rule.parts.Add(c);
            return true;
        }

        private Boolean parse_decr(ref ParsedRule parsed_rule, String[] fragments)
        {
            if (fragments.Length != 2) { parse_error("Decr", parsed_rule.unparsed_rule); return false; }
            PartClass c = new PartClass();
            c.part_type = PartEnum.Decr;
            c.negated = false;
            c.string_list.Add(fragments[1].ToLower());
            parsed_rule.parts.Add(c);
            return true;
        }

        private Boolean parse_set(ref ParsedRule parsed_rule, String[] fragments)
        {
            if (fragments.Length < 3)
            {
                parse_error("Set", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass p = new PartClass();
            p.part_type = PartEnum.Set;
            p.negated = false;
            p.string_list.Add(fragments[1]); // string_list[0] = var_name
            String value = "";
            for (Int32 i = 2; i < fragments.Length; i++) value += fragments[i];
            p.string_list.Add(value); //string_list[1] = value to assign
            p.has_count = has_a_count(p);
            parsed_rule.parts.Add(p);
            return true;
        }

        private Boolean parse_test(ref ParsedRule parsed_rule, String part, Boolean negated)
        {
            String[] fragments_in = quoted_split(part);
            String[] fragments;
            // if "Not If" then shift fragments left
            List<String> fragment_list = new List<String>();
            if (negated)
            {
                for (Int32 i = 1; i < fragments_in.Length; i++)
                {
                    fragment_list.Add(fragments_in[i]);
                }
                fragments = fragment_list.ToArray();
            }
            else
                fragments = fragments_in;

            // OK we got to here with the fragments being "If", val1, condition, val2
            if (fragments.Length < 4)
            {
                parse_error("Bad If condition", parsed_rule.unparsed_rule);
                return false;
            }
            Int32 condition_index = 0;
            for (Int32 i = 1; i < fragments.Length; i++)
            {
                if (fragments[i] == "=" ||
                    fragments[i] == "!=" ||
                    fragments[i] == "==" ||
                    fragments[i] == "<>" ||
                    fragments[i] == ">" ||
                    fragments[i] == "<" ||
                    fragments[i] == ">=" ||
                    fragments[i] == "=>" ||
                    fragments[i] == "<=" ||
                    fragments[i] == "=<" ||
                    fragments[i].ToLower() == "contains" ||
                    fragments[i].ToLower() == "word"
                   )
                {
                    condition_index = i;
                    break;
                }
            }
            if (condition_index == 0)
            {
                parse_error("If condition has bad comparison operator", parsed_rule.unparsed_rule);
                return false;
            }
            PartClass p = new PartClass();
            p.part_type = PartEnum.Test;
            p.negated = negated;
            String condition_part = "";
            for (Int32 i = 1; i < condition_index; i++)
            {
                condition_part += fragments[i];
            }
            p.string_list.Add(condition_part);
            p.string_list.Add(fragments[condition_index]);
            condition_part = "";
            for (Int32 i = condition_index + 1; i < fragments.Length; i++)
            {
                condition_part += fragments[i];
            }
            p.string_list.Add(condition_part);

            p.has_count = has_a_count(p);
            parsed_rule.parts.Add(p);
            return true;
        }

        #endregion
    }
}
