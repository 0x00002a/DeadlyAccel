using Natomic.Logging;
using Sandbox.Game;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRageMath;

namespace Natomic.DeadlyAccel
{
    class PlayerManager
    {
        class PlayerData
        {
            public int iframes = 0;
            public double toxicity_buildup = 0.0;
            public double lowest_toxic_decay = 0.0;
        }

        public Dictionary<string, float> CushioningMultipliers;

        private const int IFRAME_MAX = 3;
        private const double TOXICITY_CUTOFF = 100.0;

        public readonly JuiceTracker JuiceManager = new JuiceTracker();
        private readonly List<RayTraceHelper.RayInfo> rays_cache_ = new List<RayTraceHelper.RayInfo>();
        private readonly RayTraceHelper ray_tracer_ = new RayTraceHelper();
        private readonly Dictionary<IMyPlayer, PlayerData> players_lookup_ = new Dictionary<IMyPlayer, PlayerData>();
        private readonly List<JuiceItem> inv_item_cache_ = new List<JuiceItem>();

        #region Accel calculations
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
        private int CurrIFrames(IMyPlayer player)
        {
            var data = players_lookup_[player];
            return data.iframes;
        }
        private bool UpdateIframes(PlayerData data, IMyEntity standing_on)
        {
            if (standing_on != null)
            {
                data.iframes = IFRAME_MAX;
            }
            if (data.iframes <= 0 || standing_on != null)
            {
                return true;
            }
            else if (data.iframes > 0)
            {
                data.iframes--;
            }
            return false;
        }

        #endregion
        #region Juice 
        private double ApplyJuice(double dmg, PlayerData data, List<JuiceItem> candidates, IMyInventory inv)
        {
            if (data.toxicity_buildup >= TOXICITY_CUTOFF)
            {
                Log.Game.Debug("player has too much toxicity, skipping juice reduction");
                return dmg;
            }

            candidates.SortNoAlloc((lhs, rhs) => lhs.JuiceDef.Ranking.CompareTo(rhs.JuiceDef.Ranking));


            var i = 0;
            while (dmg > 0 && i < candidates.Count)
            {
                var item = candidates[i];
                var units_needed = dmg / item.JuiceDef.DamageMitagated;
                var units_used = Math.Min((double)item.Canister.Amount, units_needed);

                dmg -= units_used * item.JuiceDef.DamageMitagated;

                if (units_used == (double)item.Canister.Amount)
                {
                    ++i;
                }
                inv.RemoveItems(item.Canister.ItemId, (MyFixedPoint)units_used);
                ApplyToxicBuildup((float)units_used, item.JuiceDef, data);
            }
            return dmg;
        }

        #endregion
        #region Toxicity tracking
        public double ToxicBuildupFor(IMyPlayer player)
        {
            return players_lookup_[player].toxicity_buildup;
        }
        private void ApplyToxicBuildup(float units_used, API.JuiceDefinition def, PlayerData data)
        {
            data.toxicity_buildup += def.ToxicityPerMitagated * units_used;
            data.lowest_toxic_decay = Math.Min(def.ToxicityDecay, data.lowest_toxic_decay);
        }


        private void ApplyToxicityDecay(PlayerData data)
        {
            data.toxicity_buildup -= data.lowest_toxic_decay;
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
            inv_item_cache_.Clear();
            JuiceManager.AllJuiceInInv(inv_item_cache_, parent.GetInventory());


            var dmg = CalcAccelDamage(parent, accel, settings);
            dmg = ApplyJuice(dmg, players_lookup_[player], inv_item_cache_, parent.GetInventory());
            return dmg;
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
                if (!player.Character.IsDead
                    && player.Character.Parent != null
                    && !(settings.IgnoreJetpack && AccelNotDueToJetpack(player.Character))
                    && !GridIgnored((player.Character.Parent as IMyCubeBlock)?.CubeGrid, settings)
                    )
                {
                    RegisterPlayer(player);


                    var accel = CalcCharAccel(player, player.Character.Parent as IMyCubeBlock);
                    var gridOn = GridStandingOn(player.Character); // This is expensive!
                    var iframe_protected = UpdateIframes(players_lookup_[player], gridOn);
                    if (!iframe_protected)
                    {

                        if (gridOn != null)
                        {
                            accel = EntityAccel(gridOn);
                        }
                        return CalcTotalDmg(player, accel, settings);
                    }

                }

                if (players_lookup_.ContainsKey(player))
                {
                    ApplyToxicityDecay(players_lookup_[player]);
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
            if (!players_lookup_.ContainsKey(p))
            {
                players_lookup_.Add(p, new PlayerData());
            }
        }
        public void DeregisterPlayer()
        {

        }
        #endregion
    }
}
