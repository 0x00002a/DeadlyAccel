using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using Natomic.Logging;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities.Character.Components;
using VRage.ModAPI;
using Natomic.DeadlyAccel;
using VRageMath;
using System.Linq;
using VRage.Game.ModAPI.Interfaces;

namespace Natomic.DeadlyAccel
{
    class PlayerManager
    {
        public IMyPlayer Player;
        public Dictionary<string, float> CushioningMultipliers;

        public Action<double> OnApplyDamage;
        public Action OnSkipDamage;

        private int iframes_ = 0;
        private const int IFRAME_MAX = 3;

        public JuiceTracker JuiceManager;
        private readonly List<RayTraceHelper.RayInfo> rays_cache_ = new List<RayTraceHelper.RayInfo>();
        private readonly RayTraceHelper ray_tracer_ = new RayTraceHelper();

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
        private double JuiceDmgReductionCoefficent(float accel, IMyCubeBlock parent, Settings settings)
        {
            var inv = parent?.GetInventory();
            var parentGrid = parent?.CubeGrid;
                var juice_max = parentGrid != null ? JuiceManager.MaxLevelJuiceInInv(inv) : null;
            if (juice_max != null)
            {
                var juice = (JuiceItem)juice_max;
                // var juice_left = juice_manager_.QtyLeftInInv(inv, juice) >= juice.JuiceDef.ComsumptionRate;
                if (settings.SafeMaximum + juice.JuiceDef.SafePointIncrease >= accel)
                {
                    // Juice stopped damage
                    //juice.Tank.Components.Get<MyResourceSourceComponent>().SetOutput(juice.JuiceDef.ComsumptionRate);
                    //juice_manager_.RemoveJuice(inv, juice, (MyFixedPoint)juice.JuiceDef.ComsumptionRate);
                    juice.Canister.GasLevel -= juice.JuiceDef.ComsumptionRate;
                    return 0.0;
                }
            }
            return 1.0;
        }
        private double CalcAccelDamage(IMyCubeBlock parent, IMyPlayer player, float accel, Settings settings)
        {
            var cushionFactor = 0f;

            if (parent != null)
            {
                CushioningMultipliers.TryGetValue(DeadlyAccelSession.FormatCushionLookup(parent.BlockDefinition.TypeId.ToString(), parent.BlockDefinition.SubtypeId), out cushionFactor);
            }
            if (accel > settings.SafeMaximum)
            {
                var dmg = Math.Pow((accel - settings.SafeMaximum), settings.DamageScaleBase);
                dmg *= (1 - cushionFactor);
                dmg *= JuiceDmgReductionCoefficent(accel, parent, settings);
                return dmg;

            }
            return 0.0;

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
                        if (gridOn != null)
                        {
                            accel = EntityAccel(gridOn);
                            iframes_ = IFRAME_MAX;
                        }
                        if (iframes_ <= 0 || gridOn != null)
                        {
                            var dmg = CalcAccelDamage(parent, Player, accel, settings);
                            if (dmg > 0)
                            {
                                OnApplyDamage?.Invoke(dmg);
                                return;
                            }
                        }
                        else if (iframes_ > 0)
                        {
                            iframes_--;
                        }
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
