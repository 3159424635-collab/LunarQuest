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
            if (QuestManager.I == null) return;
            if (showing) QuestManager.I.OnExhibitOpened();
            else QuestManager.I.OnExhibitClosed();
        }

        void OnGUI()
        {
            if (!showing) return;
            Rect r = new Rect(Screen.width * 0.60f, Screen.height * 0.16f,
                Screen.width * 0.36f, Screen.height * 0.52f);
            GUI.Box(r, exhibitTitle);
            GUI.Label(new Rect(r.x + 14f, r.y + 36f, r.width - 28f, r.height - 90f),
                exhibitDesc + "\n\n[手机扫描展台二维码可查看 AR 模型（W9 联调启用）]");
            if (GUI.Button(new Rect(r.x + 14f, r.yMax - 42f, 130f, 28f), "关闭 (E)"))
                showing = false;
        }
    }
}
