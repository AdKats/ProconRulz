using System;
using System.Collections.Generic;
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

        #region process an ACTION

        //**********************************************************************************************
        //**********************************************************************************************
        //   EXECUTE THE ACTIONS IN THE CURRENT RULE
        //**********************************************************************************************
        //**********************************************************************************************
        // execute the next action 
        void take_action(String player_name, PartClass p, Dictionary<SubstEnum, String> keywords)
        {
            WriteDebugInfo(String.Format("ProconRulz: take_action[{0}] with action '{1}{2} {3}' by '{4}'",
                                            player_name,
                                            p.target_action ? "TargetAction " : "",
                                            Enum.GetName(typeof(PartEnum), p.part_type),
                                            p.string_list[0],
                                            keywords[SubstEnum.Player]
                                         )
                          );

            // if current action is 'Continue' or 'End' then do nothing
            if (p.part_type == PartEnum.Continue || p.part_type == PartEnum.End) return;

            // if the current action was TargetAction, add to the players list and return
            if (p.target_action)
            {
                do_action(keywords[SubstEnum.Target], p, keywords);
                return;
            }

            // e.g. from a rule "On Say;Text Yes;TargetConfirm" -- 
            // do nothing (obsolete action)
            if (p.part_type == PartEnum.TargetConfirm)
            {
                return;
            }

            if (p.part_type == PartEnum.PlayerBlock)
            {
                add_block(player_name, p.string_list[0]);
                return;
            }

            // if the action is a "Kill", then IMMEDIATELY clear this soldier out of the spawn counts
            // this will open up an opportunity for someone else to spawn with this players items
            if (p.part_type == PartEnum.Kill &&
                reservationMode == ReserveItemEnum.Player_loses_item_when_dead &&
                !protected_player(player_name))
            {
                spawn_counts.zero_player(player_name);
            }

            do_action(player_name, p, keywords);
        }

        // execute the action (kill in a separate thread which can sleep if necessary)
        void do_action(String target, PartClass a, Dictionary<SubstEnum, String> keywords)
        {
            //object[] parameters = (object[])state;
            //string target = (string)parameters[0];
            //PartClass a = (PartClass)parameters[1];
            //Dictionary<SubstEnum, string> keywords = (Dictionary<SubstEnum,string>)parameters[2];

            // replace all the %p% etc with player name etc
            String message = replace_keys(a.string_list[0], keywords);
            message = rulz_vars.replace_vars(target, message);

            // Merge from ProconRulzNL (LCARSx64): support literal \n in yell messages
            // by converting escaped newline sequences to actual newlines for yell actions
            String yellMessage = Regex.Replace(message, @"\\n", "\n");

            WriteDebugInfo(String.Format("ProconRulz: Doing action '{0} {1}' on {2}",
                                            Enum.GetName(typeof(PartEnum), a.part_type),
                                            message,
                                            target));

            switch (a.part_type)
            {
                case PartEnum.Say:
                    ExecuteCommand("procon.protected.send", "admin.say", message, "all");
                    ExecuteCommand("procon.protected.chat.write", message);
                    break;

                case PartEnum.PlayerSay:
                    ExecuteCommand("procon.protected.send", "admin.say", message, "player", target);
                    ExecuteCommand("procon.protected.chat.write", String.Format("(PlayerSay {0}) ",
                        target) + message);
                    break;

                case PartEnum.TeamSay:
                    if (!keywords.ContainsKey(SubstEnum.PlayerTeamKey)) break; // skip if we don't know player team
                    ExecuteCommand("procon.protected.send", "admin.say", message, "team", keywords[SubstEnum.PlayerTeamKey]);
                    ExecuteCommand("procon.protected.chat.write", String.Format("(TeamSay[{0}] {1}) ",
                        keywords[SubstEnum.PlayerTeam],
                        target) + message);
                    break;

                case PartEnum.SquadSay:
                    if (!keywords.ContainsKey(SubstEnum.PlayerTeamKey)) break; // skip if we don't know player team
                    if (!keywords.ContainsKey(SubstEnum.PlayerSquadKey)) break; // skip if we don't know player squad
                    ExecuteCommand("procon.protected.send",
                                   "admin.say",
                                   message,
                                   "squad",
                                   keywords[SubstEnum.PlayerTeamKey],
                                   keywords[SubstEnum.PlayerSquadKey]);
                    ExecuteCommand("procon.protected.chat.write", String.Format("(SquadSay[{0},{1}] {2}) ",
                        keywords[SubstEnum.PlayerTeam],
                        keywords[SubstEnum.PlayerSquad],
                        target) + message);
                    break;

                case PartEnum.VictimSay:
                    String victim_name = "";
                    try
                    {
                        victim_name = keywords[SubstEnum.Victim];
                    }
                    catch { }

                    if (victim_name != "")
                    {
                        ExecuteCommand("procon.protected.send",
                            "admin.say", message, "player", victim_name);
                        ExecuteCommand("procon.protected.chat.write", String.Format("(VictimSay {0}) ",
                            victim_name) + message);
                    }
                    break;

                case PartEnum.AdminSay:
                    ExecuteCommand("procon.protected.chat.write", "(AdminSay) " + message);
                    if (!admins_present()) break;
                    foreach (String player_name in players.list_players())
                        if (is_admin(player_name))
                            ExecuteCommand("procon.protected.send",
                                "admin.say", message, "player", player_name);
                    break;

                case PartEnum.Yell:
                    ExecuteCommand("procon.protected.send",
                        "admin.yell", yellMessage, a.int1.ToString(), "all");
                    ExecuteCommand("procon.protected.chat.write", message);
                    break;

                case PartEnum.PlayerYell:
                    ExecuteCommand("procon.protected.send",
                        "admin.yell", yellMessage, a.int1.ToString(), "player", target);
                    ExecuteCommand("procon.protected.chat.write",
                        String.Format("(PlayerYell {0}) ", target) + message);
                    break;

                case PartEnum.TeamYell:
                    if (!keywords.ContainsKey(SubstEnum.PlayerTeamKey)) break; // skip if we don't know player team
                    ExecuteCommand("procon.protected.send",
                        "admin.yell", yellMessage, a.int1.ToString(), "team", keywords[SubstEnum.PlayerTeamKey]);
                    ExecuteCommand("procon.protected.chat.write", String.Format("(TeamYell[{0}] {1}) ",
                        keywords[SubstEnum.PlayerTeam],
                        target) + message);
                    break;

                case PartEnum.SquadYell:
                    if (!keywords.ContainsKey(SubstEnum.PlayerTeamKey)) break; // skip if we don't know player team
                    if (!keywords.ContainsKey(SubstEnum.PlayerSquadKey)) break; // skip if we don't know player squad
                    ExecuteCommand("procon.protected.send",
                                   "admin.yell",
                                   yellMessage,
                                   a.int1.ToString(),
                                   "squad",
                                   keywords[SubstEnum.PlayerTeamKey],
                                   keywords[SubstEnum.PlayerSquadKey]);
                    ExecuteCommand("procon.protected.chat.write", String.Format("(SquadYell[{0},{1}] {2}) ",
                        keywords[SubstEnum.PlayerTeam],
                        keywords[SubstEnum.PlayerSquad],
                        target) + message);
                    break;

                case PartEnum.Both:
                    ExecuteCommand("procon.protected.send", "admin.say", message, "all");
                    ExecuteCommand("procon.protected.send",
                        "admin.yell", yellMessage, yell_delay.ToString(), "all");
                    ExecuteCommand("procon.protected.chat.write", message);
                    break;

                case PartEnum.PlayerBoth:
                    ExecuteCommand("procon.protected.send", "admin.say", message, "player", target);
                    ExecuteCommand("procon.protected.send",
                        "admin.yell", yellMessage, yell_delay.ToString(), "player", target);
                    ExecuteCommand("procon.protected.chat.write",
                        String.Format("(PlayerBoth {0}) ", target) + message);
                    break;

                case PartEnum.Log:
                    WriteLog(String.Format("ProconRulz: {0}", message));
                    break;

                case PartEnum.All:
                    ExecuteCommand("procon.protected.send", "admin.say", message, "all");
                    ExecuteCommand("procon.protected.send",
                        "admin.yell", yellMessage, yell_delay.ToString(), "all");
                    WriteLog(String.Format("ProconRulz: {0}", message));
                    break;

                case PartEnum.Kill:
                    if (protected_player(target))
                    {
                        WriteLog(String.Format("ProconRulz: Player {0} protected from Kill by ProconRulz",
                            target));
                        break;
                    }
                    do_kill(target, Int32.Parse(message));
                    break;

                case PartEnum.Kick:
                    if (protected_player(target))
                    {
                        WriteLog(String.Format("ProconRulz: Player {0} protected from Kick by ProconRulz",
                            target));
                        break;
                    }
                    //Thread.Sleep(kill_delay);
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", target, message);
                    WriteLog(String.Format("ProconRulz: Player {0} kicked", target));
                    break;

                case PartEnum.Ban:
                    if (protected_player(target))
                    {
                        WriteLog(String.Format("ProconRulz: Player {0} protected from Ban by ProconRulz",
                            target));
                        break;
                    }
                    try
                    {
                        ExecuteCommand("procon.protected.send",
                                            "banList.add",
                                            "guid",
                                            player_info[target].GUID,
                                            "perm",
                                            message);
                    }
                    catch
                    {
                        try
                        {
                            ExecuteCommand("procon.protected.send",
                                                "banList.add",
                                                "name",
                                                target,
                                                "perm",
                                                message);
                        }
                        catch
                        {
                            WriteLog(String.Format("ProconRulz: exception when banning {0}", target));
                        }
                    }
                    //Thread.Sleep(10000); // sleep for 10 seconds
                    ExecuteCommand("procon.protected.send", "banList.save");
                    //Thread.Sleep(10000); // sleep for 10 seconds
                    ExecuteCommand("procon.protected.send", "banList.list");
                    WriteLog(String.Format("ProconRulz: Player {0} banned", target));
                    break;

                case PartEnum.TempBan:
                    if (protected_player(target))
                    {
                        WriteLog(String.Format("ProconRulz: Player {0} protected from TempBan by ProconRulz",
                            target));
                        break;
                    }
                    try
                    {
                        ExecuteCommand("procon.protected.send",
                                            "banList.add",
                                            "guid",
                                            player_info[target].GUID,
                                            "seconds",
                                            a.int1.ToString(),
                                            message);
                    }
                    catch
                    {
                        try
                        {
                            ExecuteCommand("procon.protected.send",
                                                "banList.add",
                                                "name",
                                                target,
                                                "seconds",
                                                a.int1.ToString(),
                                                message);
                        }
                        catch
                        {
                            WriteLog(String.Format("ProconRulz: exception when TempBanning {0}", target));
                        }
                    }
                    //Thread.Sleep(10000); // sleep for 10 seconds
                    ExecuteCommand("procon.protected.send", "banList.save");
                    //Thread.Sleep(10000); // sleep for 10 seconds
                    ExecuteCommand("procon.protected.send", "banList.list");
                    WriteLog(String.Format("ProconRulz: Player {0} temp banned for {1} seconds",
                                                 target, a.int1.ToString()));
                    break;

                case PartEnum.PBBan:
                    if (protected_player(target))
                    {
                        WriteLog(String.Format("ProconRulz: Player {0} protected from PBBan by ProconRulz",
                            target));
                        break;
                    }
                    String guid = players.pb_guid(target);
                    if (guid == null || guid == "")
                        // no PB guid so try ban using name
                        ExecuteCommand("procon.protected.send",
                                        "punkBuster.pb_sv_command",
                                        String.Format("pb_sv_ban \"{0}\" \"{1}\"",
                                                       target,
                                                       message
                                                     )
                                      );
                    else // we have a PB guid
                        ExecuteCommand("procon.protected.send",
                                        "punkBuster.pb_sv_command",
                                        String.Format("pb_sv_banguid \"{0}\" \"{1}\" \"{2}\" \"{3}\"",
                                                       guid,
                                                       target,
                                                       players.ip(target),
                                                       message
                                                     )
                                      );
                    ExecuteCommand("procon.protected.send",
                                    "punkBuster.pb_sv_command", "pb_sv_updbanfile");
                    WriteLog(String.Format("ProconRulz: Player {0} banned via Punkbuster", target));
                    break;

                case PartEnum.PBKick:
                    if (protected_player(target))
                    {
                        WriteLog(String.Format("ProconRulz: Player {0} protected from PBKick by ProconRulz",
                            target));
                        break;
                    }
                    ExecuteCommand("procon.protected.send",
                                    "punkBuster.pb_sv_command",
                                    String.Format("pb_sv_kick \"{0}\" {1} \"{2}\"",
                                                    target,
                                                    a.int1.ToString(),
                                                    message
                                                    )
                                    );
                    ExecuteCommand("procon.protected.send",
                                    "punkBuster.pb_sv_command", "pb_sv_updbanfile");
                    WriteLog(String.Format("ProconRulz: Player {0} kick/temp banned via Punkbuster for {1} minutes",
                                                 target, a.int1.ToString()));
                    break;

                case PartEnum.Execute:
                    // We need to make a string array out of 'procon.protected.send' 
                    // and the action message
                    // Note that we delay the %% substitutions until we have 'split' 
                    // the message in case we have spaces in subst values
                    List<String> parms_list = new List<String>();
                    // v39b.1 modification - Use command directly if it begins 'procon.'
                    if (a.string_list != null &&
                        a.string_list.Count != 0 &&
                        !(a.string_list[0].ToLower().StartsWith("procon.")))
                    {
                        parms_list.Add("procon.protected.send");
                    }
                    // if this is a punkbuster command then concatenate pb command into a single string
                    // e.g. pb_sv_getss "bambam"
                    if (a.string_list != null &&
                        a.string_list.Count != 0 &&
                        a.string_list[0].ToLower().StartsWith("punkbuster.pb_sv_command"))
                    {
                        parms_list.Add("punkBuster.pb_sv_command");
                        parms_list.Add(rulz_vars.replace_vars(target, replace_keys(a.string_list[0].Substring(25).TrimStart(), keywords)));
                    }
                    else
                    // for non-punkbuster commands each param is its own string...
                    {
                        WriteDebugInfo(String.Format("ProconRulz: do_action Exec <{0}>",
                                                a.string_list[0]));
                        foreach (String element in quoted_split(a.string_list[0])) // updated v40a.1 for quoted strings
                        {
                            // we 'replace_keys' for each fragment
                            parms_list.Add(rulz_vars.replace_vars(target, replace_keys(element, keywords)));
                            WriteDebugInfo(String.Format("ProconRulz: do_action Exec added element <{0}> <{1}>",
                                                            element, rulz_vars.replace_vars(target, replace_keys(element, keywords)))
                                          );

                        }
                    }

                    ExecuteCommand(parms_list.ToArray());

                    WriteLog(String.Format("ProconRulz: Executed command [{0}]",
                                                String.Join(",", parms_list.ToArray())));
                    break;

                default:
                    WriteConsole(String.Format("ProconRulz: action thread error {0}",
                        Enum.GetName(typeof(PartEnum), a.part_type)));
                    break;

            }
        }

        // Kill in a separate thread, so we can sleep
        void do_kill(String player_name, Int32 delay)
        {
            // spawn a thread to execute the action 
            // (the thread can include Sleep without shagging ProconRulz)
            WriteLog(String.Format("ProconRulz: Player {0} killed", player_name));
            ThreadPool.QueueUserWorkItem(new WaitCallback(kill_thread), new Object[] { player_name, delay });

        }

        void kill_thread(Object state)
        {
            Object[] parameters = (Object[])state;
            String target = (String)parameters[0];
            Int32 delay = (Int32)parameters[1];

            Thread.Sleep(delay);
            ExecuteCommand("procon.protected.send", "admin.killPlayer", target);
        }
        #endregion
    }
}
