/* 
 *  DeadlyAccel Space Engineers mod
 *  Copyright (C) 2021 Natasha England-Elbro
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Sandbox.ModAPI;
using System.Collections.Generic;
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
            foreach (var arglist in rays)
            {
                MyAPIGateway.Physics.CastRay(arglist.V1, arglist.V2, hit_cache_, arglist.FilterLayer);
                hits_.AddRange(hit_cache_);
            }
            return Hits;
        }

    }
}
