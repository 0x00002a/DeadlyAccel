using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace Natomic.DeadlyAccel.API
{
    using ApiDict = Dictionary<string, Func<object, bool>>;
    
    [ProtoContract]
    struct JuiceDefinition
    {
        [ProtoMember(1)]
        public string SubtypeId;
        [ProtoMember(2)]
        public float SafePointIncrease;
        [ProtoMember(3)]
        public float ComsumptionRate;

       public override string ToString()
        {
            return $"Subtype: {SubtypeId}\nSafePointIncrease: {SafePointIncrease}\nConsumeRate: {ComsumptionRate}";
        }
    }
    class DeadlyAccelAPI
    {
        private ApiDict Hooks = null;
        private Action<DeadlyAccelAPI> initCallback;

        public const long MOD_API_MSG_ID = 2422178213;

        public void Init(Action<DeadlyAccelAPI> onInit)
        {
            initCallback = onInit;
            MyAPIGateway.Utilities.RegisterMessageHandler(MOD_API_MSG_ID, Handler);
        }
        private void Handler(object obj)
        {
            if (obj is ApiDict)
            {
                Hooks = (ApiDict)obj;
                initCallback(this);
            }
        }
        public void Dispose()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(MOD_API_MSG_ID, Handler);
        }
        public void RegisterJuiceDefinition(JuiceDefinition def)
        {
            Hooks["RegisterJuice"](MyAPIGateway.Utilities.SerializeToBinary(def));
        }

    }
}
