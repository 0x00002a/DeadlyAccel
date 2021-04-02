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

using System;
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
            public string TypeId = "MyObjectBuilder_Cockpit";
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

        [ProtoMember(5)]
        public string[] IgnoredGridNames; // List of grids to ignore damage from 

        [ProtoMember(6)]
        public bool IgnoreRespawnShips; // Whether to ignore grids where IsRespawn == true

        [ProtoIgnore]
        public static int CurrentVersionNumber = 2; 
        public int VersionNumber = CurrentVersionNumber - 1; // Increases on breaking changes. Defaults to last version number

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
                    Log.Info("Saving settings");

                    if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)) || overrwrite)
                    {
                        SaveLocal();
                    }
                    SaveWorld();

                }
                catch (Exception e)
                {
                    Log.Error($"Failed to write config: {e}");
                }
            }
        }
        private void SaveWorld()
        {
            Log.Info("Saving to world config");
            using (var sout = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(Settings)))
            {
                sout.Write(this.ToString());
            }
        }
        public void SaveLocal()
        {
            Log.Info($"Saving to local config");
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
                Log.Info("Loading from world");
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
