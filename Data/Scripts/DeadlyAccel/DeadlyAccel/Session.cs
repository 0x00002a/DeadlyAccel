﻿using System;
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
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum; // required for MyTransparentGeometry/MySimpleObjectDraw to be able to set blend type.
using Digi;
using Sandbox.Game.Entities.Character.Components;

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

        private int tick = 0;
        private const int TICKS_PER_CACHE_UPDATE = 120;

        private readonly Random rand = new Random();
        private readonly List<IMyPlayer> players_ = new List<IMyPlayer>();
        private readonly Dictionary<string, float> cushening_mulipliers_ = new Dictionary<string, float>();
        private Settings settings_ = null;
        private HUDManager hud = new HUDManager();


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

            var DefaultSettings = new Settings()
            {
                CushioningBlocks = new List<Settings.CusheningEntry>()
                {
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "PassengerSeatLarge",
                        CushenFactor = 0.6f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "PassengerSeatSmall",
                        CushenFactor = 0.55f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "LargeBlockCockpit",
                        CushenFactor = 0.7f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "SmallBlockCockpit",
                        CushenFactor = 0.65f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "LargeBlockCockpitSeat",
                        CushenFactor = 0.7f,
                    },
                    new Settings.CusheningEntry()
                    {
                        SubtypeId = "DBSmallBlockFighterCockpit",
                        CushenFactor = 0.9f,
                    }
                },
                IgnoreJetpack = true,
            };
            settings_ = Settings.TryLoad(DefaultSettings);
            settings_.Save();

            BuildCusheningCache(settings_);
        }
        private string FormatCushenLookup(string typeid, string subtypeid)
        {
            return $"{typeid}-{subtypeid}";
        }
        private void BuildCusheningCache(Settings from)
        {
            foreach(var cushen_val in from.CushioningBlocks)
            {
                cushening_mulipliers_.Add(FormatCushenLookup(cushen_val.TypeId,cushen_val.SubtypeId), cushen_val.CushenFactor);
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
                    return grid.Physics.LinearAcceleration.Length();
                }
            }
            return player.Character.Physics.LinearAcceleration.Length();

        }
        private float Clamp(float lower, float upper, float val)
        {
            return val > upper ? upper : val < lower ? lower : val;
        }
        public override void UpdateAfterSimulation()
        {
            // executed every tick, 60 times a second, after physics simulation and only if game is not paused.

            ++tick;
            try // example try-catch for catching errors and notifying player, use only for non-critical code!
            {
                // ...
                if (tick % TICKS_PER_CACHE_UPDATE == 0)
                {
                    UpdatePlayersCache();
                }

                const float g = 9.80665f;
                const float safe_max = 3 * g;
                const float death_threshold = 10 * g;

                foreach (var player in players_)
                {

                    if (!player.Character.IsDead)
                    {
                        var jetpack = player.Character.Components.Get<MyCharacterJetpackComponent>();
                        if (jetpack != null && jetpack.FinalThrust.Length() > 0 && settings_.IgnoreJetpack)
                        {
                            continue;
                        }
                        var parent = player.Character.Parent as IMyCubeBlock;
                        var accel = CalcCharAccel(player, parent);
                        var cushenFactor = 0f;

                        if (parent != null)
                        {
                            cushening_mulipliers_.TryGetValue(FormatCushenLookup(parent.BlockDefinition.TypeId.ToString(), parent.BlockDefinition.SubtypeId), out cushenFactor);
                            /* g x = x
                             * f x y = y^x
                             * h x y = (g x) - (f x y)
                             * y = cushen factor 
                             * x = accel
                             * Note: This only effects insta-death threshold
                             */
                            accel -= (float)Math.Pow(cushenFactor, accel);
                        }
                        /* 
                         * Danger zone is >3, around 10g your gonna have a hard time
                         * Safe is [0, 3] (duh)
                         */
                        if (accel > safe_max)
                        {
                            var max = (int)(100 * (1f - cushenFactor));
                            var num = (float)rand.Next((int)Clamp(0, max, accel - death_threshold), max) / 100; // Max dmg, 1 per tick or 60 per second (have to be _really_ unlucky tho)
                            player.Character.DoDamage(num, MyStringHash.GetOrCompute("F = ma"), true);

                            hud.ShowWarning();
                        } else
                        {
                            hud.ClearWarning();
                        }
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
                if (tick % 10 == 0)
                {
                    // gets called 60 times a second after all other update methods, regardless of framerate, game pause or MyUpdateOrder.
                    // NOTE: this is the only place where the camera matrix (MyAPIGateway.Session.Camera.WorldMatrix) is accurate, everywhere else it's 1 frame behind.
                    hud.Draw();
                }
            } catch(Exception e)
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
