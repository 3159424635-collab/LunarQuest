using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 所有可交互物的基类：月壤样本、舱门、科普展板、NPC 都继承它。
    /// 玩家看向它时 HUD 显示 prompt，按 E 调用 Interact。
    /// 新增一种交互物 = 写一个子类重写 Interact，交互检测逻辑零改动。
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour
    {
        [Tooltip("看向该物体时 HUD 显示的提示文字")]
        public string prompt = "按 E 交互";

        public abstract void Interact(GameObject who);
    }
}
