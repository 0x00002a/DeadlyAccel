using System;
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
Usage: /da config <edit|reload> <property name> [set|add|remove] <value>|<values>

reload: Reloads the config or syncs it with the server

edit: 
Edits config values

<property name>: Name of the value to edit (matches XML tag in the config file) 
<value>: Single value to set the property to (only works for one-valued properties)
<values>: List of values (only works for multi-valued properties). Values with spaces between MUST be quoted with single quotes ('')

Example (adds Respawn Planet Pod to list of ignored grids and updates the config):
/da config edit IgnoredGridNames add 'Respawn Planet Pod'
/da config reload

";

        private NetSync<Settings> net_settings_;
        private Settings settings_ { get
            {
                return net_settings_.Value;
            } }

        private List<string> args_cache_ = new List<string>();
        private const string RELOAD_CONF_CMD = "reloadcfg";
        public void Init(NetworkAPI net, NetSync<Settings> settings)
        {
            net.RegisterChatCommand("", OnHelp);
            net.RegisterChatCommand("help", OnHelp);
            net.RegisterChatCommand("config", (argsStr) =>
            {
                Log.Info($"Got cmd: {argsStr}");
                var first = argsStr.IndexOf(' ');
                var arg = argsStr.Substring(0, first + 1);
                switch (arg)
                {
                    case "view":
                    case "edit":
                        OnConfigEdit(argsStr);
                        break;
                    case "reload":
                        net.SendCommand(RELOAD_CONF_CMD);
                        break;
                    default:
                        var msg = $"Unknown command: '{argsStr}'";
                        Log.Error(msg, msg);
                        break;
                }

            });
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                net.RegisterNetworkCommand(RELOAD_CONF_CMD, OnConfigReloadServer);
            }

            net_settings_ = settings;
        }
        private void OnConfigReloadServer(ulong steamId, string cmd, byte[] data, DateTime timestamp)
        {
            Log.Info("Reloading config");
            net_settings_.SetValue(Settings.TryLoad(settings_));
        }
        private void PrintConfigValue<T>(T value)
        {
            MyAPIGateway.Utilities.ShowMissionScreen(DeadlyAccelSession.ModName, null, null, value.ToString());
        }
        private void ConfigValueCmd<T>(string cmd, ref T field, string value)
        {
            if (cmd == "set")
            {
                field = (T)Convert.ChangeType(value, typeof(T));
            } else
            {
                PrintConfigValue(field);
            }
        }
        private void SplitArgs(string argsStr, List<string> readin)
        {
            const char space = ' ';

            var lowerRange = 0;
            var upperRange = 0;
            bool openQuotes = false;
            foreach(var ch in argsStr)
            {
                if (ch == '\'')
                {
                    openQuotes = !openQuotes;
                }
                else if (ch == space && !openQuotes)
                {
                    if (lowerRange == upperRange)
                    {
                        readin.Add(string.Empty);
                    }
                    else
                    {
                        readin.Add(argsStr.Substring(lowerRange, upperRange));
                    }
                    lowerRange = upperRange;
                }
                ++upperRange;
            }

        }
        private void ConfigListCmd<T>(string cmd, ICollection<T> field, string value) 
        {
            switch(cmd)
            {
                case "add":
                    field.Add((T)Convert.ChangeType(value, typeof(T)));
                    break;
                case "remove":
                    field.Remove((T)Convert.ChangeType(value, typeof(T)));
                    break;
                default:
                    PrintConfigValue(field);
                    break;
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
            var value = args.Count >= 2 ? args[2] : null;
            switch(args[1])
            {
                case "IgnoreJetpack":
                    ConfigValueCmd(cmd, ref settings_.IgnoreJetpack, value);
                    break;
                case "SafeMaximum":
                    ConfigValueCmd(cmd, ref settings_.SafeMaximum, value);
                    break;

                case "DamageScaleBase":
                    ConfigValueCmd(cmd, ref settings_.DamageScaleBase, value);
                    break;
                case "IgnoreRespawnShips":
                    ConfigValueCmd(cmd, ref settings_.IgnoreRespawnShips, value);
                    break;
                case "IgnoredGridNames":
                    ConfigListCmd(cmd, settings_.IgnoredGridNames, value);
                    break;
                default:
                    var msg = $"Unkown command: '{argsStr}'";
                    Log.Error(msg, msg);
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
