using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 区域触发事件：玩家进入触发器范围时向 QuestManager 发一次事件
    /// （如 enter_crater03_sampling_area）。挂载要求：带 isTrigger 碰撞体的物体。
    /// </summary>
    public class AreaEvent : MonoBehaviour
    {
        public string eventName;
        bool fired;

        void OnTriggerEnter(Collider other)
        {
            if (fired || !other.CompareTag("Player")) return;
            fired = true;
            if (QuestManager.I != null) QuestManager.I.FireEvent(eventName);
        }
    }
}
