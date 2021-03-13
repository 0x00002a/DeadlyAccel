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

        public List<IHitInfo> Hits { get => hits_; }

        private List<IHitInfo> hits_ = new List<IHitInfo>();
        private List<IHitInfo> hit_cache_ = new List<IHitInfo>();

        public List<IHitInfo> CastRays(List<Tuple<Vector3D, Vector3D, int>> rays)
        {
            foreach(var arglist in rays)
            {
                MyAPIGateway.Physics.CastRay(arglist.Item1, arglist.Item2, hit_cache_, arglist.Item3);
                hits_.AddRange(hit_cache_);
            }
            return Hits;
        }

    }
}
