using System;
using System.Collections.Generic;
using System.Text;

using SENetworkAPI;
using Digi;
namespace Natomic.DeadlyAccel
{
    class ChatHandler
    {
        private Settings settings_;
        public void Init(NetworkAPI net)
        {
            net.RegisterChatCommand("", OnHelp);
            net.RegisterChatCommand("help", OnHelp);
            net.RegisterChatCommand("config edit", OnConfigEdit);
            net.RegisterChatCommand("config reload", OnConfigReload);
        }
        private void PrintConfigValue<T>(T value)
        {
            Log.Info("", value.ToString());
        }
        private void ConfigValueCmd<T>(string[] cmds, ref T field)
        {
            if (cmds[1] == "set")
            {
                field = (T)Convert.ChangeType(cmds[2], typeof(T));
            } else
            {
                PrintConfigValue(field);
            }
        }
        private void ConfigListCmd<T, V>(string[] cmds, T field) where T: ICollection<V>
        {
            switch(cmds[1])
            {
                case "add":
                    for (var n = 2; n != cmds.Length; ++n)
                    {
                        field.Add((V)Convert.ChangeType(cmds[n], typeof(V)));
                    }
                    break;
                case "remove":
                    for (var n = 2; n != cmds.Length; ++n)
                    {
                        field.Remove((V)Convert.ChangeType(cmds[n], typeof(V)));
                    }
                    break;
                default:
                    PrintConfigValue(field);
                    break;
            }
        }
        private void OnConfigEdit(string argsStr)
        {
            var args = argsStr.Split(' ');
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
                    break;
            }
        }
        private void OnConfigReload(string args)
        {

        }
        private void OnHelp(string args)
        {

        }
    }
}
