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
using System.Linq;

namespace Natomic.DeadlyAccel
{
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
    class FlashController<T> where T : HudAPIv2.MessageBase
    {
        public bool Flashing;
        public int IntervalTicks; // Ticks per switch 
        public T Widget;

        private int ticks_since_switch_ = 0;


        public void Init(T widget)
        {
            Widget = widget;
        }
        public void Draw()
        {
            if (Flashing)
            {
                if (ticks_since_switch_ >= IntervalTicks)
                {
                    ticks_since_switch_ = 0;
                    Widget.Visible = !Widget.Visible;
                }
                else
                {
                    ++ticks_since_switch_;
                }

            }
        }

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
        private class ToxicityHUD
        {
            private int toxicity_;
            public int Toxicity
            {
                get { return toxicity_; }
                set
                {
                    if (toxicity_levels_.Widget != null)
                    {
                        toxicity_ = value;
                        UpdateToxicityColor();
                    }
                }
            }
            private MyStringId HAZZARD_00 = MyStringId.GetOrCompute("NI_DeadlyAccel_BiohazardSymbol_0");
            private MyStringId HAZZARD_20 = MyStringId.GetOrCompute("NI_DeadlyAccel_BiohazardSymbol_20");
            private MyStringId HAZZARD_60 = MyStringId.GetOrCompute("NI_DeadlyAccel_BiohazardSymbol_60");
            private MyStringId HAZZARD_80 = MyStringId.GetOrCompute("NI_DeadlyAccel_BiohazardSymbol_80");

            public Vector2D EMERGENCY_POS = new Vector2D(0.2, 0.8);

            public bool EmergencyMode => Toxicity >= 95;

            public Vector2D ICON_NORMAL_POS = new Vector2D(0.6, -0.8);

            public HudAPIv2.BillBoardHUDMessage Icon => toxicity_levels_.Widget;

            private void UpdateToxicityColor()
            {
                if (toxicity_levels_.Widget != null)
                { 
                    var draw = toxicity_ > 0;

                    toxicity_levels_.Widget.Visible = draw;
                    toxicity_lbl_.Visible = draw;

                    toxicity_levels_.Flashing = EmergencyMode;
                    if (EmergencyMode) {
                        //toxicity_levels_.Widget.Origin = EMERGENCY_POS;
                    }


                    if (draw)
                    {
                        var new_mat = toxicity_ > 80 ? HAZZARD_80 : toxicity_ > 60 ? HAZZARD_60 : toxicity_ > 20 ? HAZZARD_20 : HAZZARD_00;
                        toxicity_levels_.Widget.Material = new_mat;
                        toxicity_lbl_.Clear();
                        toxicity_lbl_.Append($"{toxicity_}%");
                    }
                }
            }
            public void Init()
            {
                toxicity_lbl_.Init();

                toxicity_levels_.Flashing = false;
                toxicity_levels_.IntervalTicks = 15;
                toxicity_levels_.Init(new HudAPIv2.BillBoardHUDMessage(
                    Material: MyStringId.GetOrCompute("NI_DeadlyAccel_BiohazardSymbol"),
                    Origin: ICON_NORMAL_POS,
                    BillBoardColor: Color.White,
                    Width: 0.1f,
                    Height: 0.15f

                    ));
            }

            private TextLabel toxicity_lbl_ = new TextLabel(new Vector2D(-0.8, -0.4));
            private FlashController<HudAPIv2.BillBoardHUDMessage> toxicity_levels_ = new FlashController<HudAPIv2.BillBoardHUDMessage>();
        }

        public bool Enabled { get; }
        public double ToxicityLevels
        {
            set
            {
                if (value != toxicity_handler_.Toxicity)
                {
                    toxicity_handler_.Toxicity = (int)value; 
                    UpdateWarningPos();
                }
            }
        }
        public MyStringId ACCEL_WARNING_MAT = MyStringId.GetOrCompute("NI_DeadlyAccel_AccelWarning");
        private Vector2D WARNING_EMERGENCY_POS = new Vector2D(-0.2, 0.8);
        private Vector2D WARNING_NORMAL_POS = new Vector2D(0, 0.8);

        void UpdateWarningPos()
        {
            if (hud_initialised_)
            {
                if (toxicity_handler_.EmergencyMode)
                {
                    //CoordHelper.LayoutHorizontally(-0.15, 0.8, 0.15, accel_warn_.Widget, toxicity_handler_.Icon);
                    double start_x;
                    if (accel_warn_.Flashing)
                    {
                        start_x = -0.1;
                        accel_warn_.Widget.Origin = new Vector2D(start_x, 0.8);
                    } else
                    {
                        start_x = 0;
                    }
                    toxicity_handler_.Icon.Origin = new Vector2D(Math.Abs(start_x), 0.8);
                    
                    //accel_warn_.Widget.Origin = WARNING_EMERGENCY_POS;
                }
                else
                {
                    accel_warn_.Widget.Origin = WARNING_NORMAL_POS;
                    toxicity_handler_.Icon.Origin = toxicity_handler_.ICON_NORMAL_POS;
                }
            }
        }
        public void Init()
        {
            api_handle_ = new HudAPIv2(OnHudInit);
            accel_warn_.IntervalTicks = 20;
            accel_warn_.Flashing = false;
        }
        private void OnHudInit()
        {
            
            accel_warn_.Init(new HudAPIv2.BillBoardHUDMessage(
                Material: ACCEL_WARNING_MAT,
                Origin: WARNING_NORMAL_POS,
                BillBoardColor: Color.White,
                Width: 0.15f,
                Height: 0.25f
                ));
            toxicity_handler_.Init();

            hud_initialised_ = true;
        }
        public void ShowWarning()
        {
            if (hud_initialised_)
            {
                accel_warn_.Flashing = true;
                UpdateWarningPos();
            }
        }
        public void ClearWarning()
        {
            if (hud_initialised_)
            {
                accel_warn_.Flashing = false;
                accel_warn_.Widget.Visible = false;
                UpdateWarningPos();
            }
        }

        public void Draw()
        {
            if (hud_initialised_)
            {
                //toxicity_handler_.Toxicity = 95;
                accel_warn_.Draw();
                /*toxicity_handler_
                toxicity_levels_.Progress = 100;//MathHelper.CeilToInt(ToxicityLevels);
                toxicity_lbl_.Visible = toxicity_levels_.Progress > 0;
                toxicity_levels_.Draw();*/
            }
        }


        private ToxicityHUD toxicity_handler_ = new ToxicityHUD();
        private FlashController<HudAPIv2.BillBoardHUDMessage> accel_warn_ = new FlashController<HudAPIv2.BillBoardHUDMessage>();
        private HudAPIv2 api_handle_;
        private bool hud_initialised_ = false;

    }
}
