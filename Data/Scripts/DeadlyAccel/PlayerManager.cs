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
using Sandbox.Engine.Physics;
using Sandbox.Game;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRageMath;

namespace Natomic.DeadlyAccel
{
    public class PlayerManager
    {
        public class PlayerData
        {
            public int iframes;
            public double toxicity_buildup;
            public double lowest_toxic_decay;

            [XmlIgnore]
            public MyCharacterJetpackComponent jetpack;
        }
        private readonly static Guid STORAGE_GUID = new Guid("15AB8152-C66D-4064-9B5D-0F3DAE29F5F4");

        public Dictionary<string, float> CushioningMultipliers;

        private const int IFRAME_MAX = 3;
        private const double TOXICITY_CUTOFF = 100.0;

        public readonly JuiceTracker JuiceManager = new JuiceTracker();
        private readonly List<RayTraceHelper.RayInfo> rays_cache_ = new List<RayTraceHelper.RayInfo>();
        private readonly RayTraceHelper ray_tracer_ = new RayTraceHelper();
        private readonly Dictionary<long, PlayerData> players_lookup_ = new Dictionary<long, PlayerData>();
        private readonly List<JuiceItem> inv_item_cache_ = new List<JuiceItem>();

        public event Action<IMyPlayer, double> OnJuiceAvalChanged;


        #region Accel calculations
        
        private float EntityAccel(IMyEntity entity)
        {
            var physics = entity?.Physics;
            if (physics == null)
            {
                throw new ArgumentException("EntityAccel passed entity with invalid physics");
            }

            var com = physics.HasRigidBody ? physics.CenterOfMassWorld : (Vector3D)physics.Center;
            return (physics.LinearAcceleration + physics.AngularAcceleration.Cross(entity.GetPosition() - com)).Length();
        }
        internal static bool AccelNotDueToJetpack(MyCharacterJetpackComponent jetpack)
        {
            return (jetpack != null && jetpack.Running && jetpack.FinalThrust.LengthSquared() > 0);
        }


