using System;
using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 玩家交互：从视线中心发射射线，命中 InteractableBase 时显示提示，按 E 触发交互。
    /// 提示文字通过 OnPromptChanged 事件抛出，HUD 订阅即可显示——逻辑与界面解耦，
    /// 模块四拼 HUD 时不需要改这里任何代码。
    /// 挂载要求：挂在玩家根物体上（自动找子相机）。
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float interactRange = 3f;          // 交互距离（米）

        /// <summary>提示变化：看向可交互物时传入提示文字，移开时传入 null。</summary>
        public event Action<string> OnPromptChanged;

        Camera cam;
        InteractableBase current;

        void Awake() => cam = GetComponentInChildren<Camera>();

        void Update()
        {
            InteractableBase hit = null;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit info, interactRange))
                hit = info.collider.GetComponentInParent<InteractableBase>();

            // 只在目标变化时广播，避免 HUD 每帧刷新。
            // 用 ReferenceEquals 而非 !=：Unity 会把已销毁对象判为 null，
            // 样本被采集销毁后用 != 比较会漏掉这次变化，导致提示残留在屏幕上
            if (!ReferenceEquals(hit, current))
            {
                current = hit;
                OnPromptChanged?.Invoke(current ? current.prompt : null);
            }

            if (current && Input.GetKeyDown(KeyCode.E))
                current.Interact(gameObject);
        }
    }
}
