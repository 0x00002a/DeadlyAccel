/* 
 *  DeadlyAccel Space Engineers mod
 *  Copyright (C) 2021 Natasha England-Elbro
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Natomic.Logging;
using Sandbox.Game;
using Sandbox.ModAPI;
using SENetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

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
Usage: /da config <reload>|[<set|add|remove|view> <property name> <value>]

reload: Reloads the config 

set: 
Sets config values

<property name>: Name of the value to edit (matches XML tag in the config file) 
<value>: Single value to set the property to (only works for one-valued properties)

add: Adds a config value (for list properties)

remove: Removes a config value (for list properties)

view: Views a config value

Example (adds Respawn Planet Pod to list of ignored grids):
/da config add IgnoredGridNames 'Respawn Planet Pod'

Then to save it on disk (done automatically on world save, but a reload without a save will forget changes): 
/da config save 


- HideHUDInCreative: Whether to hide the HUD in creative
- CushioningBlocks: List of values for cushioning factors. 
                        Final damage per tick is multiplied by the 1 - cushioning value 
                        for the cockpit before being applied (e.g. fighter cockpit has a value of 0.9 and therefore reduces all damage by 90%).
                        You must edit the config file directly to change this option.
- IgnoreJetpack: Whether to ignore force applied due to the jetpack (Warning: Setting this to false may mean you are killed by your jetpack dampers)
- SafeMaximum: Acceleration greater than this value will cause the character to take damage. Damage is polynomially (x^k) proportional to how much
                    the current acceleration is over this value. The default is 5g (5 * 9.81)
- DamageScaleBase: Exponent for damage scaling (k). Higher values means damage will increase faster further past the safe point 
- IgnoredGridNames: List of grid names that are ignored when checking if the pilot should take damage
- IgnoreRespawnShips: Whether to ignore respawn ships when applying damage. It defaults to true since Vanilla drop-pods are otherwise death traps due to their parachutes
- IgnoreRelativeDampers: Whether to ignore acceleration from object dampered to when using the jetpack. This is false by default. Note that this
    option is effetely ignored if IgnoreJetpack is false
- IgnoreCharacter: When true completely ignores all damage unless the player is in a cockpit or some kind of seat. false by default but you may want to
                     turn this on if you are experiencing frequent deaths while just walking around (due to game limitations it is very difficult to
                     fix all the weird edge cases that cause this, hence this setting)
- TimeScaling: Coefficent of the time scaling calculation to give some buffer time in which you will take reduced damage. The
                calculation is damage / ((1/(ticks of acceleration)) * TimeScaling) and discards values of the denominator that are <10.
                Set to 0 to disable (default: 2500)

";

        private NetSync<Settings> net_settings_;
        private Settings settings_
        {
            get
            {
                return net_settings_.Value;
            }
        }

        private List<string> args_cache_ = new List<string>();
        private const string RELOAD_CONF_CMD = "reloadcfg";
        private const string SAVE_CONF_CMD = "savecfg";

        public event Action<bool> OnToggleDebug;

        private void ShowDisabledMsg()
        {
            MyVisualScriptLogicProvider.SendChatMessageColored("Apologies, config commands are currently disabled in multiplayer due to sync issues. reload is the only avaliable command", Color.Yellow);
        }
        public void Init(NetworkAPI net, NetSync<Settings> settings)
        {
            net.RegisterChatCommand("", OnHelp);
            net.RegisterChatCommand("help", OnHelp);
            net.RegisterChatCommand("debug", (args) =>
            {
                switch(args)
                {
                    case "on":
                        OnToggleDebug?.Invoke(true);
                        break;
                    case "off":
                        OnToggleDebug?.Invoke(false);
                        break;
                    default:
                        Log.UI.Error($"Invalid argument to debug: '{args}'");
                        break;
                }
            });

            net.RegisterChatCommand("config", (argsStr) =>
            {
                try
                {
                    Log.Game.Debug($"Got cmd: {argsStr}");
                    var first = argsStr.IndexOf(' ');
                    if (first == -1)
                    {
                        first = argsStr.Length;
                    }
                    var arg = argsStr.Substring(0, first);
                    Log.Game.Debug($"Arg: '{arg}'");
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
                            }
                            else
                            {
                                OnConfigReloadServer(0, null, null, DateTime.MinValue);
                            }
                            break;
                        case "save":
                            if (!MyAPIGateway.Multiplayer.IsServer)
                            {
                                net.SendCommand(SAVE_CONF_CMD);
                            }
                            else
                            {
                                OnConfigSaveServer(0, null, null, DateTime.MinValue);
                            }
                            break;
                        case "list":
                            OnConfigList();
                            break;
                        default:
                            var msg = $"Unknown command: '{argsStr}'";
                            Log.Game.Error(msg);
                            Log.UI.Error(msg);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Log.Game.Error(e);
                    Log.UI.Error($"Failed to parse command: {e.Message}. This is a bug, please report it along with the contents of your log file");

                }

            });
            if (MyAPIGateway.Multiplayer.IsServer || MyAPIGateway.Utilities.IsDedicated)
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
            foreach (var val in values)
            {
                res += val.ToString() + "\n";
            }
            PrintConfigValue(res);

        }

        private void OnConfigList()
        {
            PrintConfigValue(config_cmds.Keys.Aggregate((curr, total) => total + "\n" + curr));
        }
        
        private void ConfigPropCmd<T>(string cmd, Func<T> getter, Action<T> setter, string value, string fieldName)
        {
            if (cmd == "set")
            {
                setter((T)Convert.ChangeType(value, typeof(T)));
                var msg = $"Successfully set {fieldName} to {value}";
                LogConfigValue(msg);
            }
            else
            {
                PrintConfigValue(getter());
            }
        }
        private void ConfigValueCmd<T>(string cmd, ref T field, string value, string fieldName)
        {
            if (cmd == "set")
            {
                field = (T)Convert.ChangeType(value, typeof(T));
                var msg = $"Successfully set {fieldName} to {value}";
                LogConfigValue(msg);
            }
            else
            {
                PrintConfigValue(field);
            }
        }
        private void LogConfigValue(string msg, bool error = false)
        {
            if (!error)
            {
                Log.Game.Info(msg);
            }
            else
            {
                Log.Game.Error(msg);
            }
            MyAPIGateway.Utilities.ShowMessage(DeadlyAccelSession.ModName, msg);
        }
        private void SplitArgs(string argsStr, List<string> readin)
        {
            const char space = ' ';

            var lowerRange = 0;
            var len = 0;
            bool openQuotes = false;
            foreach (var ch in argsStr)
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
            switch (cmd)
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
                    }
                    else
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
            Log.Game.Debug("Pushed config settings to server");
        }

        private static Dictionary<string, Func<string, string, ChatHandler, Action<string>>> config_cmds =
            new Dictionary<string, Func<string, string, ChatHandler, Action<string>>>
            {
                {"IgnoreJetpack", (cmd, value, me) => name => me.ConfigValueCmd(cmd, ref me.settings_.IgnoreJetpack, value, name) },
                {"SafeMaximum", (cmd, value, me) => name => me.ConfigValueCmd(cmd, ref me.settings_.SafeMaximum, value, name) },
                {"DamageScaleBase", (cmd, value, me) => name => me.ConfigValueCmd(cmd, ref me.settings_.DamageScaleBase, value, name) },
                {"IgnoreRespawnShips", (cmd, value, me) => name => me.ConfigValueCmd(cmd, ref me.settings_.IgnoreRespawnShips, value, name) },
                {"IgnoreRelativeDampers", (cmd, value, me) => name => me.ConfigValueCmd(cmd, ref me.settings_.IgnoreRelativeDampers, value, name) },
                {"IgnoredGridNames", (cmd, value, me) => name => me.ConfigListCmd(cmd, me.settings_.IgnoredGridNames, value, name) },
                {"HideHUDInCreative", (cmd, value, me) => name =>
                    {
                        if (!MyAPIGateway.Utilities.IsDedicated)
                        {
                            bool conf_v = me.settings_.HideHUDInCreative;
                            me.ConfigValueCmd(cmd, ref conf_v, value, "HideHUDInCreative");
                            me.settings_.HideHUDInCreative = conf_v;
                        }
                    }
                },
                {"IgnoreCharacter", (cmd, value, me) => name => me.ConfigValueCmd(cmd, ref me.settings_.IgnoreCharacter, value, name) },
                {"TimeScaling", (cmd, value, me) => name => me.ConfigPropCmd<int>(cmd, () => me.settings_.TimeScaling, p => me.settings_.TimeScaling = p, value, name) },
            };
        private void OnConfigEdit(string argsStr)
        {
            args_cache_.Clear();
            SplitArgs(argsStr, args_cache_);
            var args = args_cache_;

            var cmd = args[0];
            var value = args.Count >= 3 ? args[2] : null;
            Log.Game.Debug($"Config arg: '{args[1]}'");
            Func<string, string, ChatHandler, Action<string>> act;
            if (config_cmds.TryGetValue(args[1], out act))
            {
                act.Invoke(cmd, value, this).Invoke(args[1]);
                SyncConfig();
            }
            else
            {
                var msg = $"Unknown config property: '{argsStr}'";
                LogConfigValue(msg, true);
            }
        }
        private void OnHelp(string args)
        {
            string msg;
            switch (args)
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
