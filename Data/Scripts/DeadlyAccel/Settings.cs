﻿using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;
using Sandbox.ModAPI;
using Digi;
using System.IO;

namespace Natomic.DeadlyAccel
{
    public class Settings
    {
        [ProtoContract]
        public class CusheningEntry
        {
            [ProtoMember(1)]
            public string TypeId = "Cockpit";
            [ProtoMember(2)]
            public string SubtypeId;
            [ProtoMember(3)]
            public float CushionFactor;
        }
        private const string Filename = "DeadlyAccel.cfg";

        [ProtoMember(1)]
        public List<CusheningEntry> CushioningBlocks = new List<CusheningEntry>();

        [ProtoMember(2)]
        public bool IgnoreJetpack; // Whether acceleration due to jetpacks should be ignored 

        [ProtoMember(3)]
        public float SafeMaximum; // Maximum safe acceleration before taking damage

        [ProtoMember(4)]
        public float DamageScaleBase;

        public override string ToString()
        {
            return MyAPIGateway.Utilities.SerializeToXML(this);
        }
        public void Save()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                try
                {
                    Log.Info("Saving settings");

                    if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)))
                    {
                        SaveLocal();
                    }

                    using (var sout = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(Settings)))
                    {
                        sout.Write(this.ToString());
                    }

                }
                catch (Exception e)
                {
                    Log.Error($"Failed to write save settings: {e}");
                }
            }
        }
        public void SaveLocal()
        {
            Log.Info($"Saving local settings");
            using (var sout = MyAPIGateway.Utilities.WriteFileInLocalStorage(Filename, typeof(Settings)))
            {
                sout.Write(this.ToString());
            }
        }
        private static Settings LoadFromStorage(TextReader fin)
        {
            Log.Info($"Loading settings from {Filename}");
            var content = fin.ReadToEnd();
            var settings = MyAPIGateway.Utilities.SerializeFromXML<Settings>(content);
            return settings;
        }
        private static TextReader SettingsReader()
        {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings)))
            {
                return MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(Settings));
            }
            else
            {
                Log.Info("Loading from local ");

                return MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(Settings));
            }
        }

        public static Settings TryLoad(Settings fallback)
        {

            try
            {
                if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)))
                {
                    fallback.SaveLocal();
                }
                using (var fin = SettingsReader())
                {
                    var settings = LoadFromStorage(fin);
                    return settings;
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load settings: {e}", "Failed to load settings");
            }
            return fallback;
        }
    }
}
