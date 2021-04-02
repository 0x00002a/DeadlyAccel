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
            net.RegisterChatCommand("config edit", OnConfigEdit);
            net.RegisterChatCommand("config reload", (args) => {
                net.SendCommand(RELOAD_CONF_CMD);
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
            MyAPIGateway.Utilities.ShowMessage(DeadlyAccelSession.ModName, value.ToString());
        }
        private void ConfigValueCmd<T>(List<string> cmds, ref T field)
        {
            if (cmds[1] == "set")
            {
                field = (T)Convert.ChangeType(cmds[2], typeof(T));
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
        private void ConfigListCmd<T, V>(List<string> cmds, T field) where T: ICollection<V>
        {
            switch(cmds[1])
            {
                case "add":
                    for (var n = 2; n != cmds.Count; ++n)
                    {
                        field.Add((V)Convert.ChangeType(cmds[n], typeof(V)));
                    }
                    break;
                case "remove":
                    for (var n = 2; n != cmds.Count; ++n)
                    {
                        field.Remove((V)Convert.ChangeType(cmds[n], typeof(V)));
                    }
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
            switch(args[0])
            {
                case "IgnoreJetpack":
                    ConfigValueCmd(args, ref settings_.IgnoreJetpack);
                    break;
                case "SafeMaximum":
                    ConfigValueCmd(args, ref settings_.SafeMaximum);
                    break;

                case "DamageScaleBase":
                    ConfigValueCmd(args, ref settings_.DamageScaleBase);
                    break;
                case "IgnoreRespawnShips":
                    ConfigValueCmd(args, ref settings_.IgnoreRespawnShips);
                    break;
                case "IgnoredGridNames":
                    ConfigListCmd<List<string>, string>(args, settings_.IgnoredGridNames);
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
            MyAPIGateway.Utilities.ShowMessage(DeadlyAccelSession.ModName, msg);
        }
    }
}
