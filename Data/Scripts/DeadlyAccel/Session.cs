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
using Natomic.Logging.Detail;
using Sandbox.Game;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Net = SENetworkAPI;

namespace Natomic.DeadlyAccel
{
    // This object is always present, from the world load to world unload.
    // NOTE: all clients and server run mod scripts, keep that in mind.
    // NOTE: this and gamelogic comp's update methods run on the main game thread, don't do too much in a tick or you'll lower sim speed.
    // NOTE: also mind allocations, avoid realtime allocations, re-use collections/ref-objects (except value types like structs, integers, etc).
    //
    // The MyUpdateOrder arg determines what update overrides are actually called.
    // Remove any method that you don't need, none of them are required, they're only there to show what you can use.
    // Also remove all comments you've read to avoid the overload of comments that is this file.
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation, 2)]
    public class DeadlyAccelSession : MySessionComponentBase
    {
        public static DeadlyAccelSession Instance; // the only way to access session comp from other classes and the only accepted static field.

        private const ushort ComChannelId = 15128;
        public const string ModName = "Deadly Acceleration";

        private ulong tick = 0;
        private bool players_need_update_ = false;
        private const int TICKS_PER_CACHE_UPDATE = 120;

        private readonly PlayerManager player_ = new PlayerManager();
        private readonly Dictionary<long, IMyPlayer> player_cache_ = new Dictionary<long, IMyPlayer>();
        private readonly Dictionary<string, float> cushioning_mulipliers_ = new Dictionary<string, float>();
        private Settings settings_ => net_settings_.Value;
        private Net.NetSync<Settings> net_settings_;
        private HUDManager hud = null; // Only non-null if not server
        private readonly ChatHandler cmd_handler_ = new ChatHandler();

        bool IsSP => !MyAPIGateway.Multiplayer.MultiplayerActive;
        bool IsClient => IsSP || (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer);

        private bool RegisterJuice(object obj)
        {
            try
            {
                var msg = obj as byte[];
                if (msg == null)
                {
                    Log.Game.Error("Failed to register juice definition, object was not sent as bytes");
                    return false;
                }
                var def = MyAPIGateway.Utilities.SerializeFromBinary<API.JuiceDefinition>(msg);
                player_?.JuiceManager?.AddJuiceDefinition(def);
                if (player_?.JuiceManager == null)
                {
                    Log.Game.Error("Failed to register juice definition, came in before fully initialised");
                    return false;
                }
                else
                {
                    Log.Game.Info($"Added juice definition: {def}");
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Game.Error($"Failed to add juice definition: {e.Message}\n-- Stack Trace --\n{e.StackTrace}");
                Log.UI.Error("Failed to add a juice definition");
                return false;
            }
        }

        public override void LoadData()
        {
            // amogst the earliest execution points, but not everything is available at this point.

            // These can be used anywhere, not just in this method/class:
            // MyAPIGateway. - main entry point for the API
            // MyDefinitionManager.Static. - reading/editing definitions
            // MyGamePruningStructure. - fast way of finding entities in an area
            // MyTransparentGeometry. and MySimpleObjectDraw. - to draw sprites (from TransparentMaterials.sbc) in world (they usually live a single tick)
            // MyVisualScriptLogicProvider. - mainly designed for VST but has its uses, use as a last resort.
            // System.Diagnostics.Stopwatch - for measuring code execution time.
            // ...and many more things, ask in #programming-modding in keen's discord for what you want to do to be pointed at the available things to use.

            Instance = this;
            InitLogger();

            InitNetwork();
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                BuildCushioningCache(settings_);
            }
            if (IsClient || IsSP)
            {
                hud = new HUDManager();
            }
            InitPlayerManager();
            InitPlayerEvents();
            InitAPI();
        }
        private void InitLogger()
        {
            var game = Log.Game;
            game.Add(new LogFilter { MaxLogLevel = LogType.Info, Sink = new GameLog { ModName = ModName } });
            game.Add(new FileLog { ModName = ModName });

            Log.UI.Add(new LogFilter { MaxLogLevel = LogType.Error, Sink = new ChatLog { ModName = ModName } });
        }
        private void InitPlayerEvents()
        {
            MyVisualScriptLogicProvider.PlayerConnected += OnPlayerConnect;
            MyVisualScriptLogicProvider.PlayerDisconnected += OnPlayerDC;

        }
        private void InitAPI()
        {
            var apiHooks = new Dictionary<string, Func<object, bool>>()
            {
                {"RegisterJuice", RegisterJuice}
            };

            MyAPIGateway.Utilities.SendModMessage(API.DeadlyAccelAPI.MOD_API_MSG_ID, apiHooks);
        }
        private void InitNetwork()
        {
            bool net_inited = Net.NetworkAPI.IsInitialized;
            if (!net_inited)
            {
                Net.NetworkAPI.Init(ComChannelId, ModName, "/da");
                Log.Game.Info("Initialised NetworkAPI");
            }

            net_settings_ = new Net.NetSync<Settings>(this, Net.TransferType.ServerToClient, LoadSettings(), true, false);
            if (!net_inited)
            {
                var net_api = Net.NetworkAPI.Instance;
                cmd_handler_.Init(net_api, net_settings_);
                Log.Game.Info("Initialised command handler");
            }
            Log.Game.Info($"Loaded settings: {net_settings_.Value}");
            net_settings_.ValueChangedByNetwork += (old, curr, id) =>
            {
                if (!old.ValidAgainst(curr))
                {
                    Log.Game.Error($"Got send invalid settings: {curr}, reverting to current");
                    net_settings_.SetValue(old);
                }
            };
            
        }
        private void InitPlayerManager()
        {
            player_.CushioningMultipliers = cushioning_mulipliers_;
        }
        private Settings LoadSettings()
        {
            var DefaultSettings = new Settings()
            {
                CushioningBlocks = new List<Settings.CusheningEntry>()
                {
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "PassengerSeatLarge",
                        CushionFactor = 0.2f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "PassengerSeatSmall",
                        CushionFactor = 0.15f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "LargeBlockCockpit",
                        CushionFactor = 0.5f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "SmallBlockCockpit",
                        CushionFactor = 0.5f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "CockpitOpen",
                        CushionFactor = 0.2f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "DBSmallBlockFighterCockpit",
                        CushionFactor = 0.9f,
                    }
                },
                IgnoreJetpack = true,
                SafeMaximum = 9.81f * 5, // 5g's
                DamageScaleBase = 1.1f,
                VersionNumber = Settings.CurrentVersionNumber,
                IgnoredGridNames = new List<string>(),
                IgnoreRespawnShips = true, // Vanilla respawn ships are death traps due to parachutes
            };

            if (!MyAPIGateway.Multiplayer.IsServer)
            {
                return DefaultSettings;
            }
            else
            {
                var settings = Settings.TryLoad(DefaultSettings);
                if (!settings.ValidAgainst(DefaultSettings))
                {
                    settings.FullBackup();
                    Log.Game.Info("Old config detected, performing backup and overwriting");
                    Log.UI.Info("Old config file detected. Your current config file has been backed up but you will need to transfer any changes to the new config file");
                    DefaultSettings.Save(true);
                    return DefaultSettings;
                }
                else
                {
                    settings.Save();
                    return settings;
                }
            }

        }
        private void OnPlayerConnect(long playerId)
        {
            players_need_update_ = true;
            Log.Game.Info($"Player connected: {playerId}");
        }
        public void OnPlayerDC(long playerId)
        {
            player_cache_.Remove(playerId);
            player_.DeregisterPlayer(playerId);
            Log.Game.Info($"Player disconnected: {playerId}");
        }
        public static string FormatCushionLookup(string typeid, string subtypeid)
        {
            return $"{typeid}-{subtypeid}";
        }
        private void BuildCushioningCache(Settings from)
        {
            foreach (var cushen_val in from.CushioningBlocks)
            {
                cushioning_mulipliers_.Add(FormatCushionLookup(cushen_val.TypeId, cushen_val.SubtypeId), cushen_val.CushionFactor);
            }

        }

        public override void BeforeStart()
        {
            // executed before the world starts updating
            hud?.Init();
        }

        protected override void UnloadData()
        {
            // executed when world is exited to unregister events and stuff

            Instance = null; // important for avoiding this object to remain allocated in memory
            MyVisualScriptLogicProvider.PlayerConnected -= OnPlayerConnect;
            MyVisualScriptLogicProvider.PlayerDisconnected -= OnPlayerDC;


        }

        public override void HandleInput()
        {
            // gets called 60 times a second before all other update methods, regardless of framerate, game pause or MyUpdateOrder.
        }

        public override void UpdateBeforeSimulation()
        {
            // executed every tick, 60 times a second, before physics simulation and only if game is not paused.
        }

        public override void Simulate()
        {
            // executed every tick, 60 times a second, during physics simulation and only if game is not paused.
            // NOTE in this example this won't actually be called because of the lack of MyUpdateOrder.Simulation argument in MySessionComponentDescriptor
        }
       
        private void UpdatePlayersCache()
        {
            MyAPIGateway.Multiplayer.Players.GetPlayers(null, p =>
            {
                player_cache_[p.IdentityId] = p;
                return false;
            });
        }
        private void PlayersUpdate()
        {
            bool needs_cache_update = false;
            foreach (var p in player_cache_.Values)
            {
                if (p == null)
                {
                    Log.Game.Debug("Found null player, cache out of date?");
                    needs_cache_update = true;
                }
                else if (!p.IsBot)
                {
                    var dmg = player_.Update(p, settings_);
                    if (hud != null)
                    {
                        hud.ToxicityLevels = Math.Ceiling(player_.ToxicBuildupFor(p));
                    }
                    if (dmg != 0)
                    {
                        hud?.ShowWarning();
                        if (IsSP || !IsClient)
                        {
                            p.Character.DoDamage((float)dmg, MyStringHash.GetOrCompute("F = ma"), true);
                        }
                    } else if (hud != null)
                    {
                        hud.ClearWarning();
                    }
                }
            }
            if (needs_cache_update)
            {
                UpdatePlayersCache();
            }
        }

        public override void UpdateAfterSimulation()
        {
            // executed every tick, 60 times a second, after physics simulation and only if game is not paused.

            ++tick;

            try
            // example try-catch for catching errors and notifying player, use only for non-critical code!
            {
                // ...
                if (players_need_update_ || ((IsSP || IsClient) && player_cache_.Count == 0))
                {
                    UpdatePlayersCache(); // Check every tick till we find something
                    players_need_update_ = false;
                }
                if (tick % 10 == 0)
                {
                    PlayersUpdate();
                }
            }

            catch (Exception e)
            {
                Log.Game.Error(e);
                Log.UI.Error(e.Message);
            }
        }

        public override void Draw()
        {
            try
            {
                // gets called 60 times a second after all other update methods, regardless of framerate, game pause or MyUpdateOrder.
                // NOTE: this is the only place where the camera matrix (MyAPIGateway.Session.Camera.WorldMatrix) is accurate, everywhere else it's 1 frame behind.
                hud?.Draw();
            }
            catch (Exception e)
            {
                Log.Game.Error($"Failed to draw");
                Log.Game.Error(e);
                Log.UI.Error("Failed to draw, see log for details");
            }
        }

        public override void SaveData()
        {
            // executed AFTER world was saved
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                settings_.Save();
            }
        }

        public override MyObjectBuilder_SessionComponent GetObjectBuilder()
        {
            // executed during world save, most likely before entities.

            return base.GetObjectBuilder(); // leave as-is.
        }

        public override void UpdatingStopped()
        {
            // executed when game is paused
        }
    }
}
