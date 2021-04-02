﻿using System;
using System.Collections.Generic;
using System.Text;

using SENetworkAPI;
using Digi;
using Sandbox.ModAPI;

namespace Natomic.DeadlyAccel
{
    class ChatHandler
    {
        private const string HELP_TXT = @"
Deadly Acceleration

Usage: /da <command> [arguments]


Commands:

help: Prints this help text. Use help <command> to view detailed help on a command
config: Allows viewing and modifying configuration values
";
        private const string CONF_HELP_TXT = @"
Usage: /da config <reload>|[<set|add|remove> <property name> <value>]

reload: Reloads the config 

set: 
Sets config values

<property name>: Name of the value to edit (matches XML tag in the config file) 
<value>: Single value to set the property to (only works for one-valued properties)

add: Adds a config value (for list properties)

remove: Removes a config value (for list properties)

Example (adds Respawn Planet Pod to list of ignored grids):
/da config add IgnoredGridNames 'Respawn Planet Pod'

Then to save it on disk (done automatically on world save, but a reload without a save will forget changes): 
/da config save 

";

        private NetSync<Settings> net_settings_;
        private Settings settings_ { get
            {
                return net_settings_.Value;
            } }

        private List<string> args_cache_ = new List<string>();
        private const string RELOAD_CONF_CMD = "reloadcfg";
        private const string SAVE_CONF_CMD = "savecfg";
        public void Init(NetworkAPI net, NetSync<Settings> settings)
        {
            net.RegisterChatCommand("", OnHelp);
            net.RegisterChatCommand("help", OnHelp);
            net.RegisterChatCommand("config", (argsStr) =>
            {
                try
                {
                    Log.Info($"Got cmd: {argsStr}");
                    var first = argsStr.IndexOf(' ');
                    if (first == -1)
                    {
                        first = argsStr.Length;
                    }
                    var arg = argsStr.Substring(0, first);
                    Log.Info($"Arg: '{arg}'");
                    switch (arg)
                    {
                        case "add":
                        case "remove":
                        case "view":
                        case "set":
                            OnConfigEdit(argsStr);
                            break;
                        case "reload":
                            if (!MyAPIGateway.Multiplayer.IsServer)
                            {
                                net.SendCommand(RELOAD_CONF_CMD);
                            } else
                            {
                                OnConfigReloadServer(0, null, null, DateTime.MinValue);
                            }
                            break;
                        case "save":
                            if (!MyAPIGateway.Multiplayer.IsServer)
                            {
                                net.SendCommand(SAVE_CONF_CMD);
                            } else
                            {
                                OnConfigSaveServer(0, null, null, DateTime.MinValue);
                            }
                            break;
                        default:
                            var msg = $"Unknown command: '{argsStr}'";
                            Log.Error(msg, msg);
                            break;
                    }
                } catch(Exception e)
                {
                    Log.Error(e, $"Failed to parse command: {e.Message}. This is a bug, please report it along with the contents of your log file");
                }

            });
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                net.RegisterNetworkCommand(RELOAD_CONF_CMD, OnConfigReloadServer);
                net.RegisterNetworkCommand(SAVE_CONF_CMD, OnConfigSaveServer);
            }

