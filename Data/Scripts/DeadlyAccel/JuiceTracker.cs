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
    }
    class JuiceTracker
    {
        public const string CANISTER_TYPE_ID = "MyObjectBuilder_Component";

        private readonly Dictionary<string, API.JuiceDefinition> items_ = new Dictionary<string, API.JuiceDefinition>(); // Lookup table for subtypeid against juice level


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
        public API.JuiceDefinition ItemBySubtypeId(string id)
        {
            return items_[id];
        }
        public void AllJuiceInInv(List<JuiceItem> readin, IMyInventory inv)
        {
            if (inv == null)
            {
                return;
            }

            var inv_rel = (MyInventory)inv;
            var items = (inv_rel).GetItems(); // If this cast is not safe then then universe has imploded

            foreach (var item in items)
            {
                var stype_id = item.Content.SubtypeId.ToString();
                if (item.Content.TypeId.ToString() == CANISTER_TYPE_ID && items_.ContainsKey(stype_id))
                {
                    var juice_def = items_[stype_id];
                    readin.Add(new JuiceItem { Canister = item, JuiceDef = juice_def });
                }
            }
        }
        
    }
}
