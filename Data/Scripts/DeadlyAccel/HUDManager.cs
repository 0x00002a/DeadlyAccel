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
using System;

namespace Natomic.DeadlyAccel
{
    static class CoordHelper
    {
        static Vector2D KEEN_ORIGIN = new Vector2D(-1, 1); // I wish I was joking

        /// <summary>
        /// Translates a keen -1, 1 -> 1, -1 based coordinate into a 0, 0 -> 1, 1 coordinate that can actually be reasoned about
        /// </summary>
        /// <param name="insane"></param>
        /// <returns></returns>
        public static Vector2D KeenCoordToSaneCoord(Vector2D insane)
        {
            var translation = new Vector2D(MathHelperD.Distance(insane.X, KEEN_ORIGIN.X), MathHelperD.Distance(insane.Y, KEEN_ORIGIN.Y));
            return translation / 2;
        }
        public static Vector2D SaneCoordToKeenCoord(Vector2D sane)
        {
            return Vector2D.Zero;
        }
    }
    class TextLabel { 
        public bool Visible { set { widget_.Visible = value; } get { return widget_.Visible; } }
        public double Scale { set { widget_.Scale = value; } get { return widget_.Scale; } }
        public Color InitialColor { set { widget_.InitialColor = value; } get { return widget_.InitialColor; } }

        public TextLabel(Vector2D location)
        {
            location_ = location;
        }

        public void Append(string txt)
        {
            widget_.Message.Append(txt);
            RecalcPos();
        }
        public void Clear()
        {
            widget_.Message.Clear();
            RecalcPos();
        }
        private void RecalcPos()
        {
            var size = widget_.GetTextLength();
            var center = size / 2;

            widget_.Origin = location_ - center;
        }
        public void Init()
        {
            if (widget_ == null)
            {
                widget_ = new HudAPIv2.HUDMessage(new StringBuilder(), Origin: location_);
            }
        }

        public void Draw()
        {
        }

        private Vector2D location_;
        private HudAPIv2.HUDMessage widget_;

    }
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
                    Height: 0.05f
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
        public MyStringId ACCEL_WARNING_MAT = MyStringId.GetOrCompute("NI_DeadlyAccel_AccelWarning");

        public void Init()
        {
            api_handle_ = new HudAPIv2(OnHudInit);
        }
        private void OnHudInit()
        {
            accel_warn_ = new HudAPIv2.BillBoardHUDMessage(
                Material: ACCEL_WARNING_MAT,
                Origin: new Vector2D(0, 0.3),
                BillBoardColor: Color.White,
                Width: 0.15f,
                Height: 0.25f

                );

            toxicity_levels_.Init();
            toxicity_lbl_.Init();
            toxicity_lbl_.Append("Toxicity");

            hud_initialised_ = true;
        }
        public void ShowWarning()
        {
            if (hud_initialised_)
            {
                accel_warn_.Visible = true;
            }
        }
        public void ClearWarning()
        {
            if (hud_initialised_)
            {
                accel_warn_.Visible = false;
            }
        }

        public void Draw()
        {
            if (hud_initialised_)
            {
                toxicity_levels_.Progress = 100;//MathHelper.CeilToInt(ToxicityLevels);
                toxicity_lbl_.Visible = toxicity_levels_.Progress > 0;
                toxicity_levels_.Draw();
            }
        }

        private HudAPIv2.BillBoardHUDMessage accel_warn_;
        private TextLabel toxicity_lbl_ = new TextLabel(new Vector2D(0.8, 0.9));
        private ProgressBar toxicity_levels_ = new ProgressBar(new Vector2D(0.8, 0.7), 4);
        private HudAPIv2 api_handle_;
        private bool hud_initialised_ = false;

    }
}
