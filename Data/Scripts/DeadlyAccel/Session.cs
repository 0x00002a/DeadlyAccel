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
        private const int TICKS_PER_CACHE_UPDATE = 120;

        private readonly Random rand = new Random();
        private readonly List<IMyPlayer> players_ = new List<IMyPlayer>();
        private readonly Dictionary<string, float> cushioning_mulipliers_ = new Dictionary<string, float>();
        private readonly Dictionary<IMyPlayer, int> iframes_lookup_ = new Dictionary<IMyPlayer, int>();
        private Settings Settings_ => net_settings_.Value;
        private Net.NetSync<Settings> net_settings_;
        private HUDManager hud = null; // Only non-null if not server

        private readonly RayTraceHelper ray_tracer_ = new RayTraceHelper();
        private readonly ChatHandler cmd_handler_ = new ChatHandler();


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

            bool net_inited = Net.NetworkAPI.IsInitialized;
            if (!net_inited)
            {
                Net.NetworkAPI.Init(ComChannelId, ModName, "/da");
            }

            net_settings_ = new Net.NetSync<Settings>(this, Net.TransferType.ServerToClient, LoadSettings(), true, false);
            if (!net_inited)
            {
                var net_api = Net.NetworkAPI.Instance;
                cmd_handler_.Init(net_api, net_settings_);
            }
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                BuildCushioningCache(Settings_);
            } else
            {
                hud = new HUDManager();
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
        private string FormatCushionLookup(string typeid, string subtypeid)
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
                    Log.Game.Error(fileMsg);
                    Log.UI.Error($"{fileMsg} - This is likely a problem with your cockpit mod!");
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

            if (parent != null)
            {
                cushioning_mulipliers_.TryGetValue(FormatCushionLookup(parent.BlockDefinition.TypeId.ToString(), parent.BlockDefinition.SubtypeId), out cushionFactor);
            }
            if (accel > Settings_.SafeMaximum)
            {
                var dmg = Math.Pow((accel - Settings_.SafeMaximum), Settings_.DamageScaleBase);
                dmg *= (1 - cushionFactor);
                player.Character.DoDamage((float)dmg, MyStringHash.GetOrCompute("F = ma"), true);
                Log.Game.Debug($"Applied damage: {dmg} to player: {player.DisplayName}");

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
        private bool GridIgnored(IMyCubeGrid grid)
        {
            if (grid == null)
            {
                return false;
            }
            else if (Settings_.IgnoreRespawnShips && grid.IsRespawnGrid)
            {
                Log.Game.Debug($"Ignored respawn ship: {grid.CustomName}");
                return true;
            }
            else if (Settings_.IgnoredGridNames.Contains(grid.CustomName))
            {
                Log.Game.Debug($"Ignored grid: {grid.CustomName}");
                return true;
            } else
            {
                return false;
            }
        }

        private void PlayersUpdate()
        {
            foreach (var player in players_)
            {
                if (player?.Character == null)
                {
                    // In MP, player references can be null when joining 
                    Log.Game.Debug("Skipped player because null, is someone joining?");
                    continue;
                }

                if (!player.Character.IsDead)
                {

                    const int IFRAMES = 3;
                    if (!iframes_lookup_.ContainsKey(player))
                    {
                        iframes_lookup_.Add(player, 0);
                    }

                    var parentBase = player.Character.Parent;

                    if ((parentBase != null || !(AccelNotDueToJetpack(player.Character) && Settings_.IgnoreJetpack)))
                    {

                        var parent = parentBase as IMyCubeBlock;
                        if (GridIgnored(parent?.CubeGrid))
                        {
                            continue;
                        }
                        
                        var accel = CalcCharAccel(player, parent);
                        var gridOn = GridStandingOn(player.Character); // This is expensive!
                        if (gridOn != null)
                        {
                            accel = EntityAccel(gridOn);
                            iframes_lookup_[player] = IFRAMES;
                        }
                        if (iframes_lookup_[player] <= 0 || gridOn != null)
                        {
                            if (ApplyAccelDamage(parent, player, accel))
                            {
                                hud?.ShowWarning();
                                continue;
                            }
                        }
                        else if (iframes_lookup_[player] > 0)
                        {
                            iframes_lookup_[player]--;
                        }

                    }

                }

                hud?.ClearWarning();
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
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    if (tick % TICKS_PER_CACHE_UPDATE == 0)
                    {
                        UpdatePlayersCache();
                    }
                    if (tick % 10 == 0)
                    {
                        PlayersUpdate();
                    }
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
                Settings_.Save();
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
