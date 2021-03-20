using System;
using System.Collections.Generic;
using System.Text;

namespace Natomic.DeadlyAccel.API
{
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
}
