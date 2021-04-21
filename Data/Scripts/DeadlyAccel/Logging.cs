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

using Natomic.Logging.Detail;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
namespace Natomic.Logging
{
    namespace Detail
    {
        enum LogType
        {
            Debug = 3,
            Info = 2,
            Error = 0,
        }

        interface LogSink
        {
            void Write(LogType t, string message);
            void Close();
        }
        class GameLog : LogSink
        {
            public string ModName;
            public void Write(LogType t, string message)
            {
                MyLog.Default.WriteLineAndConsole($"[{ModName}]{message}");
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
        class LogFilter : LogSink
        {
            public LogType MaxLogLevel;
            public LogSink Sink;

            public void Write(LogType t, string str)
            {
                if (t <= MaxLogLevel)
                {
                    Sink.Write(t, str);
                }
            }

            public void Close()
            {
                Sink.Close();
            }
        }
        static class Util
        {
            public static string VERSION = "1.0.2";

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
                MyLog.Default.WriteLineAndConsole($"[{name}]: Error while logging '{msg}' the rest of this mods log may be unreliable");

            }
        }
        class FileLog : LogSink
        {
            class LogFile
            {
                public string Name;

                private TextWriter handle_ = null;
                private bool open_ = false;
                private int messages_since_flush_ = 0;
                private const int MESSAGES_PER_FLUSH = 100;
                private TextWriter Handle
                {
                    get
                    {
                        if (!open_)
                        {
                            handle_ = MyAPIGateway.Utilities.WriteFileInLocalStorage($"{Name}_{DateTime.Now.ToString("yyyy-mm-dd_HHmmss")}.log", typeof(Log));
                            open_ = true;
                        }
                        return handle_;
                    }
                }
                public void PrintBanner(string mod_name)
                {
                    if (handle_ == null)
                    {
                        Handle.Write($@"
== Log for '{mod_name}' ==
Logger v{Util.VERSION} written by Natomic
Start timestamp: {DateTime.UtcNow:u}
All timestamps are in UTC
-==- Start -==-
");
                    }

                }
                public void Write(string str)
                {
                    if (messages_since_flush_ >= MESSAGES_PER_FLUSH)
                    {
                        Handle.Flush();
                    }
                    Handle.WriteLine(str);

                }

                public void PrintClose()
                {
                    if (handle_ != null)
                    {
                        Handle.WriteLine("-==- End -==-");
                    }
                }

                public void Close()
                {
                    if (open_)
                    {
                        handle_.Flush();
                        handle_.Close();
                        handle_.Dispose(); // Yeah I'm paranoid ok
                        handle_ = null;
                        open_ = false;
                    }
                }

            }
            private readonly Dictionary<LogType, LogFile> files_ = new Dictionary<LogType, LogFile>
            {
                { LogType.Error, new LogFile{Name = "error" } },
                {LogType.Info, new LogFile{Name = "info"} },
                {LogType.Debug, new LogFile{Name = "debug" } },
            };

            public string ModName;

            public void Write(LogType t, string message)
            {
                files_[t].Write(message);
            }

            public FileLog()
            {
                foreach (var f in files_.Values)
                {
                    f.PrintBanner(ModName);
                }
            }

            public void Close()
            {
                foreach (var f in files_.Values)
                {
                    f.PrintClose();
                    f.Close();
                }
            }
        }
        struct StoredLogMsg
        {
            public LogType Type;
            public string Msg;
        }
        class Logger
        {
            #region Fields
            private List<StoredLogMsg> pre_init_msgs_;
            private List<LogSink> writers_ = new List<LogSink>();
            private readonly StringBuilder sc_ = new StringBuilder();
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
                MyAPIGateway.Session.OnSessionReady += OnSessionReady;
            }

            public void Add(LogSink sink)
            {
                writers_.Add(sink);
            }


            public void Close()
            {
                for (var n = 0; n != writers_.Count; ++n)
                {
                    writers_[n].Close();
                    writers_[n] = null;
                }

                writers_.Clear();
                writers_ = null;
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
                    sc_.Append(message);

                    if (!session_ready_ || !initialised_)
                    {
                        if (pre_init_msgs_ == null)
                        {
                            pre_init_msgs_ = new List<StoredLogMsg>();
                        }
                        pre_init_msgs_.Add(new StoredLogMsg { Msg = sc_.ToString(), Type = t });

                    }
                    else if (pre_init_msgs_ != null)
                    {
                        foreach (var msg in pre_init_msgs_)
                        {
                            foreach (var w in writers_)
                            {
                                w.Write(msg.Type, msg.Msg);
                            }
                        }
                        pre_init_msgs_.Clear();
                        pre_init_msgs_ = null;
                    }

                    foreach (var w in writers_)
                    {
                        w.Write(t, sc_.ToString());
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
    class Log : MySessionComponentBase
    {
        private static Log instance_;

        public static Logger Game { get { return instance_?.game_logs_; } }
        public static Logger UI { get { return instance_?.user_logs_; } }

        private Logger game_logs_ = null;
        private Logger user_logs_ = null;


        public string ModName;


        public override void LoadData()
        {
            instance_ = this;
            ModName = ModContext.ModName;
            InitLoggers();
        }
        private void AddDefaultSinksPriv()
        {
            game_logs_.Add(new FileLog { ModName = ModName });
            game_logs_.Add(new GameLog { ModName = ModName });

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                user_logs_.Add(new ChatLog() { ModName = ModName });
            }
        }
        public static void AddDefaultSinks()
        {
            instance_?.AddDefaultSinksPriv();
        }
        private void InitLoggers()
        {
            try
            {
                if (game_logs_ == null)
                {
                    game_logs_ = new Logger();
                    game_logs_.Init(this);

                }
                if (user_logs_ == null)
                {
                    user_logs_ = new Logger();
                    user_logs_.Init(this);
                }
            }
            catch (Exception e)
            {
                Util.MetaLogErr(ModName, "Failed to init loggers: " + Util.FmtErr(e));
            }

        }
        protected override void UnloadData()
        {
            game_logs_.Close();
            user_logs_.Close();
            instance_ = null;
        }



    }
}
