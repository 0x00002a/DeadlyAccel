using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Natomic.DeadlyAccel.API
{
    using ApiDict = Dictionary<string, Func<object, bool>>;
    struct JuiceDefinition
    {
        public string SubypeId;
        public float SafePointIncrease;
        public float ConsumeRate;

        public override string ToString()
        {
            return $"Subtype: {SubypeId}\nSafePointIncrease: {SafePointIncrease}\nConsumeRate: {ConsumeRate}";
        }
    }
    class DeadlyAccelAPI
    {
        public ApiDict Hooks = null;

        public const long MOD_API_MSG_ID = 2422178213;

        public void Init(Action<ApiDict> onRegister)
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(MOD_API_MSG_ID, obj =>
            {
                if (obj is ApiDict)
                {
                    Hooks = (ApiDict)obj;
                    onRegister(Hooks);
                }
            });
        }

    }
}
