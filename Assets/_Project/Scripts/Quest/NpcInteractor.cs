using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// NPC 对话入口：按 E 触发 QuestManager 的 interact_{characterId} 事件，
    /// 由 dialogue.json 的条件决定播出哪一段（接任务/暂缓/样本不足/已完成…）。
    /// 挂载要求：NPC 物体（需 Animator 含 talk 状态）。
    /// </summary>
    public class NpcInteractor : InteractableBase
    {
        public string characterId;
        public Animator animator;

        public override void Interact(GameObject who)
        {
            if (animator) animator.CrossFade("talk", 0.15f);
            if (QuestManager.I != null) QuestManager.I.OnNpc(characterId);
        }
    }
}
