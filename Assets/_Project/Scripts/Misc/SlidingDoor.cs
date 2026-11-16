using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 科幻滑动门：双扇沿本地 X 轴对向侧滑。默认自动感应——玩家/月球车靠近自动开、
    /// 离开自动合（探测当前主相机位置，步行与驾驶状态均生效）；按 E 可手动开关。
    /// 挂载要求：门根物体；panelL/panelR 拖两个门扇（子物体，带碰撞体）。
    /// </summary>
    public class SlidingDoor : InteractableBase
    {
        public Transform panelL;
        public Transform panelR;
        public float openOffset = 1.35f;   // 单扇滑移距离（米）
        public float speed = 2.2f;         // 开合速度
        public bool autoOpen = true;       // 自动感应开关
        public float autoRange = 3.5f;     // 感应半径（米）

        bool open;
        bool manualHold;
        float t;
        Vector3 l0, r0;

        void Awake()
        {
            if (panelL) l0 = panelL.localPosition;
            if (panelR) r0 = panelR.localPosition;
        }

        void Update()
        {
            if (autoOpen)
            {
                var cam = Camera.main;
                if (cam)
                {
                    bool near = (cam.transform.position - transform.position).sqrMagnitude
                        < autoRange * autoRange;
                    if (near) open = true;
                    else if (!manualHold) open = false;
                }
            }

            t = Mathf.MoveTowards(t, open ? 1f : 0f, speed * Time.deltaTime);
            if (panelL) panelL.localPosition = l0 + Vector3.left * (openOffset * t);
            if (panelR) panelR.localPosition = r0 + Vector3.right * (openOffset * t);
        }

        public override void Interact(GameObject who)
        {
            open = !open;
            manualHold = open;   // 手动操作后暂缓自动关闭，离开感应范围再恢复
        }
    }
}