        #endregion
        #region Grid and voxel checks 
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
        private bool GridIgnored(IMyCubeGrid grid, Settings settings)
        {
            if (grid == null)
            {
                return false;
            }
            else if (settings.IgnoreRespawnShips && grid.IsRespawnGrid)
            {
                Log.Game.Debug($"Ignored respawn ship: {grid.CustomName}");
                return true;
            }
            else if (settings.IgnoredGridNames.Contains(grid.CustomName))
            {
                Log.Game.Debug($"Ignored grid: {grid.CustomName}");
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion
        #region Misc invincibility

        internal static bool MovingUnderOwnPower(IMyCharacter character)
        {
            var state = character.CurrentMovementState;
            switch(state)
            {
                // I _really_ hope Keen doesn't add any more of these damn things
                case MyCharacterMovementEnum.Ladder:
                case MyCharacterMovementEnum.LadderUp:
                case MyCharacterMovementEnum.LadderDown:
                case MyCharacterMovementEnum.LadderOut:
                case MyCharacterMovementEnum.Died:
                case MyCharacterMovementEnum.Falling:
                case MyCharacterMovementEnum.Flying:
                case MyCharacterMovementEnum.Standing:
                case MyCharacterMovementEnum.Crouching:
                    return false;
                default:
                    return true;
            }
            
        }


        #endregion
        #region Juice 
        private double ApplyJuice(double dmg, PlayerData data, List<JuiceItem> candidates, IMyInventory inv, IMyPlayer player)
        {
            if (data.toxicity_buildup >= TOXICITY_CUTOFF)
            {
                Log.Game.Debug("player has too much toxicity, skipping juice reduction");
                return dmg;
            }

            candidates.SortNoAlloc((lhs, rhs) => lhs.JuiceDef.Ranking.CompareTo(rhs.JuiceDef.Ranking));


            var i = 0;
            double aval = 0;
            while (dmg > 0 && i < candidates.Count)
            {
                var item = candidates[i];
                var units_needed = dmg * item.JuiceDef.ConsumptionRate;
                var units_used = Math.Min((double)item.Canister.Amount, units_needed);

                var dmg_blocked = 0.0;
                if (item.JuiceDef.ConsumptionRate > 0)
                {
                    dmg_blocked = units_used / item.JuiceDef.ConsumptionRate;
                }
                dmg -= dmg_blocked;

                if (units_used == (double)item.Canister.Amount)
                {    
                    ++i;        
                }
                OnJuiceAvalChanged?.Invoke(player, (double)item.Canister.Amount - units_used);
                inv.RemoveItems(item.Canister.ItemId, (MyFixedPoint)units_used);
                ApplyToxicBuildup((float)dmg_blocked, item.JuiceDef, data);
            }

            return dmg;
        }

        #endregion
        #region Toxicity tracking

        private void ResetToxicityMultiplier(PlayerData data)
        {
            data.lowest_toxic_decay = 0;
        }
        public double ToxicBuildupFor(IMyPlayer player)
        {
            if (players_lookup_.ContainsKey(player.IdentityId))
            {
                return players_lookup_[player.IdentityId].toxicity_buildup;
            } else
            {
                return 0.0;
            }
        }
        private void ApplyToxicBuildup(float units_used, API.JuiceDefinition def, PlayerData data)
        {
            data.toxicity_buildup += def.ToxicityPerMitagated * units_used;
            data.toxicity_buildup = Math.Min(data.toxicity_buildup, 100);
            if (data.lowest_toxic_decay > 0)
            {
                data.lowest_toxic_decay = Math.Min(def.ToxicityDecay, data.lowest_toxic_decay);
            } else
            {
                data.lowest_toxic_decay = def.ToxicityDecay;
            }
        }

        public void ApplyToxicityDecay(long pid, float multiplier)
        {
            if (players_lookup_.ContainsKey(pid))
            {
                var data = players_lookup_[pid];
                data.toxicity_buildup = Math.Max(data.toxicity_buildup - multiplier, 0);

                if (data.toxicity_buildup == 0)
                {
                    ResetToxicityMultiplier(data);
                }
            }
        }

       private void ApplyToxicityDecay(PlayerData data)
        {
            data.toxicity_buildup -= data.lowest_toxic_decay;
            data.toxicity_buildup = Math.Max(data.toxicity_buildup, 0);
        }

        private double CalcAccelDamage(IMyCubeBlock parent, float accel, Settings settings)
        {
            var cushionFactor = 0f;

            if (parent != null)
            {
                CushioningMultipliers.TryGetValue(DeadlyAccelSession.FormatCushionLookup(parent.BlockDefinition.TypeId.ToString(), parent.BlockDefinition.SubtypeId), out cushionFactor);
            }
            if (accel > settings.SafeMaximum)
            {
                var rem_accel = (accel - settings.SafeMaximum);
                var dmg = Math.Pow(rem_accel, settings.DamageScaleBase);
                dmg *= (1 - cushionFactor);
                return dmg;

            }
            return 0.0;

        }

        #endregion
        #region Control/top level 
        private double CalcTotalDmg(IMyPlayer player, float accel, Settings settings)
        {
            var parent = player.Character.Parent as IMyCubeBlock;

            var pdata = players_lookup_[player.IdentityId];
            var dmg = CalcAccelDamage(parent, accel, settings);

            var mod_dmg = dmg;
            if (parent != null)
            {
                inv_item_cache_.Clear();
                JuiceManager.AllJuiceInInv(inv_item_cache_, parent.GetInventory());
                mod_dmg = ApplyJuice(dmg, pdata, inv_item_cache_, parent.GetInventory(), player);
                
            }
            if (dmg == mod_dmg)
            {
                // Juice not used 
                ApplyToxicityDecay(pdata);
            }
            
            return mod_dmg;
        }

        public double Update(IMyPlayer player, Settings settings)
        {
            try
            {
                if (player?.Character == null)
                {
                    // In MP, player references can be null when joining 
                    //Log.Game.Debug("Skipped player because null, is someone joining?");
                    return 0.0;
                }

                var pid = player.IdentityId;
                RegisterPlayer(player);
                var pdata = players_lookup_[pid];
                if (pdata.jetpack == null)
                {
                    pdata.jetpack = player.Character.Components.Get<MyCharacterJetpackComponent>();
                }
                var dampers_relative_to = (player.Character as Sandbox.Game.Entities.IMyControllableEntity)?.RelativeDampeningEntity;


                var damper_dmg_ignored = pdata.jetpack == null || !pdata.jetpack.Running || settings.IgnoreRelativeDampers || dampers_relative_to == null;
                var jp_dmg_ignored = (settings.IgnoreJetpack || AccelNotDueToJetpack(pdata.jetpack)) && damper_dmg_ignored;

                if (!player.Character.IsDead
                    && (!(pdata.jetpack?.Running ?? false) ||  !jp_dmg_ignored)
                    && !GridIgnored((player.Character.Parent as IMyCubeBlock)?.CubeGrid, settings)
                    )
                {
                    IMyEntity grid_parent = null;
                    if (player.Character.Parent != null)
                    {
                        grid_parent = (player.Character.Parent as IMyCubeBlock)?.CubeGrid;
                    }
                    else
                    {
                        var standing_on = GridStandingOn(player.Character);  /* This is expensive! */ 
                        if (standing_on != null)
                        {
                            grid_parent = standing_on;
                            if (GridIgnored(standing_on as IMyCubeGrid, settings))
                            {
                                return 0.0;
                            }
                        } else
                        {
                            grid_parent = player.Character;
                        }
                    }
                    
                    var accel_reference = grid_parent ?? player.Character;


                    var accel = EntityAccel(accel_reference);

                    if (grid_parent != null || !MovingUnderOwnPower(player.Character)) // SE reports accel values in the hundreds for walking around
                    {
                        return CalcTotalDmg(player, accel, settings);
                    } 
                }

                if (players_lookup_.ContainsKey(pid))
                {
                    ApplyToxicityDecay(players_lookup_[pid]);
                }
                if (player?.Character?.Parent == null)
                {
                    OnJuiceAvalChanged?.Invoke(player, 0);
                }
                return 0.0;
            }
            catch (Exception e)
            {
                Log.Game.Error($"Failed to update player: '{player?.IdentityId}'");
                Log.Game.Error(e);
            }

            return 0.0;
        }




        #endregion
        #region Player tracking
        private void RegisterPlayer(IMyPlayer p)
        {
            if (!players_lookup_.ContainsKey(p.IdentityId))
            {
                players_lookup_.Add(p.IdentityId, LoadPlayerData(p));
            }
        }
        public void DeregisterPlayer(IMyPlayer p)
        {
            SavePlayerData(p);
            players_lookup_.Remove(p.IdentityId);
        }
        public PlayerData DataForPlayer(long pid)
        {
            PlayerData p;
            players_lookup_.TryGetValue(pid, out p);
            return p;
        }
        private PlayerData LoadPlayerData(IMyPlayer player)
        {
            if (player?.Character?.Storage == null)
            {
                return new PlayerData();
            }

            Log.Game.Debug($"Loaded stored data for {player.DisplayName}");
            return MyAPIGateway.Utilities.SerializeFromXML<PlayerData>(player.Character.Storage[STORAGE_GUID]);
        }

        public void SavePlayerData(IMyPlayer player)
        {
            if (player?.Character == null || !players_lookup_.ContainsKey(player.IdentityId))
            {
                return;
            }

            var data = players_lookup_[player.IdentityId];

            if (player.Character.Storage == null)
            {
                player.Character.Storage = new MyModStorageComponent { [STORAGE_GUID] = "" };
            }
            player.Character.Storage[STORAGE_GUID] = MyAPIGateway.Utilities.SerializeToXML(data);
            Log.Game.Debug($"Saved storage data for {player.DisplayName}");
            
        }
        #endregion
    }
}
