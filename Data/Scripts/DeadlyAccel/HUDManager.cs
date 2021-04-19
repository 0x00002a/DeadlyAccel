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

using Draygo.API;
using System.Text;
using VRageMath;

namespace Natomic.DeadlyAccel
{
    class HUDManager
    {

        public bool Enabled { get; }

        public void Init()
        {
            api_handle_ = new HudAPIv2(OnHudInit);
        }
        private void OnHudInit()
        {
            message_ = new HudAPIv2.HUDMessage(text_, txt_origin_, null, -1, 1.5);
            message_.InitialColor = Color.Red;
        }
        public void ShowWarning()
        {
            if (text_.Length == 0)
            {
                text_.Append("Warning: Acceleration is beyond safety limits");
            }

        }
        public void ClearWarning()
        {
            text_.Clear();
        }

        public void Draw()
        {
            if (message_ != null)
            {
                message_.Visible = text_.Length > 0;
            }
        }

        private HudAPIv2.HUDMessage message_;
        private HudAPIv2 api_handle_;
        private Vector2D txt_origin_ = new Vector2D(-0.3, 0.5); // Top center
        private StringBuilder text_ = new StringBuilder("");

    }
}
