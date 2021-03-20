using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using System.Linq;

using MyInventoryItem = VRage.Game.ModAPI.Ingame.MyInventoryItem;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;
using Sandbox.Game;
using VRage;
using Sandbox.ModAPI;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Entities;

using Digi;
using Sandbox.Definitions;

namespace Natomic.DeadlyAccel
{
    struct JuiceItem: IComparable<JuiceItem>
    {
        public API.JuiceDefinition JuiceDef;
        public IMyGasTank Tank;

        public int CompareTo(JuiceItem other)
        {
            return JuiceDef.SafePointIncrease.CompareTo(other.JuiceDef.SafePointIncrease);
        }
    }
    class JuiceTracker
    {
        private readonly Dictionary<string, API.JuiceDefinition> items_ = new Dictionary<string, API.JuiceDefinition>(); // Lookup table for subtypeid against juice level
        private readonly List<MyInventoryItem> inventory_cache_ = new List<MyInventoryItem>();
        private readonly Dictionary<MyCubeGrid, List<IMyGasTank>> tanks_ = new Dictionary<MyCubeGrid, List<IMyGasTank>>();
        private readonly List<IMySlimBlock> blocks_cache_ = new List<IMySlimBlock>();

       /* public void InitLookupTbl(List<Settings.JuiceValue> values)
        {
            foreach(var val in values)
            {
                items_.Add(val.SubtypeId, val.SafePointIncrease);
            }

        }*/

        public void AddJuiceDefinition(API.JuiceDefinition def)
        {
            items_.Add(def.SubypeId, def);
        }
        public void UpdateTanksCache(MyCubeGrid grid)
        {
            if (!tanks_.ContainsKey(grid))
            {  
                tanks_.Add(grid, new List<IMyGasTank>());
                grid.OnBlockAdded += TankGrid_OnBlockAdded;
                grid.OnBlockRemoved += TankGrid_OnBlockRemoved;

                var blocks = grid.GetFatBlocks();

                foreach (var b in blocks)
                {
                    TankGrid_OnBlockAdded(b.SlimBlock);
                }
            }
        }
        private bool IsCustomTank(IMyGasTank tank)
        {

            var def = (MyGasTankDefinition)tank.SlimBlock.BlockDefinition;
            return items_.ContainsKey(def.StoredGasId.SubtypeName);
        }

        private void TankGrid_OnBlockRemoved(IMySlimBlock obj)
        {
            if (obj.FatBlock is IMyGasTank)
            {
                var tank = (IMyGasTank)obj.FatBlock;
                if (IsCustomTank(tank))
                {
                    var grid = (MyCubeGrid)obj.CubeGrid;
                    var tanks = tanks_[grid];
                    tanks.RemoveAtFast(tanks.IndexOf(tank));
                }
                
            }
        }

        private void TankGrid_OnBlockAdded(IMySlimBlock obj)
        {
            if (obj.FatBlock is IMyGasTank)
            {
                var tank = (IMyGasTank)obj.FatBlock;
                if (IsCustomTank(tank))
                {
                    var grid = (MyCubeGrid)obj.CubeGrid;
                    
                    tanks_[grid].Add(tank);
                }
            }
        }

        public JuiceItem? MaxLevelJuiceInInv(IMyInventory inv, MyCubeGrid grid)
        {
            if (!tanks_.ContainsKey(grid))
            {
                throw new ArgumentException("Grid is not in cache");
            }
            var tanks_on_grid = tanks_[grid];
            return tanks_on_grid
                .Where(t => t.FilledRatio > 0 && inv.IsConnectedTo(t.GetInventory()))
                .Select(t => {
                    var tankDef = (MyGasTankDefinition)t.SlimBlock.BlockDefinition;
                    return (JuiceItem?)(new JuiceItem() { Tank = t, JuiceDef = items_[tankDef.StoredGasId.SubtypeName]});
                    })
                .Max();

        }
        public void RemoveJuice(IMyInventory inv, JuiceItem item, MyFixedPoint qty)
        {
            inventory_cache_.Clear();
            
            //inv.RemoveItems(item.ItemId, qty);
            
        }

       
    }
}
