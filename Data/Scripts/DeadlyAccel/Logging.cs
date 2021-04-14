/* 
 *  Logging.cs Log helper for Space Engineers mods 
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

using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;

using Natomic.Logging.Detail;
namespace Natomic.Logging
{
    namespace Detail
    {
        enum LogType
        {
            Debug,
            Info,
            Error
        }

        interface LogSink
        {
            void Write(LogType t, string message);
            void Close();
        }
        class GameLog : LogSink
        {
            public void Write(LogType t, string message)
            {
                MyLog.Default.WriteLineAndConsole(message);
            }
            public void Close() { }

        }
        class ChatLog : LogSink
        {
            public string ModName;
            public void Write(LogType t, string message)
            {
                if (MyAPIGateway.Session?.Player != null)
                {
                    var msg_c = Util.ColorFor(t);
                    MyVisualScriptLogicProvider.SendChatMessageColored(message, msg_c, ModName, MyAPIGateway.Session.Player.IdentityId);
                }
            }

            public void Close() { }

        }
        static class Util
        {

            public static void Rename(string from, string to, Type reference)
            {
                var old = MyAPIGateway.Utilities.ReadFileInLocalStorage(from, reference);
                var to_handle = MyAPIGateway.Utilities.WriteFileInLocalStorage(from, reference);
                to_handle.Write(old.ReadToEnd());
            }
            public static string Prefix(LogType t)
            {
                switch (t)
                {
                    case LogType.Debug:
                        return "debug";
                    case LogType.Info:
                        return "info";
                    case LogType.Error:
                        return "error";
                }
                return "";
            }
            public static Color ColorFor(LogType t)
            {
                switch (t)
                {
                    case LogType.Debug: return Color.Green;
                    case LogType.Info: return Color.White;
                    case LogType.Error: return Color.Red;
                }
                return Color.White;
            }
            public static string FmtErr(Exception e)
            {
                return $"{e.Message}\n-- Stack trace --\n{e.StackTrace}";
            }
            public static string FmtDateStamped(string filename)
            {
                return $"{filename}.{DateTime.UtcNow.ToString("_u")}";
            }
            public static void MetaLogErr(string name, string msg)// For when the logger needs to log an error
            {
                MyLog.Default.WriteLineAndConsole($"[{name}]: Error while logging '{msg}' the rest of this mods log way be unreliable");

            }
        }
        class FileLog : LogSink
        {
            private TextWriter err_writer_;
            private TextWriter info_writer_;
            private TextWriter debug_writer_;

            public const string INFO_NAME = "info.log";
            public const string ERROR_NAME = "error.log";
            public const string DEBUG_NAME = "debug.log";

            private TextWriter GetHandle(string file, System.Type reference)
            {
                var n = 0;
                var tmp_name = file;
                while (MyAPIGateway.Utilities.FileExistsInLocalStorage(tmp_name, reference))
                {
                    tmp_name += n;
                    ++n;
                }
                return MyAPIGateway.Utilities.WriteFileInLocalStorage(tmp_name, reference);

            }
            private TextWriter LogTypeToHandle(LogType t)
            {
                switch (t)
                {
                    case LogType.Debug:
                        return debug_writer_;
                    case LogType.Error:
                        return err_writer_;
                    case LogType.Info:
                        return info_writer_;
                }
                throw new Exception("LogTypeToHandle didn't catch all the branches... wat");
            }

            public void Write(LogType t, string message)
            {
                var handle = LogTypeToHandle(t);
                handle.WriteLine(message);

            }
            public FileLog()
            {
                err_writer_ = GetHandle(ERROR_NAME, typeof(Log));
                info_writer_ = GetHandle(INFO_NAME, typeof(Log));
                debug_writer_ = GetHandle(DEBUG_NAME, typeof(Log));
            }
            private void SaveDated()
            {
                err_writer_.Close();
                info_writer_.Close();
                debug_writer_.Close();
                Util.Rename(ERROR_NAME, Util.FmtDateStamped(ERROR_NAME), typeof(Log));
                Util.Rename(INFO_NAME, Util.FmtDateStamped(INFO_NAME), typeof(Log));
                Util.Rename(DEBUG_NAME, Util.FmtDateStamped(DEBUG_NAME), typeof(Log));
            }

            public void Close()
            {
                SaveDated();
            }
        }
        class Logger
        {
            #region Fields
            private List<Tuple<string, LogType>> pre_init_msgs_;
            private List<LogSink> writers_ = new List<LogSink>();
            private StringBuilder sc_ = new StringBuilder();
            const string DATE_FMT = "[HH:mm:ss.fffff]";
            private bool session_ready_ = false;
            private bool initialised_ = false;
            private string mod_name_;
            #endregion

            #region General methods
            public void Init(Log ses)
            {
                initialised_ = true;
                mod_name_ = ses.ModName;
            }

            public void Add(LogSink sink)
            {
                writers_.Add(sink);
            }


            public void Close()
            {
                if (initialised_)
                {
                    foreach (var w in writers_)
                    {
                        w?.Close();
                    }
                    writers_ = null;
                }
                initialised_ = false;
                MyAPIGateway.Session.OnSessionReady -= OnSessionReady;
            }
            private void OnSessionReady()
            {
                session_ready_ = true;
            }
            #endregion
            #region Logging 
            /// <summary>
            /// <para>Log a message based on type. You probably want to use one of the helper overloads instead (Info, Debug, Error, etc)</para>
            /// </summary>
            public void LogMsg(LogType t, string message)
            {
                try
                {
                    sc_.Clear();

                    if (!initialised_ || !session_ready_)
                    {
                        sc_.Append("[Pre Init]");
                    }
                    sc_.Append(DateTime.UtcNow.ToString(DATE_FMT));
                    sc_.Append("[");
                    sc_.Append(Util.Prefix(t));
                    sc_.Append("]: ");

                    if (!initialised_)
                    {
                        if (pre_init_msgs_ == null)
                        {
                            pre_init_msgs_ = new List<Tuple<string, LogType>>();
                        }
                        pre_init_msgs_.Add(Tuple.Create(sc_.ToString(), t));

                    }
                    if (initialised_ && pre_init_msgs_ != null)
                    {
                        foreach (var msg in pre_init_msgs_)
                        {
                            foreach (var w in writers_)
                            {
                                w.Write(msg.Item2, msg.Item1);
                            }
                        }
                    }

                    foreach (var w in writers_)
                    {
                        w.Write(t, message);
                    }
                }
                catch (Exception e)
                {
                    Util.MetaLogErr(mod_name_, Util.FmtErr(e));
                }
            }

            /// <summary>
            /// <para>Log a message with type Info</para>
            /// </summary>
            public void Info(string message)
            {
                LogMsg(LogType.Info, message);
            }
            /// <summary>
            /// <para>Log a message with type Error</para>
            /// </summary>
            /// <param name="msg"></param>
            public void Error(string msg)
            {
                LogMsg(LogType.Error, msg);
            }
            public void Error(Exception e)
            {
                Error(Util.FmtErr(e));
            }
            /// <summary>
            /// <para>Log a message with type Debug</para>
            /// </summary>
            /// <param name="msg"></param>
            public void Debug(string msg)
            {
                LogMsg(LogType.Debug, msg);
            }

        }
        #endregion
    }

    /// <summary>
    /// <para>Basic logger. Logs to 3 files, the game log, and the chat by default</para>
    /// <para>For redistribution rights, see the license at the top. If you have any questions @Natomic on the Keen discord should find me</para>
    /// <para>Based on Log.cs by Digi</para>
    /// </summary>

    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate, priority: int.MaxValue)]
    class Log: MySessionComponentBase
    {
        private static Log instance_;

        public static Logger Game { get { return instance_?.game_logs_; } }
        public static Detail.Logger UI { get { return instance_?.user_logs_; } }

        private Logger game_logs_ = new Logger();
        private Logger user_logs_ = new Logger();


        public string ModName;


        public override void LoadData()
        {
            instance_ = this;
            ModName = ModContext.ModName;
            InitLoggers();
        }
        private void InitLoggers()
        {
            try
            {
                game_logs_.Init(this);
                user_logs_.Init(this);

                if (!MyAPIGateway.Utilities.IsDedicated)
                {
                    user_logs_.Add(new ChatLog() { ModName = ModName });
                }
                game_logs_.Add(new FileLog());
                game_logs_.Add(new GameLog());
             } catch(Exception e)
            {
                Util.MetaLogErr(ModName, "Failed to init loggers: " + Util.FmtErr(e));
            }
            
        }
        protected override void UnloadData()
        {
            game_logs_.Close();
            user_logs_.Close();
            instance_ = null; // Don't want memory leaks
        }
        
       
       
    }
}