            net_settings_ = settings;
        }
        private void OnConfigSaveServer(ulong steamId, string cmd, byte[] data, DateTime timestamp)
        {
            settings_.Save();
            LogConfigValue("Saved config");
        }
        private void OnConfigReloadServer(ulong steamId, string cmd, byte[] data, DateTime timestamp)
        {
            net_settings_.SetValue(Settings.TryLoad(settings_));
            LogConfigValue("Reloaded config");
        }
        private void PrintConfigValue<T>(T value)
        {
            MyAPIGateway.Utilities.ShowMissionScreen(DeadlyAccelSession.ModName, null, null, value.ToString());
        }
        private void PrintConfigValueList<T>(ICollection<T> values)
        {
            string res = "";
            foreach(var val in values)
            {
                res += val.ToString() + "\n";
            }
            PrintConfigValue(res);

        }
        private void ConfigValueCmd<T>(string cmd, ref T field, string value, string fieldName)
        {
            if (cmd == "set")
            {
                field = (T)Convert.ChangeType(value, typeof(T));
                var msg = $"Successfully set {fieldName} to {value}";
                LogConfigValue(msg);
            } else
            {
                PrintConfigValue(field);
            }
        }
        private void LogConfigValue(string msg, bool error = false)
        {
            if (!error)
            {
                Log.Info(msg);
            } else
            {
                Log.Error(msg);
            }
            MyAPIGateway.Utilities.ShowMessage(DeadlyAccelSession.ModName, msg);
        }
        private void SplitArgs(string argsStr, List<string> readin)
        {
            const char space = ' ';

            var lowerRange = 0;
            var len = 0;
            bool openQuotes = false;
            foreach(var ch in argsStr)
            {
                var append = false;
                if (ch == '\'')
                {
                    openQuotes = !openQuotes;
                    append = true;
                }
                else if (ch == space && !openQuotes)
                {
                    append = true;
                }
                if (append)
                {
                    if (len != 0 && len < argsStr.Length)
                    {
                        readin.Add(argsStr.Substring(lowerRange, len));
                    }
                    lowerRange += len + 1;
                    len = -1;

                }
                ++len;
            }
            if (lowerRange != len && lowerRange != argsStr.Length)
            {
                readin.Add(argsStr.Substring(lowerRange));
            }

        }
        private void ConfigListCmd<T>(string cmd, ICollection<T> field, string value, string fieldName) 
        {
            string logMsg = "";
            switch(cmd)
            {
                case "add":
                    field.Add((T)Convert.ChangeType(value, typeof(T)));
                    logMsg = $"Sucessfully added {value} to {fieldName}";
                    break;
                case "remove":
                    var val = (T)Convert.ChangeType(value, typeof(T));
                    if (field.Contains(val))
                    {
                        field.Remove(val);
                        logMsg = $"Successfully removed {value} from {fieldName}";
                    } else
                    {
                        logMsg = $"Failed to remove {value} from {fieldName}: {value} does not exist in {fieldName}";
                    }
                    break;
                default:
                    PrintConfigValueList(field);
                    break;
            }
            if (logMsg.Length > 0)
            {
                LogConfigValue(logMsg);
            }
        }
        private void SyncConfig()
        {
            net_settings_.Push();
        }
        private void OnConfigEdit(string argsStr)
        {
            args_cache_.Clear();
            SplitArgs(argsStr, args_cache_);
            var args = args_cache_;
            var successful = true;

            var cmd = args[0];
            var value = args.Count >= 3 ? args[2] : null;
            Log.Info($"Config arg: '{args[1]}'");
            switch(args[1])
            {
                case "IgnoreJetpack":
                    ConfigValueCmd(cmd, ref settings_.IgnoreJetpack, value, "IgnoreJetpack");
                    break;
                case "SafeMaximum":
                    ConfigValueCmd(cmd, ref settings_.SafeMaximum, value, "SafeMaximum");
                    break;

                case "DamageScaleBase":
                    ConfigValueCmd(cmd, ref settings_.DamageScaleBase, value, "DamageScaleBase");
                    break;
                case "IgnoreRespawnShips":
                    ConfigValueCmd(cmd, ref settings_.IgnoreRespawnShips, value, "IgnoreRespawnShips");
                    break;
                case "IgnoredGridNames":
                    ConfigListCmd(cmd, settings_.IgnoredGridNames, value, "IgnoredGridNames");
                    break;
                default:
                    var msg = $"Unkown command: '{argsStr}'";
                    LogConfigValue(msg, true);
                    successful = false;
                    break;
            }
            if (successful) { SyncConfig(); }
        }
        private void OnHelp(string args)
        {
            string msg = null;
            switch(args)
            {
                case "config":
                    msg = CONF_HELP_TXT;
                    break;
                default:
                    msg = HELP_TXT;
                    break;
            }
            PrintConfigValue(msg);
        }
    }
}