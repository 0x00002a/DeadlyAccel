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
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Natomic.Logging;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.Entities.Character;

using Net = SENetworkAPI;
using VRage.Input;
using VRageMath;
using System.Linq;
using VRage.Game.ModAPI.Interfaces;
using VRage.Collections;
using Sandbox.Game.EntityComponents;
using VRage;

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
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class DeadlyAccelSession : MySessionComponentBase
    {
        public static DeadlyAccelSession Instance; // the only way to access session comp from other classes and the only accepted static field.

        private const ushort ComChannelId = 15128;
        public const string ModName = "Deadly Acceleration";

        private int tick = 0;
        private bool players_need_update_ = false;
        private const int TICKS_PER_CACHE_UPDATE = 120;

        private PlayerManager player_;
        private readonly Dictionary<long, IMyPlayer> player_cache_ = new Dictionary<long, IMyPlayer>();
        private readonly Dictionary<string, float> cushioning_mulipliers_ = new Dictionary<string, float>();
        private Settings settings_ => net_settings_.Value;
        private Net.NetSync<Settings> net_settings_;
        private HUDManager hud = null; // Only non-null if not server

        private readonly RayTraceHelper ray_tracer_ = new RayTraceHelper();
        private JuiceTracker juice_manager_;

        private bool RegisterJuice(object obj)
        {
            try
            {
                var msg = obj as byte[];
                if (msg == null)
                {
                    Log.Error("Failed to register juice definition, object was not sent as bytes");
                    return false;
                }
                var def = MyAPIGateway.Utilities.SerializeFromBinary<API.JuiceDefinition>(msg);
                juice_manager_.AddJuiceDefinition(def);
                Log.Info($"Added juice definition: {def}");
                return true;
            } catch(Exception e)
            {
                Log.Error($"Failed to add juice definition: {e.Message}\n-- Stack Trace --\n{e.StackTrace}", "Failed to add a juice definition");
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

            if (MyAPIGateway.Utilities.IsDedicated || MyAPIGateway.Multiplayer.IsServer)
            {
                ServerSideInit();
            }

            var apiHooks = new Dictionary<string, Func<object, bool>>()
            {
                {"RegisterJuice", RegisterJuice}
            };

            MyAPIGateway.Utilities.SendModMessage(API.DeadlyAccelAPI.MOD_API_MSG_ID, apiHooks);

            BuildCushioningCache(Settings_);
        }
        private void InitNetwork()
        {
            bool net_inited = Net.NetworkAPI.IsInitialized;
            if (!net_inited)
            {
                Net.NetworkAPI.Init(ComChannelId, ModName, "/da");
                Log.Game.Info("Initialised NetworkAPI");
            }

            net_settings_ = new Net.NetSync<Settings>(this, Net.TransferType.ServerToClient, LoadSettings(), false, false);
            if (!net_inited)
            {
                var net_api = Net.NetworkAPI.Instance;
                cmd_handler_.Init(net_api, net_settings_);
                Log.Game.Info("Initialised command handler");
            }
            if (IsClient)
            {
                net_settings_.Fetch();
            } else
            {
                net_settings_.Push();
            }
            
        }
        private void InitPlayerManager()
        {
player_ = new PlayerManager { CushioningMultipliers = cushioning_mulipliers_};
            if (IsClient)
            {

                player_.OnApplyDamage += dmg =>
                {
                    if (hud == null)
                    {
                        Log.Game.Error("HUD is null for clientside player");
                        return;
                    }
                    hud.ShowWarning();
                };
                player_.OnSkipDamage += () => hud?.ClearWarning();
            }
            if (!IsClient || IsSP)
            {
                player_.OnApplyDamage += dmg =>
                            {
                                player_.Player.Character.DoDamage((float)dmg, MyStringHash.GetOrCompute("F = ma"), true);
                                Log.Game.Debug($"Applied damage: {dmg} to player: {player_.Player.DisplayName}");
                            };
            }
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
            players_.Clear();
            MyAPIGateway.Players.GetPlayers(players_);
            players_.RemoveAll(p => p.IsBot);

        }
        private bool PlayerHasJuice(IMyCharacter character)
        {
            return character.GetInventory().ContainItems(1, VRage.Game.ModAPI.Ingame.MyItemType.MakeComponent("NI_JuiceLvl_1"));
        }
        private float CalcCharAccel(IMyPlayer player, IMyCubeBlock parent)
        {
            var physics = player.Character.Physics;
            var worldPos = player.Character.GetPosition();
            var com = player.Character.WorldAABB.Center;
            if (parent != null)
            {
                var grid = parent.CubeGrid;
                if (grid == null)
                {
                    var fileMsg = $"Character parent was not decended from IMyCubeBlock";
                    Log.Error(fileMsg, $"{fileMsg} - This is likely a problem with your cockpit mod!");
                }
                else
                {
                    physics = grid.Physics;
                    worldPos = grid.GetPosition();
                    com = physics.CenterOfMassWorld;
                }
            }

            return physics != null ? (physics.LinearAcceleration + physics.AngularAcceleration.Cross(worldPos - com)).Length() : 0;

        }
        private float EntityAccel(IMyEntity entity)
        {
            var physics = entity?.Physics;
            if (physics == null || physics.CenterOfMassWorld == null)
            {
                throw new ArgumentException("EntityAccel passed entity with invalid physics");
            }
            return (physics.LinearAcceleration + physics.AngularAcceleration.Cross(entity.GetPosition() - physics.CenterOfMassWorld)).Length();



        }
        private bool AccelNotDueToJetpack(IMyCharacter character)
        {
            var jetpack = character.Components.Get<MyCharacterJetpackComponent>();
            return (jetpack != null && jetpack.Running && jetpack.FinalThrust.Length() > 0);
        }

        private bool ApplyAccelDamage(IMyCubeBlock parent, IMyPlayer player, float accel)
        {
            var cushionFactor = 0f;

            var parentGrid = parent == null ? null : (MyCubeGrid)parent?.CubeGrid;
            if (parentGrid != null)
            {
            }

            if (parent != null)
            {
                cushioning_mulipliers_.TryGetValue(FormatCushionLookup(parent.BlockDefinition.TypeId.ToString(), parent.BlockDefinition.SubtypeId), out cushionFactor);
            }

            if (accel > Settings_.SafeMaximum)
            {

                var inv = parent?.GetInventory();
                var juice_max = parentGrid != null ? juice_manager_.MaxLevelJuiceInInv(inv) : null;
                if (juice_max != null)
                {
                    var juice = (JuiceItem)juice_max;
                   // var juice_left = juice_manager_.QtyLeftInInv(inv, juice) >= juice.JuiceDef.ComsumptionRate;
                    if (Settings_.SafeMaximum + juice.JuiceDef.SafePointIncrease >= accel)
                    {
                        // Juice stopped damage
                        //juice.Tank.Components.Get<MyResourceSourceComponent>().SetOutput(juice.JuiceDef.ComsumptionRate);
                        //juice_manager_.RemoveJuice(inv, juice, (MyFixedPoint)juice.JuiceDef.ComsumptionRate);
                        juice.Canister.GasLevel -= juice.JuiceDef.ComsumptionRate;
                        return false;
                    }
                }
                var dmg = Math.Pow((accel - Settings_.SafeMaximum), Settings_.DamageScaleBase);
                //dmg *= 10; // Scale it up since only run every 10 ticks
                dmg *= (1 - cushionFactor);
                player.Character.DoDamage((float)dmg, MyStringHash.GetOrCompute("F = ma"), true);

                return true;
            }
            return false;

        }
        private readonly List<RayTraceHelper.RayInfo> rays_cache_ = new List<RayTraceHelper.RayInfo>();

        private void AddRayToCache(Vector3D v1, Vector3D v2)
        {
            const int FILTER_LAYER = 18;
            rays_cache_.Add(new RayTraceHelper.RayInfo() { V1 = v1, V2 = v2, FilterLayer = FILTER_LAYER });
        }
        private void GenerateRays(Vector3D v1, Vector3D v2, Vector3D v3, Vector3D v4)
        {
            AddRayToCache(v1, v2);
            AddRayToCache(v1 + v3, v2 + v3);
            AddRayToCache(v1 + v4, v2 + v4);
        }


        private IMyEntity GridStandingOn(IMyCharacter character)
        {
            var GROUND_SEARCH = 2;
            var pos = character.PositionComp.GetPosition();
            var worldRef = character.PositionComp.WorldMatrixRef;

            rays_cache_.Clear();
            ray_tracer_.Hits.Clear();

            var up = pos + worldRef.Up * 0.5;
            var down = up + worldRef.Down * GROUND_SEARCH;
            var forward = worldRef.Forward * 0.2;
            var back = -forward;

            GenerateRays(up, down, forward, back);

            var hits = ray_tracer_.CastRays(rays_cache_);

            var validHit = hits.FirstOrDefault(h => h != null && h.HitEntity != null && h.HitEntity != ((IMyCameraController)character).Entity.Components);
            if (validHit != null)
            {
                var entity = validHit.HitEntity.GetTopMostParent();

                if (Vector3D.DistanceSquared(validHit.Position, up) < (double)GROUND_SEARCH * GROUND_SEARCH)
                {
                    return entity;
                }

            }
            return null;
        }
        
        private void PlayersUpdate()
        {
            bool needs_cache_update = false;
            foreach(var p in player_cache_.Values)
            {
                if (p == null)
                {
                    Log.Game.Debug("Found null player, cache out of date?");
                    needs_cache_update = true;
                } else if (!p.IsBot)
                {
                    player_.Player = p;
                    player_.Update(settings_);
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
                if (players_need_update_ || IsSP && player_cache_.Count == 0)
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
