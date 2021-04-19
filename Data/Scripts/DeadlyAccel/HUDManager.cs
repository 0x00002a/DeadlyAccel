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
using VRage.Utils;
using VRageMath;
using Natomic.Logging;

namespace Natomic.DeadlyAccel
{
    class ProgressBar
    {
        private int progress_;
        private Vector2D location_;
        private int max_width_;

        public int Progress { set { widget_.Visible = value != 0; progress_ = value; } get { return progress_; } }
        public const int MAX_PROGRESS = 100;

        public ProgressBar(Vector2D location, int max_w)
        {
            location_ = location;
            max_width_ = max_w;
        }

        public void Init()
        {
            if (widget_ == null)
            {
                widget_ = new HudAPIv2.BillBoardHUDMessage(
                    Material: MyStringId.GetOrCompute("Square"),
                    Origin: location_,
                    BillBoardColor: Color.Red,
                    Height: 0.1f
                    );
            }

        }

        public void Draw()
        {
            widget_.Width = progress_ == 0 ? 0 : (float)progress_ / MAX_PROGRESS;
        }

        private HudAPIv2.BillBoardHUDMessage widget_;
    }

    class HUDManager
    {

        public bool Enabled { get; }
        public double ToxicityLevels;

        public void Init()
        {
            api_handle_ = new HudAPIv2(OnHudInit);
        }
        private void OnHudInit()
        {
            message_ = new HudAPIv2.HUDMessage(text_, txt_origin_, null, -1, 1.5);
            message_.InitialColor = Color.Red;

            toxicity_levels_.Init();
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

                toxicity_levels_.Progress = 50;//(int)ToxicityLevels;
                toxicity_levels_.Draw();
            }
            
        }

        private HudAPIv2.HUDMessage message_;
        private ProgressBar toxicity_levels_ = new ProgressBar(new Vector2D(-0.8, -0.7), 4);
        private HudAPIv2 api_handle_;
        private Vector2D txt_origin_ = new Vector2D(-0.3, 0.5); // Top center
        private StringBuilder text_ = new StringBuilder("");

    }
}
