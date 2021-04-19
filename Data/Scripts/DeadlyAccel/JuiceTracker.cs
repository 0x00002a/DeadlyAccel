using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using Natomic.Logging;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;
using MyInventoryItem = VRage.Game.ModAPI.Ingame.MyInventoryItem;
using VRage.Game.Entity;

namespace Natomic.DeadlyAccel
{
    struct JuiceItem
    {
        public API.JuiceDefinition JuiceDef;
        public MyPhysicalInventoryItem Canister;
        public MyInventory Inv;
    }
    class JuiceTracker
    {
        public const string CANISTER_TYPE_ID = "MyObjectBuilder_Component";

        private readonly Dictionary<string, API.JuiceDefinition> items_ = new Dictionary<string, API.JuiceDefinition>(); // Lookup table for subtypeid against juice level
        private readonly List<MyInventoryItem> inventory_cache_ = new List<MyInventoryItem>();
        private readonly List<IMySlimBlock> blocks_cache_ = new List<IMySlimBlock>();


        public void AddJuiceDefinition(API.JuiceDefinition def)
        {
            if (!items_.ContainsKey(def.SubtypeId))
            {
                items_.Add(def.SubtypeId, def);
            } else
            {
                Log.Game.Info($"Skipped juice def '{def.SubtypeId}' because it has already been added");
            }
        }
        public JuiceItem? MaxLevelJuiceInInv(IMyInventory inv)
        {
            inventory_cache_.Clear();
            var inv_rel = (MyInventory)inv;
            var items = (inv_rel).GetItems(); // If this cast is not safe then then universe has imploded

            JuiceItem? curr_max = null;
            foreach (var item in items)
            {
                var stype_id = item.Content.SubtypeId.ToString();
                if (item.Content.TypeId.ToString() == CANISTER_TYPE_ID && items_.ContainsKey(stype_id))
                {
                    var juice_def = items_[stype_id];
                    if ((float)item.Amount >= juice_def.ComsumptionRate)
                    {
                        // There is gas enough gas in the canister for at least 1 more use
                        if (curr_max == null || curr_max?.JuiceDef.ComsumptionRate < juice_def.ComsumptionRate)
                        {
                            curr_max = new JuiceItem { JuiceDef = juice_def, Canister = item, Inv = inv_rel };
                        }
                    }
                }
            }
            return curr_max;

        }
    }
}
