using System;
using System.Collections.Generic;
using System.Text;
using Draygo.API;
using VRageMath;
using Digi;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

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
