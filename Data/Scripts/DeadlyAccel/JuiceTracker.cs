using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;
using MyInventoryItem = VRage.Game.ModAPI.Ingame.MyInventoryItem;

namespace Natomic.DeadlyAccel
{
    struct JuiceItem
    {
        public API.JuiceDefinition JuiceDef;
        public MyObjectBuilder_GasContainerObject Canister;

    }
    class JuiceTracker
    {
        public const string CANISTER_TYPE_ID = "MyObjectBuilder_OxygenContainerDefinition";

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
            var items = ((MyInventory)inv).GetItems(); // If this cast is not safe then then universe has imploded

            JuiceItem? curr_max = null;
            foreach (var item in items)
            {
                var stype_id = item.Content.SubtypeId.ToString();
                if (item.Content.TypeId.ToString() == CANISTER_TYPE_ID && items_.ContainsKey(stype_id))
                {
                    var canister = (MyObjectBuilder_GasContainerObject)item.Content;
                    var juice_def = items_[stype_id];
                    if (canister.GasLevel >= juice_def.ComsumptionRate)
                    {
                        // There is gas enough gas in the canister for at least 1 more use
                        if (curr_max == null || curr_max?.JuiceDef.ComsumptionRate < juice_def.ComsumptionRate)
                        {
                            curr_max = new JuiceItem { JuiceDef = juice_def, Canister = canister };
                        }
                    }
                }
            }
            return curr_max;

        }
    }
}
