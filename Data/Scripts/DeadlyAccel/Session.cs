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
using Digi;
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
        private const string ModName = "Deadly Acceleration";

        private int tick = 0;
        private const int TICKS_PER_CACHE_UPDATE = 120;

        private readonly Random rand = new Random();
        private readonly List<IMyPlayer> players_ = new List<IMyPlayer>();
        private readonly Dictionary<string, float> cushioning_mulipliers_ = new Dictionary<string, float>();
        private readonly Dictionary<IMyPlayer, int> iframes_lookup_ = new Dictionary<IMyPlayer, int>();
        private Settings Settings_ => net_settings_.Value;
        private Net.NetSync<Settings> net_settings_;
        private readonly HUDManager hud = new HUDManager();

        private readonly RayTraceHelper ray_tracer_ = new RayTraceHelper();


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

            if (!Net.NetworkAPI.IsInitialized)
            {
                Net.NetworkAPI.Init(ComChannelId, ModName);
            }

            net_settings_ = new Net.NetSync<Settings>(this, Net.TransferType.ServerToClient, LoadSettings(), true, false);

            BuildCushioningCache(Settings_);
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
            };

            if (!MyAPIGateway.Multiplayer.IsServer)
            {
                return DefaultSettings;
            }
            else
            {
                var settings = Settings.TryLoad(DefaultSettings);
                settings.Save();
                return settings;
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
            hud.Init();
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
        private float Clamp(float lower, float upper, float val)
        {
            return val > upper ? upper : val < lower ? lower : val;
        }

        private bool AccelNotDueToJetpack(IMyCharacter character)
        {
            var jetpack = character.Components.Get<MyCharacterJetpackComponent>();
            return (jetpack != null && jetpack.Running && jetpack.FinalThrust.Length() > 0);
        }
        private bool PlayerTryingToMove()
        {
            if (!MyAPIGateway.Multiplayer.IsServer && !MyAPIGateway.Utilities.IsDedicated)
            {
                return MyAPIGateway.Input.GetPositionDelta().LengthSquared() > 0;
            }
            return false;
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
            rays_cache_.Add(new RayTraceHelper.RayInfo() { V1 = v1, V2 = v2, FilterLayer= FILTER_LAYER });
        }
        private void GenerateRays(Vector3D v1, Vector3D v2, Vector3D v3, Vector3D v4)
        {
            AddRayToCache(v1, v2);
            AddRayToCache(v1 + v3, v2 + v3);
            AddRayToCache(v1 + v4, v2 + v4);
        }
        public static bool AnyBlocksInsideSphereFast(MyCubeGrid grid, ref BoundingSphereD sphere, bool checkDestroyed)
        {
            var radius = sphere.Radius;
            radius *= grid.GridSizeR;
            var center = grid.WorldToGridInteger(sphere.Center);
            var gridMin = grid.Min;
            var gridMax = grid.Max;
            double radiusSq = radius * radius;
            int radiusCeil = (int)Math.Ceiling(radius);
            int i, j, k;
            Vector3I max2 = Vector3I.Min(Vector3I.One * radiusCeil, gridMax - center);
            Vector3I min2 = Vector3I.Max(Vector3I.One * -radiusCeil, gridMin - center);
            for (i = min2.X; i <= max2.X; ++i)
            {
                for (j = min2.Y; j <= max2.Y; ++j)
                {
                    for (k = min2.Z; k <= max2.Z; ++k)
                    {
                        if (i * i + j * j + k * k < radiusSq)
                        {
                            MyCube cube;
                            var vector3I = center + new Vector3I(i, j, k);

                            if (grid.TryGetCube(vector3I, out cube))
                            {
                                var slim = (IMySlimBlock)cube.CubeBlock;
                                if (slim.Position == vector3I)
                                {
                                    if (checkDestroyed && slim.IsDestroyed)
                                        continue;

                                    return true;

                                }
                            }
                        }
                    }
                }
            }
            return false;
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
            var right = worldRef.Right * 0.2;
            var left = worldRef.Left * 0.2;

            GenerateRays(up, down, forward, back);
            //GenerateRays(right, left, forward, back);

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
            foreach (var player in players_)
            {

                if (!player.Character.IsDead)
                {
                    if (tick % 60 == 0)
                    {

                    }
                    var parentBase = player.Character.Parent;


                    const int IFRAMES = 3;
                    if (!iframes_lookup_.ContainsKey(player))
                    {
                        iframes_lookup_.Add(player, 0);
                    }
                    if ((parentBase != null || !(AccelNotDueToJetpack(player.Character) && Settings_.IgnoreJetpack)))
                    {
                        
                        var parent = parentBase as IMyCubeBlock;
                        var accel = CalcCharAccel(player, parent);
                        var gridOn = GridStandingOn(player.Character); // This is expensive!
                        if (gridOn != null)
                        {
                            accel = EntityAccel(gridOn);
                            iframes_lookup_[player] = IFRAMES;
                        }
                        else if (iframes_lookup_[player] <= 0)
                        {
                            if (ApplyAccelDamage(parent, player, accel))
                            {
                                hud.ShowWarning();
                                continue;
                            }
                        } else if (iframes_lookup_[player] > 0)
                        {
                            iframes_lookup_[player]--;
                        }
                        
                    }

                }

                hud.ClearWarning();
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
                if (MyAPIGateway.Multiplayer.IsServer) {
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

            catch (Exception e) // NOTE: never use try-catch for code flow or to ignore errors! catching has a noticeable performance impact.
            {
                Log.Error(e, e.Message);

            }
        }

        public override void Draw()
        {
            try
            {
                // gets called 60 times a second after all other update methods, regardless of framerate, game pause or MyUpdateOrder.
                // NOTE: this is the only place where the camera matrix (MyAPIGateway.Session.Camera.WorldMatrix) is accurate, everywhere else it's 1 frame behind.
                hud.Draw();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to draw: {e}", "Failed to draw, see log for details");
            }
        }

        public override void SaveData()
        {
            // executed AFTER world was saved
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
