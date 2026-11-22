using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 勘测点样本石：按 E 采集，交给 QuestManager 计数（唯一勘测点、接取校验、
    /// 0/3 进度由 dialogue.json 的事件节点播出）。采集后石头消失。
    /// 挂载要求：勘测点岩石；pointId 填唯一标识（A/B/C）。
    /// </summary>
    public class SampleRock : InteractableBase
    {
        [HideInInspector] public string pointId;

        public override void Interact(GameObject who)
        {
            bool collected = QuestManager.I != null && QuestManager.I.OnSamplePoint(pointId);
            if (!collected) return;
            Debug.Log($"[采样] {who.name} 采集勘测点 {pointId}");
            Destroy(gameObject);
        }
    }
}
