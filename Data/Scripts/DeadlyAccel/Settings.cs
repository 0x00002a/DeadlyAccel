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
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;

namespace Natomic.DeadlyAccel
{
    

    [ProtoContract]
    public class Settings
    {

        [ProtoContract]
        public class CusheningEntry
        {
            [ProtoMember(1)]
            public string TypeId = "MyObjectBuilder_Cockpit";
            [ProtoMember(2)]
            public string SubtypeId;
            [ProtoMember(3)]
            public float CushionFactor;
        }
        
        private const string Filename = "DeadlyAccel.cfg";

        [ProtoMember(1)]
        public List<CusheningEntry> CushioningBlocks
        {
            get
            {
                if (cushening_entries_ == null)
                {
                    cushening_entries_ = new List<CusheningEntry>();
                }
                return cushening_entries_;
            }
            set { cushening_entries_ = value; }
        }

        [ProtoMember(2)]
        public bool IgnoreJetpack; // Whether acceleration due to jetpacks should be ignored 

        [ProtoMember(3)]
        public float SafeMaximum; // Maximum safe acceleration before taking damage

        [ProtoMember(4)]
        public float DamageScaleBase;

        [ProtoMember(5)]
        public List<string> IgnoredGridNames
        {
            get
            {
                if (ignored_grid_names_ == null)
                {
                    ignored_grid_names_ = new List<string>();
                }
                return ignored_grid_names_;
            }
            set { ignored_grid_names_ = value; }
        } // List of grids to ignore damage from 

        [ProtoMember(6)]
        public bool IgnoreRespawnShips; // Whether to ignore grids where IsRespawn == true

        [ProtoIgnore]
        public const int CurrentVersionNumber = 3; // Increases on breaking changes

        [ProtoMember(7)]
        public int VersionNumber;

        [ProtoMember(8)]
        [DefaultValue(false)]
        public bool IgnoreRelativeDampers;

        [ProtoMember(9)]
        [DefaultValue(true)]
        public bool HideHUDInCreative
        {
            set { hide_hud_creative_ = value; hide_hud_creative_set_ = true; }
            get
            {
                if (!hide_hud_creative_set_)
                {
                    hide_hud_creative_ = true;
                    hide_hud_creative_set_ = true;
                }
                return hide_hud_creative_;
            }
        }

        [ProtoMember(10)] [DefaultValue(false)]
        public bool IgnoreCharacter; // Do not damage if not in cockpit

        [ProtoMember(11)] // https://www.geogebra.org/calculator/fwsgbpzh
        public int TimeScaling
        {
            set { time_scaling_ = value; }
            get { return time_scaling_; }
        } // Time based damage scaling

        private int time_scaling_ = 2500;
        internal bool hide_hud_creative_;
        internal bool hide_hud_creative_set_ = false;
        internal List<string> ignored_grid_names_;
        internal List<CusheningEntry> cushening_entries_;

        public override string ToString()
        {
            return MyAPIGateway.Utilities.SerializeToXML(this);
        }

        public bool ValidAgainst(Settings other)
        {
            return VersionNumber >= other.VersionNumber;
        }

        private string GenerateBackupFilename(bool localStorage)
        {
            var n = 0;
            Func<string, bool> check = fname => localStorage ? MyAPIGateway.Utilities.FileExistsInLocalStorage(fname, typeof(Settings)) : MyAPIGateway.Utilities.FileExistsInWorldStorage(fname, typeof(Settings));
            string bfname;
            do
            {
                bfname = $"{Filename}.backup.{n}";
                ++n;
            } while (check(bfname));
            return bfname;
        }
        public void Backup(bool localStorage)
        {

            var fname = GenerateBackupFilename(localStorage);
            using (var sout = localStorage ? MyAPIGateway.Utilities.WriteFileInLocalStorage(fname, typeof(Settings)) : MyAPIGateway.Utilities.WriteFileInWorldStorage(fname, typeof(Settings)))
            {
                sout.Write(this.ToString());
            }
        }
        public void FullBackup()
        {
            if (MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)))
            {
                Backup(true);
            }
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings)))
            {
                Backup(false);
            }
        }

        public void Save(bool overrwrite = false)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                try
                {
                    Log.Game.Debug("Saving settings");

                    if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)) || overrwrite)
                    {
                        SaveLocal();
                    }
                    SaveWorld();

                }
                catch (Exception e)
                {
                    Log.Game.Error($"Failed to write config: {e}");
                }
            }
        }
        private void SaveWorld()
        {
            Log.Game.Info("Saving to world config");
            using (var sout = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(Settings)))
            {
                sout.Write(this.ToString());
            }
        }
        public void SaveLocal()
        {
            Log.Game.Debug($"Saving to local config");
            using (var sout = MyAPIGateway.Utilities.WriteFileInLocalStorage(Filename, typeof(Settings)))
            {
                sout.Write(this.ToString());
            }
        }
        private static Settings LoadFromStorage(TextReader fin)
        {
            Log.Game.Info($"Loading settings from {Filename}");
            var content = fin.ReadToEnd();
            var settings = MyAPIGateway.Utilities.SerializeFromXML<Settings>(content);
            return settings;
        }
        private static TextReader SettingsReader()
        {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings)))
            {
                Log.Game.Debug("Loading from world");
                return MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(Settings));
            }
            else
            {
                Log.Game.Debug("Loading from local ");

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
                Log.Game.Error("Failed to load settings");
                Log.Game.Error(e);
                Log.UI.Error("Failed to load settings");
            }
            return fallback;
        }
    }
}
