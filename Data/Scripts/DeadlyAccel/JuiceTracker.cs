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
using Sandbox.Common.ObjectBuilders.Definitions;

namespace Natomic.DeadlyAccel
{
    struct JuiceItem: IComparable<JuiceItem>
    {
        public API.JuiceDefinition JuiceDef;
        public MyObjectBuilder_GasContainerObject Canister;

        public int CompareTo(JuiceItem other)
        {
            return JuiceDef.SafePointIncrease.CompareTo(other.JuiceDef.SafePointIncrease);
        }
    }
    class JuiceTracker
    {
        private readonly Dictionary<string, API.JuiceDefinition> items_ = new Dictionary<string, API.JuiceDefinition>(); // Lookup table for subtypeid against juice level
        private readonly List<MyInventoryItem> inventory_cache_ = new List<MyInventoryItem>();
        private readonly List<IMySlimBlock> blocks_cache_ = new List<IMySlimBlock>();


        public void AddJuiceDefinition(API.JuiceDefinition def)
        {
            items_.Add(def.SubtypeId, def);
        }
       public JuiceItem? MaxLevelJuiceInInv(IMyInventory inv)
        {
            inventory_cache_.Clear();
            var items = inv.GetItems();

            return items
                .Where(i => i.Content.TypeId.ToString() == "MyObjectBuilder_OxygenContainerDefinition" && items_.ContainsKey(i.Content.SubtypeId.ToString()))
                .Select(i => new JuiceItem { JuiceDef = items_[i.Content.SubtypeId.ToString()], Canister = i.Content as MyObjectBuilder_GasContainerObject })
                .Where(item => item.Canister.GasLevel >= item.JuiceDef.ComsumptionRate)
                .Select(i => (JuiceItem?)i)
                .Max();
        }       
    }
}
