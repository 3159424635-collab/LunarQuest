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

        void Awake()
        {
            EnsureInteractionCollider();
        }

        public override void Interact(GameObject who)
        {
            var launchTour = GetComponent<LaunchSiteTourGuide>();
            if (launchTour != null && launchTour.HandleInteraction(who)) return;

            if (animator) animator.CrossFade("talk", 0.15f);
            if (QuestManager.I != null) QuestManager.I.OnNpc(characterId);
        }

        public void EnsureInteractionCollider()
        {
            if (GetComponentInChildren<Collider>() != null) return;

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 scale = transform.lossyScale;
            float sy = Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
            float sxz = Mathf.Max(Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)), 0.0001f);
            float height = Mathf.Max(1.2f, bounds.size.y / sy);
            float radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.45f / sxz,
                0.32f, height * 0.4f);

            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = transform.InverseTransformPoint(bounds.center);
            capsule.height = Mathf.Max(height + 0.2f, radius * 2f);
            capsule.radius = radius;
        }
    }
}
