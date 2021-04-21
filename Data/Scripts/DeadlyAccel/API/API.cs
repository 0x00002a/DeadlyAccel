using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;

namespace Natomic.DeadlyAccel.API
{
    using ApiDict = Dictionary<string, Func<object, bool>>;

    [ProtoContract]
    public struct JuiceDefinition
    {
        [ProtoMember(1)]
        public string SubtypeId;
        [ProtoMember(2)]
        public float ToxicityPerMitagated;
        [ProtoMember(3)]
        public float ToxicityDecay; // Reduction of toxicity per update (when juice not used that update)
        [ProtoMember(4)]
        public int Ranking; // Avaliable juice is consumed in order from lowest to highest ranking
        [ProtoMember(5)]
        public float ConsumptionRate; // Qty of item consumed per use

        public override string ToString()
        {
            return MyAPIGateway.Utilities.SerializeToXML(this);
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
