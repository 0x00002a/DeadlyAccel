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
        public IMyPlayer Player;
        public Dictionary<string, float> CushioningMultipliers;

        public Action<double> OnApplyDamage;
        public Action OnSkipDamage;

        private readonly Dictionary<IMyPlayer, int> iframes_ = new Dictionary<IMyPlayer, int>(); 
        private const int IFRAME_MAX = 3;
        private const double TOXICITY_CUTOFF = 100.0;

        public readonly JuiceTracker JuiceManager = new JuiceTracker();
        private readonly List<RayTraceHelper.RayInfo> rays_cache_ = new List<RayTraceHelper.RayInfo>();
        private readonly Dictionary<IMyPlayer, double> toxicity_buildups_ = new Dictionary<IMyPlayer, double>();
        private readonly RayTraceHelper ray_tracer_ = new RayTraceHelper();
        private readonly List<IMyCubeBlock> parents_cache_ = new List<IMyCubeBlock>();
        private bool in_player_update_ = false;

/*
        public void RegisterParent(IMyCubeBlock parent)
        {
            if (parent != null && parent.HasInventory && juice_cache_.ContainsKey(parent))
            {

                var cache_list = new List<JuiceItem>();
                JuiceManager.AllJuiceInInv(cache_list, parent.GetInventory());
                juice_cache_[parent] = cache_list;

                var inv = (MyInventory)parent.GetInventory();
                inv.ContentsRemoved += OnParentInvUpdateRm;
                
            }
            
        }

        private void OnParentInvUpdateAdd(MyInventoryBase item)
        {

        }
        private void OnParentInvUpdateRm(MyPhysicalInventoryItem item, MyFixedPoint nb)
        {
            if (item.Content.TypeId.ToString() == JuiceTracker.CANISTER_TYPE_ID)
            {
                foreach (var parent in parents_cache_)
                {
                    var inv = (MyInventory)parent.GetInventory();
                    if (inv.)
                }
                

            }

        }
        public void DeregisterParent(IMyCubeBlock parent)
        {

        }*/
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
        public double CurrToxicBuildup()
        {
            double toxic_lvl;
            if (!toxicity_buildups_.TryGetValue(Player, out toxic_lvl))
            {
                toxic_lvl = 0;
            }
            return toxic_lvl;
        }
        private void ApplyToxicBuildup(float units_used, API.JuiceDefinition def)
        {
            var toxicity = CurrToxicBuildup(); // Have to create it first
            toxicity_buildups_[Player] = toxicity + def.ToxicityPerMitagated * units_used;        
        }
        private JuiceItem? CurrJuiceItem()
        {
            var parent = Player?.Character?.Parent as IMyCubeBlock;
            var inv = parent?.GetInventory();
            var parentGrid = parent?.CubeGrid;
            var juice_max = parentGrid != null ? JuiceManager.MaxLevelJuiceInInv(inv) : null;
            return juice_max;
        }
        
        private void ApplyToxicityDecay(double curr, API.JuiceDefinition def)
        {
            var buildup = curr - def.ToxicityDecay;
            if (buildup >= 0)
            {
                toxicity_buildups_[Player] = buildup;
            }
        }
        private double CalcAccelDamage(IMyCubeBlock parent, JuiceItem? curr_juice, float accel, Settings settings)
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
                dmg *= JuiceDmgReductionCoefficent(rem_accel, curr_juice);
                return dmg;

            }
            return 0.0;

        }
        public void UpdateParents(List<IMyPlayer> players)
        {
        }

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
        private bool PlayerHasJuice(IMyCharacter character)
        {
            return character.GetInventory().ContainItems(1, VRage.Game.ModAPI.Ingame.MyItemType.MakeComponent("NI_JuiceLvl_1"));
        }
        private int CurrIFrames()
        {
            int frames;
            if (!iframes_.TryGetValue(Player, out frames))
            {
                frames = 0;
            }
            return frames;
        }
        private double ApplyJuice(double dmg, List<JuiceItem> candidates)
        {
            var toxicity = CurrToxicBuildup();
            if (toxicity >= TOXICITY_CUTOFF)
            {
                Log.Game.Debug("Player has too much toxicity, skipping juice reduction");
                return dmg;
            }


            var i = 0;
            while(dmg > 0 && i < candidates.Count)
            {
                var item = candidates[i];
                var units_needed = dmg / item.JuiceDef.DamageMitagated;
                var units_used = Math.Min((double)item.Canister.Amount, units_needed);

                dmg -= units_used * item.JuiceDef.DamageMitagated;
                
                if (units_used == (double)item.Canister.Amount)
                {
                    ++i;
                }
                item.Inv.RemoveItems(item.Canister.ItemId, (MyFixedPoint)units_used);
                ApplyToxicBuildup((float)units_used, item.JuiceDef);
            }
            return dmg;
        }
        private void RegisterInvEvents()
        {

        }

        public void Update(Settings settings)
        {
            try
            {
                if (Player?.Character == null)
                {
                    // In MP, player references can be null when joining 
                    //Log.Game.Debug("Skipped player because null, is someone joining?");
                    return;
                }

                if (!Player.Character.IsDead)
                {
                    var juice = CurrJuiceItem();

                    var parentBase = Player.Character.Parent;

                    if ((parentBase != null || !(AccelNotDueToJetpack(Player.Character) && settings.IgnoreJetpack)))
                    {

                        var parent = parentBase as IMyCubeBlock;
                        if (GridIgnored(parent?.CubeGrid, settings))
                        {
                            return;
                        }

                        var accel = CalcCharAccel(Player, parent);
                        var gridOn = GridStandingOn(Player.Character); // This is expensive!
                        var curr_frames = CurrIFrames();
                        if (gridOn != null)
                        {
                            accel = EntityAccel(gridOn);
                            curr_frames = IFRAME_MAX;
                        }
                        if (curr_frames <= 0 || gridOn != null)
                        {
                            var dmg = CalcAccelDamage(parent, juice, accel, settings);
                            dmg = ApplyJuice(juice, dmg);

                            if (dmg > 0)
                            {
                                OnApplyDamage?.Invoke(dmg);
                                return;
                            }
                        }
                        else if (curr_frames > 0)
                        {
                            curr_frames--;
                        }
                        iframes_[Player] = curr_frames;
                    }
                    if (juice != null)
                    {
                        ApplyToxicityDecay(CurrToxicBuildup(), ((JuiceItem)juice).JuiceDef);
                    }

                }
                OnSkipDamage?.Invoke();

            }
            catch (Exception e)
            {
                Log.Game.Error($"Failed to update player: '{Player?.IdentityId}'");
                Log.Game.Error(e);
            }
        }
    }
}
