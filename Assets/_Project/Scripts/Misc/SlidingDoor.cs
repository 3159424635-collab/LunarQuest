using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 滑动门：按 E 开/关，双扇沿本地 X 轴对向滑移。扇面自带碰撞体，关门时阻挡。
    /// 挂载要求：门根物体；panelL/panelR 拖两个门扇（子物体，带 BoxCollider）。
    /// </summary>
    public class SlidingDoor : InteractableBase
    {
        public Transform panelL;
        public Transform panelR;
        public float openOffset = 1.35f;   // 单扇滑移距离（米）
        public float speed = 2.2f;         // 开合速度

        bool open;
        float t;
        Vector3 l0, r0;

        void Awake()
        {
            if (panelL) l0 = panelL.localPosition;
            if (panelR) r0 = panelR.localPosition;
        }

        void Update()
        {
            t = Mathf.MoveTowards(t, open ? 1f : 0f, speed * Time.deltaTime);
            if (panelL) panelL.localPosition = l0 + Vector3.left * (openOffset * t);
            if (panelR) panelR.localPosition = r0 + Vector3.right * (openOffset * t);
        }

        public override void Interact(GameObject who) => open = !open;
    }
}
