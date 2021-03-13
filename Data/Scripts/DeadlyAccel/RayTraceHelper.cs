using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using VRageMath;

namespace Natomic.DeadlyAccel
{
    class RayTraceHelper
    {
        public struct RayInfo
        {
            public Vector3D V1;
            public Vector3D V2;
            public int FilterLayer;
        }

        public List<IHitInfo> Hits { get { return hits_; } }

        private List<IHitInfo> hits_ = new List<IHitInfo>();
        private List<IHitInfo> hit_cache_ = new List<IHitInfo>();

        public List<IHitInfo> CastRays(List<RayInfo> rays)
        {
            foreach(var arglist in rays)
            {
                MyAPIGateway.Physics.CastRay(arglist.V1, arglist.V2, hit_cache_, arglist.FilterLayer);
                hits_.AddRange(hit_cache_);
            }
            return Hits;
        }

    }
}
