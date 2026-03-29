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

        #region plugin settings incl. load the rulz
        //**********************************************************************************************
        //**********************************************************************************************
        //                         MANAGE THE PLUGIN SETTINGS
        //**********************************************************************************************
        //**********************************************************************************************

        // load rulz .txt files listed in rulz_filenames
        void load_rulz_from_files()
        {
            if (rulz_filenames.Count == 0) return;
            try
            {
                //string folder = @"Plugins\"+game_id.ToString()+@"\";
                String folder = @"Plugins" + Path.DirectorySeparatorChar + game_id.ToString() + Path.DirectorySeparatorChar;
                WriteConsole("ProconRulz: Loading rulz from .txt files in " + folder);
                //string[] file_paths = Directory.GetFiles(folder, "proconrulz_*.txt");
                // do nothing and return if no rulz files found
                //if (file_paths.Length == 0)
                //{
                //    WriteConsole("ProconRulz: no user rulz files defined (will just use settings)");
                //    return;
                //}

                // start with an empty list of user rulz from files
                filez_rulz.Clear();

                foreach (String filename in rulz_filenames)
                {
                    if (File.Exists(folder + filename))
                    {
                        WriteConsole("ProconRulz: Loading " + folder + filename);
                        String[] rulz = System.IO.File.ReadAllLines(folder + filename);
                        if (rulz.Length > 0 & !rulz[0].Contains("#disable"))
                            filez_rulz.Add(filename, rulz);
                    }
                    else
                    {
                        String path = Path.GetFullPath(folder + filename);
                        WriteConsole("ProconRulz: Skipping " + path + " NOT FOUND");
                    }
                }
            }
            catch (Exception ex)
            {
                PrintException(ex);
            }

        }

        // plugin variables are declared in 'Global Vars' region above

        // ASSIGN values to program globals FROM server_ip.cfg and from Plugin Settings pane
        public void SetPluginVariable(String strVariable, String raw_strValue)
        {
            try
            {
                String strValue = CPluginVariable.Decode(raw_strValue);
                switch (strVariable)
                {
                    case "Game":
                        game_id = (GameIdEnum)Enum.Parse(typeof(GameIdEnum), strValue);
                        break;
                    case "Delay before kill":
                        kill_delay = Int32.Parse(strValue);
                        break;
                    case "Yell seconds":
                        yell_delay = Int32.Parse(strValue);
                        break;
                    //case "Player keeps items on respawn":
                    //    reservationMode = (ReserveItemEnum)Enum.Parse(typeof(ReserveItemEnum), strValue);
                    //    break;
                    case "EA Rules of Conduct read and accepted":
                        roc_read = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
                        break;
                    case "Trace rules":
                        trace_rules = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
                        break;
                    case "Protect these players from Kick or Kill":
                        protect_players
                            = (ProtectEnum)Enum.Parse(typeof(ProtectEnum), strValue);
                        break;
                    case "Rules":
                        String strValueHtmlDecode = raw_strValue.Contains(" ") ? raw_strValue : raw_strValue.Replace("+", " ");
                        String strValueUnencoded;
                        try
                        {
                            strValueUnencoded = Uri.UnescapeDataString(strValueHtmlDecode);
                        }
                        catch
                        {
                            strValueUnencoded = strValueHtmlDecode;
                        }
                        unparsed_rules = new List<String>(strValueUnencoded.Split(new Char[] { '|' }));
                        rulz_vars.reset();
                        reset_counts();
                        parse_rules();
                        break;
                    case "Rulz .txt filenames":
                        rulz_filenames = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                        // look for rulz files and add those to unparsed_rules
                        load_rulz_from_files();
                        rulz_vars.reset();
                        reset_counts();
                        parse_rules();
                        break;
                    case "Player name whitelist":
                        whitelist_players = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                        break;
                    case "Clan name whitelist":
                        whitelist_clans = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                        break;
                    case "Send ProconRulz Log messages to:":
                        log_file = (LogFileEnum)Enum.Parse(typeof(LogFileEnum), strValue);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                PrintException(ex);
            }
        }

        // Allow procon to READ current values of program globals (and write values to server_ip.cfg)
        public List<PRoCon.Core.CPluginVariable> GetDisplayPluginVariables()
        {
            try
            {
                List<CPluginVariable> lst = new List<CPluginVariable>();
                lst.Add(new CPluginVariable("Game",
                    CreateEnumString(typeof(GameIdEnum)), game_id.ToString()));
                lst.Add(new CPluginVariable("Delay before kill", typeof(Int32), kill_delay));
                lst.Add(new CPluginVariable("Yell seconds", typeof(Int32), yell_delay));
                //lst.Add(new CPluginVariable("Player keeps items on respawn",
                //    CreateEnumString(typeof(ReserveItemEnum)), reservationMode.ToString()));
                lst.Add(new CPluginVariable("EA Rules of Conduct read and accepted",
                    typeof(enumBoolYesNo), roc_read));
                lst.Add(new CPluginVariable("Protect these players from Kick or Kill",
                    CreateEnumString(typeof(ProtectEnum)), protect_players.ToString()));
                lst.Add(new CPluginVariable("Trace rules", typeof(enumBoolYesNo), trace_rules));
                lst.Add(new CPluginVariable("Rules", typeof(String[]), unparsed_rules.ToArray()));
                lst.Add(new CPluginVariable("Rulz .txt filenames",
                    typeof(String[]), rulz_filenames.ToArray()));
                lst.Add(new CPluginVariable("Player name whitelist",
                    typeof(String[]), whitelist_players.ToArray()));
                lst.Add(new CPluginVariable("Clan name whitelist",
                    typeof(String[]), whitelist_clans.ToArray()));
                lst.Add(new CPluginVariable("Send ProconRulz Log messages to:",
                    CreateEnumString(typeof(LogFileEnum)), log_file.ToString()));
                return lst;
            }
            catch (Exception ex)
            {
                PrintException(ex);
                return new List<CPluginVariable>();
            }
        }

        public List<PRoCon.Core.CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }

        #endregion
    }
}
