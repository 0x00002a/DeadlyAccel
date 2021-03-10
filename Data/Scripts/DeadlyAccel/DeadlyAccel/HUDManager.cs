using System;
using System.Collections.Generic;
using System.Text;
using Draygo.API;
using VRageMath;

namespace Natomic.DeadlyAccel
{
    class HUDManager
    {
        
        public bool Enabled { get;  }

        public void Init()
        {
            api_handle_ = new HudAPIv2(OnHudInit);
        }
        private void OnHudInit()
        {
            message_ = new HudAPIv2.HUDMessage(text_, txt_origin_);


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
            message_.Visible = text_.Length > 0;
        }

        private HudAPIv2.HUDMessage message_;
        private HudAPIv2 api_handle_;
        private Vector2D txt_origin_ = new Vector2D(0.1, 0.5); // Center vertically and slightly in horizontal
        private StringBuilder text_ = new StringBuilder("");

    }
}
