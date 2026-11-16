using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 展览台：按 E 弹出展品数据卡（标题/介绍/二维码提示），再按 E 或点按钮关闭。
    /// 手机扫码进入 AR 场景查看拟真模型的部分由 S6 场景承担（二维码真实编码 W9 接入）。
    /// 挂载要求：展台物体；字段在 Inspector 里填展品内容。
    /// </summary>
    public class ExhibitStand : InteractableBase
    {
        [TextArea] public string exhibitTitle = "展品";
        [TextArea(4, 8)] public string exhibitDesc = "介绍文本";

        bool showing;

        public override void Interact(GameObject who)
        {
            showing = !showing;
            if (SciFiHUD.I != null)
            {
                if (showing) SciFiHUD.I.ShowCard(exhibitTitle,
                    exhibitDesc + "\n\n[手机扫描展台二维码可查看 AR 模型（W9 联调启用）]");
                else SciFiHUD.I.HideCard();
            }
            if (QuestManager.I == null) return;
            if (showing) QuestManager.I.OnExhibitOpened();
            else QuestManager.I.OnExhibitClosed();
        }
    }
}
